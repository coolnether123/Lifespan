using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;

namespace Lifespan.Tests
{
    [TestFixture]
    public class AgeTrackerTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;
        private AgeTracker _tracker;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            _config = new LifespanConfig();
            _tracker = new AgeTracker(_mockCtx.Object, _config);
        }

        [Test]
        public void GenerateInitialAge_WithinBounds()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            bool isChild = true;
            _config.initialChildAgeYears = 10;
            
            int ageWeeks = _tracker.GenerateInitialAge(member, isChild);
            int ageYears = ageWeeks / 52;

            Assert.GreaterOrEqual(ageYears, 0);
            Assert.LessOrEqual(ageYears, 17);
        }

        [Test]
        public void IncrementAge_PersistsCorrectly()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            int memberId = 123;
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, memberId);
            
            // Manually set an initial age in the tracker's internal data to avoid Gaussian side effects
            _tracker.SetAgeWeeks(member, 1000);
            
            int initialAge = _tracker.GetAgeWeeks(member);
            int increment = 10;
            
            int newAge = _tracker.IncrementAge(member, increment);
            
            Assert.AreEqual(initialAge + increment, newAge);
            Assert.AreEqual(newAge, _tracker.GetAgeWeeks(member));
        }
    }
}
