using ModAPI.Core;

namespace Lifespan
{
    internal interface IAgeDataStore
    {
        bool HasSavedData { get; }
        AgeData Load();
        void Save(AgeData data);
        void Clear();
    }

    internal sealed class AgeDataStore : IAgeDataStore
    {
        public const string SaveKey = "LifeSpan.AgeData";

        private readonly IModLogger _log;
        private readonly AgeDataSerializable _container;

        public AgeDataStore(IPluginContext ctx)
        {
            _log = ctx.Log;
            _container = new AgeDataSerializable();
            ctx.SaveSystem.RegisterModData(SaveKey, _container);
            _log.Debug("[AgeTracker] Save container registered: " + SaveKey);
        }

        public bool HasSavedData => _container != null && _container.HasAnyData();

        public AgeData Load()
        {
            return AgeData.FromSerializable(_container);
        }

        public void Save(AgeData data)
        {
            _container.CopyFrom(data != null ? data.ToSerializable() : null);
        }

        public void Clear()
        {
            _container.Clear();
        }
    }
}
