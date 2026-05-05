using System;
using System.Collections.Generic;

namespace Lifespan
{
    /// <summary>
    /// Runtime model for lifespan state. Converted to/from AgeDataSerializable for storage.
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
        public int lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek;

        public Dictionary<int, int> deceasedDeathDays = new Dictionary<int, int>();
        public Dictionary<int, int> deceasedDeathAges = new Dictionary<int, int>();

        public AgeDataSerializable ToSerializable()
        {
            var s = new AgeDataSerializable();
            foreach (var kvp in familyMemberAges) s.ages.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in externalCharacterAges) s.externalAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in elderIllnesses) s.illnesses.Add(new IllnessEntry { id = kvp.Key, illnessIds = kvp.Value ?? new List<string>() });
            foreach (var kvp in greyProfiles) s.greyProfiles.Add(new GreyProfileEntry { id = kvp.Key, profile = kvp.Value });
            foreach (var kvp in developmentGenes) s.developmentGenes.Add(new DevelopmentGeneEntry { id = kvp.Key, gene = kvp.Value });
            foreach (var kvp in onsetTiming) s.onsetData.Add(new OnsetEntry { id = kvp.Key, onsetWeeks = kvp.Value ?? new Dictionary<string, int>() });
            foreach (var kvp in deceasedDeathDays) s.deathDays.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });
            foreach (var kvp in deceasedDeathAges) s.deathAges.Add(new AgeEntry { id = kvp.Key, weeks = kvp.Value });

            foreach (var kvp in triggeredMilestones) s.triggeredMilestones.Add(new MilestoneEntry { id = kvp.Key, keys = new List<string>(kvp.Value ?? new HashSet<string>()) });
            foreach (var kvp in lastBirthdayYear) s.lastBirthdays.Add(new BirthdayEntry { id = kvp.Key, year = kvp.Value });
            foreach (var kvp in dialogueHistory) s.dialogueHistory.Add(new DialogueHistoryEntry { key = kvp.Key, hashes = new List<int>(kvp.Value ?? new HashSet<int>()) });
            s.lastProcessedAgingWeek = lastProcessedAgingWeek;

            return s;
        }

        public static AgeData FromSerializable(AgeDataSerializable s)
        {
            var data = new AgeData();
            if (s == null) return data;

            if (s.ages != null)
                foreach (var entry in s.ages)
                    if (entry != null) data.familyMemberAges[entry.id] = entry.weeks;

            if (s.externalAges != null)
                foreach (var entry in s.externalAges)
                    if (entry != null) data.externalCharacterAges[entry.id] = entry.weeks;

            if (s.illnesses != null)
                foreach (var entry in s.illnesses)
                    if (entry != null) data.elderIllnesses[entry.id] = entry.illnessIds ?? new List<string>();

            if (s.greyProfiles != null)
                foreach (var entry in s.greyProfiles)
                    if (entry != null) data.greyProfiles[entry.id] = entry.profile;

            if (s.developmentGenes != null)
                foreach (var entry in s.developmentGenes)
                    if (entry != null) data.developmentGenes[entry.id] = entry.gene;

            if (s.onsetData != null)
                foreach (var entry in s.onsetData)
                    if (entry != null) data.onsetTiming[entry.id] = entry.onsetWeeks ?? new Dictionary<string, int>();

            if (s.deathDays != null)
                foreach (var entry in s.deathDays)
                    if (entry != null) data.deceasedDeathDays[entry.id] = entry.weeks;

            if (s.deathAges != null)
                foreach (var entry in s.deathAges)
                    if (entry != null) data.deceasedDeathAges[entry.id] = entry.weeks;

            if (s.triggeredMilestones != null)
                foreach (var entry in s.triggeredMilestones)
                    if (entry != null) data.triggeredMilestones[entry.id] = new HashSet<string>(entry.keys ?? new List<string>());

            if (s.lastBirthdays != null)
                foreach (var entry in s.lastBirthdays)
                    if (entry != null) data.lastBirthdayYear[entry.id] = entry.year;

            if (s.dialogueHistory != null)
                foreach (var entry in s.dialogueHistory)
                    if (entry != null && entry.key != null) data.dialogueHistory[entry.key] = new HashSet<int>(entry.hashes ?? new List<int>());

            data.lastProcessedAgingWeek = s.lastProcessedAgingWeek;

            return data;
        }
    }

    /// <summary>
    /// Pure data transfer object for the save system.
    /// Keep field names stable to maintain compatibility with existing saves.
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
        public int lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek;

        public void Clear()
        {
            ClearList(ref ages);
            ClearList(ref externalAges);
            ClearList(ref illnesses);
            ClearList(ref greyProfiles);
            ClearList(ref developmentGenes);
            ClearList(ref onsetData);
            ClearList(ref deathDays);
            ClearList(ref deathAges);
            ClearList(ref triggeredMilestones);
            ClearList(ref lastBirthdays);
            ClearList(ref dialogueHistory);
            lastProcessedAgingWeek = WeeklyAgingPolicy.NoProcessedWeek;
        }

        public void CopyFrom(AgeDataSerializable source)
        {
            if (source == null)
            {
                Clear();
                return;
            }

            CopyList(ref ages, source.ages);
            CopyList(ref externalAges, source.externalAges);
            CopyList(ref illnesses, source.illnesses);
            CopyList(ref greyProfiles, source.greyProfiles);
            CopyList(ref developmentGenes, source.developmentGenes);
            CopyList(ref onsetData, source.onsetData);
            CopyList(ref deathDays, source.deathDays);
            CopyList(ref deathAges, source.deathAges);
            CopyList(ref triggeredMilestones, source.triggeredMilestones);
            CopyList(ref lastBirthdays, source.lastBirthdays);
            CopyList(ref dialogueHistory, source.dialogueHistory);
            lastProcessedAgingWeek = source.lastProcessedAgingWeek;
        }

        public bool HasAnyData()
        {
            return HasAny(ages)
                || HasAny(externalAges)
                || HasAny(illnesses)
                || HasAny(greyProfiles)
                || HasAny(developmentGenes)
                || HasAny(onsetData)
                || HasAny(deathDays)
                || HasAny(deathAges)
                || HasAny(triggeredMilestones)
                || HasAny(lastBirthdays)
                || HasAny(dialogueHistory)
                || lastProcessedAgingWeek != WeeklyAgingPolicy.NoProcessedWeek;
        }

        private static void ClearList<T>(ref List<T> list)
        {
            if (list == null) list = new List<T>();
            else list.Clear();
        }

        private static void CopyList<T>(ref List<T> target, List<T> source)
        {
            if (target == null) target = new List<T>();
            else target.Clear();

            if (source != null) target.AddRange(source);
        }

        private static bool HasAny<T>(List<T> list)
        {
            return list != null && list.Count > 0;
        }
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
