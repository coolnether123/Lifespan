using ModAPI.Core;
using ModAPI.Saves;
using ModAPI.Util;
using ModAPI.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Tracks the age of all family members and relevant NPCs in weeks.
    /// Persists age data per-save using save events.
    /// </summary>
    public class AgeTracker
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly IPluginContext _ctx;
        private readonly System.Random _random;
        private AgeData _currentAgeData;

        public AgeTracker(IPluginContext ctx, LifespanConfig config)
        {
            _ctx = ctx;
            _config = config;
            _log = ctx.Log;
            _currentAgeData = new AgeData();
            _random = new System.Random();
        }

        public void Reset()
        {
            LifespanLoggerExtensions.Debug(_log, "[AgeTracker] Resetting tracking data for fresh session.");
            _currentAgeData = new AgeData();
        }

        public int AdultAgeWeeks => _config.adultAgeYears * 52;

        private int NormalDistribution(int mean, int stdDev)
        {
            // Box-Muller transform
            double u1 = 1.0 - _random.NextDouble(); // uniform(0,1] random doubles
            double u2 = 1.0 - _random.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); // random normal(0,1)
            return (int)(mean + stdDev * randStdNormal);
        }

        public int GenerateInitialAge(FamilyMember member, bool isChild)
        {
            if (isChild)
            {
                // Most children 5-14, some younger/older
                return Mathf.Clamp(NormalDistribution(_config.initialChildAgeYears, 4), 0, 17) * 52;
            }
            else
            {
                // Most adults 32-45, some younger/older
                // Shifted mean to 32 to help distinguish "Reset" ages from legitimate young adults
                return Mathf.Clamp(NormalDistribution(32, 14), 18, 90) * 52;
            }
        }

        /// <summary>
        /// Generates age for any character based on context.
        /// Called by NPCs, recruits, explorers, and pregnancy mod.
        /// </summary>
        public int GenerateAgeForContext(BaseCharacter character, AgeContext context)
        {
            if (character == null) return 0;
            
            // Check if we already know this character's age (for NPCs that might be saved across sessions)
            int id = character.GetId();
            if (_currentAgeData.externalCharacterAges.ContainsKey(id))
            {
                return _currentAgeData.externalCharacterAges[id];
            }

            // Generate based on context
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
                    ageWeeks = 0; // 0 weeks for newborns
                    break;
                default:
                    ageWeeks = GenerateDefaultNPCAge(character);
                    break;
            }

            // Store it so we don't regenerate
            _currentAgeData.externalCharacterAges[id] = ageWeeks;
            
            // Publish event for other mods
            ModEventBus.Publish("Lifespan.CharacterAgeGenerated", 
                new CharacterAgeGeneratedArgs(character, ageWeeks, context));
            
            return ageWeeks;
        }

        private int GenerateExplorerAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.explorerMeanAge, _config.explorerStdDev), 18, 55) * 52;
        }

        private int GenerateShelterRecruitAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.recruiterMeanAge, _config.recruiterStdDev), 18, 80) * 52;
        }

        private int GenerateTraderAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(_config.traderMeanAge, _config.traderStdDev), 25, 70) * 52;
        }

        private int GenerateDefaultNPCAge(BaseCharacter character)
        {
            return Mathf.Clamp(NormalDistribution(35, 12), 18, 65) * 52;
        }

        /// <summary>
        /// Gets the age of a family member in weeks by ID.
        /// Returns 0 if not found.
        /// </summary>
        public int GetAgeWeeks(int id)
        {
            // Check family members first
            if (_currentAgeData.familyMemberAges.ContainsKey(id))
            {
                return _currentAgeData.familyMemberAges[id];
            }
            // Check external characters
            if (_currentAgeData.externalCharacterAges.ContainsKey(id))
            {
                return _currentAgeData.externalCharacterAges[id];
            }
            return 0;
        }

        /// <summary>
        /// Gets the age of a family member by name.
        /// Useful when ID is not available (e.g. legacy ObituaryInfo).
        /// </summary>
        public int GetAgeWeeksByName(string firstName)
        {
            if (string.IsNullOrEmpty(firstName)) return 0;

            // 1. Try to resolve from active family members
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

            // 2. Fallback: Search deceased profiles (this requires a reverse lookup check)
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
        /// Gets the age of a family member in weeks.
        /// Returns 0 if not tracked yet (new member).
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

            // New member - initialize using Gaussian distribution
            int initialAge = GenerateInitialAge(member, member.isChild);
            _currentAgeData.familyMemberAges[memberId] = initialAge;
            if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[Lifespan] Initialized age for {member.firstName} to {initialAge / 52} years (ID: {memberId}).");
            
            // Fire event for other mods
            ModEventBus.Publish("Lifespan.AgeInitialized", new AgeChangedArgs(member, initialAge, 0));

            return initialAge;
        }

        /// <summary>
        /// Gets the age of any character (FamilyMember or NPC) in weeks.
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
        /// Sets the age of an external character (NPC).
        /// </summary>
        public void SetExternalCharacterAge(int id, int ageWeeks)
        {
            _currentAgeData.externalCharacterAges[id] = ageWeeks;
        }

        /// <summary>
        /// Gets the age of a family member in years.
        /// </summary>
        public int GetAgeYears(FamilyMember member)
        {
            return GetAgeWeeks(member) / 52;
        }

        /// <summary>
        /// Sets the age of a family member in weeks.
        /// </summary>
        public void SetAgeWeeks(FamilyMember member, int weeks)
        {
            if (object.ReferenceEquals(member, null))
                return;

            int memberId = member.GetId();
            _currentAgeData.familyMemberAges[memberId] = Math.Max(0, weeks);
            if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[Lifespan] Manual age override: {member.firstName} set to {weeks / 52} years.");
        }

        /// <summary>
        /// Sets the age of any character in weeks.
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
        /// Increments the age of a family member by a specific amount of weeks.
        /// Returns the new age in weeks.
        /// </summary>
        public int IncrementAge(FamilyMember member, int weeks)
        {
            if (object.ReferenceEquals(member, null)) return 0;

            // GetAgeWeeks handles initialization if needed
            int currentAge = GetAgeWeeks(member);
            int newAge = currentAge + weeks;
            
            int memberId = member.GetId();
            _currentAgeData.familyMemberAges[memberId] = newAge;

            // Log if a new year is reached
            if (newAge / 52 > currentAge / 52)
            {
                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[Lifespan] BIRTHDAY! {member.firstName} is now {newAge / 52} years old.");
            }

            if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, $"{member.firstName} aged to {newAge} weeks (+{weeks}).");

            return newAge;
        }

        /// <summary>
        /// Checks if a family member is an elder (past elder threshold).
        /// </summary>
        public bool IsElder(FamilyMember member)
        {
            int ageWeeks = GetAgeWeeks(member);
            bool isElder = ageWeeks >= (_config.elderAgeYears * 52);
            if (isElder) LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] {member.firstName} is an elder ({ageWeeks / 52}y).");
            return isElder;
        }

        /// <summary>
        /// Saves age data for the current save.
        /// PersistentDataAPI automatically handles slot separation.
        /// </summary>
        public void SaveAgeData()
        {
            LifespanLoggerExtensions.Debug(_log, "[AgeTracker] SaveAgeData() started.");
            try
            {
                // Sync current members before saving to ensure everything is up to date
                SyncWithFamilyManager();

                var serializableData = _currentAgeData.ToSerializable();
                _ctx.SaveData("LifeSpan.AgeData", serializableData);
                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"[Lifespan] Persisted age data for {serializableData.ages.Count} members and {serializableData.externalAges.Count} NPCs.");
                if (LifespanLoggerExtensions.VerboseEnabled) LifespanLoggerExtensions.Debug(_log, $"Records: {serializableData.deathDays.Count} deaths, {serializableData.illnesses.Count} illnesses.");
            }
            catch (Exception ex)
            {
                _log.Error($"[Lifespan] Failed to save age data: {ex.Message}");
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
                // GetAgeWeeks handles initialization if member not tracked yet
                GetAgeWeeks(member);
            }
        }

        /// <summary>
        /// Loads age data for the current save.
        /// PersistentDataAPI automatically handles slot separation.
        /// </summary>
        public void LoadAgeData()
        {
            LifespanLoggerExtensions.Debug(_log, "[AgeTracker] LoadAgeData() started.");
            try
            {
                AgeDataSerializable serializableData;
                if (_ctx.LoadData("LifeSpan.AgeData", out serializableData))
                {
                    _currentAgeData = AgeData.FromSerializable(serializableData);
                    LifespanLoggerExtensions.Debug(_log, $"DATA LOADED: Found records for {_currentAgeData.familyMemberAges.Count} members.");
                }
                else
                {
                    _log.Warn("[Lifespan] DATA MISSING: No age records found in save file. Assuming NEW Lifespan save or DATA LOSS.");
                    _currentAgeData = new AgeData();
                    InitializeExistingMembers();
                }
            }
            catch (Exception ex)
            {
                _log.Error($"[Lifespan] CRITICAL FAILURE loading age data: {ex.Message}");
                _currentAgeData = new AgeData();
                InitializeExistingMembers();
            }
        }

        private void InitializeExistingMembers()
        {
            if (FamilyManager.Instance == null) return;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            LifespanLoggerExtensions.Debug(_log, "[AgeTracker] Initializing members... (This should only happen once per save!)");
            foreach (var member in members)
            {
                if (!object.ReferenceEquals(member, null) && !member.isDead)
                {
                    // If the ID is somehow already tracked, GetAgeWeeks won't overwrite it.
                    // But if we are here, _currentAgeData is likely empty.
                    int newAge = GetAgeWeeks(member);
                    LifespanLoggerExtensions.Debug(_log, $"Generated FRESH age for {member.firstName}: {newAge / 52} years.");
                }
            }
        }

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

            // Also preserve IDs of dead characters so we can show their age in the obituary
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
                LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Cleaning up {toRemove.Count} IDs that no longer exist.");
            }
            foreach (var id in toRemove)
            {
                _currentAgeData.familyMemberAges.Remove(id);
                _currentAgeData.elderIllnesses.Remove(id);
            }

            if (toRemove.Count > 0)
            {
                if (LifespanLoggerExtensions.VerboseEnabled) _log.Info($"Cleaned up age tracking for {toRemove.Count} members no longer in family.");
            }
        }

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

        public void AddIllness(FamilyMember member, string illnessId)
        {
            if (member == null) return;
            var list = GetIllnesses(member);
            if (!list.Contains(illnessId))
            {
                list.Add(illnessId);
                LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Added illness {illnessId} to {member.firstName}.");
            }
        }

        public void RemoveIllness(FamilyMember member, string illnessId)
        {
            if (member == null) return;
            int id = member.GetId();
            if (_currentAgeData.elderIllnesses.ContainsKey(id))
            {
                _currentAgeData.elderIllnesses[id].Remove(illnessId);
                // Also remove onset timing
                if (_currentAgeData.onsetTiming.ContainsKey(id))
                {
                    _currentAgeData.onsetTiming[id].Remove(illnessId);
                }
                LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Removed illness {illnessId} from {member.firstName}.");
            }
        }

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
        /// Gets or generates the hair greying profile for a member.
        /// This determines when they start greying and how fast.
        /// </summary>
        public GreyProfile GetOrGenerateGreyProfile(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            if (_currentAgeData.greyProfiles.ContainsKey(id))
            {
                var existing = _currentAgeData.greyProfiles[id];
                // Migration: If Gene is null (old save), generate it
                if (existing.Gene == null)
                {
                    existing.Gene = GreyingGene.GenerateRandom(_random);
                    
                    LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Migrated legacy GreyProfile for {member.firstName} to use GreyingGene.");
                }
                return existing;
            }

            // Generate new profile
            Color currentHair = ReflectionHelper.GetField<Color>(member, "m_hairColor");
            // If checking fails, default to black/grey
            if (currentHair == default(Color)) currentHair = Color.black; 

            var profile = new GreyProfile();
            profile.OriginalColor = new float[] { currentHair.r, currentHair.g, currentHair.b, currentHair.a };
            
            // Generate Gene
            profile.Gene = GreyingGene.GenerateRandom(_random);

            _currentAgeData.greyProfiles[id] = profile;
            LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Generated Grey Profile for {member.firstName}. Start: {profile.Gene.StartAge}y, Duration: {profile.Gene.DurationYears}y, Coverage: {profile.Gene.MaxCoverage:P0}.");

            return profile;
        }

        /// <summary>
        /// Gets or generates the development gene for a member.
        /// Determines stat growth potential.
        /// </summary>
        public DevelopmentGene GetOrGenerateDevelopmentGene(FamilyMember member)
        {
            if (member == null) return null;
            int id = member.GetId();

            if (_currentAgeData.developmentGenes.ContainsKey(id))
            {
                return _currentAgeData.developmentGenes[id];
            }

            // Generate new gene
            var gene = DevelopmentGene.GenerateRandom(_random);
            
            _currentAgeData.developmentGenes[id] = gene;
            LifespanLoggerExtensions.Debug(_log, $"[AgeTracker] Generated Dev Gene for {member.firstName}. Potentials: Pre-A: {gene.PreAdultPotential}, Post-A: {gene.PostAdultPotential}, Pre-E: {gene.PreElderPotential}, Post-E: {gene.PostElderPotential}.");

            return gene;
        }

        public void RecordDeath(FamilyMember member, int backdatedDay)
        {
            if (member == null) return;
            int id = member.GetId();
            int ageWeeks = GetAgeWeeks(member);
            
            _currentAgeData.deceasedDeathDays[id] = backdatedDay;
            _currentAgeData.deceasedDeathAges[id] = ageWeeks;
            _log.Info($"[Lifespan] Recorded death for {member.firstName}. Day: {backdatedDay}, Age: {ageWeeks / 52}y.");
        }

        public bool TryGetDeathInfo(int id, out int day, out int ageWeeks)
        {
            day = 0;
            ageWeeks = 0;
            bool foundDay = _currentAgeData.deceasedDeathDays.TryGetValue(id, out day);
            bool foundAge = _currentAgeData.deceasedDeathAges.TryGetValue(id, out ageWeeks);
            return foundDay || foundAge;
        }

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
    }

    [Serializable]
    public class AgeData
    {
        public Dictionary<int, int> familyMemberAges = new Dictionary<int, int>();
        // Stores age for NPCs across sessions
        public Dictionary<int, int> externalCharacterAges = new Dictionary<int, int>();
        public Dictionary<int, List<string>> elderIllnesses = new Dictionary<int, List<string>>();
        public Dictionary<int, GreyProfile> greyProfiles = new Dictionary<int, GreyProfile>();
        public Dictionary<int, DevelopmentGene> developmentGenes = new Dictionary<int, DevelopmentGene>();
        // Key: Member ID, Value: Map of IllnessID -> Week it should upgrade/trigger next stage
        public Dictionary<int, Dictionary<string, int>> onsetTiming = new Dictionary<int, Dictionary<string, int>>();

        public Dictionary<int, int> deceasedDeathDays = new Dictionary<int, int>();
        public Dictionary<int, int> deceasedDeathAges = new Dictionary<int, int>();

        public AgeDataSerializable ToSerializable()
        {
            var s = new AgeDataSerializable();
            if (familyMemberAges != null)
            {
                foreach (var kvp in familyMemberAges)
                    s.ages.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            }
            if (externalCharacterAges != null)
            {
                foreach (var kvp in externalCharacterAges)
                    s.externalAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            }
            if (elderIllnesses != null)
            {
                foreach (var kvp in elderIllnesses)
                    s.illnesses.Add(new IllnessEntry { id = kvp.Key, illnessIds = kvp.Value });
            }
            if (greyProfiles != null)
            {
                foreach (var kvp in greyProfiles)
                    s.greyProfiles.Add(new GreyProfileEntry { id = kvp.Key, profile = kvp.Value });
            }
            if (developmentGenes != null)
            {
                foreach (var kvp in developmentGenes)
                    s.developmentGenes.Add(new DevelopmentGeneEntry { id = kvp.Key, gene = kvp.Value });
            }
            if (onsetTiming != null)
            {
                foreach (var kvp in onsetTiming)
                    s.onsetData.Add(new OnsetEntry { id = kvp.Key, onsetWeeks = kvp.Value });
            }
            if (deceasedDeathDays != null)
            {
                foreach (var kvp in deceasedDeathDays)
                    s.deathDays.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            }
            if (deceasedDeathAges != null)
            {
                foreach (var kvp in deceasedDeathAges)
                    s.deathAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            }
            return s;
        }

        public static AgeData FromSerializable(AgeDataSerializable s)
        {
            var data = new AgeData();
            if (s != null)
            {
                if (s.ages != null)
                {
                    foreach (var entry in s.ages)
                        data.familyMemberAges[entry.id] = entry.weeks;
                }
                if (s.externalAges != null)
                {
                    foreach (var entry in s.externalAges)
                        data.externalCharacterAges[entry.id] = entry.weeks;
                }
                if (s.illnesses != null)
                {
                    foreach (var entry in s.illnesses)
                        data.elderIllnesses[entry.id] = entry.illnessIds;
                }
                if (s.greyProfiles != null)
                {
                    foreach (var entry in s.greyProfiles)
                        data.greyProfiles[entry.id] = entry.profile;
                }
                if (s.developmentGenes != null)
                {
                    foreach (var entry in s.developmentGenes)
                        data.developmentGenes[entry.id] = entry.gene;
                }
                if (s.onsetData != null)
                {
                    foreach (var entry in s.onsetData)
                        data.onsetTiming[entry.id] = entry.onsetWeeks;
                }
                if (s.deathDays != null)
                {
                    foreach (var entry in s.deathDays)
                        data.deceasedDeathDays[entry.id] = entry.weeks;
                }
                if (s.deathAges != null)
                {
                    foreach (var entry in s.deathAges)
                        data.deceasedDeathAges[entry.id] = entry.weeks;
                }
            }
            return data;
        }
    }

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
    }

    [Serializable]
    public class AgeEntry
    {
        public int id;
        public int weeks;
    }

    [Serializable]
    public class IllnessEntry
    {
        public int id;
        public List<string> illnessIds = new List<string>();
    }

    [Serializable]
    public class GreyProfile
    {
        public float[] OriginalColor;
        public GreyingGene Gene;
        // Legacy fields maintained for deserialization safety, though unused
        public int StartAgeWeeks; 
        public int DurationWeeks;
    }

    [Serializable]
    public class GreyProfileEntry
    {
        public int id;
        public GreyProfile profile;
    }

    [Serializable]
    public class OnsetEntry
    {
        public int id;
        public Dictionary<string, int> onsetWeeks = new Dictionary<string, int>();
    }
}
