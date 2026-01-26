using NUnit.Framework;
using Lifespan;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Lifespan.Tests
{
    [TestFixture]
    public class HairGreyingTests
    {
        [Test]
        public void GetGreyFactor_BeforeStartAge_ReturnsZero()
        {
            // Arrange
            int startAge = 40;
            int duration = 10;
            float maxCoverage = 1.0f;
            var gene = new GreyingGene(startAge, duration, maxCoverage);

            // Act
            float factor = gene.GetGreyFactor(39f);

            // Assert
            Assert.AreEqual(0f, factor);
        }

        [Test]
        public void GetGreyFactor_HalfwayThrough_ReturnsHalfMaxCoverage()
        {
            // Arrange
            int startAge = 40;
            int duration = 10;
            float maxCoverage = 1.0f;
            var gene = new GreyingGene(startAge, duration, maxCoverage);

            // Act
            // 40 + (10/2) = 45
            float factor = gene.GetGreyFactor(45f);

            // Assert
            Assert.AreEqual(0.5f, factor, 0.001f);
        }

        [TestCase(1.0f)]
        [TestCase(0.5f)]
        public void GetGreyFactor_AfterDuration_ReturnsMaxCoverage(float maxCoverage)
        {
            // Arrange
            int startAge = 40;
            int duration = 10;
            var gene = new GreyingGene(startAge, duration, maxCoverage);

            // Act
            float factor = gene.GetGreyFactor(51f);

            // Assert
            Assert.AreEqual(maxCoverage, factor, 0.001f);
        }

        [Test]
        public void Statistical_GreyingTimeAnalysis()
        {
            // Test 10,000 generations to verify the distribution of greying speeds
            int iterations = 10000;
            var rng = new System.Random(12345);
            
            List<int> durations = new List<int>();
            List<int> startAges = new List<int>();
            int noGreyCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                var gene = GreyingGene.GenerateRandom(rng);
                
                // Check "No Grey" condition (StartAge 99)
                if (gene.StartAge >= 99)
                {
                    noGreyCount++;
                    continue;
                }

                durations.Add(gene.DurationYears);
                startAges.Add(gene.StartAge);
            }

            // Analyze
            double avgDuration = durations.Average();
            int minDuration = durations.Min();
            int maxDuration = durations.Max();
            double avgStartAge = startAges.Average();

            TestContext.WriteLine($"Analysis of {iterations} genes:");
            TestContext.WriteLine($"No Grey Ratio: {noGreyCount / (float)iterations:P1} (Expected ~10%)");
            TestContext.WriteLine($"Avg Duration: {avgDuration:F2} years");
            TestContext.WriteLine($"Min Duration: {minDuration} years");
            TestContext.WriteLine($"Max Duration: {maxDuration} years");
            TestContext.WriteLine($"Avg Start Age: {avgStartAge:F2} years");

            // Assertions based on "GenerateRandom" logic
            // 1. Min duration should be around 3 (Fastest possible defined as 3)
            Assert.GreaterOrEqual(minDuration, 3);
            
            // 2. Max duration should be around 45 (Slowest defined as 45)
            Assert.LessOrEqual(maxDuration, 50);

            // 3. Average duration is weighted:
            // 20% * ~6.5 (3-10 range)
            // 40% * ~13 (10-16 range)
            // 40% * ~32.5 (20-45 range)
            // Approx Weighted Avg = 1.3 + 5.2 + 13 = ~19.5
            // Allow some variance
            Assert.That(avgDuration, Is.InRange(15, 25), "Average duration falls outside expected statistical weighting.");

            // 4. No Grey Ratio should be around 10%
            float noGreyRatio = noGreyCount / (float)iterations;
            Assert.That(noGreyRatio, Is.InRange(0.08f, 0.12f));
        }

        [Test]
        public void Curve_LinearityTest()
        {
            // Verify the linear progression logic
            int startAge = 30;
            int duration = 20;
            var gene = new GreyingGene(startAge, duration, 1.0f);

            // 25% progress
            Assert.AreEqual(0.25f, gene.GetGreyFactor(35), 0.001f);
            
            // 50% progress
            Assert.AreEqual(0.50f, gene.GetGreyFactor(40), 0.001f);
            
            // 75% progress
            Assert.AreEqual(0.75f, gene.GetGreyFactor(45), 0.001f);
        }
    }
}
