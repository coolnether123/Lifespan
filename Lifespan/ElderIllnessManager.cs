using ModAPI.Core;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using Lifespan.Dialogue.Content;

namespace Lifespan
{
    public class ElderIllnessManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly ModRandomStream _random;

        public const string ILLNESS_DEMENTIA = "lifespan.illness.dementia";
        public const string ILLNESS_MILD_DEMENTIA = "lifespan.illness.mild.dementia";
        public const string ILLNESS_ARTHRITIS = "lifespan.illness.arthritis";
        public const string ILLNESS_MILD_ARTHRITIS = "lifespan.illness.mild.arthritis";
        public const string ILLNESS_HEART = "lifespan.illness.heart";
        public const string ILLNESS_MILD_HEART = "lifespan.illness.mild.heart";
        public const string ILLNESS_FRAILTY = "lifespan.illness.frailty";
        public const string ILLNESS_MILD_FRAILTY = "lifespan.illness.mild.frailty";
        public const string ILLNESS_RESPIRATORY = "lifespan.illness.respiratory";
        public const string ILLNESS_MILD_RESPIRATORY = "lifespan.illness.mild.respiratory";

        private DeathManager _deathManager;
        private DialogueScheduler _scheduler;
        private readonly DialogueHelper _dialogueHelper;
        private IModLogger Log => _log;

