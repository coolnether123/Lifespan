using ModAPI.Core;
using ModAPI.Util;
using System;
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
        internal bool TryGetCharacterAgeWeeks(BaseCharacter character, out int ageWeeks) => _ageTracker.TryGetAgeWeeks(character, out ageWeeks);
        internal void TransferAdoptedCharacterAge(int externalId, FamilyMember member, int ageWeeks) => _ageTracker.TransferExternalCharacterAge(externalId, member, ageWeeks);
        public List<BaseCharacter> GetTrackedExternalCharacters() => _ageTracker.GetTrackedExternalCharacters();
        
        public int IncrementCharacterAge(BaseCharacter character, int weeks)
        {
            if (object.ReferenceEquals(character, null)) return 0;

            int newAge;
            TryIncrementCharacterAge(character, weeks, true, out newAge);
            return newAge;
        }

        public int IncrementNPCAge(BaseCharacter character, int weeks) => IncrementCharacterAge(character, weeks);

        public event System.Action<BaseCharacter, int> OnCharacterAged;
        public event System.Func<BaseCharacter, bool> OnBeforeCharacterAged;

        internal bool ShouldCancelAging(BaseCharacter character)
        {
             if (OnBeforeCharacterAged == null) return false;
             
             foreach(System.Func<BaseCharacter, bool> handler in OnBeforeCharacterAged.GetInvocationList())
             {
                 try
                 {
                     if (!handler(character)) return true;
                 }
                 catch (Exception ex)
                 {
                     _log.Warn($"OnBeforeCharacterAged handler failed for character {GetCharacterIdForLog(character)}; cancelling aging for safety. Error: {ex.Message}");
                     return true;
                 }
             }
             return false;
        }

        /// <summary>
        /// Updates age for all tracked external characters (NPCs) that are NOT family members.
        /// </summary>
        public void UpdateExternalCharacters(int weeks)
        {
            var characters = _ageTracker.GetTrackedExternalCharacters();

            if (characters.Count == 0 && _log.IsDebugEnabled && _ageTracker.GetAllTrackedExternalIds().Count > 0)
            {
                _log.Debug("[LifespanAPI] External age records exist, but no live external characters are registered for this runtime.");
            }

            foreach(BaseCharacter character in characters)
            {
                int ignored;
                TryIncrementCharacterAge(character, weeks, true, out ignored);
            }
        }

        private bool TryIncrementCharacterAge(BaseCharacter character, int weeks, bool respectCancellation, out int newAge)
        {
            newAge = 0;
            if (object.ReferenceEquals(character, null)) return false;

            int current = _ageTracker.GetAgeWeeks(character);
            newAge = current;

            if (respectCancellation && ShouldCancelAging(character))
            {
                return false;
            }

            newAge = LifespanMath.NormalizeAgeWeeks((long)current + weeks);
            if (character is FamilyMember familyMember)
            {
                _ageTracker.SetAgeWeeks(familyMember, newAge);
            }
            else
            {
                _ageTracker.SetExternalCharacterAge(character, newAge);
            }

            var handlers = OnCharacterAged;
            if (handlers != null)
            {
                foreach (System.Action<BaseCharacter, int> handler in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(character, newAge);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn($"OnCharacterAged handler failed for character {GetCharacterIdForLog(character)}; continuing other handlers. Error: {ex.Message}");
                    }
                }
            }
            return true;
        }

        private static string GetCharacterIdForLog(BaseCharacter character)
        {
            if (object.ReferenceEquals(character, null)) return "<null>";

            try
            {
                return character.GetId().ToString();
            }
            catch
            {
                return "<unknown>";
            }
        }

        public void SetConfiguration(LifespanConfig config)
        {
            if (config != null)
            {
                if (_config == null)
                {
                    _config = config;
                    _config.ValidateAndClamp();
                }
                else
                {
                    _config.CopyFrom(config);
                }
                
                try
                {
                    // Keep managers with cached config references aligned after API-driven settings changes.
                    if (_devGeneManager != null) _devGeneManager.RefreshSettings(_config);
                    if (_childManager != null) _childManager.RefreshSettings(_config);

                    var mod = _ctx != null ? _ctx.Mod : null;
                    if (mod != null && mod.SettingsProvider is ModAPI.Spine.SettingsController controller)
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
            foreach (BaseCharacter external in _ageTracker.GetTrackedExternalCharacters())
            {
                if (!object.ReferenceEquals(external, null) && (filter == null || filter(external)))
                {
                    count++;
                }
            }

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

            foreach (BaseCharacter external in _ageTracker.GetTrackedExternalCharacters())
            {
                if (object.ReferenceEquals(external, null)) continue;

                int age = _ageTracker.GetAgeWeeks(external) / LifespanConstants.WeeksPerYear;
                if (age >= minAgeYears && age <= maxAgeYears)
                {
                    count++;
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
