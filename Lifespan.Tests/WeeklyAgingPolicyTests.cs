using System;
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
    public class WeeklyAgingPolicyTests
    {
        private LifespanConfig _config;
        private AgeTracker _tracker;

        [SetUp]
        public void Setup()
        {
            var mockCtx = new Mock<IPluginContext>();
            var mockLog = new Mock<IModLogger>();
            var mockSave = new Mock<ISaveSystem>();

            mockCtx.Setup(c => c.Log).Returns(mockLog.Object);
            mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);

            _config = new LifespanConfig();
            _tracker = new AgeTracker(mockCtx.Object, _config, new ModRandomStream(12345));
        }

        [Test]
        public void ShouldSkipWeeklyAging_AwayMember_ReturnsAwayReason()
        {
            var member = CreateMember(1, isAway: true);

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, null, out string reason);

            Assert.IsTrue(skip);
            Assert.AreEqual("away", reason);
        }

        [Test]
        public void ShouldSkipWeeklyAging_ShelterMember_AllowsAging()
        {
            var member = CreateMember(2);
            _tracker.SetAgeWeeks(member, 20 * LifespanConstants.WeeksPerYear);

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, null, out string reason);
            if (!skip)
            {
                _tracker.IncrementAge(member, 4);
            }

            Assert.IsFalse(skip, reason);
            Assert.AreEqual((20 * LifespanConstants.WeeksPerYear) + 4, _tracker.GetAgeWeeks(member));
        }

        [Test]
        public void ShouldProcessAgingWeek_SameWeekTwice_OnlyAgesOnce()
        {
            var member = CreateMember(3);
            _tracker.SetAgeWeeks(member, 30 * LifespanConstants.WeeksPerYear);

            int week = 12;
            if (WeeklyAgingPolicy.ShouldProcessAgingWeek(week, _tracker.LastProcessedAgingWeek, out _))
            {
                _tracker.MarkAgingWeekProcessed(week);
                _tracker.IncrementAge(member, 4);
            }

            if (WeeklyAgingPolicy.ShouldProcessAgingWeek(week, _tracker.LastProcessedAgingWeek, out _))
            {
                _tracker.MarkAgingWeekProcessed(week);
                _tracker.IncrementAge(member, 4);
            }

            Assert.AreEqual((30 * LifespanConstants.WeeksPerYear) + 4, _tracker.GetAgeWeeks(member));
        }

        [Test]
        public void ShouldSkipWeeklyAging_DeadOrDyingMembers_ReturnsTrue()
        {
            var dead = CreateMember(4, isDead: true);
            var dying = CreateMember(5, isDying: true);

            Assert.IsTrue(WeeklyAgingPolicy.ShouldSkipWeeklyAging(dead, null, out string deadReason));
            Assert.AreEqual("dead", deadReason);

            Assert.IsTrue(WeeklyAgingPolicy.ShouldSkipWeeklyAging(dying, null, out string dyingReason));
            Assert.AreEqual("dying", dyingReason);
        }

        [Test]
        public void ShouldSkipWeeklyAging_ApiCancellation_ReturnsTrue()
        {
            var member = CreateMember(6);

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, _ => true, out string reason);

            Assert.IsTrue(skip);
            Assert.AreEqual("api-cancelled", reason);
        }

        [Test]
        public void ShouldSkipWeeklyAging_ShelterNewborn_AllowsAging()
        {
            var member = CreateMember(7, isChild: true);
            _tracker.SetAgeWeeks(member, 0);

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, null, out string reason);
            if (!skip)
            {
                _tracker.IncrementAge(member, 1);
            }

            Assert.IsFalse(skip, reason);
            Assert.Greater(_tracker.GetAgeWeeks(member), 0);
        }

        [Test]
        public void ShouldSkipWeeklyAging_DepartureAiJob_ReturnsDepartingReason()
        {
            var member = CreateMember(8);
            member.ai_queue.AddJob(CreateGoToLocationJob(Job_GoToLocation.LocationReachedAction.LeftForExpedition));

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, null, out string reason);

            Assert.IsTrue(skip);
            Assert.AreEqual("departing", reason);
        }

        [Test]
        public void ShouldSkipWeeklyAging_ReturnAiJob_ReturnsReturningReason()
        {
            var member = CreateMember(9);
            member.ai_queue.AddJob(CreateGoToLocationJob(Job_GoToLocation.LocationReachedAction.ReturnedFromExpedition));

            bool skip = WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, null, out string reason);

            Assert.IsTrue(skip);
            Assert.AreEqual("returning", reason);
        }

        private static FamilyMember CreateMember(
            int id,
            bool isAway = false,
            bool isDead = false,
            bool isDying = false,
            bool isChild = false)
        {
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));

            member.job_queue = new JobQueue(2);
            member.ai_queue = new JobQueue(4);
            member.SetId(id);

            SetField(member, typeof(BaseCharacter), "m_firstName", "Member" + id);
            SetField(member, typeof(BaseCharacter), "m_health", isDead ? 0 : 100);
            SetField(member, typeof(BaseCharacter), "m_pendingDeath", isDying);
            SetField(member, typeof(BaseCharacter), "m_child", isChild);
            SetField(member, typeof(FamilyMember), "m_isAway", isAway);

            return member;
        }

        private static Job CreateGoToLocationJob(Job_GoToLocation.LocationReachedAction action)
        {
            var job = (Job_GoToLocation)FormatterServices.GetUninitializedObject(typeof(Job_GoToLocation));
            SetField(job, typeof(Job), "type", "go_to_location");
            SetField(job, typeof(Job_GoToLocation), "m_callbackAction", action);
            return job;
        }

        private static void SetField(object instance, Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(instance, value);
        }
    }
}
