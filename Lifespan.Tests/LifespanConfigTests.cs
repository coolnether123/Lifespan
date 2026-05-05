using NUnit.Framework;
using Lifespan;
using System;

namespace Lifespan.Tests
{
    [TestFixture]
    public class LifespanConfigTests
    {
        private LifespanConfig _config;

        [SetUp]
        public void Setup()
        {
            _config = new LifespanConfig();
        }

        [Test]
        public void DefaultValues_MatchStandardDifficulty()
        {
            // Verify that the default constructor provides the "Standard" game balance
            
            // Life Stages
            Assert.AreEqual(18, _config.adultAgeYears);
            Assert.AreEqual(60, _config.elderAgeYears);

            // Child Development (Updated defaults for realism)
            Assert.AreEqual(1, _config.mobileAgeYears);
            Assert.AreEqual(6, _config.childJobAgeYears);

            // Aging Speed
            Assert.AreEqual(1, _config.agingIntervalWeeks);
            Assert.AreEqual(52, _config.weeksAgedPerInterval); // 1 year per tick

            // Stat Growth (Percentage Scale)
            Assert.AreEqual(0.25f, _config.baseStatGainChance, 0.001f); // 0.25%
            Assert.AreEqual(2, _config.sparkMultiplier);

            // Illness
            Assert.IsTrue(_config.enableDementia);
            Assert.IsTrue(_config.enableHeartDisease);
            Assert.IsTrue(_config.enableArthritis);
            
            // Death
            Assert.AreEqual(1.0f, _config.deathProbabilityMultiplier);
            Assert.AreEqual(20f, _config.heartAttackDamage);
        }

        [Test]
        public void ValidateAndClamp_FixesInvalidValues()
        {
            // Set invalid values
            _config.adultAgeYears = -5;
            _config.agingIntervalWeeks = 0;
            _config.elderIllnessBaseChance = 500.0f; // > 100.0
            _config.deathProbabilityMultiplier = -1.0f;

            // Act
            _config.ValidateAndClamp();

            // Assert
            Assert.AreEqual(18, _config.adultAgeYears); // Default child max requires adulthood at 18+
            Assert.AreEqual(1, _config.agingIntervalWeeks); // Min 1
            Assert.AreEqual(5.0f, _config.elderIllnessBaseChance); // Max 5.0
            Assert.AreEqual(0.1f, _config.deathProbabilityMultiplier); // Min 0.1
        }

        [Test]
        public void ValidateAndClamp_EnforcesLogicalAgeOrder()
        {
            // Elder age should not be less than Adult age
            _config.adultAgeYears = 40;
            _config.elderAgeYears = 30; // Illogical

            // Act
            _config.ValidateAndClamp();

            // Assert
            Assert.AreEqual(41, _config.elderAgeYears, "Elder age should be clamped to at least Adult age + 1");
        }

        [Test]
        public void ValidateAndClamp_EnforcesChildAndExpeditionOrder()
        {
            _config.adultAgeYears = 18;
            _config.mobileAgeYears = 9;
            _config.childJobAgeYears = 5;
            _config.expeditionMinAgeAccompanied = 4;
            _config.expeditionMinAgeSolo = 3;

            _config.ValidateAndClamp();

            Assert.GreaterOrEqual(_config.childJobAgeYears, _config.mobileAgeYears);
            Assert.GreaterOrEqual(_config.expeditionMinAgeAccompanied, _config.childJobAgeYears);
            Assert.GreaterOrEqual(_config.expeditionMinAgeSolo, _config.expeditionMinAgeAccompanied);
        }
        
        [Test]
        public void Config_SimulateHardDifficulty()
        {
            // Simulate a "Hard Mode" configuration where:
            // - Aging is faster
            // - Death is more likely
            // - Elders get sick easier
            
            _config.agingIntervalWeeks = 1;
            _config.weeksAgedPerInterval = 104; // 2 years per tick!
            _config.deathProbabilityMultiplier = 2.0f; // Double death chance
            _config.elderIllnessBaseChance = 1.0f; // 1% per week (10x default)

            _config.ValidateAndClamp();

            Assert.AreEqual(104, _config.weeksAgedPerInterval);
            Assert.AreEqual(2.0f, _config.deathProbabilityMultiplier);
            Assert.AreEqual(1.0f, _config.elderIllnessBaseChance);
        }

        [Test]
        public void Config_SimulateEasyDifficulty()
        {
            // Simulate an "Easy Mode" configuration where:
            // - Death is less likely
            // - Illness is disabled
            
            _config.deathProbabilityMultiplier = 0.1f;
            _config.enableDementia = false;
            _config.enableHeartDisease = false;
            _config.baseStatGainChance = 5.0f; // Super high learning (5%)

            _config.ValidateAndClamp();

            Assert.AreEqual(0.1f, _config.deathProbabilityMultiplier);
            Assert.IsFalse(_config.enableDementia);
            Assert.AreEqual(5.0f, _config.baseStatGainChance);
        }
    }
}
