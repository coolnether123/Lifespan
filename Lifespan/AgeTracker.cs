using ModAPI.Core;
using ModAPI.Util;
using ModAPI.Events;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Core component of the Lifespan mod. Tracks biological age for all family members and NPCs in weeks.
    /// Handles age generation using normal distributions, accelerated childhood logic, and persistence 
    /// of both age data and dialogue history across save/load cycles.
    /// </summary>
    public class AgeTracker
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly ModRandomStream _random;
        private readonly IAgeDataStore _dataStore;
        private readonly LifespanState _state;
        private readonly Dictionary<int, BaseCharacter> _externalCharacterRefs;
        private bool _isDataHydrated;
        private bool _allowFreshInitialization;
        
        private IModLogger Log => _log;

        // --- Biological Constants ---

        private const int InitialFamilyGenerationDay = 1;
        private const int StandardAgeVariance = 2;
        private const int AdultAgeVariance = 14;

        /// <summary>
        /// Initializes a new instance of the AgeTracker.
        /// </summary>
        /// <param name="ctx">Plugin context for accessing save systems and logging.</param>
        /// <param name="config">Lifespan configuration settings.</param>
        /// <param name="random">Seeded random stream for deterministic age generation.</param>
        public AgeTracker(IPluginContext ctx, LifespanConfig config, ModRandomStream random)
            : this(ctx, config, random, new AgeDataStore(ctx), new LifespanState())
        {
        }

        internal AgeTracker(IPluginContext ctx, LifespanConfig config, ModRandomStream random, IAgeDataStore dataStore)
            : this(ctx, config, random, dataStore, new LifespanState())
        {
        }

        internal AgeTracker(IPluginContext ctx, LifespanConfig config, ModRandomStream random, IAgeDataStore dataStore, LifespanState state)
        {
            _config = config;
            _log = ctx.Log;
            _state = state ?? new LifespanState();
            _random = random;
            _dataStore = dataStore;
            _externalCharacterRefs = new Dictionary<int, BaseCharacter>();
            _isDataHydrated = false;
            _allowFreshInitialization = false;
        }

        internal LifespanState State => _state;

        /// <summary>
        /// Clear all tracking data. Typically called when starting a new game or changing slots.
        /// </summary>
        public void Reset(bool clearPersistentContainer = false)
        {
            Log.Debug("Resetting tracking data for fresh session.");
            _state.Replace(new AgeData());
            _externalCharacterRefs.Clear();
            _isDataHydrated = false;
            _allowFreshInitialization = false;

            // Leave the registered container intact during normal scene/session resets.
            // ModAPI hydration may not have run yet at this point.
            if (clearPersistentContainer)
            {
                ClearSaveContainer();
            }
        }

        public bool IsDataHydrated => _isDataHydrated;

        public int LastProcessedAgingWeek => _state.Ages.LastProcessedAgingWeek;

        public void MarkAgingWeekProcessed(int week)
        {
            _state.Ages.LastProcessedAgingWeek = week;
        }

        /// <summary>
        /// Calculated threshold for adulthood in weeks based on configuration.
        /// </summary>
        public int AdultAgeWeeks => _config.adultAgeYears * LifespanConstants.WeeksPerYear;

        /// <summary>
        /// Generates a random integer follow a Normal (Gaussian) distribution.
        /// Uses the Box-Muller transform for high-quality distribution.
        /// </summary>
        /// <param name="mean">The center of the distribution.</param>
        /// <param name="stdDev">The spread (standard deviation) of the distribution.</param>
        /// <returns>A normally distributed integer.</returns>
        private int NormalDistribution(int mean, int stdDev)
        {
            // Box-Muller transform using the seeded random stream
            double u1 = 1.0 - _random.Value(); 
            double u2 = 1.0 - _random.Value();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); 
            return (int)(mean + stdDev * randStdNormal);
        }

        /// <summary>
        /// Generates a starting age for a character during the early-game initialization phase.
        /// </summary>
        /// <param name="member">The family member to generate for.</param>
        /// <param name="isChild">Whether the character is currently categorized as a child.</param>
        /// <returns>Age in weeks.</returns>
        public int GenerateInitialAge(FamilyMember member, bool isChild)
        {
            if (isChild)
            {
                bool isInitialGeneration = GameTime.Day <= InitialFamilyGenerationDay;
                int min = Math.Max(0, _config.initialChildAgeMinYears);
                int max = Math.Max(min, Math.Min(_config.initialChildAgeMaxYears, _config.adultAgeYears - 1));

                int mean;
                if (isInitialGeneration && _config.useRoleplayFamilyAgeModel)
                {
                    mean = (min + max) / 2;
                }
                else
                {
                    mean = Mathf.Clamp(_config.initialChildAgeYears, min, max);
                }

                return Mathf.Clamp(NormalDistribution(mean, StandardAgeVariance), min, max) * LifespanConstants.WeeksPerYear;
            }
            else
            {
                int min = Math.Max(18, _config.initialAdultAgeMinYears);
                int max = Math.Max(min, _config.initialAdultAgeMaxYears);
                int mean = Mathf.Clamp(_config.initialAdultAgeYears, min, max);

                return Mathf.Clamp(NormalDistribution(mean, AdultAgeVariance), min, max) * LifespanConstants.WeeksPerYear;
            }
        }

        /// <summary>
        /// Context-aware age generator for varying gameplay scenarios (Recruits, Traders, Explorers).
        /// </summary>
        /// <param name="character">The character to evaluate.</param>
        /// <param name="context">The gameplay scenario triggering the generation.</param>
        /// <returns>Age in weeks.</returns>
        public int GenerateAgeForContext(BaseCharacter character, AgeContext context)
        {
            if (character == null) return 0;
            
            int id = character.GetId();
            bool isFamilyMember = character is FamilyMember;

            // Check if we already have data to prevent re-rolling
            int existingAgeWeeks;
            if (isFamilyMember && _state.Ages.TryGetFamilyAgeWeeks(id, out existingAgeWeeks))
            {
                return existingAgeWeeks;
            }

            if (!isFamilyMember && _state.Ages.TryGetExternalAgeWeeks(id, out existingAgeWeeks))
            {
                return existingAgeWeeks;
            }

            // Route generation to specific handlers
            int ageWeeks;
            switch (context)
            {
                case AgeContext.ExplorerEncounter:
                    ageWeeks = GenerateExplorerAge(character);
                    break;
                case AgeContext.ShelterRecruit:
                    ageWeeks = GenerateShelterRecruitAge(character);
                    break;
                case AgeContext.NPC_Trader:
                    ageWeeks = GenerateTraderAge(character);
                    break;
                case AgeContext.NewbornFromPregnancy:
                    ageWeeks = 0;
                    break;
                default:
                    ageWeeks = GenerateDefaultNPCAge(character);
                    break;
            }

            // Commit to memory
            if (isFamilyMember)
            {
                _state.Ages.SetFamilyAgeWeeks(id, ageWeeks);
            }
            else
            {
                TrackExternalCharacter(character);
                _state.Ages.SetExternalAgeWeeks(id, ageWeeks);
            }
            
            // Notify other mod systems (API Hook)
            ModEventBus.Publish("Lifespan.CharacterAgeGenerated", 
                new CharacterAgeGeneratedArgs(character, ageWeeks, context));
            
            return ageWeeks;
        }

        private int GenerateExplorerAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.explorerMeanAge, _config.explorerStdDev), 18, 55) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateShelterRecruitAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.recruiterMeanAge, _config.recruiterStdDev), 18, 80) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateTraderAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.traderMeanAge, _config.traderStdDev), 25, 70) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateDefaultNPCAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(35, 12), 18, 65) * LifespanConstants.WeeksPerYear;
        }

        /// <summary>
        /// Retrieves age from the cache based on character ID.
        /// </summary>
        public int GetAgeWeeks(int id)
        {
            return _state.Ages.GetAgeWeeksOrDefault(id);
        }

        public bool TryGetAgeWeeks(int id, out int ageWeeks)
        {
            return _state.Ages.TryGetAgeWeeks(id, out ageWeeks);
        }

        /// <summary>
        /// Legacy resolver for Obituaries where IDs might be missing. Use sparingly.
        /// </summary>
        public int GetAgeWeeksByName(string firstName)
        {
            if (string.IsNullOrEmpty(firstName)) return 0;

            if (FamilyManager.Instance != null)
            {
                var members = FamilyManager.Instance.GetAllFamilyMembers();
                if (members != null)
                {
                    foreach (var member in members)
                    {
                        if (member.firstName == firstName)
                        {
                            return GetAgeWeeks(member);
                        }
                    }
                }
            }

            // Check graveyard records
            if (FamilyManager.Instance != null)
            {
                var deadInfo = FamilyManager.Instance.GetDeadFamilyMemberInfo();
                if (deadInfo != null)
                {
                    foreach (var dead in deadInfo)
                    {
                        if (dead.first_name == firstName)
                        {
                             if (dead.id > -1)
                             {
                                 int age = GetAgeWeeks(dead.id);
                                 if (age > 0) return age;
                             }
                        }
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// Returns age in weeks, generating it if the character is new to the system.
        /// </summary>
        public int GetAgeWeeks(FamilyMember member)
        {
            if (object.ReferenceEquals(member, null))
                return 0;

            int memberId = member.GetId();
            int existingAgeWeeks;
            if (_state.Ages.TryGetFamilyAgeWeeks(memberId, out existingAgeWeeks))
            {
                return existingAgeWeeks;
            }

            if (!_isDataHydrated && !_allowFreshInitialization)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"[AgeTracker] Age requested before hydration for {member.firstName} (ID: {memberId}). Returning 0 until load completes.");
                }
                return 0;
            }

            // Lazy initialization for members recruited through vanilla means
            int initialAge = GenerateInitialAge(member, member.isChild);
            _state.Ages.SetFamilyAgeWeeks(memberId, initialAge);
            if (Log.IsDebugEnabled) _log.Info($"Initialized age for {member.firstName} to {initialAge / LifespanConstants.WeeksPerYear} years (ID: {memberId}).");
            
            ModEventBus.Publish("Lifespan.AgeInitialized", new AgeChangedArgs(member, initialAge, 0));

            return initialAge;
        }

        public bool TryGetAgeWeeks(FamilyMember member, out int ageWeeks)
        {
            ageWeeks = 0;
            if (object.ReferenceEquals(member, null)) return false;
            int memberId = member.GetId();
            return _state.Ages.TryGetFamilyAgeWeeks(memberId, out ageWeeks);
        }

        /// <summary>
        /// Generic accessor for any character type.
        /// </summary>
        public int GetAgeWeeks(BaseCharacter character)
        {
            if (object.ReferenceEquals(character, null)) return 0;

            if (character is FamilyMember fm)
            {
                return GetAgeWeeks(fm);
            }

            int id = character.GetId();
            int ageWeeks;
            if (_state.Ages.TryGetExternalAgeWeeks(id, out ageWeeks))
            {
                TrackExternalCharacter(character);
                return ageWeeks;
            }

            return 0;
        }

        /// <summary>
        /// Manually update NPC age (e.g. from an external mod).
        /// </summary>
        public void SetExternalCharacterAge(int id, int ageWeeks)
        {
            _state.Ages.SetExternalAgeWeeks(id, ageWeeks);
        }

        /// <summary>
        /// Manually update NPC age and keep the live object available for API events.
        /// </summary>
        public void SetExternalCharacterAge(BaseCharacter character, int ageWeeks)
        {
            if (object.ReferenceEquals(character, null)) return;

            TrackExternalCharacter(character);
            SetExternalCharacterAge(character.GetId(), ageWeeks);
        }

        /// <summary>
        /// Helper for UI and Dialogue where years are more readable.
        /// </summary>
        public int GetAgeYears(FamilyMember member)
        {
            return GetAgeWeeks(member) / LifespanConstants.WeeksPerYear;
        }

        /// <summary>
        /// Overrides a member's age. Only use for debugging or special events.
        /// </summary>
        public void SetAgeWeeks(FamilyMember member, int weeks)
        {
            if (object.ReferenceEquals(member, null))
                return;

            int memberId = member.GetId();
            _state.Ages.SetFamilyAgeWeeks(memberId, weeks);
            if (Log.IsDebugEnabled) _log.Info($"Manual age override: {member.firstName} set to {weeks / LifespanConstants.WeeksPerYear} years.");
        }

        /// <summary>
        /// Overrides any character's age.
        /// </summary>
        public void SetAgeWeeks(BaseCharacter character, int weeks)
        {
            if (object.ReferenceEquals(character, null)) return;

            if (character is FamilyMember fm)
            {
                SetAgeWeeks(fm, weeks);
                return;
            }

            int id = character.GetId();
            TrackExternalCharacter(character);
            _state.Ages.SetExternalAgeWeeks(id, weeks);
        }

        /// <summary>
        /// Returns all NPC/Explorer IDs currently in tracking.
        /// </summary>
        public List<int> GetAllTrackedExternalIds()
        {
            return _state.Ages.GetAllExternalIds();
        }

        /// <summary>
        /// Returns live external characters currently known to the runtime API.
        /// Saved external age records without a live object are intentionally excluded.
        /// </summary>
        public List<BaseCharacter> GetTrackedExternalCharacters()
        {
            List<BaseCharacter> characters = new List<BaseCharacter>();
            List<int> staleIds = new List<int>();

            foreach (var kvp in _externalCharacterRefs)
            {
                BaseCharacter character = kvp.Value;
                if (object.ReferenceEquals(character, null))
                {
                    staleIds.Add(kvp.Key);
                    continue;
                }

                characters.Add(character);
            }

            foreach (int id in staleIds)
            {
                _externalCharacterRefs.Remove(id);
            }

            return characters;
        }

        private void TrackExternalCharacter(BaseCharacter character)
        {
            if (object.ReferenceEquals(character, null) || character is FamilyMember)
            {
                return;
            }

            _externalCharacterRefs[character.GetId()] = character;
        }

        /// <summary>
        /// Progresses a character's biological age. Accounts for accelerated childhood.
        /// </summary>
        /// <param name="member">The member to age.</param>
        /// <param name="weeks">Amount of time passed.</param>
        /// <returns>The new age in weeks.</returns>
        public int IncrementAge(FamilyMember member, int weeks)
        {
            if (object.ReferenceEquals(member, null)) return 0;

            int currentAge = GetAgeWeeks(member);
            
            // Progression Logic: Applies accelerated childhood aging if enabled.
            // Note: Per design requirements, acceleration is inhibited for characters aged 10 and older.
            int increment = weeks;
            int currentAgeYears = currentAge / LifespanConstants.WeeksPerYear;
            
            if (_config.enableAcceleratedChildhood && currentAgeYears < LifespanConstants.ChildhoodAccelerationStopAge)
            {
                increment *= 2;
                if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Accelerated biological aging applied to {member.firstName}: {weeks} -> {increment} weeks.");
            }

            int newAge = currentAge + increment;
            
            int memberId = member.GetId();
            _state.Ages.SetFamilyAgeWeeks(memberId, newAge);

            // Birthday logging
            if (newAge / LifespanConstants.WeeksPerYear > currentAge / LifespanConstants.WeeksPerYear)
            {
                if (Log.IsDebugEnabled) _log.Info($"BIRTHDAY! {member.firstName} is now {newAge / LifespanConstants.WeeksPerYear} years old.");
            }

            if (Log.IsDebugEnabled) Log.Debug($"{member.firstName} aged to {newAge} weeks (+{increment}).");

            return newAge;
        }

        /// <summary>
        /// Checks if a member has crossed the elder configuration threshold.
        /// </summary>
        public bool IsElder(FamilyMember member)
        {
            int ageWeeks = GetAgeWeeks(member);
            bool isElder = ageWeeks >= (_config.elderAgeYears * LifespanConstants.WeeksPerYear);
            if (isElder && Log.IsDebugEnabled) Log.Debug($"{member.firstName} is classified as an elder ({ageWeeks / LifespanConstants.WeeksPerYear}y).");
            return isElder;
        }

        /// <summary>
        /// Synchronization point with the Sheltered Save System.
        /// Call this before the game writes to disk to ensure the container is fresh.
        /// </summary>
        public void SaveAgeData()
        {
            Log.Debug("Preparing age data for SaveSystem.");
            try
            {
                if (!_isDataHydrated)
                {
                    _log.Warn("[AgeTracker] Save requested before hydration completed. Skipping age container sync.");
                    return;
                }

                SyncWithFamilyManager();
                _dataStore.Save(_state.Data);

                if (Log.IsDebugEnabled) Log.Debug($"Synced {_state.Ages.FamilyCount} members and {_state.Ages.ExternalCount} NPCs to save container.");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to prepare save data: {ex.Message}");
            }
        }

        private void ClearSaveContainer()
        {
            _dataStore.Clear();
        }

        private void SyncWithFamilyManager()
        {
            if (FamilyManager.Instance == null) return;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            foreach (var member in members)
            {
                if (object.ReferenceEquals(member, null)) continue;
                GetAgeWeeks(member);
            }
        }

        /// <summary>
        /// Restores state from the save container.
        /// In v1.2, the framework handles the actual disk read before this is called.
        /// </summary>
        public void LoadAgeData()
        {
            Log.Debug("Hydrating age data from SaveSystem container.");
            try
            {
                _allowFreshInitialization = false;

                if (_dataStore.HasSavedData)
                {
                    _state.Replace(_dataStore.Load());
                    Log.Info($"[AgeTracker] Hydrated from v1.2 container. Members: {_state.Ages.FamilyCount}, External: {_state.Ages.ExternalCount}.");
                }
                else
                {
                    _log.Warn("No records found in save container.");
                    Log.Info("[AgeTracker] Fresh initialization path taken (no saved age data found).");
                    _state.Replace(new AgeData());
                    _allowFreshInitialization = true;
                    try
                    {
                        InitializeExistingMembers();
                    }
                    finally
                    {
                        _allowFreshInitialization = false;
                    }
                }

                _isDataHydrated = true;
                Log.Debug("[AgeTracker] Hydration complete.");
            }
            catch (Exception ex)
            {
                _log.Error($"CRITICAL FAILURE loading age data: {ex.Message}");
                _state.Replace(new AgeData());
                _allowFreshInitialization = true;
                try
                {
                    InitializeExistingMembers();
                }
                finally
                {
                    _allowFreshInitialization = false;
                }
                _isDataHydrated = true;
            }
        }

        private void InitializeExistingMembers()
        {
            if (FamilyManager.Instance == null) return;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            if (Log.IsDebugEnabled) Log.Debug("[AgeTracker] Initializing members... (This should only happen once per save!)");
            foreach (var member in members)
            {
                if (!object.ReferenceEquals(member, null) && !member.isDead)
                {
                    int newAge = GetAgeWeeks(member);
                    Log.Debug($"Generated FRESH age for {member.firstName}: {newAge / LifespanConstants.WeeksPerYear} years.");
                }
            }
        }

        /// <summary>
        /// Purges orphans from tracking to prevent bloated save files.
        /// </summary>
        public void CleanupMissingMembers()
        {
            if (FamilyManager.Instance == null) return;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            HashSet<int> validIds = new HashSet<int>();
            foreach (var member in members)
            {
                if (!object.ReferenceEquals(member, null))
                {
                    validIds.Add(member.GetId());
                }
            }

            var deadMembers = FamilyManager.Instance.GetDeadFamilyMemberInfo();
            if (deadMembers != null)
            {
                foreach (var dead in deadMembers)
                {
                    validIds.Add(dead.id);
                }
            }

            List<int> toRemove = _state.Ages.GetFamilyIds().Where(id => !validIds.Contains(id)).ToList();
            if (toRemove.Count > 0)
            {
                if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Cleaning up {toRemove.Count} IDs that no longer exist.");
            }
            foreach (var id in toRemove)
            {
                _state.Ages.RemoveFamilyMember(id);
                _state.Illnesses.RemoveMember(id);
            }

            if (toRemove.Count > 0)
            {
                if (Log.IsDebugEnabled) _log.Info($"Cleaned up age tracking for {toRemove.Count} members no longer in family.");
            }
        }

        /// <summary>
        /// Retrieves active illnesses for a member.
        /// </summary>
        public List<string> GetIllnesses(FamilyMember member)
        {
            if (member == null) return new List<string>();
            int id = member.GetId();
            return _state.Illnesses.GetIllnesses(id);
        }

        /// <summary>
        /// Assigns a new age-related condition to a character.
        /// </summary>
        public void AddIllness(FamilyMember member, string illnessId)
        {
            if (member == null) return;
            int id = member.GetId();
            var list = _state.Illnesses.GetIllnesses(id);
            if (!list.Contains(illnessId))
            {
                _state.Illnesses.AddIllness(id, illnessId);
                if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Added illness {illnessId} to {member.firstName}.");
            }
        }

        /// <summary>
        /// Clears a condition from a character.
        /// </summary>
        public void RemoveIllness(FamilyMember member, string illnessId)
        {
            if (member == null) return;
            int id = member.GetId();
            _state.Illnesses.RemoveIllness(id, illnessId);
            if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Removed illness {illnessId} from {member.firstName}.");
        }

        /// <summary>
        /// Sets the predicted week for a condition stage transition.
        /// </summary>
        public void SetOnsetWeek(FamilyMember member, string illnessId, int targetWeek)
        {
             if (member == null) return;
             int id = member.GetId();
             _state.Illnesses.SetOnsetWeek(id, illnessId, targetWeek);
        }

        /// <summary>
        /// Gets the predicted transition week for a condition.
        /// </summary>
        public int GetOnsetWeek(FamilyMember member, string illnessId)
        {
             if (member == null) return -1;
             int id = member.GetId();
             return _state.Illnesses.GetOnsetWeek(id, illnessId);
        }

        /// <summary>
        /// Returns the visual hair greying state profile.
        /// Deterministic based on the character's unique GreyingGene.
        /// </summary>
        public GreyProfile GetOrGenerateGreyProfile(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            GreyProfile existing;
            if (_state.Genetics.TryGetGreyProfile(id, out existing))
            {
                if (existing.Gene == null)
                {
                    existing.Gene = GreyingGene.GenerateRandom(_random);
                }
                return existing;
            }

            Color currentHair = Color.black;
            try
            {
                currentHair = Traverse.Create(member).Field("m_hairColor").GetValue<Color>();
            }
            catch (Exception ex)
            {
                if (Log.IsDebugEnabled) Log.Debug($"Could not read hair color for {member.firstName}; using black fallback. Error: {ex.Message}");
            }

            if (currentHair == default(Color)) currentHair = Color.black; 

            var profile = new GreyProfile();
            profile.OriginalColor = new float[] { currentHair.r, currentHair.g, currentHair.b, currentHair.a };
            profile.Gene = GreyingGene.GenerateRandom(_random);

            _state.Genetics.SetGreyProfile(id, profile);
            return profile;
        }

        /// <summary>
        /// Returns the character's development gene, governing stat growth.
        /// </summary>
        public DevelopmentGene GetOrGenerateDevelopmentGene(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            return _state.Genetics.GetOrGenerateDevelopmentGene(id, _random);
        }

        /// <summary>
        /// Logs a character's death for record-keeping and obituary persistence.
        /// </summary>
        public void RecordDeath(FamilyMember member, int backdatedDay)
        {
            if (member == null) return;
            int id = member.GetId();
            int ageWeeks = GetAgeWeeks(member);
            
            _state.Deaths.RecordDeath(id, backdatedDay, ageWeeks);
            _log.Info($"[Lifespan] Recorded death for {member.firstName}. Day: {backdatedDay}, Age: {ageWeeks / LifespanConstants.WeeksPerYear}y.");
        }

        /// <summary>
        /// Retrieves historical death details.
        /// </summary>
        public bool TryGetDeathInfo(int id, out int day, out int ageWeeks)
        {
            day = 0;
            ageWeeks = 0;
            return _state.Deaths.TryGetDeathInfo(id, out day, out ageWeeks);
        }

        /// <summary>
        /// Returns the day of the most recent death in the shelter.
        /// </summary>
        public int GetMaxDeathDay()
        {
            return _state.Deaths.GetMaxDeathDay();
        }

        /// <summary>
        /// Persistence Bridge: Returns the persistent milestone map.
        /// </summary>
        public Dictionary<int, HashSet<string>> GetTriggeredMilestones() => _state.Milestones.TriggeredMilestones;
        
        /// <summary>
        /// Persistence Bridge: Returns the persistent birthday tracking map.
        /// </summary>
        public Dictionary<int, int> GetLastBirthdayYears() => _state.Milestones.LastBirthdayYears;
        
        /// <summary>
        /// Persistence Bridge: Returns the persistent dialogue history 'bag'.
        /// </summary>
        public Dictionary<string, HashSet<int>> GetDialogueHistory() => _state.Dialogue.History;
    }

}
