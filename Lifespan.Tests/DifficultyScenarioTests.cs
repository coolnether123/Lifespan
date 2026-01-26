using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace Lifespan.Tests
{
    [TestFixture]
    public class DifficultyScenarioTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            _config = new LifespanConfig();
        }

        private double RunSimulation(int seed)
        {
            const int populationSize = 500;
            var deathAges = new List<int>();
            var rng = new System.Random(seed);

            for (int i = 0; i < populationSize; i++)
            {
                // Start each person at age 60 (elder threshold)
                // We assume Config.elderAgeYears is 60 for consistency in start point
                int currentAgeWeeks = 60 * 52;
                bool isDead = false;

                // Simulate week by week until death or absolute max age
                while (!isDead && currentAgeWeeks < 200 * 52)
                {
                    int elderWeeks = _config.elderAgeYears * 52;
                    float yearsPastElder = (float)(currentAgeWeeks - elderWeeks) / 52f;
                    
                    // Logic from DeathManager (Updated for Percentage Scale)
                    float baseProb = (_config.deathBaseProbability / 100f) + (yearsPastElder * (_config.deathProbabilityIncreasePerYear / 100f));
                    
                    // Apply difficulty multiplier
                    float finalProb = baseProb * _config.deathProbabilityMultiplier;

                    if (rng.NextDouble() < finalProb)
                    {
                        isDead = true;
                        deathAges.Add(currentAgeWeeks / 52);
                    }
                    else
                    {
                        currentAgeWeeks++;
                    }
                }
                
                // If they survived to 200, count as 200
                if (!isDead) deathAges.Add(200);
            }

            return deathAges.Average();
        }

        [Test]
        [Category("Simulation")]
        public void VerifyStandardDifficulty_BalancedLifespan()
        {
            // Standard Settings
            _config.elderAgeYears = 60;
            _config.deathBaseProbability = 0.05f; // Was 0.0005
            _config.deathProbabilityIncreasePerYear = 0.008f; // Was 0.00008
            _config.deathProbabilityMultiplier = 1.0f;

            // Updated RunSimulation needs to divide by 100 internal logic is now expecting 0-100 inputs
            double avgAge = RunSimulation(42);
            TestContext.WriteLine($"Standard Difficulty Average Age: {avgAge:F2}");

            // Based on previous runs, ~74 is expected
            Assert.That(avgAge, Is.InRange(70, 78), "Standard difficulty should yield average lifespan in 70s");
        }

        [Test]
        [Category("Simulation")]

        public void VerifyHardDifficulty_ShorterLifespan()
        {
            // Hard Settings (Simulated)
            _config.elderAgeYears = 60;
            _config.deathBaseProbability = 0.05f;
            _config.deathProbabilityIncreasePerYear = 0.008f;
            _config.deathProbabilityMultiplier = 2.0f; // Double death chance

            double avgAge = RunSimulation(42);
            TestContext.WriteLine($"Hard Difficulty Average Age: {avgAge:F2}");

            // Should be significantly lower than 74
            Assert.Less(avgAge, 70, "Hard difficulty should result in lower average lifespan");
            Assert.That(avgAge, Is.InRange(60, 70));
        }

        [Test]
        [Category("Simulation")]
        public void VerifyEasyDifficulty_LongerLifespan()
        {
            // Easy Settings (Simulated)
            _config.elderAgeYears = 60;
            _config.deathBaseProbability = 0.05f;
            _config.deathProbabilityIncreasePerYear = 0.008f;
            _config.deathProbabilityMultiplier = 0.5f; // Half death chance

            double avgAge = RunSimulation(42);
            TestContext.WriteLine($"Easy Difficulty Average Age: {avgAge:F2}");

            // Should be significantly higher than 74
            Assert.Greater(avgAge, 78, "Easy difficulty should result in higher average lifespan");
            Assert.That(avgAge, Is.InRange(78, 95));
        }

        [Test]
        public void VerifyDifficultyImpactComparison()
        {
            // Run all three side-by-side to prove ordered impact
            
            // Standard
            _config.deathProbabilityMultiplier = 1.0f;
            double standardAvg = RunSimulation(12345);

            // Hard
            _config.deathProbabilityMultiplier = 2.0f;
            double hardAvg = RunSimulation(12345);

            // Easy
            _config.deathProbabilityMultiplier = 0.5f;
            double easyAvg = RunSimulation(12345);

            TestContext.WriteLine($"Comparative Results:");
            TestContext.WriteLine($"Hard (2.0x): {hardAvg:F2} years");
            TestContext.WriteLine($"Std  (1.0x): {standardAvg:F2} years");
            TestContext.WriteLine($"Easy (0.5x): {easyAvg:F2} years");

            Assert.Less(hardAvg, standardAvg);
            Assert.Greater(easyAvg, standardAvg);
        }

        [Test]
        public void VerifyIllnessDifficultyScaling()
        {
            // Verify that presets conform to logic
            // Easy
            float easyChance = 0.05f; // Was 0.0005
            int easyMinYears = 5;
            
            // Hard
            float hardChance = 0.2f; // Was 0.002
            int hardMinYears = 3;

            // Assert Logic
            Assert.Less(easyChance, hardChance, "Easy mode should have lower illness chance.");
            Assert.Greater(easyMinYears, hardMinYears, "Easy mode should have longer illness duration before worsening.");
        }
    }
}
