using NUnit.Framework;
using Lifespan;
using ModAPI.Core;
using ModAPI.Saves;
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
