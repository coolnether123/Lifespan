using ModAPI.Core;
using ModAPI.Util;
using System.Collections.Generic;

namespace Lifespan
{
    /// <summary>
    /// Implementation of the public Lifespan API.
    /// </summary>
    internal class LifespanAPIImpl : ILifespanAPI
    {
        private readonly AgeTracker _ageTracker;
        private readonly ElderIllnessManager _illnessManager;
        private readonly DevelopmentGeneManager _devGeneManager;
        private readonly ChildDevelopmentManager _childManager;
        private LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly IPluginContext _ctx;

        public LifespanAPIImpl(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ElderIllnessManager illnessManager, DevelopmentGeneManager devGeneManager, ChildDevelopmentManager childManager)
        {
            _ctx = ctx;
            _config = config;
            _ageTracker = ageTracker;
            _illnessManager = illnessManager;
            _devGeneManager = devGeneManager;
            _childManager = childManager;
            _log = ctx.Log;
        }

        public int GetCharacterAgeWeeks(FamilyMember member) => _ageTracker.GetAgeWeeks(member);
        public int GetCharacterAgeYears(FamilyMember member) => _ageTracker.GetAgeYears(member);
        public void SetCharacterAgeWeeks(FamilyMember member, int weeks) => _ageTracker.SetAgeWeeks(member, weeks);
        public void SetCharacterAgeWeeks(BaseCharacter character, int weeks) => _ageTracker.SetAgeWeeks(character, weeks);
        public bool IsElder(FamilyMember member) => _ageTracker.IsElder(member);
        public List<string> GetActiveIllnesses(FamilyMember member) => _illnessManager.GetActiveIllnesses(member);
        public void AddIllness(FamilyMember member, string illnessId) => _illnessManager.AddIllnessExternal(member, illnessId);
        public void RemoveIllness(FamilyMember member, string illnessId) => _illnessManager.RemoveIllnessExternal(member, illnessId);
        public LifespanConfig GetConfiguration() => _config;

        // Generic / NPC API Implementation
        public int GenerateAgeForNPC(BaseCharacter character, AgeContext context) => _ageTracker.GenerateAgeForContext(character, context);
        public int GetCharacterAgeWeeks(BaseCharacter character) => _ageTracker.GetAgeWeeks(character);
        
        public int IncrementNPCAge(BaseCharacter character, int weeks)
        {
            if (character == null) return 0;
            
            int current = _ageTracker.GetAgeWeeks(character);
            int newAge = current + weeks;
            
            _ageTracker.SetExternalCharacterAge(character.GetId(), newAge);
            OnCharacterAged?.Invoke(character, newAge);
            
            return newAge;
        }

        public event System.Action<BaseCharacter, int> OnCharacterAged;
        public event System.Func<BaseCharacter, bool> OnBeforeCharacterAged;

        internal bool ShouldCancelAging(BaseCharacter character)
        {
             if (OnBeforeCharacterAged == null) return false;
             
             foreach(System.Func<BaseCharacter, bool> handler in OnBeforeCharacterAged.GetInvocationList())
             {
                 // If any handler returns FALSE (meaning "don't proceed"), we cancel.
                 // Wait, standard Func delegates return a value. 
                 // The API spec said "Return FALSE to cancel".
                 if (!handler(character)) return true; // Cancelled
             }
             return false;
        }

        /// <summary>
        /// Updates age for all tracked external characters (NPCs) that are NOT family members.
        /// </summary>
        public void UpdateExternalCharacters(int weeks)
        {
            // We need a way to get all tracked NPCs from AgeTracker
            // Ideally AgeTracker should expose a method for this safe iteration
            var externalIds = _ageTracker.GetAllTrackedExternalIds();
            
            foreach(int id in externalIds)
            {
                // We don't have the BaseCharacter object here easily unless we track it or resolve it.
                // However, for pure data aging, we can update the ID-based record.
                // IF we had the object, we could invoke OnBeforeCharacterAged.
                // Limitation: Without the object, we can't run the detailed cancellation check efficiently 
                // unless we change how we store external characters (ID -> Object ref).
                // For now, we update the data.
                
                int currentAge = _ageTracker.GetAgeWeeks(id);
                int newAge = currentAge + weeks;
                _ageTracker.SetExternalCharacterAge(id, newAge);
                
                // Note: OnCharacterAged event typically desires the object. 
                // Passing null implies we only updated data for an off-screen entity.
                OnCharacterAged?.Invoke(null, newAge);
            }
        }

        public void SetConfiguration(LifespanConfig config)
        {
            if (config != null)
            {
                config.ValidateAndClamp();
                
                // Update the shared config object reference (effectively updating the plugin's state)
                _config = config;
                
                // Also update the plugin's public reference if possible, or assume they share the same object ref if passed around correctly.
                // In this case, we update the object the API holds. Since the Plugin passed it by reference, 
                // we should copy values if we want to retain the original object instance, OR assume caller replaces it.
                // Best practice: Overwrite values on the existing instance to maintain references held by other managers.
                // However, since we don't have a Copy method, and existing code replaced _config, we'll stick to that but we MUST ensure persistence.
                
                try
                {
                    // Update DevGeneManager with new hot-reloaded values
                    if (_devGeneManager != null) _devGeneManager.RefreshSettings(_config);

                    if (_ctx.Mod.SettingsProvider is ModAPI.Spine.SettingsController controller)
                    {
                        controller.Save();
                        _log.Info("Configuration updated and saved via SettingsController.");
                    }
                    else
                    {
                        _log.Warn("Could not save configuration: SettingsProvider is not a SettingsController.");
                    }
                }
                catch (System.Exception ex)
                {
                    _log.Error($"Failed to save configuration: {ex.Message}");
                }
            }
        }
        public void SetInitialDevelopmentPotential(FamilyMember member, int potential)
        {
            // Ensure we have valid references
            if (member == null || _devGeneManager == null) return;

            // Get the gene (or generate a default one if missing)
            var gene = _devGeneManager.GetOrGenerateGene(member);
            if (gene != null)
            {
                gene.PreAdultPotential = potential;
                // Optional: Reset gains if you want this to be a hard reset
                // gene.PreAdultGainsAwarded = 0;
                _log.Info($"Set initial development potential for {member.firstName} to {potential}.");
            }
        }

        public ChildStage GetChildStage(FamilyMember member)
        {
            if (_childManager == null) return ChildStage.Adult;
            return _childManager.GetStage(member);
        }

        public int CalculateTotalPopulation(System.Func<BaseCharacter, bool> filter = null)
        {
            // Count family members
            int count = 0;
            if (FamilyManager.Instance != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (m != null && !m.isDead && (filter == null || filter(m)))
                        {
                            count++;
                        }
                    }
                }
            }
            // FUTURE: Iterate over external/tracked NPCs if AgeTracker exposes them
            return count;
        }

        public int GetPopulationInAgeRange(int minAgeYears, int maxAgeYears)
        {
            int count = 0;
             if (FamilyManager.Instance != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (m != null && !m.isDead)
                        {
                            int age = _ageTracker.GetAgeYears(m);
                            if (age >= minAgeYears && age <= maxAgeYears)
                            {
                                count++;
                            }
                        }
                    }
                }
            }
            return count;
        }

        public List<FamilyMember> GetCharactersByStage(ChildStage stage)
        {
            var result = new List<FamilyMember>();
            if (FamilyManager.Instance != null && _childManager != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    foreach (var m in members)
                    {
                        if (m != null && !m.isDead && _childManager.GetStage(m) == stage)
                        {
                            result.Add(m);
                        }
                    }
                }
            }
            return result;
        }
    }
}
