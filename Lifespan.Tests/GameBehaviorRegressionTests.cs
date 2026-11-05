using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Lifespan;
using ModAPI.Core;
using Moq;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class GameBehaviorRegressionTests
    {
        private IPluginContext _context;
        private AgeTracker _tracker;
        private LifespanConfig _config;

        [SetUp]
        public void SetUp()
        {
            var context = new Mock<IPluginContext>();
            context.Setup(c => c.Log).Returns(new Mock<IModLogger>().Object);
            context.Setup(c => c.SaveSystem).Returns(new Mock<ISaveSystem>().Object);
            _context = context.Object;
            _config = new LifespanConfig { enableChildDevelopment = true };
            _tracker = new AgeTracker(_context, _config, new ModRandomStream(71));
        }

        [Test]
        public void VisitorIdentity_UninitializedVisitorsDoNotShareAnAgeRecord()
        {
            var first = Character<NpcVisitor>();
            var second = Character<NpcVisitor>();
            Set(first, typeof(NpcVisitor), "m_npcId", -1);
            Set(second, typeof(NpcVisitor), "m_npcId", -1);
            Assert.AreEqual(0, _tracker.GenerateAgeForContext(first, AgeContext.NPC_Trader));
            _tracker.SetAgeWeeks((BaseCharacter)second, 900);
            Assert.IsEmpty(_tracker.GetAllTrackedExternalIds());

            Set(first, typeof(NpcVisitor), "m_npcId", 10);
            Set(second, typeof(NpcVisitor), "m_npcId", 11);
            _tracker.SetAgeWeeks((BaseCharacter)first, 1000);
            _tracker.SetAgeWeeks((BaseCharacter)second, 2000);
            Assert.AreEqual(1000, _tracker.GetAgeWeeks((BaseCharacter)first));
            Assert.AreEqual(2000, _tracker.GetAgeWeeks((BaseCharacter)second));
        }

        [Test]
        public void LoadedVisitors_PreserveSavedAgesAndInitializeMissingRecords()
        {
            var known = Character<NpcVisitor>();
            var missing = Character<NpcVisitor>();
            Set(known, typeof(NpcVisitor), "m_npcId", 10);
            Set(missing, typeof(NpcVisitor), "m_npcId", 11);
            Set(known, typeof(NpcVisitor), "m_type", NpcVisitor.NpcType.Trader);
            Set(missing, typeof(NpcVisitor), "m_type", NpcVisitor.NpcType.Trader);
            var saved = new AgeData();
            saved.externalCharacterAges.Add(10, 1234);
            _tracker.State.Replace(saved);

            var plugin = Character<LifespanPlugin>();
            Set(plugin, typeof(LifespanPlugin), "_api", new LifespanAPIImpl(_context, _config, _tracker, null, null, null));
            Set(plugin, typeof(ModManagerBase), "_scopedLog", _context.Log);
            var visitors = Character<NpcVisitManager>();
            Set(visitors, typeof(NpcVisitManager), "m_visitors", new List<NpcVisitor> { known, missing });
            var pluginField = typeof(LifespanPlugin).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var visitorsField = typeof(NpcVisitManager).GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var saveField = typeof(SaveManager).GetField("m_instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPlugin = pluginField.GetValue(null);
            var previousVisitors = visitorsField.GetValue(null);
            var previousSave = saveField.GetValue(null);
            try
            {
                pluginField.SetValue(null, plugin);
                visitorsField.SetValue(null, visitors);
                var save = Character<SaveManager>();
                saveField.SetValue(null, save);
                Set(save, typeof(SaveManager), "m_pendingLoad", true);
                IntegrationPatches.NpcVisitor_Initialize_Patch.Postfix(missing, missing.npcType);
                Assert.AreEqual(0, _tracker.GetAgeWeeks((BaseCharacter)missing));
                Set(save, typeof(SaveManager), "m_pendingLoad", false);
                typeof(LifespanPlugin).GetMethod("RestoreVisitorAges", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plugin, null);
                Assert.AreEqual(1234, _tracker.GetAgeWeeks((BaseCharacter)known));
                Assert.Greater(_tracker.GetAgeWeeks((BaseCharacter)missing), 0);
                CollectionAssert.AreEquivalent(new[] { known, missing }, _tracker.GetTrackedExternalCharacters());
            }
            finally
            {
                pluginField.SetValue(null, previousPlugin);
                visitorsField.SetValue(null, previousVisitors);
                saveField.SetValue(null, previousSave);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ExpeditionFilter_ReselectsFromTheRemainingCandidates(bool includeAdult)
        {
            var child = Character<FamilyMember>();
            Set(child, typeof(BaseCharacter), "m_child", true);
            var adult = Character<FamilyMember>();
            var panel = Character<ExpeditionMainPanelNew>();
            var candidates = new List<FamilyMember> { child };
            if (includeAdult) candidates.Add(adult);
            Set(panel, typeof(ExpeditionMainPanelNew), "m_eligiblePeople", candidates);
            Set(panel, typeof(ExpeditionMainPanelNew), "m_currentPerson1Index", 0);
            Set(panel, typeof(ExpeditionMainPanelNew), "m_currentPerson2Index", 1);
            ChildCapabilityPatches.Initialize(_context, new ChildDevelopmentManager(_context, _config, _tracker));
            try
            {
                ChildCapabilityPatches.ExpeditionOnShowPatch.Postfix(panel);
                Assert.AreEqual(includeAdult ? 1 : 0, candidates.Count);
                Assert.AreEqual(includeAdult ? 0 : -1, panel.currentPerson1Index);
                Assert.AreEqual(-1, panel.currentPerson2Index);
                if (includeAdult) Assert.AreSame(adult, candidates[panel.currentPerson1Index]);
            }
            finally
            {
                ChildCapabilityPatches.Manager = null;
            }
        }

        [TestCase("Optimist", Traits.Strength.Optimistic)]
        [TestCase("Courageous", Traits.Strength.Courageous)]
        [TestCase("HardWorker", Traits.Strength.Proactive)]
        public void DialogueWeighting_RecognizesVanillaStrengths(string tag, Traits.Strength strength)
        {
            var member = Character<FamilyMember>();
            var traits = new Traits();
            Set(member, typeof(BaseCharacter), "m_traits", traits);
            Assert.IsFalse(DialogueHelper.HasTrait(member, tag));
            traits.AddStrength(strength);
            Assert.IsTrue(DialogueHelper.HasTrait(member, tag));
        }

        [TestCase("Pessimist", Traits.Weakness.Pessimistic)]
        [TestCase("Cowardly", Traits.Weakness.Cowardice)]
        public void DialogueWeighting_RecognizesVanillaWeaknesses(string tag, Traits.Weakness weakness)
        {
            var member = Character<FamilyMember>();
            var traits = new Traits();
            Set(member, typeof(BaseCharacter), "m_traits", traits);
            Assert.IsFalse(DialogueHelper.HasTrait(member, tag));
            traits.AddWeakness(weakness, true);
            Assert.IsTrue(DialogueHelper.HasTrait(member, tag));
        }

        [Test]
        public void SessionReset_DiscardsAdultVisualWorkForReusedFamilyIds()
        {
            var manager = new ChildTransitionManager(_context, _config, _tracker, new ModRandomStream(72));
            var retries = (Dictionary<int, int>)Get(manager, "_pendingVisualRetries");
            var deadlines = (Dictionary<int, float>)Get(manager, "_nextVisualRetryAt");
            retries.Add(0, 3);
            deadlines.Add(0, 10f);
            manager.Reset();
            Assert.IsEmpty(retries);
            Assert.IsEmpty(deadlines);
            var nextSessionAdult = Character<FamilyMember>();
            nextSessionAdult.SetId(0);
            Assert.DoesNotThrow(() => manager.TransitionToAdult(nextSessionAdult));
        }

        [Test]
        public void RadioConfirmation_QueuesOnceAndSurvivesReloadWithoutAnotherRadioRequest()
        {
            var member = Character<FamilyMember>();
            member.SetId(25);
            Set(member, typeof(BaseCharacter), "m_health", 100);
            var manager = new DeathManager(_context, _config, _tracker, new ModRandomStream(73), _tracker.State.Deaths, () => 10f);
            var awaiting = (IList)Get(manager, "_awaitingRadioDeaths");
            var entryType = awaiting.GetType().GetGenericArguments()[0];
            var entry = Activator.CreateInstance(entryType);
            entryType.GetField("Member").SetValue(entry, member);
            awaiting.Add(entry);

            manager.ConfirmRadioDeath(member, "Old age", 30);
            manager.ConfirmRadioDeath(member, "Old age", 30);
            manager.CapturePendingDeaths();
            Assert.AreEqual(1, _tracker.State.Data.pendingDeaths.Count);
            Assert.IsTrue(_tracker.State.Data.pendingDeaths[0].radioConfirmed);
            Assert.AreEqual(30, _tracker.State.Data.pendingDeaths[0].targetDay);

            var saved = _tracker.State.Data.ToSerializable();
            _tracker.State.Replace(AgeData.FromSerializable(saved));
            manager.RestorePendingDeaths(new List<FamilyMember> { member });
            manager.CapturePendingDeaths();
            Assert.IsTrue(_tracker.State.Data.pendingDeaths[0].radioConfirmed);
            Assert.AreEqual(0f, _tracker.State.Data.pendingDeaths[0].remainingSeconds);
            manager.Reset();
            manager.ConfirmRadioDeath(member, "Old age", 30);
            manager.CapturePendingDeaths();
            Assert.IsEmpty(_tracker.State.Data.pendingDeaths);
        }

        private static T Character<T>() where T : UnityEngine.Object
        {
            var value = (T)FormatterServices.GetUninitializedObject(typeof(T));
            Set(value, typeof(UnityEngine.Object), "m_CachedPtr", new IntPtr(1));
            return value;
        }

        private static object Get(object instance, string field)
        {
            return instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);
        }

        private static void Set(object instance, Type owner, string field, object value)
        {
            owner.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        }
    }
}
