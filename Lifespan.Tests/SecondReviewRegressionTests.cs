using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using Moq;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class SecondReviewRegressionTests
    {
        private static IPluginContext Context(IModLogger log)
        {
            var context = new Mock<IPluginContext>();
            context.Setup(c => c.Log).Returns(log);
            return context.Object;
        }

        [Test]
        public void FailedLoad_PreservesContainerAndBlocksSaveUntilSuccessfulRetry()
        {
            var store = new TestAgeDataStore();
            var saved = new AgeData();
            saved.familyMemberAges[7] = 1234;
            store.Data = saved;
            var tracker = new AgeTracker(Context(new Mock<IModLogger>().Object), new LifespanConfig(), new ModRandomStream(1), store);
            tracker.LoadAgeData();
            store.FailLoad = true;

            Assert.Throws<InvalidOperationException>(() => tracker.LoadAgeData());
            Assert.IsFalse(tracker.IsDataHydrated);
            tracker.SaveAgeData();
            Assert.AreEqual(0, store.SaveCount);
            Assert.AreEqual(0, store.ClearCount);
            Assert.AreEqual(1234, saved.familyMemberAges[7]);

            store.FailLoad = false;
            tracker.LoadAgeData();
            Assert.IsTrue(tracker.IsDataHydrated);
            Assert.AreEqual(1234, tracker.State.Data.familyMemberAges[7]);
        }

        [Test]
        public void Clear_AllowsSpeechInANewSessionOnTheSameDay()
        {
            var scheduler = new DialogueScheduler(new Mock<IModLogger>().Object, new ModRandomStream(1));
            scheduler.Enqueue(null, "Before reload", false);
            scheduler.Clear();
            scheduler.Enqueue(null, "After reload", false);
            var queue = (ICollection)typeof(DialogueScheduler).GetField("_messageQueue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scheduler);
            Assert.AreEqual(1, queue.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JournalSetting_AppliesToDeathAndIllnessMessages(bool enabled)
        {
            var config = new LifespanConfig { enableJournalEntries = enabled };
            var context = Context(new Mock<IModLogger>().Object);
            var random = new ModRandomStream(1);
            var scheduler = new DialogueScheduler(context.Log, random);
            var death = new DeathManager(context, config, null, random);
            var illness = new ElderIllnessManager(context, config, null, random, null);
            death.SetScheduler(scheduler);
            illness.SetScheduler(scheduler);
            typeof(DeathManager).GetMethod("TriggerJournal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(death, new object[] { "Death", DialogueScheduler.Priority.Routine });
            typeof(ElderIllnessManager).GetMethod("TriggerJournal", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(illness, new object[] { "Illness", DialogueScheduler.Priority.Routine, null });
            var queue = (ICollection)typeof(DialogueScheduler).GetField("_messageQueue", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scheduler);
            Assert.AreEqual(enabled ? 2 : 0, queue.Count);
        }

        [Test]
        public void VerboseLogging_FollowsRuntimeConfigurationAndPreservesWarnings()
        {
            var inner = new Mock<IModLogger>();
            inner.SetupGet(l => l.IsDebugEnabled).Returns(true);
            var config = new LifespanConfig();
            var logger = new LifespanLogger(inner.Object, config);
            Assert.IsFalse(logger.IsDebugEnabled);
            logger.Debug("hidden");
            config.verboseLogging = true;
            Assert.IsTrue(logger.IsDebugEnabled);
            logger.Debug("visible");
            config.verboseLogging = false;
            logger.Warn("warning");
            inner.Verify(l => l.Debug("hidden"), Times.Never);
            inner.Verify(l => l.Debug("visible"), Times.Once);
            inner.Verify(l => l.Warn("warning"), Times.Once);
        }

        [Test]
        public void MilestoneOffset_AcceptsAMemberWithNoName()
        {
            var context = Context(new Mock<IModLogger>().Object);
            var config = new LifespanConfig();
            var store = new TestAgeDataStore();
            var random = new ModRandomStream(1);
            var tracker = new AgeTracker(context, config, random, store);
            tracker.LoadAgeData();
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(member, new IntPtr(1));
            member.SetId(9);
            tracker.SetAgeWeeks(member, 18 * 52);
            var milestones = new MilestoneManager(context, config, tracker, random, null);
            int triggered = 0;
            Action<FamilyMember, int> action = (m, age) => triggered++;
            typeof(MilestoneManager).GetMethod("TryTriggerFuzzyUnique", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(milestones, new object[] { member, 18, 0, "Adult", action });
            Assert.AreEqual(1, triggered);
        }

        [Test]
        public void AdultTransition_DoesNotAccessStartingFamilyTemplates()
        {
            foreach (var method in typeof(ChildTransitionManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.ContainsGenericParameters) continue;
                foreach (var instruction in PatchProcessor.GetOriginalInstructions(method))
                    if (instruction.operand is MemberInfo member)
                        Assert.AreNotEqual(typeof(SaveTemp), member.DeclaringType, method.Name + " accesses starting-family templates");
            }
        }

        private sealed class TestAgeDataStore : IAgeDataStore
        {
            internal AgeData Data = new AgeData();
            internal bool FailLoad;
            internal int SaveCount;
            internal int ClearCount;
            public bool HasSavedData => true;
            public AgeData Load()
            {
                if (FailLoad) throw new InvalidOperationException("Unreadable container");
                return Data;
            }
            public void Save(AgeData data) { SaveCount++; Data = data; }
            public void Clear() { ClearCount++; Data = new AgeData(); }
        }
    }
}
