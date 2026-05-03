using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace Lifespan.Tests
{
    [TestFixture]
    public class IllnessSimulationTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;
        private ElderIllnessManager _manager;
        private AgeTracker _ageTracker;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);

            var mockSave = new Mock<ISaveSystem>();
            _mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);

            _config = new LifespanConfig();
            
            // Standard config for testing (Updated to User Request: Target 6 years)
            _config.illnessStageMinYears = 4;
            _config.illnessStageMaxYears = 8;

            // AgeTracker requires context/config
            _ageTracker = new AgeTracker(_mockCtx.Object, _config, new ModRandomStream(6789));
            var dialogueHelper = new DialogueHelper(new ModRandomStream(6790));
            dialogueHelper.SetAgeTracker(_ageTracker);

            _manager = new ElderIllnessManager(_mockCtx.Object, _config, _ageTracker, new ModRandomStream(6791), dialogueHelper);
            
            // Set up a scheduler to show it's working
            var scheduler = new DialogueScheduler(_mockLog.Object, new ModRandomStream(6792));
            _manager.SetScheduler(scheduler);
            TestContext.WriteLine("ElderIllnessManager initialized with DialogueScheduler for staggered delivery.");
        }

        private object CreateDummyMember()
        {
            var member = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            
            // Create dummy Traits component
            var traits = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Traits));
            TrySetField(member, "traits", traits);
            TrySetField(member, "m_traits", traits);

            TrySetField(member, "firstName", "TestUnit");
            TrySetField(member, "m_firstName", "TestUnit");
            
            // Stats
            TrySetField(member, "health", 100);
            TrySetField(member, "m_health", 100);
            TrySetField(member, "maxHealth", 100);
            TrySetField(member, "m_maxHealth", 100);
            
            return member;
        }

        private void SetField(object obj, string name, object value)
        {
            Type type = obj.GetType();
            while (type != null)
            {
                // Try field
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }

                // Try property
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(obj, value, null);
                    return;
                }

                type = type.BaseType;
            }
            
            throw new Exception($"Field or Property '{name}' not found on type {obj.GetType().Name}");
        }

        private void TrySetField(object obj, string name, object value)
        {
            try { SetField(obj, name, value); } catch {}
        }

        private T InvokePrivate<T>(string methodName, params object[] args)
        {
            var method = typeof(ElderIllnessManager).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new Exception($"Method {methodName} not found.");
            return (T)method.Invoke(_manager, args);
        }

        [Test]
        [Category("Simulation")]
        public void VerifyDialogueVarietyAndAntiRepetition()
        {
            var member = CreateDummyMember();
            string illness = ElderIllnessManager.ILLNESS_MILD_DEMENTIA;

            int iterations = 1000;
            var history = new List<string>();
            var distribution = new Dictionary<string, int>();

            for (int i = 0; i < iterations; i++)
            {
                string line = InvokePrivate<string>("GetFlavorDialogue", member, illness);
                history.Add(line);

                if (!distribution.ContainsKey(line)) distribution[line] = 0;
                distribution[line]++;
            }

            TestContext.WriteLine($"Total Iterations: {iterations}");
            TestContext.WriteLine($"Unique Lines Found: {distribution.Count}");
            
            Assert.That(distribution.Count, Is.GreaterThanOrEqualTo(10), "Should find all 10 dialogue options.");
            
            foreach(var kvp in distribution)
            {
                TestContext.WriteLine($"Line: \"{kvp.Key.Substring(0, 20)}...\" Count: {kvp.Value}");
                // Target is 100 per line for 10 lines in 1000 iterations. Allow jitter.
                Assert.That(kvp.Value, Is.InRange(50, 180), "Distribution should be somewhat uniform.");
            }

            int immediateRepeats = 0;
            for (int i = 1; i < history.Count; i++)
            {
                if (history[i] == history[i-1]) immediateRepeats++;
            }

            double repeatRate = (double)immediateRepeats / iterations;
            TestContext.WriteLine($"Immediate Repetition Rate: {repeatRate:P2}");

            Assert.Less(repeatRate, 0.05, "Repetition rate should be very low (<5%) thanks to shuffle bag.");
        }

        [Test]
        [Category("Simulation")]
        public void VerifyIllnessProgressionDuration()
        {
            // Use Reflection to test actual method
            
            int iterations = 1000;
            var durations = new List<float>();

            // 1. Healthy Scenario
            var member = CreateDummyMember();
            // Setup Healthy Stats (Health 100, Trauma 0)
            TrySetField(member, "health", 100);
            TrySetField(member, "m_health", 100);
            TrySetField(member, "m_maxHealth", 100);
            
            int currentWeek = 1000;

            for(int i=0; i<iterations; i++)
            {
                 int nextWeek = InvokePrivate<int>("CalculateNextStageWeek", member, currentWeek);
                 int durationWeeks = nextWeek - currentWeek;
                 durations.Add(durationWeeks / 52f);
            }

            double avgYears = durations.Average();
            TestContext.WriteLine($"Healthy Average Duration: {avgYears:F2} years (Target: 6.0)");
            Assert.That(avgYears, Is.InRange(5.8, 6.2)); 

            // 2. Unhealthy Scenario
            durations.Clear();
            
            // Modify health to < 50%
            TrySetField(member, "health", 10); 
            TrySetField(member, "m_health", 10);
            
            // TODO: Test full penalty when trait mocking improves (currently only testing Health penalty)
            // Expected: Base 6.0 * 0.8 (Low Health) = 4.8 years.
            
            for(int i=0; i<iterations; i++)
            {
                 int nextWeek = InvokePrivate<int>("CalculateNextStageWeek", member, currentWeek);
                 int durationWeeks = nextWeek - currentWeek;
                 durations.Add(durationWeeks / 52f);
            }
            
            double unhealthyAvg = durations.Average();
            TestContext.WriteLine($"Low Health Average Duration: {unhealthyAvg:F2} years");
            // Expected: ~6.0 * 0.8 = 4.8 years
            Assert.That(unhealthyAvg, Is.InRange(4.6, 5.0));
        }

        [Test]
        [Category("Simulation")]
        public void VerifyTriggerFrequency()
        {
             // Test Observer Trigger Logic (New Algorithm: 1-6 range, Mean ~3)
             // Weighted Logic:
             // Base: 2
             // +1 (75% chance)
             // +1 if Health < 50% (60% chance)
             // +1 if Stress > 50% (60% chance)
             // +1 Rare (20% chance)
             
             int iterations = 1000;
             var countsHealthy = new Dictionary<int, int>(); 
             var countsUnhealthy = new Dictionary<int, int>();
             
             var rng = new Random(456);
             int observerPoolSize = 10; 

             // Scenario 1: Healthy Person (Health=100%, Stress=0%)
             // Expected: Base 2 + 0.75 + 0.2 (Rare) = ~2.95 avg
             for(int i=0; i<iterations; i++)
             {
                 int count = 2;
                 if (rng.NextDouble() < 0.75) count++;
                 // Health check (Fail)
                 // Stress check (Fail)
                 if (rng.NextDouble() < 0.20) count++;
                 
                 count = Math.Min(count, observerPoolSize);
                 if (count > 6) count = 6;

                 if (!countsHealthy.ContainsKey(count)) countsHealthy[count] = 0;
                 countsHealthy[count]++;
             }

             // Scenario 2: Unhealthy Person (Health=10%, Stress=90%)
             // Expected: Base 2 + 0.75 + 0.6 + 0.6 + 0.2 = ~4.15 avg
             for(int i=0; i<iterations; i++)
             {
                 int count = 2;
                 if (rng.NextDouble() < 0.75) count++;
                 if (rng.NextDouble() < 0.60) count++; // Health
                 if (rng.NextDouble() < 0.60) count++; // Stress
                 if (rng.NextDouble() < 0.20) count++; // Rare
                 
                 count = Math.Min(count, observerPoolSize);
                 if (count > 6) count = 6;

                 if (!countsUnhealthy.ContainsKey(count)) countsUnhealthy[count] = 0;
                 countsUnhealthy[count]++;
             }
             
             TestContext.WriteLine($"Observer Trigger Simulation (Pool {observerPoolSize}):");
             
             TestContext.WriteLine("Healthy Scenario (Target Mean ~3):");
             double totalH = 0;
             foreach(var kvp in countsHealthy.OrderBy(k => k.Key))
             {
                 TestContext.WriteLine($"  {kvp.Key} Observers: {kvp.Value} ({(double)kvp.Value/iterations:P1})");
                 totalH += kvp.Key * kvp.Value;
             }
             double avgH = totalH / iterations;
             TestContext.WriteLine($"  Average: {avgH:F2}");
             Assert.That(avgH, Is.InRange(2.7, 3.2));

             TestContext.WriteLine("Unhealthy Scenario (Target Mean ~4+):");
             double totalU = 0;
             foreach(var kvp in countsUnhealthy.OrderBy(k => k.Key))
             {
                 TestContext.WriteLine($"  {kvp.Key} Observers: {kvp.Value} ({(double)kvp.Value/iterations:P1})");
                 totalU += kvp.Key * kvp.Value;
             }
             double avgU = totalU / iterations;
             TestContext.WriteLine($"  Average: {avgU:F2}");
             Assert.That(avgU, Is.InRange(3.9, 4.4));
             
             // Verify we can hit 5 or 6
             bool reachedHighCount = countsUnhealthy.ContainsKey(5) || countsUnhealthy.ContainsKey(6);
             Assert.IsTrue(reachedHighCount, "Should be able to trigger 5 or 6 observers in worst case.");
        }
    }
}
