using ModAPI.Core;
using ModAPI.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Manages the Development Gene system for stat growth across life stages.
    /// Handles rolling for stat gains, spark effects, catch-up mechanics, and forfeit logic.
    /// </summary>
    public class DevelopmentGeneManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly System.Random _random;

        // ========================================
        // CONFIGURABLE WEIGHTS - Ready for Settings UI hookup
        // TODO: Hook these up to ModSettings when UI is implemented
        // ========================================

        /// <summary>Base chance for stat gain per age-up (1/400 = 0.0025 = 0.25%)</summary>
        public float BaseStatGainChance = 0.0025f; // 1/400

        /// <summary>Maximum stress influence on the roll (±30%)</summary>
        public float MaxStressInfluence = 0.30f;

        /// <summary>Chance modifier per remaining potential point when catching up</summary>
        public float CatchUpBonusPerPoint = 0.05f; // Increased to 5% per point to be impactful

        /// <summary>Years before milestone where catch-up kicks in</summary>
        public int CatchUpWindowYears = 3;

        /// <summary>Base chance to forfeit remaining potential when very close to milestone</summary>
        public float ForfeitBaseChance = 0.10f; // 10%

        /// <summary>
        /// Updates internal tuning values from the provided configuration.
        /// </summary>
        public void RefreshSettings(LifespanConfig config)
        {
            if (config.baseStatGainChance.HasValue) 
                BaseStatGainChance = config.baseStatGainChance.Value;
            if (config.maxStressInfluence.HasValue) 
                MaxStressInfluence = config.maxStressInfluence.Value;
        }

        /// <summary>Years before milestone where forfeit risk begins</summary>
        public int ForfeitWindowYears = 1;

        /// <summary>Multiplier applied when "Spark" effect triggers (double stat gain)</summary>
        public int SparkMultiplier = 2;

        /// <summary>Multiplier for stat gain chance during childhood (PreAdult)</summary>
        public float ChildGrowthMultiplier = 4.0f;

        /// <summary>Chance for Spark effect when stress is low (applied on successful stat gain)</summary>
        public float SparkBaseChance = 0.05f; // 5% base

        /// <summary>Pre-Elder stage starts this many years before elder age</summary>
        public int PreElderWindowYears = 5;

        // Speech bubble messages for Spark effect
        private readonly Dictionary<BaseStats.StatType, string[]> _sparkMessages = new Dictionary<BaseStats.StatType, string[]>
        {
            { BaseStats.StatType.Strength, new[] { "I feel so much stronger today!", "All that hard work is paying off!" } },
            { BaseStats.StatType.Dexterity, new[] { "My reflexes feel sharper than ever!", "I'm getting faster every day!" } },
            { BaseStats.StatType.Intelligence, new[] { "Something just clicked in my head!", "I understand things so much better now!" } },
            { BaseStats.StatType.Charisma, new[] { "I feel like I can talk to anyone!", "People seem to listen to me more!" } },
            { BaseStats.StatType.Perception, new[] { "I notice things I never saw before!", "The world looks clearer somehow!" } }
        };

        public DevelopmentGeneManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _random = new System.Random();
        }

        /// <summary>
        /// Gets or generates the development gene for a character.
        /// </summary>
        public DevelopmentGene GetOrGenerateGene(FamilyMember member)
        {
            return _ageTracker.GetOrGenerateDevelopmentGene(member);
        }

        /// <summary>
        /// Processes development stat gains for a character on age-up.
        /// Called every time a character ages (weekly cycle).
        /// </summary>
        public void ProcessDevelopment(FamilyMember member, int currentAgeWeeks, int weeksAgedThisInterval)
        {
            if (member == null || member.isDead) return;

            var gene = GetOrGenerateGene(member);
            if (gene == null) return;

            int currentAgeYears = currentAgeWeeks / 52;
            LifeStage stage = DetermineLifeStage(currentAgeYears);

            LifespanLoggerExtensions.Debug(_log, $"[DevGene] Processing {member.firstName} (Age {currentAgeYears}y, Stage: {stage}, Interval: {weeksAgedThisInterval}w)");

            // Check remaining potential
            int remainingPotential = gene.GetRemainingPotential(stage);
            if (remainingPotential <= 0)
            {
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] {member.firstName} has no potential left for {stage}.");
                return;
            }

            // Calculate years until next milestone
            int yearsToMilestone = GetYearsToMilestone(currentAgeYears, stage);
            LifespanLoggerExtensions.Debug(_log, $"[DevGene] {remainingPotential} potential remaining, {yearsToMilestone} years to milestone.");

            // ========================================
            // FORFEIT CHECK - Very close to milestone with unlucky history
            // Scale forfeit chance by time passed as well
            // ========================================
            if (yearsToMilestone <= ForfeitWindowYears && remainingPotential > 0)
            {
                // Scale chance: if we aged 52 weeks, we check "52 times" effectively
                // Probability of at least one success in n trials = 1 - (1 - p)^n
                float baseForfeitP = ForfeitBaseChance * remainingPotential;
                float scaledForfeitChance = 1f - Mathf.Pow(1f - baseForfeitP, weeksAgedThisInterval);

                float forfeitRoll = (float)_random.NextDouble();
                
                if (forfeitRoll < scaledForfeitChance)
                {
                    _log.Info($"[Lifespan] {member.firstName} failed to reach their full potential for the {stage} stage.");
                    LifespanLoggerExtensions.Debug(_log, $"[DevGene] FORFEIT SUCCESS (Roll: {forfeitRoll:F3} < {scaledForfeitChance:F3})");
                    gene.ForfeitPotential(stage);
                    return;
                }
            }

            // ========================================
            // STAT GAIN ROLL
            // ========================================
            float finalChance = CalculateStatGainChance(member, gene, stage, currentAgeYears, yearsToMilestone, remainingPotential, weeksAgedThisInterval);
            float gainRoll = (float)_random.NextDouble();

            LifespanLoggerExtensions.Debug(_log, $"[DevGene] {member.firstName} Stat Gain Roll: {gainRoll:F4} vs Chance: {finalChance:F4}");

            if (gainRoll < finalChance)
            {
                // SUCCESS - Award stat gain
                AwardStatGain(member, gene, stage);
            }
        }

        /// <summary>
        /// Calculates the final chance for a stat gain, including all modifiers and time scaling.
        /// </summary>
        private float CalculateStatGainChance(FamilyMember member, DevelopmentGene gene, LifeStage stage, 
            int currentAgeYears, int yearsToMilestone, int remainingPotential, int weeksAged)
        {
            // 1. Calculate Base Probability for a single week
            float chance = BaseStatGainChance;

            // Apply Child Growth Bonus
            if (stage == LifeStage.PreAdult)
            {
                chance *= ChildGrowthMultiplier;
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] Applied Child Bonus ({ChildGrowthMultiplier}x). Chance now: {chance:F4}");
            }

            // ========================================
            // STRESS/TRAUMA MODIFIER (±30%)
            // Lower stress = higher chance, higher stress = lower chance
            // ========================================
            float stressModifier = 0f;
            if (member.stats != null && member.stats.trauma != null)
            {
                // Trauma is 0-100, normalize to -0.3 to +0.3
                // Low trauma (0) = +30% bonus, High trauma (100) = -30% penalty
                float traumaNormalized = member.stats.trauma.NormalizedValue; // 0.0 to 1.0
                stressModifier = MaxStressInfluence * (1f - (2f * traumaNormalized)); // Maps 0->+0.3, 0.5->0, 1->-0.3
            }
            float stressMultiplier = 1f + stressModifier;
            chance *= stressMultiplier;

            LifespanLoggerExtensions.Debug(_log, $"[DevGene] Base chance {BaseStatGainChance:F4}, Stress modifier: {stressModifier:F2}");

            // ========================================
            // CATCH-UP BONUS - Higher chance if behind on gains
            // ========================================
            if (yearsToMilestone <= CatchUpWindowYears && remainingPotential > 0)
            {
                float catchUpBonus = CatchUpBonusPerPoint * remainingPotential * (CatchUpWindowYears - yearsToMilestone + 1);
                chance += catchUpBonus;
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] Catch-up bonus: +{catchUpBonus:F4}");
            }

            // 2. Scale Probability by Time Passed
            // P(at least one success) = 1 - (1 - p)^n
            // where p is single-week chance and n is weeks passed
            float p = Mathf.Clamp01(chance);
            float scaledChance = 1f - Mathf.Pow(1f - p, weeksAged);

            LifespanLoggerExtensions.Debug(_log, $"[DevGene] Scaled Chance ({weeksAged} weeks): {p:F4} -> {scaledChance:F4}");

            return scaledChance;
        }

        /// <summary>
        /// Awards a stat gain to the character.
        /// </summary>
        private void AwardStatGain(FamilyMember member, DevelopmentGene gene, LifeStage stage)
        {
            // Pick a random stat based on gene weights
            BaseStats.StatType statType = PickWeightedStat(gene);
            
            int gainAmount = 1;
            bool sparkTriggered = false;

            // ========================================
            // SPARK CHECK - Low stress can trigger double gain
            // ========================================
            float traumaNormalized = 0.5f;
            if (member.stats != null && member.stats.trauma != null)
            {
                traumaNormalized = member.stats.trauma.NormalizedValue;
            }

            // Spark chance is higher when stress is low
            // At 0% stress: SparkBaseChance + 30%, At 50% stress: SparkBaseChance, At 100%: SparkBaseChance - 30%
            float sparkChance = SparkBaseChance + (MaxStressInfluence * (1f - (2f * traumaNormalized)));
            sparkChance = Mathf.Clamp01(sparkChance);

            float sparkRoll = (float)_random.NextDouble();
            if (sparkRoll < sparkChance)
            {
                gainAmount *= SparkMultiplier;
                sparkTriggered = true;
                _log.Info($"[Lifespan] SPARK! {member.firstName} had a developmental breakthrough (+{gainAmount} {statType})!");
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] SPARK SUCCESS (Roll: {sparkRoll:F3} < {sparkChance:F3})");
            }

            // Apply the stat gain
            ApplyStatGain(member, statType, gainAmount);
            
            // Record the gain
            // Logic Change: If adult gets a Spark, the EXTRA point is "free" and doesn't consume potential.
            // For children/elders, or normal gains, it consumes potential as usual.
            int potentialConsumed = 1;
            if (sparkTriggered && stage == LifeStage.PostAdult)
            {
                // Adult spark: Gain is 2, but we only record 1 against potential
                potentialConsumed = 1;
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] Adult Spark Bonus: {member.firstName} gained +{gainAmount} but only used {potentialConsumed} potential.");
            }
            else
            {
                // Normal behavior: Consume potential equal to gain (or 1 if we treat 'gainAmount' as the multiplier)
                // Wait, logic above sets gainAmount = 1 or 2.
                // If sparkTriggered is true, gainAmount is 2.
                // If NOT an adult spark, we should consume all gained points?
                // The prompt says: "The spark double should not effect the pool potential of the adult. It should still for elders."
                // This implies for elders/kids, a spark consumes 2 potential points.
                // For adults, it consumes 1.
                potentialConsumed = gainAmount;
            }

            gene.RecordGain(stage, potentialConsumed);

            LifespanLoggerExtensions.Debug(_log, $"[DevGene] {member.firstName} gained +{gainAmount} {statType} (Stage: {stage}, Remaining: {gene.GetRemainingPotential(stage)})");

            // Show speech bubble if Spark triggered
            if (sparkTriggered)
            {
                ShowSparkMessage(member, statType);
            }

            // Notify player via Activity Log
            if (ActivityLog.Instance != null)
            {
                string message = sparkTriggered 
                    ? $"{member.firstName} had a developmental breakthrough! (+{gainAmount} {statType})"
                    : $"{member.firstName} is developing well. (+{gainAmount} {statType})";
                // ActivityLog.Instance.Add(ActivityLog.Activity.Custom, new ActivityLog.ExtraInfoString(message, false));
            }
        }

        /// <summary>
        /// Applies a stat increase to the character.
        /// </summary>
        private void ApplyStatGain(FamilyMember member, BaseStats.StatType statType, int amount)
        {
            if (member.BaseStats == null) return;

            BaseStat stat = GetStatByType(member.BaseStats, statType);
            if (stat == null) return;

            // Increase experience by enough to gain 'amount' levels
            // Each level requires roughly 100-1000 XP depending on current level
            // For simplicity, grant a fixed XP boost that should level up
            int xpPerLevel = 500; // Approximate mid-game XP per level
            int totalXp = xpPerLevel * amount;

            stat.IncreaseExp(totalXp);
            LifespanLoggerExtensions.Debug(_log, $"[DevGene] Granted {totalXp} XP to {statType} for {member.firstName}. New Level: {stat.Level}");
        }

        /// <summary>
        /// Gets a BaseStat from BaseStats by type.
        /// </summary>
        private BaseStat GetStatByType(BaseStats stats, BaseStats.StatType type)
        {
            switch (type)
            {
                case BaseStats.StatType.Strength: return stats.Strength;
                case BaseStats.StatType.Dexterity: return stats.Dexterity;
                case BaseStats.StatType.Intelligence: return stats.Intelligence;
                case BaseStats.StatType.Charisma: return stats.Charisma;
                case BaseStats.StatType.Perception: return stats.Perception;
                default: return null;
            }
        }

        /// <summary>
        /// Picks a random stat weighted by the gene's preferences.
        /// Implements "Skill Fatigue" to reduce consecutive same-stat gains.
        /// </summary>
        private BaseStats.StatType PickWeightedStat(DevelopmentGene gene)
        {
            float strW = gene.StrengthWeight;
            float dexW = gene.DexterityWeight;
            float intW = gene.IntelligenceWeight;
            float chaW = gene.CharismaWeight;
            float perW = gene.PerceptionWeight;

            // Apply fatigue if we have a history
            if (gene.HasLastGainedStat)
            {
                float fatigueFactor = 0.3f; // Reduce chance to 30% of normal
                switch (gene.LastGainedStat)
                {
                    case BaseStats.StatType.Strength: strW *= fatigueFactor; break;
                    case BaseStats.StatType.Dexterity: dexW *= fatigueFactor; break;
                    case BaseStats.StatType.Intelligence: intW *= fatigueFactor; break;
                    case BaseStats.StatType.Charisma: chaW *= fatigueFactor; break;
                    case BaseStats.StatType.Perception: perW *= fatigueFactor; break;
                }
                LifespanLoggerExtensions.Debug(_log, $"[DevGene] Skill fatigue active for {gene.LastGainedStat}. Weights adjusted.");
            }

            float totalWeight = strW + dexW + intW + chaW + perW;
            
            // Safety check: If all weights are zero (e.g. extreme fatigue or bad gene), fallback to Strength
            if (totalWeight <= 0f)
            {
                return BaseStats.StatType.Strength;
            }

            float roll = (float)_random.NextDouble() * totalWeight;

            float cumulative = 0f;
            BaseStats.StatType selected;
            
            cumulative += strW;
            if (roll < cumulative) selected = BaseStats.StatType.Strength;
            else
            {
                cumulative += dexW;
                if (roll < cumulative) selected = BaseStats.StatType.Dexterity;
                else
                {
                    cumulative += intW;
                    if (roll < cumulative) selected = BaseStats.StatType.Intelligence;
                    else
                    {
                        cumulative += chaW;
                        if (roll < cumulative) selected = BaseStats.StatType.Charisma;
                        else selected = BaseStats.StatType.Perception;
                    }
                }
            }

            // Update fatigue tracking
            if (gene.HasLastGainedStat && gene.LastGainedStat == selected)
            {
                // Picked the same stat again - fatigue persists (and re-applies next time)
                // No change needed to state, just remains "fatigued" on this stat
            }
            else
            {
                // Picked a different stat (or first time)
                // Reset fatigue for next time? No, the user wants "wipes all weights... and continues after a skill is gained again"
                // Actually: "unless another skill other than that one triggers which wipes all weights... and continues after a skill is gained again"
                // This implies: 
                // 1. Gain STR -> Fatigue STR
                // 2. Gain DEX -> Wipe fatigue (STR weight normal), Fatigue DEX
                
                // So we essentially ALWAYS set the new stat as the LastGainedStat
                gene.LastGainedStat = selected;
                gene.HasLastGainedStat = true;
            }

            return selected;
        }

        /// <summary>
        /// Shows a speech bubble when Spark effect triggers.
        /// </summary>
        private void ShowSparkMessage(FamilyMember member, BaseStats.StatType statType)
        {
            if (_sparkMessages.TryGetValue(statType, out string[] messages) && messages.Length > 0)
            {
                string message = messages[_random.Next(messages.Length)];
                try
                {
                    member.Say(message);
                }
                catch (Exception ex)
                {
                    LifespanLoggerExtensions.Debug(_log, $"[DevGene] Failed to show spark bubble: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Determines the current life stage based on age.
        /// </summary>
        private LifeStage DetermineLifeStage(int ageYears)
        {
            int adultAge = _config.adultAgeYears;
            int elderAge = _config.elderAgeYears;
            int preElderStart = elderAge - PreElderWindowYears;

            if (ageYears < adultAge)
                return LifeStage.PreAdult;
            else if (ageYears < preElderStart)
                return LifeStage.PostAdult;
            else if (ageYears < elderAge)
                return LifeStage.PreElder;
            else
                return LifeStage.PostElder;
        }

        /// <summary>
        /// Gets years remaining until the next milestone.
        /// </summary>
        private int GetYearsToMilestone(int currentAgeYears, LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.PreAdult:
                    return Math.Max(0, _config.adultAgeYears - currentAgeYears);
                case LifeStage.PostAdult:
                    return Math.Max(0, (_config.elderAgeYears - PreElderWindowYears) - currentAgeYears);
                case LifeStage.PreElder:
                    return Math.Max(0, _config.elderAgeYears - currentAgeYears);
                case LifeStage.PostElder:
                    return Math.Max(0, _config.maxAgeYears - currentAgeYears);
                default:
                    return 10; // Default fallback
            }
        }

        // ========================================
        // SETTINGS HOOKS - Ready for UI integration
        // TODO: Implement settings UI and call these methods
        // ========================================

        /// <summary>
        /// Sets the base stat gain chance. Default: 0.0025 (1/400).
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetBaseStatGainChance(float chance)
        {
            BaseStatGainChance = Mathf.Clamp(chance, 0.0001f, 0.1f);
            _log.Info($"[Settings] BaseStatGainChance set to {BaseStatGainChance}");
        }

        /// <summary>
        /// Sets the maximum stress influence. Default: 0.30 (30%).
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetMaxStressInfluence(float influence)
        {
            MaxStressInfluence = Mathf.Clamp(influence, 0f, 0.5f);
            _log.Info($"[Settings] MaxStressInfluence set to {MaxStressInfluence}");
        }

        /// <summary>
        /// Sets the catch-up bonus per remaining potential point. Default: 0.001.
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetCatchUpBonus(float bonus)
        {
            CatchUpBonusPerPoint = Mathf.Clamp(bonus, 0f, 0.01f);
            _log.Info($"[Settings] CatchUpBonusPerPoint set to {CatchUpBonusPerPoint}");
        }

        /// <summary>
        /// Sets the forfeit base chance. Default: 0.10 (10%).
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetForfeitChance(float chance)
        {
            ForfeitBaseChance = Mathf.Clamp(chance, 0f, 0.5f);
            _log.Info($"[Settings] ForfeitBaseChance set to {ForfeitBaseChance}");
        }

        /// <summary>
        /// Sets the spark multiplier. Default: 2.
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetSparkMultiplier(int multiplier)
        {
            SparkMultiplier = Mathf.Clamp(multiplier, 1, 5);
            _log.Info($"[Settings] SparkMultiplier set to {SparkMultiplier}");
        }

        /// <summary>
        /// Sets the spark base chance. Default: 0.05 (5%).
        /// Call from settings UI when implemented.
        /// </summary>
        public void SetSparkBaseChance(float chance)
        {
            SparkBaseChance = Mathf.Clamp(chance, 0f, 0.25f);
            _log.Info($"[Settings] SparkBaseChance set to {SparkBaseChance}");
        }
    }
}
