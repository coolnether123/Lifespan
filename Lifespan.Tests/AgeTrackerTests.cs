using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using ModAPI.Saves;
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

            var mockSave = new Mock<ISaveSystem>();
            _mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);

            _config = new LifespanConfig();
            _config.enableChildDevelopment = true;
            _tracker = new AgeTracker(_mockCtx.Object, _config, new ModRandomStream(12345));
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

        [Test]
        public void IncrementAge_UsesFixedChildCutoffAtTenYears()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            int memberId = 124;
            typeof(FamilyMember).GetField("familyId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(member, memberId);

            // Age 9 -> accelerated (+2 years per 1-year tick)
            _tracker.SetAgeWeeks(member, 9 * 52);
            int nineYearResult = _tracker.IncrementAge(member, 52);
            Assert.AreEqual((9 * 52) + 104, nineYearResult);

            // Age 10 -> normal speed (+1 year per 1-year tick)
            _tracker.SetAgeWeeks(member, 10 * 52);
            int tenYearResult = _tracker.IncrementAge(member, 52);
            Assert.AreEqual((10 * 52) + 52, tenYearResult);
        }
    }
}
