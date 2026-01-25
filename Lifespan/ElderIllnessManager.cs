using ModAPI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public class ElderIllnessManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly System.Random _random;

        public const string ILLNESS_DEMENTIA = "lifespan.illness.dementia";
        public const string ILLNESS_ARTHRITIS = "lifespan.illness.arthritis";
        public const string ILLNESS_HEART = "lifespan.illness.heart";
        public const string ILLNESS_FRAILTY = "lifespan.illness.frailty";
        public const string ILLNESS_RESPIRATORY = "lifespan.illness.respiratory";

        private DeathManager _deathManager;

        public ElderIllnessManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = new System.Random();
        }

        public void SetDeathManager(DeathManager deathManager)
        {
            _deathManager = deathManager;
        }

        public void ProcessElderIllnessRoll(FamilyMember member, int currentAgeWeeks)
        {
            if (member == null || member.isDead) return;

            LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Processing illness roll for {member.firstName} ({currentAgeWeeks/52}y).");

            // 1. Roll for new illness
            float healthFactor = (float)member.health / member.maxHealth;
            
            // Calculate stress factor (Trauma is 0-100 usually)
            float stressFactor = 0f;
            if (member.stats != null && member.stats.trauma != null)
            {
                stressFactor = member.stats.trauma.Value / 100f; // 0.0 to 1.0
            }

            // Calculate fatigue factor (Fatigue is 0-100)
            float fatigueFactor = 0f;
            if (member.stats != null && member.stats.fatigue != null)
            {
                fatigueFactor = member.stats.fatigue.Value / 100f; // 0.0 to 1.0
            }

            // Base chance (now much lower, e.g. 0.001)
            float baseChance = _config.elderIllnessBaseChance;
            
            // Multipliers
            // Health: 1.0 (full health) -> 3.0 (near death)
            float healthMult = 1.0f + (2.0f * (1.0f - healthFactor));
            
            // Stress: 1.0 (calm) -> 2.5 (max stress)
            float stressMult = 1.0f + (1.5f * stressFactor);
            
            // Fatigue: 1.0 (rested) -> 1.5 (exhausted)
            float fatigueMult = 1.0f + (0.5f * fatigueFactor);

            float acquiredChance = baseChance * healthMult * stressMult * fatigueMult;

            float rollValue = (float)_random.NextDouble();
            LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] {member.firstName} Roll: {rollValue:F4} VS Chance: {acquiredChance:F4} (H:{healthMult:F1}x S:{stressMult:F1}x F:{fatigueMult:F1}x).");

            if (rollValue < acquiredChance)
            {
                LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Roll SUCCESS for {member.firstName}. Choosing illness...");
                AcquireRandomIllness(member);
            }

            // 2. Process existing illness effects
            ApplyOngoingEffects(member);
        }

        private void AcquireRandomIllness(FamilyMember member)
        {
            List<string> possible = new List<string>();
            if (_config.enableDementia) possible.Add(ILLNESS_DEMENTIA);
            if (_config.enableArthritis) possible.Add(ILLNESS_ARTHRITIS);
            if (_config.enableHeartDisease) possible.Add(ILLNESS_HEART);
            if (_config.enableFrailty) possible.Add(ILLNESS_FRAILTY);
            if (_config.enableRespiratory) possible.Add(ILLNESS_RESPIRATORY);

            var current = _ageTracker.GetIllnesses(member);
            LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Current: {string.Join(", ", current.ToArray())}.");
            
            possible.RemoveAll(i => current.Contains(i));

            if (possible.Count > 0)
            {
                string picked = possible[_random.Next(possible.Count)];
                _log.Info($"[Lifespan] {member.firstName} has developed {GetIllnessName(picked)}.");
                _ageTracker.AddIllness(member, picked);
                ApplyInitialEffect(member, picked);
                
                if (JournalManager.Instance != null)
                {
                    LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Inserting journal entry for {GetIllnessName(picked)}.");
                    ReflectionHelper.InvokeMethod(JournalManager.Instance, "InsertJournalEntry", $"{member.firstName} has developed {GetIllnessName(picked)}.", "", false);
                }
            }
            else
            {
                LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] {member.firstName} already has all possible illnesses.");
            }
        }

        public void ReapplyModifiers(FamilyMember member)
        {
            if (member == null || member.isDead) return;
            var illnesses = _ageTracker.GetIllnesses(member);
            if (illnesses.Count > 0)
            {
                LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Re-applying {illnesses.Count} modifiers for {member.firstName}.");
                foreach (var id in illnesses)
                {
                    ApplyInitialEffect(member, id, true);
                }
            }
        }

        private void ApplyInitialEffect(FamilyMember member, string illnessId, bool silent = false)
        {
            LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] Applying effect: {GetIllnessName(illnessId)} -> {member.firstName}.");
            switch (illnessId)
            {
                case ILLNESS_ARTHRITIS:
                    if (member.traits != null && !member.traits.HasWeakness(Traits.Weakness.Lazy))
                    {
                        LifespanLoggerExtensions.Debug(_log, "[ElderIllness] Arthritis -> Adding 'Lazy' trait.");
                        member.traits.AddWeakness(Traits.Weakness.Lazy, true);
                    }
                    break;
                case ILLNESS_FRAILTY:
                    LifespanLoggerExtensions.Debug(_log, "[ElderIllness] Frailty effect active.");
                    break;
                case ILLNESS_DEMENTIA:
                    LifespanLoggerExtensions.Debug(_log, "[ElderIllness] Dementia effect active.");
                    break;
            }
        }

        private void ApplyOngoingEffects(FamilyMember member)
        {
            var illnesses = _ageTracker.GetIllnesses(member);
            foreach (var id in illnesses)
            {
                if (id == ILLNESS_HEART)
                {
                    float roll = (float)_random.NextDouble();
                    if (roll < _config.heartDiseaseAttackChance)
                    {
                        LifespanLoggerExtensions.Debug(_log, $"[ElderIllness] HEART ATTACK ROLL SUCCESS ({roll:F3} < {_config.heartDiseaseAttackChance:F3}).");
                        TriggerHeartAttack(member);
                    }
                }
            }
        }

        private void TriggerHeartAttack(FamilyMember member)
        {
            _log.Info($"[Lifespan] HEART ATTACK! {member.firstName} is having a coronary event.");
            
            bool willKill = member.health <= (int)_config.heartAttackDamage;

            if (willKill && _deathManager != null)
            {
                _log.Info($"[Lifespan] {member.firstName}'s heart has stopped.");
                _deathManager.ScheduleDeath(member, "Heart Failure");
            }
            else
            {
                _log.Warn($"{member.firstName} survived a heart attack.");
                member.Damage((int)_config.heartAttackDamage, BaseCharacter.DamageType.Undefined, "Heart Attack");
            }
            
            if (JournalManager.Instance != null)
            {
                ReflectionHelper.InvokeMethod(JournalManager.Instance, "InsertJournalEntry", $"{member.firstName} suffered a heart attack!", "", false);
            }
        }

        private string GetIllnessName(string id)
        {
            switch (id)
            {
                case ILLNESS_DEMENTIA: return "Dementia";
                case ILLNESS_ARTHRITIS: return "Arthritis";
                case ILLNESS_HEART: return "Heart Disease";
                case ILLNESS_FRAILTY: return "Frailty";
                case ILLNESS_RESPIRATORY: return "Respiratory Issues";
                default: return "Unknown Condition";
            }
        }

        public List<string> GetActiveIllnesses(FamilyMember member) => _ageTracker.GetIllnesses(member);

        public void AddIllnessExternal(FamilyMember member, string id)
        {
            LifespanLoggerExtensions.Debug(_log, $"[DEBUG] ElderIllnessManager: AddIllnessExternal called for {id} on {member.firstName}.");
            _ageTracker.AddIllness(member, id);
            ApplyInitialEffect(member, id);
        }

        public void RemoveIllnessExternal(FamilyMember member, string id)
        {
            LifespanLoggerExtensions.Debug(_log, $"[DEBUG] ElderIllnessManager: RemoveIllnessExternal called for {id} on {member.firstName}.");
            _ageTracker.RemoveIllness(member, id);
        }
    }
}
