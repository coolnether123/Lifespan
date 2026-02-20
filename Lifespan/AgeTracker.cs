using ModAPI.Core;
using ModAPI.Saves;
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
        private readonly IPluginContext _ctx;
        private readonly ModRandomStream _random;
        private AgeData _currentAgeData;
        private AgeDataSerializable _saveContainer; 
        
        private IModLogger Log => _log;

        // --- Biological Constants ---

        private const int InitialFamilyGenerationDay = 1;
        private const int StartChildAgeMin = 10;
        private const int StartChildAgeMax = 17;
        private const int StartChildAgeMean = 13;

        private const int AdultMinAge = 18;
        private const int AdultMaxAge = 90;
        private const int StandardAgeVariance = 2;
        private const int AdultAgeVariance = 14;

        /// <summary>
        /// Initializes a new instance of the AgeTracker.
        /// </summary>
        /// <param name="ctx">Plugin context for accessing save systems and logging.</param>
        /// <param name="config">Lifespan configuration settings.</param>
        /// <param name="random">Seeded random stream for deterministic age generation.</param>
        public AgeTracker(IPluginContext ctx, LifespanConfig config, ModRandomStream random)
        {
            _ctx = ctx;
            _config = config;
            _log = ctx.Log;
            _currentAgeData = new AgeData();
            _random = random;
            
            // Register for automatic v1.2 isolated save data tagging.
            // This ensures data is specific to this mod and won't collide with others.
            _saveContainer = new AgeDataSerializable();
            _ctx.SaveSystem.RegisterModData("LifeSpan.AgeData", _saveContainer);
        }

        /// <summary>
        /// Clear all tracking data. Typically called when starting a new game or changing slots.
        /// </summary>
        public void Reset()
        {
            Log.Debug("Resetting tracking data for fresh session.");
            _currentAgeData = new AgeData();
            _saveContainer = new AgeDataSerializable(); // Clear container too
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
                // During initial family generation (Day 1), children are constrained to a narrower age range (10-17).
                // recruits and subsequent character generation use configured defaults.
                bool isInitialGeneration = GameTime.Day <= InitialFamilyGenerationDay;
                
                int mean = isInitialGeneration ? StartChildAgeMean : _config.initialChildAgeYears;
                int min = isInitialGeneration ? StartChildAgeMin : 0;
                int max = StartChildAgeMax;
                
                return Mathf.Clamp(NormalDistribution(mean, StandardAgeVariance), min, max) * LifespanConstants.WeeksPerYear;
            }
            else
            {
                // Adults approximate a standard population distribution centered on the recruit age configuration.
                return Mathf.Clamp(NormalDistribution(_config.initialAdultAgeYears, AdultAgeVariance), AdultMinAge, AdultMaxAge) * LifespanConstants.WeeksPerYear;
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
            if (isFamilyMember && _currentAgeData.familyMemberAges.ContainsKey(id))
            {
                return _currentAgeData.familyMemberAges[id];
            }

            if (!isFamilyMember && _currentAgeData.externalCharacterAges.ContainsKey(id))
            {
                return _currentAgeData.externalCharacterAges[id];
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
                _currentAgeData.familyMemberAges[id] = ageWeeks;
                if (_currentAgeData.externalCharacterAges.ContainsKey(id))
                {
                    _currentAgeData.externalCharacterAges.Remove(id);
                }
            }
            else
            {
                _currentAgeData.externalCharacterAges[id] = ageWeeks;
            }
            
            // Notify other mod systems (API Hook)
            ModEventBus.Publish("Lifespan.CharacterAgeGenerated", 
                new CharacterAgeGeneratedArgs(character, ageWeeks, context));
            
            return ageWeeks;
        }

        private int GenerateExplorerAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.explorerMeanAge, _config.explorerStdDev), AdultMinAge, 55) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateShelterRecruitAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.recruiterMeanAge, _config.recruiterStdDev), AdultMinAge, 80) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateTraderAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.traderMeanAge, _config.traderStdDev), 25, 70) * LifespanConstants.WeeksPerYear;
        }

        private int GenerateDefaultNPCAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(35, 12), AdultMinAge, 65) * LifespanConstants.WeeksPerYear;
        }

        /// <summary>
        /// Retrieves age from the cache based on character ID.
        /// </summary>
        public int GetAgeWeeks(int id)
        {
            if (_currentAgeData.familyMemberAges.ContainsKey(id))
            {
                return _currentAgeData.familyMemberAges[id];
            }
            if (_currentAgeData.externalCharacterAges.ContainsKey(id))
            {
                return _currentAgeData.externalCharacterAges[id];
            }
            return 0;
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
            if (_currentAgeData.familyMemberAges.ContainsKey(memberId))
            {
                return _currentAgeData.familyMemberAges[memberId];
            }

            // Lazy initialization for members recruited through vanilla means
            int initialAge = GenerateInitialAge(member, member.isChild);
            _currentAgeData.familyMemberAges[memberId] = initialAge;
            if (Log.IsDebugEnabled) _log.Info($"Initialized age for {member.firstName} to {initialAge / LifespanConstants.WeeksPerYear} years (ID: {memberId}).");
            
            ModEventBus.Publish("Lifespan.AgeInitialized", new AgeChangedArgs(member, initialAge, 0));

            return initialAge;
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
            if (_currentAgeData.externalCharacterAges.ContainsKey(id))
            {
                return _currentAgeData.externalCharacterAges[id];
            }

            return 0;
        }

        /// <summary>
        /// Manually update NPC age (e.g. from an external mod).
        /// </summary>
        public void SetExternalCharacterAge(int id, int ageWeeks)
        {
            _currentAgeData.externalCharacterAges[id] = ageWeeks;
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
            _currentAgeData.familyMemberAges[memberId] = Math.Max(0, weeks);
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
            _currentAgeData.externalCharacterAges[id] = Math.Max(0, weeks);
        }

        /// <summary>
        /// Returns all NPC/Explorer IDs currently in tracking.
        /// </summary>
        public List<int> GetAllTrackedExternalIds()
        {
            return new List<int>(_currentAgeData.externalCharacterAges.Keys);
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
            _currentAgeData.familyMemberAges[memberId] = newAge;

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
                SyncWithFamilyManager();

                // Convert current model to serializable format
                var freshData = _currentAgeData.ToSerializable();
                
                // Deep-copy to the registered container
                _saveContainer.ages = freshData.ages;
                _saveContainer.externalAges = freshData.externalAges;
                _saveContainer.illnesses = freshData.illnesses;
                _saveContainer.greyProfiles = freshData.greyProfiles;
                _saveContainer.developmentGenes = freshData.developmentGenes;
                _saveContainer.onsetData = freshData.onsetData;
                _saveContainer.deathDays = freshData.deathDays;
                _saveContainer.deathAges = freshData.deathAges;
                _saveContainer.triggeredMilestones = freshData.triggeredMilestones;
                _saveContainer.lastBirthdays = freshData.lastBirthdays;
                _saveContainer.dialogueHistory = freshData.dialogueHistory;

                if (Log.IsDebugEnabled) Log.Debug($"Synced {freshData.ages.Count} members and {freshData.externalAges.Count} NPCs to save container.");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to prepare save data: {ex.Message}");
            }
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
                if (_saveContainer != null && (_saveContainer.ages.Count > 0 || _saveContainer.externalAges.Count > 0))
                {
                    _currentAgeData = AgeData.FromSerializable(_saveContainer);
                    Log.Debug($"Metadata restored for {_currentAgeData.familyMemberAges.Count} members.");
                }
                else
                {
                    _log.Warn("No records found in v1.2 save container. Check for legacy data...");
                    
                    // Backward compatibility: Migration from v1.1 file-based storage
                    AgeDataSerializable legacyData;
                    if (_ctx.LoadData("LifeSpan.AgeData", out legacyData))
                    {
                        Log.Info("Migrated legacy v1.1 age data to v1.2 container.");
                        _currentAgeData = AgeData.FromSerializable(legacyData);
                    }
                    else
                    {
                        Log.Info("Starting fresh age data for session.");
                        _currentAgeData = new AgeData();
                        InitializeExistingMembers();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"CRITICAL FAILURE loading age data: {ex.Message}");
                _currentAgeData = new AgeData();
                InitializeExistingMembers();
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

            List<int> toRemove = _currentAgeData.familyMemberAges.Keys.Where(id => !validIds.Contains(id)).ToList();
            if (toRemove.Count > 0)
            {
                if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Cleaning up {toRemove.Count} IDs that no longer exist.");
            }
            foreach (var id in toRemove)
            {
                _currentAgeData.familyMemberAges.Remove(id);
                _currentAgeData.elderIllnesses.Remove(id);
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
            if (!_currentAgeData.elderIllnesses.ContainsKey(id))
            {
                _currentAgeData.elderIllnesses[id] = new List<string>();
            }
            return _currentAgeData.elderIllnesses[id];
        }

        /// <summary>
        /// Assigns a new age-related condition to a character.
        /// </summary>
        public void AddIllness(FamilyMember member, string illnessId)
        {
            if (member == null) return;
            var list = GetIllnesses(member);
            if (!list.Contains(illnessId))
            {
                list.Add(illnessId);
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
            if (_currentAgeData.elderIllnesses.ContainsKey(id))
            {
                _currentAgeData.elderIllnesses[id].Remove(illnessId);
                if (_currentAgeData.onsetTiming.ContainsKey(id))
                {
                    _currentAgeData.onsetTiming[id].Remove(illnessId);
                }
                if (Log.IsDebugEnabled) Log.Debug($"[AgeTracker] Removed illness {illnessId} from {member.firstName}.");
            }
        }

        /// <summary>
        /// Sets the predicted week for a condition stage transition.
        /// </summary>
        public void SetOnsetWeek(FamilyMember member, string illnessId, int targetWeek)
        {
             if (member == null) return;
             int id = member.GetId();
             if (!_currentAgeData.onsetTiming.ContainsKey(id))
             {
                 _currentAgeData.onsetTiming[id] = new Dictionary<string, int>();
             }
             _currentAgeData.onsetTiming[id][illnessId] = targetWeek;
        }

        /// <summary>
        /// Gets the predicted transition week for a condition.
        /// </summary>
        public int GetOnsetWeek(FamilyMember member, string illnessId)
        {
             if (member == null) return -1;
             int id = member.GetId();
             if (_currentAgeData.onsetTiming.TryGetValue(id, out var timing) && timing.TryGetValue(illnessId, out int week))
             {
                 return week;
             }
             return -1;
        }

        /// <summary>
        /// Returns the visual hair greying state profile.
        /// Deterministic based on the character's unique GreyingGene.
        /// </summary>
        public GreyProfile GetOrGenerateGreyProfile(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            if (_currentAgeData.greyProfiles.ContainsKey(id))
            {
                var existing = _currentAgeData.greyProfiles[id];
                if (existing.Gene == null)
                {
                    existing.Gene = GreyingGene.GenerateRandom(_random);
                }
                return existing;
            }

            Color currentHair = Color.black;
            try { currentHair = Traverse.Create(member).Field("m_hairColor").GetValue<Color>(); } catch {}
            if (currentHair == default(Color)) currentHair = Color.black; 

            var profile = new GreyProfile();
            profile.OriginalColor = new float[] { currentHair.r, currentHair.g, currentHair.b, currentHair.a };
            profile.Gene = GreyingGene.GenerateRandom(_random);

            _currentAgeData.greyProfiles[id] = profile;
            return profile;
        }

        /// <summary>
        /// Returns the character's development gene, governing stat growth.
        /// </summary>
        public DevelopmentGene GetOrGenerateDevelopmentGene(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            if (_currentAgeData.developmentGenes.ContainsKey(id))
            {
                return _currentAgeData.developmentGenes[id];
            }

            var gene = DevelopmentGene.GenerateRandom(_random);
            _currentAgeData.developmentGenes[id] = gene;
            return gene;
        }

        /// <summary>
        /// Logs a character's death for record-keeping and obituary persistence.
        /// </summary>
        public void RecordDeath(FamilyMember member, int backdatedDay)
        {
            if (member == null) return;
            int id = member.GetId();
            int ageWeeks = GetAgeWeeks(member);
            
            _currentAgeData.deceasedDeathDays[id] = backdatedDay;
            _currentAgeData.deceasedDeathAges[id] = ageWeeks;
            _log.Info($"[Lifespan] Recorded death for {member.firstName}. Day: {backdatedDay}, Age: {ageWeeks / LifespanConstants.WeeksPerYear}y.");
        }

        /// <summary>
        /// Retrieves historical death details.
        /// </summary>
        public bool TryGetDeathInfo(int id, out int day, out int ageWeeks)
        {
            day = 0;
            ageWeeks = 0;
            bool foundDay = _currentAgeData.deceasedDeathDays.TryGetValue(id, out day);
            bool foundAge = _currentAgeData.deceasedDeathAges.TryGetValue(id, out ageWeeks);
            return foundDay || foundAge;
        }

        /// <summary>
        /// Returns the day of the most recent death in the shelter.
        /// </summary>
        public int GetMaxDeathDay()
        {
            if (_currentAgeData.deceasedDeathDays.Count == 0) return 0;
            int max = 0;
            foreach (var day in _currentAgeData.deceasedDeathDays.Values)
            {
                if (day > max) max = day;
            }
            return max;
        }

        /// <summary>
        /// Persistence Bridge: Returns the persistent milestone map.
        /// </summary>
        public Dictionary<int, HashSet<string>> GetTriggeredMilestones() => _currentAgeData.triggeredMilestones;
        
        /// <summary>
        /// Persistence Bridge: Returns the persistent birthday tracking map.
        /// </summary>
        public Dictionary<int, int> GetLastBirthdayYears() => _currentAgeData.lastBirthdayYear;
        
        /// <summary>
        /// Persistence Bridge: Returns the persistent dialogue history 'bag'.
        /// </summary>
        public Dictionary<string, HashSet<int>> GetDialogueHistory() => _currentAgeData.dialogueHistory;
    }

    /// <summary>
    /// Runtime model for age tracking. Converted to/from AgeDataSerializable for storage.
    /// </summary>
    [Serializable]
    public class AgeData
    {
        public Dictionary<int, int> familyMemberAges = new Dictionary<int, int>();
        public Dictionary<int, int> externalCharacterAges = new Dictionary<int, int>();
        public Dictionary<int, List<string>> elderIllnesses = new Dictionary<int, List<string>>();
        public Dictionary<int, GreyProfile> greyProfiles = new Dictionary<int, GreyProfile>();
        public Dictionary<int, DevelopmentGene> developmentGenes = new Dictionary<int, DevelopmentGene>();
        public Dictionary<int, Dictionary<string, int>> onsetTiming = new Dictionary<int, Dictionary<string, int>>();
        public Dictionary<int, HashSet<string>> triggeredMilestones = new Dictionary<int, HashSet<string>>();
        public Dictionary<int, int> lastBirthdayYear = new Dictionary<int, int>();
        public Dictionary<string, HashSet<int>> dialogueHistory = new Dictionary<string, HashSet<int>>();

        public Dictionary<int, int> deceasedDeathDays = new Dictionary<int, int>();
        public Dictionary<int, int> deceasedDeathAges = new Dictionary<int, int>();

        public AgeDataSerializable ToSerializable()
        {
            var s = new AgeDataSerializable();
            foreach (var kvp in familyMemberAges) s.ages.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in externalCharacterAges) s.externalAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in elderIllnesses) s.illnesses.Add(new IllnessEntry { id = kvp.Key, illnessIds = kvp.Value });
            foreach (var kvp in greyProfiles) s.greyProfiles.Add(new GreyProfileEntry { id = kvp.Key, profile = kvp.Value });
            foreach (var kvp in developmentGenes) s.developmentGenes.Add(new DevelopmentGeneEntry { id = kvp.Key, gene = kvp.Value });
            foreach (var kvp in onsetTiming) s.onsetData.Add(new OnsetEntry { id = kvp.Key, onsetWeeks = kvp.Value });
            foreach (var kvp in deceasedDeathDays) s.deathDays.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in deceasedDeathAges) s.deathAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            
            foreach (var kvp in triggeredMilestones) s.triggeredMilestones.Add(new MilestoneEntry { id = kvp.Key, keys = new List<string>(kvp.Value) });
            foreach (var kvp in lastBirthdayYear) s.lastBirthdays.Add(new BirthdayEntry { id = kvp.Key, year = kvp.Value });
            foreach (var kvp in dialogueHistory) s.dialogueHistory.Add(new DialogueHistoryEntry { key = kvp.Key, hashes = new List<int>(kvp.Value) });
            
            return s;
        }

        public static AgeData FromSerializable(AgeDataSerializable s)
        {
            var data = new AgeData();
            if (s == null) return data;
            foreach (var entry in s.ages) data.familyMemberAges[entry.id] = entry.weeks;
            foreach (var entry in s.externalAges) data.externalCharacterAges[entry.id] = entry.weeks;
            foreach (var entry in s.illnesses) data.elderIllnesses[entry.id] = entry.illnessIds;
            foreach (var entry in s.greyProfiles) data.greyProfiles[entry.id] = entry.profile;
            foreach (var entry in s.developmentGenes) data.developmentGenes[entry.id] = entry.gene;
            foreach (var entry in s.onsetData) data.onsetTiming[entry.id] = entry.onsetWeeks;
            foreach (var entry in s.deathDays) data.deceasedDeathDays[entry.id] = entry.weeks;
            foreach (var entry in s.deathAges) data.deceasedDeathAges[entry.id] = entry.weeks;
            
            if (s.triggeredMilestones != null) foreach (var entry in s.triggeredMilestones) data.triggeredMilestones[entry.id] = new HashSet<string>(entry.keys);
            if (s.lastBirthdays != null) foreach (var entry in s.lastBirthdays) data.lastBirthdayYear[entry.id] = entry.year;
            if (s.dialogueHistory != null) foreach (var entry in s.dialogueHistory) data.dialogueHistory[entry.key] = new HashSet<int>(entry.hashes);
            
            return data;
        }
    }

    /// <summary>
    /// Pure data transfer object (DTO) for Serialization. 
    /// Keeps data flat and type-safe for the Save System.
    /// </summary>
    [Serializable]
    public class AgeDataSerializable
    {
        public List<AgeEntry> ages = new List<AgeEntry>();
        public List<AgeEntry> externalAges = new List<AgeEntry>();
        public List<IllnessEntry> illnesses = new List<IllnessEntry>();
        public List<GreyProfileEntry> greyProfiles = new List<GreyProfileEntry>();
        public List<DevelopmentGeneEntry> developmentGenes = new List<DevelopmentGeneEntry>();
        public List<OnsetEntry> onsetData = new List<OnsetEntry>();
        public List<AgeEntry> deathDays = new List<AgeEntry>();
        public List<AgeEntry> deathAges = new List<AgeEntry>();
        public List<MilestoneEntry> triggeredMilestones = new List<MilestoneEntry>();
        public List<BirthdayEntry> lastBirthdays = new List<BirthdayEntry>();
        public List<DialogueHistoryEntry> dialogueHistory = new List<DialogueHistoryEntry>();
    }

    [Serializable] public class MilestoneEntry { public int id; public List<string> keys = new List<string>(); }
    [Serializable] public class BirthdayEntry { public int id; public int year; }
    [Serializable] public class DialogueHistoryEntry { public string key; public List<int> hashes = new List<int>(); }
    [Serializable] public class AgeEntry { public int id; public int weeks; }
    [Serializable] public class IllnessEntry { public int id; public List<string> illnessIds = new List<string>(); }
    [Serializable] public class GreyProfile { public float[] OriginalColor; public GreyingGene Gene; public int StartAgeWeeks; public int DurationWeeks; }
    [Serializable] public class GreyProfileEntry { public int id; public GreyProfile profile; }
    [Serializable] public class OnsetEntry { public int id; public Dictionary<string, int> onsetWeeks = new Dictionary<string, int>(); }
}
