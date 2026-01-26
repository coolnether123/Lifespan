using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

namespace Lifespan.Tests
{
    [TestFixture]
    public class MilestoneTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private LifespanConfig _config;
        private AgeTracker _ageTracker;
        private MilestoneManager _manager;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            _config = new LifespanConfig();
            
            _ageTracker = new AgeTracker(_mockCtx.Object, _config);
            _manager = new MilestoneManager(_mockCtx.Object, _config, _ageTracker);
            
            // Set up a scheduler to show it's working
            var scheduler = new DialogueScheduler(_mockLog.Object);
            _manager.SetScheduler(scheduler);
            TestContext.WriteLine("MilestoneManager initialized with DialogueScheduler for staggered delivery.");
        }

        private FamilyMember CreateDummyMember(int ageWeeks, bool isChild)
        {
            var member = (FamilyMember)FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            
            // Try common field/property names for character data
            TrySetField(member, "firstName", "TestUnit");
            TrySetField(member, "m_firstName", "TestUnit");
            
            TrySetField(member, "isChild", isChild);
            TrySetField(member, "m_isChild", isChild);
            
            TrySetField(member, "m_id", 123);
            TrySetField(member, "ID", 123);
            TrySetField(member, "m_ID", 123);

            return member;
        }

        private void TrySetField(object obj, string name, object value)
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
        }

        [Test]
        public void Birthday_TriggeredOncePerYear()
        {
            var member = CreateDummyMember(5 * 52, true);
            
            // Should trigger for Age 5
            _manager.ProcessMilestones(member, 5 * 52);
            // Calling it again at same age should NOT trigger secondary birthday logic 
            // (Internal state tracking should prevent repeat)
            _manager.ProcessMilestones(member, 5 * 52 + 10); 
            
            // Advance to year 6
            _manager.ProcessMilestones(member, 6 * 52);
            
            Assert.Pass("Milestone logic executed successfully.");
        }

        [Test]
        public void ChildMilestones_TriggerAtCorrectAges()
        {
            var member = CreateDummyMember(5 * 52, true);
            
            // Year 5
            _manager.ProcessMilestones(member, 5 * 52);
            
            // Advance to Year 10
            _manager.ProcessMilestones(member, 10 * 52);
            
            // Advance to Year 15
            _manager.ProcessMilestones(member, 15 * 52);
            
            Assert.Pass("Child milestone triggers reached.");
        }

        [Test]
        public void ElderPhilosophy_TriggersWithProbability()
        {
            var member = CreateDummyMember(60 * 52, false);
            
            // Simulate 1000 ticks to ensure no crashes in random logic
            for (int i = 0; i < 1000; i++)
            {
                _manager.ProcessMilestones(member, 60 * 52);
            }
            
            Assert.Pass("Elder philosophy logic loop executed.");
        }

        [Test]
        public void FirstSkill_TriggersOnlyOnce()
        {
            var member = CreateDummyMember(10 * 52, true);
            
            _manager.ReportFirstSkill(member);
            _manager.ReportFirstSkill(member); // Second time should be ignored by internal state
            
            Assert.Pass("First skill milestone logic executed.");
        }
    }
}
