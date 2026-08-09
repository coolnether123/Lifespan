using System;
using ModAPI.Core;
using ModAPI.Persistence;

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
        private readonly AgePersistenceContainer _container;
        private Action _prepareForSave;
        private Action _restoreAfterLoad;
        private Func<bool> _validateAfterLoad;

        public AgeDataStore(IPluginContext ctx)
        {
            _log = ctx.Log;
            _container = new AgePersistenceContainer(this);
            ctx.SaveSystem.RegisterModData(SaveKey, _container);
            _log.Debug("[AgeTracker] Save container registered: " + SaveKey);
        }

        public void ConfigurePersistenceLifecycle(Action prepareForSave, Action restoreAfterLoad, Func<bool> validateAfterLoad)
        {
            _prepareForSave = prepareForSave;
            _restoreAfterLoad = restoreAfterLoad;
            _validateAfterLoad = validateAfterLoad;
        }

        public bool HasSavedData => _container != null && _container.HasAnyData();

        public AgeData Load()
        {
            return AgeData.FromSerializable(_container);
        }

        public void Save(AgeData data)
        {
            _container.CopyFrom(data);
        }

        public void Clear()
        {
            _container.Clear();
        }

        private sealed class AgePersistenceContainer : AgeDataSerializable, IModPersistenceLifecycle
        {
            private readonly AgeDataStore owner;

            internal AgePersistenceContainer(AgeDataStore owner)
            {
                this.owner = owner;
            }

            public void PrepareForSave(IModSaveContext context)
            {
                if (owner._prepareForSave != null)
                    owner._prepareForSave();
            }

            public void RestoreAfterLoad(IModSaveContext context)
            {
                if (owner._restoreAfterLoad != null)
                    owner._restoreAfterLoad();
            }

            public bool ValidateAfterLoad(IModSaveContext context, out string diagnosticMessage)
            {
                if (owner._validateAfterLoad == null || owner._validateAfterLoad())
                {
                    diagnosticMessage = null;
                    return true;
                }

                diagnosticMessage = "AgeTracker did not finish hydrating the restored age container.";
                return false;
            }
        }
    }
}
