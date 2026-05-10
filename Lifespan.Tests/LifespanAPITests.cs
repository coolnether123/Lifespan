using System.Runtime.Serialization;
using System.Reflection;
using Lifespan;
using ModAPI.Core;
using ModAPI.Util;
using Moq;
using NUnit.Framework;

namespace Lifespan.Tests
{
    [TestFixture]
    public class LifespanAPITests
    {
        private Mock<IPluginContext> _ctx;
        private LifespanConfig _config;
        private AgeTracker _tracker;
        private LifespanAPIImpl _api;

        [SetUp]
        public void Setup()
        {
            var log = new Mock<IModLogger>();
            _ctx = new Mock<IPluginContext>();
            _ctx.Setup(c => c.Log).Returns(log.Object);

            _config = new LifespanConfig();
            _tracker = new AgeTracker(_ctx.Object, _config, new ModRandomStream(4321), new InMemoryAgeDataStore());
            _tracker.LoadAgeData();
            _api = new LifespanAPIImpl(_ctx.Object, _config, _tracker, null, null, null);
        }

        [Test]
        public void UpdateExternalCharacters_RespectsCancellation()
        {
            var visitor = CreateNpc(501);
            _api.SetCharacterAgeWeeks(visitor, 100);

            bool eventFired = false;
            _api.OnBeforeCharacterAged += character => false;
            _api.OnCharacterAged += (character, ageWeeks) => eventFired = true;

            _api.UpdateExternalCharacters(4);

            Assert.AreEqual(100, _api.GetCharacterAgeWeeks(visitor));
            Assert.IsFalse(eventFired);
        }

        [Test]
        public void UpdateExternalCharacters_PublishesConcreteCharacter()
        {
            var visitor = CreateNpc(502);
            _api.SetCharacterAgeWeeks(visitor, 100);

            BaseCharacter agedCharacter = null;
            int agedWeeks = 0;
            _api.OnCharacterAged += (character, ageWeeks) =>
            {
                agedCharacter = character;
                agedWeeks = ageWeeks;
            };

            _api.UpdateExternalCharacters(4);

            Assert.AreSame(visitor, agedCharacter);
            Assert.AreEqual(104, agedWeeks);
            Assert.AreEqual(104, _api.GetCharacterAgeWeeks(visitor));
        }

        [Test]
        public void GetTrackedExternalCharacters_ReturnsLiveRegisteredExternalCharacters()
        {
            var visitor = CreateNpc(503);
            _api.SetCharacterAgeWeeks(visitor, 100);

            var tracked = _api.GetTrackedExternalCharacters();

            Assert.AreEqual(1, tracked.Count);
            Assert.AreSame(visitor, tracked[0]);
        }

        [Test]
        public void SetConfiguration_UpdatesSharedConfigInstance()
        {
            var updated = new LifespanConfig
            {
                weeksAgedPerInterval = 13,
                adultAgeYears = 20,
                elderAgeYears = 10
            };

            _api.SetConfiguration(updated);

            Assert.AreSame(_config, _api.GetConfiguration());
            Assert.AreEqual(13, _config.weeksAgedPerInterval);
            Assert.AreEqual(20, _config.adultAgeYears);
            Assert.AreEqual(21, _config.elderAgeYears);
        }

        private static NpcVisitor CreateNpc(int id)
        {
            var visitor = (NpcVisitor)FormatterServices.GetUninitializedObject(typeof(NpcVisitor));
            SetField(visitor, typeof(NpcVisitor), "m_npcId", id);
            return visitor;
        }

        private static void SetField(object instance, System.Type type, string fieldName, object value)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(instance, value);
        }

        private sealed class InMemoryAgeDataStore : IAgeDataStore
        {
            private AgeData _data = new AgeData { lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek };

            public bool HasSavedData => true;

            public AgeData Load()
            {
                return _data;
            }

            public void Save(AgeData data)
            {
                _data = data;
            }

            public void Clear()
            {
                _data = new AgeData { lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek };
            }
        }
    }
}
