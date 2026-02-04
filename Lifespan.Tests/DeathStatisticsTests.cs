using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using ModAPI.Saves;
using Moq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lifespan.Tests
{
    [TestFixture]
    public class DeathStatisticsTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private Mock<AgeTracker> _mockTracker;
        private LifespanConfig _config;
        private DeathManager _manager;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            
            var mockSave = new Mock<ISaveSystem>();
            _mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);

            _config = new LifespanConfig();
            // Default config values (Percentage Scale)
            _config.elderAgeYears = 60;
            _config.deathBaseProbability = 0.05f; // Was 0.0005
            _config.deathProbabilityIncreasePerYear = 0.008f; // Was 0.00008
            _config.deathProbabilityMultiplier = 1.0f;

            // Important: AgeTracker ctor will now work because ScanSystem is mocked
            _mockTracker = new Mock<AgeTracker>(_mockCtx.Object, _config);
            _manager = new DeathManager(_mockCtx.Object, _config, _mockTracker.Object);
        }

        [Test]
        [Category("Simulation")]
        public void SimulatePopulationDeath_CalculateAverages()
        {
            const int populationSize = 1000;
            var deathAges = new List<int>();
            var rng = new System.Random(42); // Deterministic seed

            for (int i = 0; i < populationSize; i++)
            {
                // Start each person at age 60 (elder threshold)
                int currentAgeWeeks = 60 * 52;
                bool isDead = false;

                // Simulate week by week until death or absolute max age
                while (!isDead && currentAgeWeeks < 200 * 52)
                {
                    // 2. Probability Roll (Simplified version of DeathManager logic)
                    int elderWeeks = _config.elderAgeYears * 52;
                    float yearsPastElder = (float)(currentAgeWeeks - elderWeeks) / 52f;
                    
                    // From DeathManager.cs (Updated for Percentage Scale):
                    float baseProb = (_config.deathBaseProbability / 100f) + (yearsPastElder * (_config.deathProbabilityIncreasePerYear / 100f));
                    
                    // Assume healthy population (healthImpact = 1.0)
                    float finalProb = baseProb * _config.deathProbabilityMultiplier;

                    if (rng.NextDouble() < finalProb)
                    {
                        isDead = true;
                        deathAges.Add(currentAgeWeeks / 52);
                    }
                    else
                    {
                        currentAgeWeeks++; // Age 1 week
                    }
                }
            }

            double averageAge = deathAges.Average();
            int minAge = deathAges.Min();
            int maxAge = deathAges.Max();

            TestContext.WriteLine($"Population Size: {populationSize}");
            TestContext.WriteLine($"Average Age of Death: {averageAge:F2} years");
            TestContext.WriteLine($"Min Age of Death: {minAge} years");
            TestContext.WriteLine($"Max Age of Death: {maxAge} years");

            // Distribution buckets
            var buckets = deathAges.GroupBy(a => (a / 5) * 5).OrderBy(g => g.Key);
            TestContext.WriteLine("\nAge Distribution:");
            foreach (var group in buckets)
            {
                TestContext.WriteLine($"{group.Key}-{group.Key+4}: {group.Count()}");
            }

            Assert.GreaterOrEqual(averageAge, 70, "Average age seems too low.");
            Assert.LessOrEqual(averageAge, 85, "Average age seems too high.");
        }
    }
}
