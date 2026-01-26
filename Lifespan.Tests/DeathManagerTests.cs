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
        private Mock<AgeTracker> _mockTracker;
        private LifespanConfig _config;
        private DeathManager _manager;

        [SetUp]
        public void Setup()
        {
            _mockCtx = new Mock<IPluginContext>();
            _mockLog = new Mock<IModLogger>();
            _mockCtx.Setup(c => c.Log).Returns(_mockLog.Object);
            
            _config = new LifespanConfig();
            _mockTracker = new Mock<AgeTracker>(_mockCtx.Object, _config);
            
            _manager = new DeathManager(_mockCtx.Object, _config, _mockTracker.Object);
        }
    }
}
