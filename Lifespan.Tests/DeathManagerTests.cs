using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using Moq;

namespace Lifespan.Tests
{
    [TestFixture]
    public class DeathManagerTests
    {
        private Mock<IPluginContext> _mockCtx;
        private Mock<IModLogger> _mockLog;
        private AgeTracker _tracker;
        private LifespanConfig _config;
        private DeathManager _manager;

        [Test]
        public void PendingDeath_RestoreAndSave_BindsCurrentMemberAndPreservesDecision()
        {
            var member = (FamilyMember)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FamilyMember));
            typeof(UnityEngine.Object).GetField("m_CachedPtr", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(member, new System.IntPtr(1));
            member.SetId(501);
            typeof(BaseCharacter).GetField("m_health", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(member, 100);
            _tracker.State.Replace(AgeData.FromSerializable(new AgeDataSerializable
            {
                pendingDeaths = new System.Collections.Generic.List<PendingDeathEntry>
                {
                    new PendingDeathEntry { id = 501, targetDay = 107, reason = "Old age", surge = true, remainingSeconds = 4f }
                }
            }));
            _manager = new DeathManager(_mockCtx.Object, _config, _tracker, new ModRandomStream(5678), _tracker.State.Deaths, () => 10f);
            _manager.RestorePendingDeaths(new System.Collections.Generic.List<FamilyMember> { member });
            _manager.CapturePendingDeaths();
            Assert.AreEqual(1, _tracker.State.Data.pendingDeaths.Count);
            Assert.AreEqual(501, _tracker.State.Data.pendingDeaths[0].id);
            Assert.AreEqual(107, _tracker.State.Data.pendingDeaths[0].targetDay);
            Assert.AreEqual("Old age", _tracker.State.Data.pendingDeaths[0].reason);
            Assert.IsTrue(_tracker.State.Data.pendingDeaths[0].surge);
            Assert.AreEqual(4f, _tracker.State.Data.pendingDeaths[0].remainingSeconds);
            _manager.Reset();
            _manager.CapturePendingDeaths();
            Assert.IsEmpty(_tracker.State.Data.pendingDeaths);
        }

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            var mockSave = new Mock<ISaveSystem>();
            _mockCtx.Setup(c => c.SaveSystem).Returns(mockSave.Object);
            
            _config = new LifespanConfig();
            _tracker = new AgeTracker(_mockCtx.Object, _config, new ModRandomStream(1234));
            
            _manager = new DeathManager(_mockCtx.Object, _config, _tracker, new ModRandomStream(5678));
        }
    }
}
