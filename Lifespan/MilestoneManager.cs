using ModAPI.Core;
using System;
using System.Collections.Generic;
using Lifespan.Dialogue.Content;

namespace Lifespan
{
    /// <summary>
    /// Schedules saved milestone, birthday, and elder dialogue.
    /// </summary>
    public class MilestoneManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly IMilestoneProgressState _milestoneState;
        private readonly ModRandomStream _random;
        private DialogueScheduler _scheduler;

        private Dictionary<int, HashSet<string>> _triggeredMilestones => _milestoneState.TriggeredMilestones;
        private Dictionary<int, int> _lastBirthdayYear => _milestoneState.LastBirthdayYears;
        private readonly DialogueHelper _dialogueHelper;
        private IModLogger Log => _log;

        public MilestoneManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random, DialogueHelper dialogueHelper)
            : this(ctx, config, ageTracker, random, dialogueHelper, ageTracker != null ? ageTracker.State.Milestones : null)
        {
        }

        internal MilestoneManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random, DialogueHelper dialogueHelper, IMilestoneProgressState milestoneState)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _milestoneState = milestoneState;
            _random = random;
            _dialogueHelper = dialogueHelper;
        }

        public void SetScheduler(DialogueScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        public void Reset()
        {
            // Persistent milestone maps are owned by AgeTracker save data.
        }

        private void TriggerSpeech(FamilyMember member, string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(member, text, false, priority, validation);
            else
            {
                try
                {
                    if (validation == null || validation()) member.Say(text);
                }
                catch (Exception ex)
                {
                    if (Log.IsDebugEnabled) Log.Debug($"Failed to show milestone speech line: {ex.Message}");
                }
            }
        }

        private void TriggerJournal(string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (!_config.enableJournalEntries) return;
            if (_scheduler != null) _scheduler.Enqueue(null, text, true, priority, validation);
            else if (validation == null || validation()) JournalEntryWriter.TryInsert(text, Log);
        }

        public void ProcessMilestones(FamilyMember member, int ageWeeks)
        {
            if (member == null || member.isDead) return;

            int ageYears = ageWeeks / LifespanConstants.WeeksPerYear;
            int memberId = member.GetId();

            if (!_triggeredMilestones.ContainsKey(memberId))
                _triggeredMilestones[memberId] = new HashSet<string>();

            // Offset each milestone within its age window, but keep the offset stable per survivor.
            TryTriggerFuzzyUnique(member, 18, 2, "Milestone_Adult", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 25, 2, "Milestone_Prime", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 30, 2, "Milestone_30s", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 40, 3, "Milestone_MidLife", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 50, 3, "Milestone_50s", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 60, 4, "Milestone_Elder", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 70, 5, "Milestone_70s", (m, age) => TriggerBirthdayJournal(m, age));
            TryTriggerFuzzyUnique(member, 80, 5, "Milestone_80s", (m, age) => TriggerBirthdayJournal(m, age));

            // Persisted lastBirthdayYear prevents repeat triggers if the game is reloaded.
            if (!_lastBirthdayYear.ContainsKey(memberId)) _lastBirthdayYear[memberId] = ageYears - 1;
            if (ageYears > _lastBirthdayYear[memberId])
            {
                _lastBirthdayYear[memberId] = ageYears;
                if (_random.Value() < 0.3f) TriggerRoutineBirthdaySpeech(member, ageYears);
            }

            if (member.isChild && ageYears > 0 && ageYears % 5 == 0)
                TryTriggerUnique(member, "GrowthObs_" + ageYears, (m) => TriggerAgeObservation(m, ageYears));

            // Elders occasionally comment on life in the bunker.
            if (ageYears >= _config.elderAgeYears && _random.Value() < 0.06f)
                TriggerElderPhilosophy(member);
        }

        /// <summary>
        /// Runs an event once for each survivor and milestone key.
        /// </summary>
        private void TryTriggerUnique(FamilyMember member, string key, Action<FamilyMember> action)
        {
            int memberId = member.GetId();
            if (!_triggeredMilestones.ContainsKey(memberId)) _triggeredMilestones[memberId] = new HashSet<string>();
            if (!_triggeredMilestones[memberId].Contains(key))
            {
                action(member);
                _triggeredMilestones[memberId].Add(key);
            }
        }

        /// <summary>
        /// Uses the survivor's name to choose a stable offset within the milestone's age window.
        /// </summary>
        private void TryTriggerFuzzyUnique(FamilyMember member, int targetAge, int range, string keyBase, Action<FamilyMember, int> action)
        {
            int currentAge = _ageTracker.GetAgeWeeks(member) / LifespanConstants.WeeksPerYear;
            if (currentAge < targetAge || currentAge > targetAge + range) return;
            int offset = Math.Abs(member.firstName.GetHashCode()) % (range + 1);
            if (currentAge >= targetAge + offset) TryTriggerUnique(member, keyBase, (m) => action(m, currentAge));
        }

        public void ReportSkillGain(FamilyMember member, BaseStats.StatType statType)
        {
            if (member == null || !member.isChild) return;
            TryTriggerUnique(member, "FirstSkill", (m) => TriggerSkillGainMessage(m, statType));
            TryTriggerUnique(member, "FirstStat_" + statType, (m) => TriggerSkillGainMessage(m, statType));
            if (_random.Value() < 0.10f) TriggerSkillGainMessage(member, statType);
        }

        #region Trigger Logic

        private void TriggerBirthdayJournal(FamilyMember member, int currentAge)
        {
            bool oldest = IsOldestSurvivor(member);
            bool youngest = IsYoungestSurvivor(member);
            bool hasChildren = HasChildrenInFamily(member);

            var options = MilestoneDialogue.GetJournalBirthdayOptions(currentAge, oldest, youngest, hasChildren);
            string line = _dialogueHelper.PickLine($"JournalMilestone_{currentAge}", options);
            
            // Format parameters
            line = string.Format(line, member.firstName, currentAge);
            TriggerJournal(line, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetAgeWeeks(member) / LifespanConstants.WeeksPerYear == currentAge);
        }

        private FamilyMember GetObserver(FamilyMember excluded)
        {
            if (FamilyManager.Instance == null) return null;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return null;
            List<FamilyMember> candidates = new List<FamilyMember>();
            foreach (var m in members) if (m != null && !m.isDead && m.GetId() != excluded.GetId()) candidates.Add(m);
            if (candidates.Count == 0) return null;
            return candidates[_random.Range(0, candidates.Count)];
        }

        private void TriggerAgeObservation(FamilyMember member, int ageYears)
        {
            var observer = GetObserver(member);
            if (observer == null) return;
            int observerAge = _ageTracker.GetAgeWeeks(observer) / LifespanConstants.WeeksPerYear;

            var options = MilestoneDialogue.GetAgeObservationOptions(member.firstName, ageYears, observerAge);
            string personal = GetStatPersonalNote(member, ageYears > 7);
            if (!string.IsNullOrEmpty(personal)) options.Add(personal);

            string line = _dialogueHelper.PickLine($"AgeObs_{ageYears}", options, observer);
            TriggerSpeech(observer, line, DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / LifespanConstants.WeeksPerYear == ageYears);
        }

        private void TriggerSkillGainMessage(FamilyMember member, BaseStats.StatType statType)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            int speakerLevel = GetStatLevel(observer, statType);
            int learnerLevel = GetStatLevel(member, statType);
            bool isCompetitive = speakerLevel > learnerLevel && speakerLevel <= learnerLevel + 3;

            var theme = SkillDialogue.GetTheme(statType, isCompetitive);
            string opener = _dialogueHelper.PickLine($"Skill_{statType}_{(isCompetitive ? "Comp" : "Pride")}_Opener", theme.Openers, observer);
            if (_scheduler == null)
            {
                TriggerSpeech(observer, opener, DialogueScheduler.Priority.Reactive);
                if (_random.Value() < 0.50f)
                {
                    string responseFallback = _dialogueHelper.PickLine($"Skill_{statType}_Response", theme.Responses, member);
                    TriggerSpeech(member, responseFallback, DialogueScheduler.Priority.Reactive);
                    string finalFallback = _dialogueHelper.PickLine("Encouragement", SkillDialogue.GetEncouragementOptions(), observer);
                    TriggerSpeech(observer, finalFallback, DialogueScheduler.Priority.Reactive);
                }
                return;
            }

            var turns = new List<DialogueScheduler.ConversationTurn>();
            turns.Add(new DialogueScheduler.ConversationTurn(observer, opener));

            if (_random.Value() < 0.50f)
            {
                string response = _dialogueHelper.PickLine($"Skill_{statType}_Response", theme.Responses, member);
                turns.Add(new DialogueScheduler.ConversationTurn(member, response));
                string final = _dialogueHelper.PickLine("Encouragement", SkillDialogue.GetEncouragementOptions(), observer);
                turns.Add(new DialogueScheduler.ConversationTurn(observer, final));
            }

            _scheduler.EnqueueConversation(turns, DialogueScheduler.Priority.Reactive, 0.9f, 1.8f);
        }

        private int GetStatLevel(FamilyMember member, BaseStats.StatType statType)
        {
            if (member == null || member.BaseStats == null) return 0;
            switch (statType)
            {
                case BaseStats.StatType.Strength: return member.BaseStats.Strength.Level;
                case BaseStats.StatType.Dexterity: return member.BaseStats.Dexterity.Level;
                case BaseStats.StatType.Intelligence: return member.BaseStats.Intelligence.Level;
                case BaseStats.StatType.Charisma: return member.BaseStats.Charisma.Level;
                case BaseStats.StatType.Perception: return member.BaseStats.Perception.Level;
                default: return 0;
            }
        }

        private void TriggerElderPhilosophy(FamilyMember member)
        {
            int category = _random.Range(0, 4);
            var options = SkillDialogue.GetPhilosophyOptions(category);
            string line = _dialogueHelper.PickLine($"Philosophy_{category}", options, member);
            TriggerSpeech(member, line);
        }

        private void TriggerRoutineBirthdaySpeech(FamilyMember member, int age)
        {
            float trauma = (member.stats?.trauma != null) ? member.stats.trauma.NormalizedValue : 0f;
            var options = MilestoneDialogue.GetRoutineBirthdayOptions(
                member.firstName, 
                age, 
                GetRandomBunkerItem(), 
                age >= 50, 
                age < 30, 
                trauma > 0.4f, 
                trauma < 0.2f
            );

            string line = _dialogueHelper.PickLine($"Birthday_{member.GetId()}", options, member);
            TriggerSpeech(member, line, DialogueScheduler.Priority.Routine, () => _ageTracker.GetAgeWeeks(member) / LifespanConstants.WeeksPerYear == age);
        }

        private string GetStatPersonalNote(FamilyMember member, bool olderChild)
        {
            if (member == null || member.BaseStats == null) return null;
            return MilestoneDialogue.GetStatPersonalNote(member.firstName, member.BaseStats.Strength.Level, member.BaseStats.Dexterity.Level, member.BaseStats.Intelligence.Level, olderChild);
        }

        private string GetRandomBunkerItem()
        {
            string[] items = { "ration", "can of water", "bandage", "bit of soap", "proper book" };
            return items[_random.Range(0, items.Length)];
        }

        private bool IsOldestSurvivor(FamilyMember member)
        {
            if (FamilyManager.Instance == null) return false;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return false;
            int myAge = _ageTracker.GetAgeWeeks(member);
            bool foundOlder = false, anyOther = false;
            foreach (var m in members)
            {
                if (m == null || m.isDead || m.GetId() == member.GetId()) continue;
                anyOther = true;
                if (_ageTracker.GetAgeWeeks(m) > myAge) foundOlder = true;
            }
            return anyOther && !foundOlder;
        }

        private bool IsYoungestSurvivor(FamilyMember member)
        {
            if (FamilyManager.Instance == null) return false;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return false;
            int myAge = _ageTracker.GetAgeWeeks(member);
            bool foundYounger = false, anyOther = false;
            foreach (var m in members)
            {
                if (m == null || m.isDead || m.GetId() == member.GetId()) continue;
                anyOther = true;
                if (_ageTracker.GetAgeWeeks(m) < myAge) foundYounger = true;
            }
            return anyOther && !foundYounger;
        }

        private bool HasChildrenInFamily(FamilyMember excluded)
        {
            if (FamilyManager.Instance == null) return false;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return false;
            foreach (var m in members)
            {
                if (m == null || m.isDead || m.GetId() == excluded?.GetId()) continue;
                if (m.isChild) return true;
            }
            return false;
        }
        #endregion
    }
}
