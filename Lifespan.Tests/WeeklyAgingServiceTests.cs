using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Lifespan;
using ModAPI.Core;
using ModAPI.Util;
using Moq;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class WeeklyAgingServiceTests
    {
        private LifespanConfig _config;
        private AgeTracker _tracker;
        private Mock<IModLogger> _log;

        [SetUp]
        public void Setup()
        {
            var ctx = new Mock<IPluginContext>();
            _log = new Mock<IModLogger>();
            ctx.Setup(c => c.Log).Returns(_log.Object);

            _config = new LifespanConfig();
            _config.enableAcceleratedChildhood = false;
            _config.enableHairGreying = false;
            _config.enableNaturalDeath = true;
            _config.agingIntervalWeeks = 1;
            _config.weeksAgedPerInterval = 4;

            _tracker = new AgeTracker(ctx.Object, _config, new ModRandomStream(2468), new InMemoryAgeDataStore());
            _tracker.LoadAgeData();
        }

        [Test]
        public void ProcessNewWeek_RunsManagersInPreservedOrder()
        {
            var member = CreateMember(101);
            int startAge = _config.elderAgeYears * LifespanConstants.WeeksPerYear;
            _tracker.SetAgeWeeks(member, startAge);
            Assert.IsFalse(
                WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, character => false, out string skipReason),
                skipReason);

            var calls = new List<string>();
            var service = CreateService(
                currentWeek: 12,
                members: new List<FamilyMember> { member },
                calls: calls,
                hasExternalAging: true);

            service.ProcessNewWeek();

            int expectedAge = startAge + _config.weeksAgedPerInterval;
            CollectionAssert.AreEqual(
                new[]
                {
                    "illness:" + expectedAge + ":" + _config.weeksAgedPerInterval,
                    "hair:" + expectedAge,
                    "development:" + expectedAge + ":" + _config.weeksAgedPerInterval,
                    "milestones:" + expectedAge,
                    "death:" + expectedAge + ":" + _config.weeksAgedPerInterval,
                    "publish:" + expectedAge,
                    "cleanup",
                    "external:" + _config.weeksAgedPerInterval,
                    "ui"
                },
                calls);
            Assert.AreEqual(expectedAge, _tracker.GetAgeWeeks(member));
            Assert.AreEqual(12, _tracker.LastProcessedAgingWeek);
        }

        [Test]
        public void ProcessNewWeek_SameWeekTwice_DoesNotReprocess()
        {
            var member = CreateMember(102);
            _tracker.SetAgeWeeks(member, 30 * LifespanConstants.WeeksPerYear);

            var calls = new List<string>();
            var service = CreateService(8, new List<FamilyMember> { member }, calls);

            service.ProcessNewWeek();
            int ageAfterFirstPass = _tracker.GetAgeWeeks(member);
            calls.Clear();

            service.ProcessNewWeek();

            Assert.AreEqual(ageAfterFirstPass, _tracker.GetAgeWeeks(member));
            Assert.AreEqual(8, _tracker.LastProcessedAgingWeek);
            CollectionAssert.IsEmpty(calls);
        }

        [Test]
        public void ProcessNewWeek_CatchesUpMissedBiologicalTicks()
        {
            var member = CreateMember(105);
            int startAge = _config.elderAgeYears * LifespanConstants.WeeksPerYear;
            _tracker.SetAgeWeeks(member, startAge);
            _tracker.MarkAgingWeekProcessed(8);

            var calls = new List<string>();
            var service = CreateService(12, new List<FamilyMember> { member }, calls);

            service.ProcessNewWeek();

            Assert.AreEqual(startAge + (4 * _config.weeksAgedPerInterval), _tracker.GetAgeWeeks(member));
            Assert.AreEqual(12, _tracker.LastProcessedAgingWeek);
            Assert.AreEqual(4, calls.FindAll(call => call.StartsWith("illness:")).Count);
            Assert.AreEqual(4, calls.FindAll(call => call.StartsWith("death:")).Count);
            Assert.AreEqual("cleanup", calls[calls.Count - 2]);
            Assert.AreEqual("ui", calls[calls.Count - 1]);
        }

        [Test]
        public void ProcessNewWeek_IntervalSkipped_DoesNotMarkProcessedWeek()
        {
            _config.agingIntervalWeeks = 2;

            var member = CreateMember(103);
            int startAge = 30 * LifespanConstants.WeeksPerYear;
            _tracker.SetAgeWeeks(member, startAge);

            var calls = new List<string>();
            var service = CreateService(3, new List<FamilyMember> { member }, calls);

            service.ProcessNewWeek();

            Assert.AreEqual(startAge, _tracker.GetAgeWeeks(member));
            Assert.AreEqual(WeeklyAgingPolicy.NoProcessedWeek, _tracker.LastProcessedAgingWeek);
            CollectionAssert.IsEmpty(calls);
        }

        [Test]
        public void ProcessNewWeek_SkippedMember_IsNotAgedButWeekIsMarked()
        {
            var member = CreateMember(104, isAway: true);
            int startAge = 30 * LifespanConstants.WeeksPerYear;
            _tracker.SetAgeWeeks(member, startAge);

            var calls = new List<string>();
            var service = CreateService(14, new List<FamilyMember> { member }, calls);

            service.ProcessNewWeek();

            Assert.AreEqual(startAge, _tracker.GetAgeWeeks(member));
            Assert.AreEqual(14, _tracker.LastProcessedAgingWeek);
            CollectionAssert.AreEqual(new[] { "cleanup", "ui" }, calls);
        }

        private WeeklyAgingService CreateService(
            int currentWeek,
            List<FamilyMember> members,
            List<string> calls,
            bool hasExternalAging = false)
        {
            return new WeeklyAgingService(
                _log.Object,
                _config,
                _tracker,
                new WeeklyAgingOperations
                {
                    GetCurrentDay = () => 84,
                    GetCurrentWeek = () => currentWeek,
                    HasFamilyManager = () => true,
                    GetFamilyMembers = () => members,
                    IsNullMember = member => object.ReferenceEquals(member, null),
                    ShouldCancelAging = character => false,
                    TransitionToAdult = member => calls.Add("adult"),
                    ProcessElderIllnessRoll = (member, ageWeeks, elapsedWeeks) => calls.Add("illness:" + ageWeeks + ":" + elapsedWeeks),
                    ProcessHairGreying = (member, ageWeeks) => calls.Add("hair:" + ageWeeks),
                    ProcessDevelopment = (member, ageWeeks, weeksAged) => calls.Add("development:" + ageWeeks + ":" + weeksAged),
                    ProcessMilestones = (member, ageWeeks) => calls.Add("milestones:" + ageWeeks),
                    ProcessDeathRoll = (member, ageWeeks, elapsedWeeks) => calls.Add("death:" + ageWeeks + ":" + elapsedWeeks),
                    CleanupMissingMembers = () => calls.Add("cleanup"),
                    HasExternalCharacterAging = () => hasExternalAging,
                    UpdateExternalCharacters = weeks => calls.Add("external:" + weeks),
                    ForceAvatarUpdate = () => calls.Add("ui"),
                    PublishCharacterAged = (member, ageWeeks) => calls.Add("publish:" + ageWeeks)
                });
        }

        private static FamilyMember CreateMember(int id, bool isAway = false)
        {
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));

            member.job_queue = new JobQueue(2);
            member.ai_queue = new JobQueue(4);
            member.SetId(id);

            SetField(member, typeof(BaseCharacter), "m_firstName", "Member" + id);
            SetField(member, typeof(BaseCharacter), "m_health", 100);
            SetField(member, typeof(BaseCharacter), "m_pendingDeath", false);
            SetField(member, typeof(BaseCharacter), "m_child", false);
            SetField(member, typeof(FamilyMember), "m_isAway", isAway);

            return member;
        }

        private static void SetField(object instance, Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private sealed class InMemoryAgeDataStore : IAgeDataStore
        {
            private AgeData _data = new AgeData { lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek };

            public bool HasSavedData => true;

            public AgeData Load()
            {
                return _data;
            }

            public void Save(AgeData data)
            {
                _data = data;
            }

            public void Clear()
            {
                _data = new AgeData { lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek };
            }
        }
    }
}