        public ElderIllnessManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random, DialogueHelper dialogueHelper)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = random;
            _dialogueHelper = dialogueHelper;
        }

        public void SetDeathManager(DeathManager deathManager) => _deathManager = deathManager;
        public void SetScheduler(DialogueScheduler scheduler) => _scheduler = scheduler;

        private void TriggerJournal(string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(null, text, true, priority, validation);
            else if (JournalManager.Instance != null && (validation == null || validation()))
            {
                try { Traverse.Create(JournalManager.Instance).Method("InsertJournalEntry", new object[] { text, "", false }).GetValue(); } catch { }
            }
        }

        private void TriggerSpeech(FamilyMember member, string text, DialogueScheduler.Priority priority = DialogueScheduler.Priority.Routine, System.Func<bool> validation = null)
        {
            if (_scheduler != null) _scheduler.Enqueue(member, text, false, priority, validation);
            else try { if (validation == null || validation()) member.Say(text); } catch { }
        }

        public void ProcessElderIllnessRoll(FamilyMember member, int currentAgeWeeks)
        {
            if (member == null || member.isDead) return;
            CheckProgression(member);

            float healthFactor = (float)member.health / member.maxHealth;
            float stressFactor = (member.stats?.trauma != null) ? member.stats.trauma.Value / 100f : 0f;
            float fatigueFactor = (member.stats?.fatigue != null) ? member.stats.fatigue.Value / 100f : 0f;

            float acquiredChance = _config.elderIllnessBaseChance * 
                                  (1.0f + (2.0f * (1.0f - healthFactor))) * 
                                  (1.0f + (1.5f * stressFactor)) * 
                                  (1.0f + (0.5f * fatigueFactor));

            if (_random.Value() < acquiredChance) AcquireRandomIllness(member);
            ApplyOngoingEffects(member);
        }

        private void CheckProgression(FamilyMember member)
        {
            int currentWeek = _ageTracker.GetAgeWeeks(member);
            var illnesses = _ageTracker.GetIllnesses(member);
            var toUpgrade = new List<string>();

            foreach (var ill in illnesses)
            {
                if (!ill.Contains(".mild.")) continue;
                int targetWeek = _ageTracker.GetOnsetWeek(member, ill);
                if (targetWeek <= 0)
                {
                    targetWeek = CalculateNextStageWeek(member, currentWeek);
                    _ageTracker.SetOnsetWeek(member, ill, targetWeek);
                }
                if (currentWeek >= targetWeek) toUpgrade.Add(ill);
            }

            foreach (var mild in toUpgrade)
            {
                string severe = mild.Replace(".mild.", ".");
                _ageTracker.RemoveIllness(member, mild);
                _ageTracker.AddIllness(member, severe);
                ApplyInitialEffect(member, severe);

                string victimLine = _dialogueHelper.PickLine($"Flavor_{severe}", IllnessDialogue.GetFlavorOptions(severe));
                TriggerSpeech(member, victimLine, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(severe));
                TriggerJournal($"Condition Worsened: {member.firstName} now has {GetIllnessName(severe)}.", DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(severe));
                TriggerObserverReactions(member, severe);
            }
        }

        private int CalculateNextStageWeek(FamilyMember member, int currentWeek)
        {
             int duration = (int)(_random.Range(_config.illnessStageMinYears * LifespanConstants.WeeksPerYear, _config.illnessStageMaxYears * LifespanConstants.WeeksPerYear));
             float modifier = 1.0f;
             if (((float)member.health / member.maxHealth) < 0.5f) modifier -= 0.2f;
             if ((member.stats?.trauma?.Value ?? 0) > 50f) modifier -= 0.2f;
             if (member.traits != null && member.traits.HasWeakness(Traits.Weakness.Lazy)) modifier -= 0.1f; 
             if (member.BaseStats?.Dexterity?.Level >= 12) modifier += 0.2f;

             return currentWeek + Math.Max(4, (int)(duration * modifier));
        }

        private void AcquireRandomIllness(FamilyMember member)
        {
            List<string> possible = new List<string>();
            var current = _ageTracker.GetIllnesses(member);
            if (_config.enableDementia && !current.Contains(ILLNESS_MILD_DEMENTIA) && !current.Contains(ILLNESS_DEMENTIA)) possible.Add(ILLNESS_MILD_DEMENTIA);
            if (_config.enableArthritis && !current.Contains(ILLNESS_MILD_ARTHRITIS) && !current.Contains(ILLNESS_ARTHRITIS)) possible.Add(ILLNESS_MILD_ARTHRITIS);
            if (_config.enableHeartDisease && !current.Contains(ILLNESS_MILD_HEART) && !current.Contains(ILLNESS_HEART)) possible.Add(ILLNESS_MILD_HEART);
            if (_config.enableFrailty && !current.Contains(ILLNESS_MILD_FRAILTY) && !current.Contains(ILLNESS_FRAILTY)) possible.Add(ILLNESS_MILD_FRAILTY);
            if (_config.enableRespiratory && !current.Contains(ILLNESS_MILD_RESPIRATORY) && !current.Contains(ILLNESS_RESPIRATORY)) possible.Add(ILLNESS_MILD_RESPIRATORY);
            
            if (possible.Count > 0)
            {
                string picked = possible[_random.Range(0, possible.Count)];
                _ageTracker.AddIllness(member, picked);
                _ageTracker.SetOnsetWeek(member, picked, CalculateNextStageWeek(member, _ageTracker.GetAgeWeeks(member)));
                ApplyInitialEffect(member, picked);
                
                string flavor = _dialogueHelper.PickLine($"Flavor_{picked}", IllnessDialogue.GetFlavorOptions(picked));
                TriggerSpeech(member, flavor, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(picked));
                TriggerJournal($"{member.firstName} is showing signs of {GetIllnessName(picked)}.", DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(member).Contains(picked));
            }
        }

        private void TriggerObserverReactions(FamilyMember victim, string illnessId)
        {
            if (FamilyManager.Instance == null) return;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            var observers = new List<FamilyMember>();
            foreach (var m in members) if (m != null && !m.isDead && m.GetId() != victim.GetId() && !m.isChild) observers.Add(m);
            if (observers.Count == 0) return;

            int count = Math.Min(observers.Count, 2 + (_random.Value() < 0.75f ? 1 : 0));
            // Shuffle
            for (int i = 0; i < observers.Count; i++) { var t = observers[i]; int r = _random.Range(i, observers.Count); observers[i] = observers[r]; observers[r] = t; }

            for (int i = 0; i < count; i++)
            {
                var obs = observers[i];
                string line = _dialogueHelper.PickLine($"Observe_{illnessId}", IllnessDialogue.GetObservationOptions(victim.firstName, illnessId), obs);
                TriggerSpeech(obs, line, DialogueScheduler.Priority.Reactive, () => _ageTracker.GetIllnesses(victim).Contains(illnessId) && !victim.isDead);
            }
        }

        public void ReapplyModifiers(FamilyMember member)
        {
            if (member == null || member.isDead) return;
            foreach (var id in _ageTracker.GetIllnesses(member)) ApplyInitialEffect(member, id, true);
        }

        private void ApplyInitialEffect(FamilyMember member, string illnessId, bool silent = false)
        {
            if (illnessId == ILLNESS_ARTHRITIS && member.traits != null && !member.traits.HasWeakness(Traits.Weakness.Lazy))
                member.traits.AddWeakness(Traits.Weakness.Lazy, true);
        }

        private void ApplyOngoingEffects(FamilyMember member)
        {
            foreach (var id in _ageTracker.GetIllnesses(member))
            {
                if (id == ILLNESS_HEART && _random.Value() < _config.heartDiseaseAttackChance / 100f) TriggerHeartAttack(member);
                else if (id == ILLNESS_MILD_HEART && _random.Value() < 0.05f) TriggerJournal(IllnessDialogue.GetMinorHeartPalpitationMessage(member.firstName));

                // 2% chance per cycle to trigger a 3-turn conversation about the illness
                if (_random.Value() < 0.02f)
                {
                    TriggerIllnessConversation(member, id);
                }
            }
        }

        private void TriggerIllnessConversation(FamilyMember member, string illnessId)
        {
            var observer = GetObserver(member);
            if (observer == null) return;

            var data = IllnessDialogue.GetConversation(illnessId);
            string opener = _dialogueHelper.PickLine($"IllnessConv_{illnessId}_Opener", data.Openers, observer);
            TriggerSpeech(observer, opener, DialogueScheduler.Priority.Routine);

            // 70% chance for the full 3-turn cycle
            if (_random.Value() < 0.70f)
            {
                string response = _dialogueHelper.PickLine($"IllnessConv_{illnessId}_Response", data.Responses, member);
                TriggerSpeech(member, response, DialogueScheduler.Priority.Routine);

                string closer = _dialogueHelper.PickLine($"IllnessConv_{illnessId}_Closer", data.Closers, observer);
                TriggerSpeech(observer, closer, DialogueScheduler.Priority.Routine);
            }
        }

        private FamilyMember GetObserver(FamilyMember excluded)
        {
            if (FamilyManager.Instance == null) return null;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return null;
            var candidates = new List<FamilyMember>();
            foreach (var m in members) if (m != null && !m.isDead && m.GetId() != excluded.GetId() && !m.isChild) candidates.Add(m);
            if (candidates.Count == 0) return null;
            return candidates[_random.Range(0, candidates.Count)];
        }

        private void TriggerHeartAttack(FamilyMember member)
        {
            if (member.health <= (int)_config.heartAttackDamage && _deathManager != null) _deathManager.ScheduleDeath(member, "Heart Failure");
            else member.Damage((int)_config.heartAttackDamage, BaseCharacter.DamageType.Undefined, "Heart Attack");
            TriggerJournal(IllnessDialogue.GetHeartAttackMessage(member.firstName), DialogueScheduler.Priority.Reactive, () => !member.isDead);
        }

        private string GetIllnessName(string id)
        {
            switch (id)
            {
                case ILLNESS_DEMENTIA: return "Severe Dementia";
                case ILLNESS_MILD_DEMENTIA: return "Early Onset Dementia";
                case ILLNESS_ARTHRITIS: return "Crippling Arthritis";
                case ILLNESS_MILD_ARTHRITIS: return "Joint Pain";
                case ILLNESS_HEART: return "Heart Disease";
                case ILLNESS_MILD_HEART: return "Arrhythmia";
                case ILLNESS_FRAILTY: return "Frailty";
                case ILLNESS_MILD_FRAILTY: return "General Weakness";
                case ILLNESS_RESPIRATORY: return "Lung Failure";
                case ILLNESS_MILD_RESPIRATORY: return "Chronic Cough";
                default: return "Unknown Condition";
            }
        }

        public List<string> GetActiveIllnesses(FamilyMember member) => _ageTracker.GetIllnesses(member);

        public void AddIllnessExternal(FamilyMember member, string id)
        {
            _ageTracker.AddIllness(member, id);
            ApplyInitialEffect(member, id);
        }

        public void RemoveIllnessExternal(FamilyMember member, string id)
        {
            _ageTracker.RemoveIllness(member, id);
        }
    }
}
