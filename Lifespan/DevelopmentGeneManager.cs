using ModAPI.Core;
using ModAPI.Reflection;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Applies saved development potential to stat gains across life stages.
    /// </summary>
    public class DevelopmentGeneManager
    {
        private LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly IGeneticState _geneticState;
        private readonly ModRandomStream _random;
        private MilestoneManager _milestoneManager;
        private DialogueScheduler _scheduler;

        // ====================================================================
        // CONSTANTS & LOCAL DATA
        // ====================================================================

        /// <summary>Pre-Elder stage starts this many years before elder age.</summary>
        public const int PRE_ELDER_WINDOW_YEARS = 5;

        /// <summary>Speech bubble messages triggered when a 'Spark' (critical success) occurs.</summary>
        private readonly Dictionary<BaseStats.StatType, string[]> _sparkMessages = new Dictionary<BaseStats.StatType, string[]>
        {
            { BaseStats.StatType.Strength, new[] { 
                "I feel so much stronger today!", 
                "All that hard work is paying off!",
                "I can feel the power in my muscles!",
                "My body feels like it can do anything!",
                "I'm getting stronger every day!"
            } },
            { BaseStats.StatType.Dexterity, new[] { 
                "My reflexes feel sharper than ever!", 
                "I'm getting faster every day!",
                "My hands have never been this steady!",
                "I feel like I'm moving in slow motion!",
                "I can feel the precision in every move!"
            } },
            { BaseStats.StatType.Intelligence, new[] { 
                "Something just clicked in my head!", 
                "I understand things so much better now!",
                "The pieces are all falling into place!",
                "I feel smarter than I've ever been!",
                "Everything makes sense now!"
            } },
            { BaseStats.StatType.Charisma, new[] { 
                "I feel like I can talk to anyone!", 
                "People seem to listen to me more!",
                "I've never felt this confident before!",
                "Words just flow naturally now!",
                "I can read people like a book!"
            } },
            { BaseStats.StatType.Perception, new[] { 
                "I notice things I never saw before!", 
                "The world looks clearer somehow!",
                "My senses feel razor sharp!",
                "Nothing escapes my attention anymore!",
                "I can see details I always missed!"
            } }
        };
        private IModLogger Log => _log;

        public DevelopmentGeneManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ModRandomStream random)
            : this(ctx, config, ageTracker != null ? ageTracker.State.Genetics : null, random)
        {
        }

        internal DevelopmentGeneManager(IPluginContext ctx, LifespanConfig config, IGeneticState geneticState, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _geneticState = geneticState;
            _random = random;
        }

        /// <summary>
        /// Synchronizes the manager with a new configuration instance.
        /// </summary>
        public void RefreshSettings(LifespanConfig config)
        {
            if (config != null) _config = (LifespanConfig)config;
        }

        public void SetMilestoneManager(MilestoneManager manager)
        {
            _milestoneManager = manager;
        }

        // ====================================================================
        // CORE LOGIC
        // ====================================================================

        /// <summary>
        /// Gets or generates the development gene for a character.
        /// </summary>
        public DevelopmentGene GetOrGenerateGene(FamilyMember member)
        {
            if (member == null || _geneticState == null) return null;

            int id = member.GetId();
            return _geneticState.GetOrGenerateDevelopmentGene(id, _random);
        }

        /// <summary>
        /// Processes development stat gains for a character during their aging cycle.
        /// This method calculates probabilities, handles potential forfeit, and awards gains.
        /// </summary>
        public void ProcessDevelopment(FamilyMember member, int currentAgeWeeks, int weeksAgedThisInterval)
        {
            if (member == null || member.isDead) return;

            var gene = GetOrGenerateGene(member);
            if (gene == null) return;

            int currentAgeYears = currentAgeWeeks / LifespanConstants.WeeksPerYear;
            LifeStage stage = DetermineLifeStage(currentAgeYears);

            Log.Debug($"Processing {member.firstName} (Age {currentAgeYears}y, Stage: {stage}, Interval: {weeksAgedThisInterval}w)");

            // Check if the character has any remaining development potential for this stage.
            int remainingPotential = gene.GetRemainingPotential(stage);
            if (remainingPotential <= 0)
            {
                Log.Debug($"{member.firstName} has no potential left for {stage}.");
                return;
            }

            // Calculate progress towards the next milestone.
            int yearsToMilestone = GetYearsToMilestone(currentAgeYears, stage);
            Log.Debug($"{remainingPotential} potential remaining, {yearsToMilestone} years to milestone.");

            // Unused potential can expire near a milestone.
            if (yearsToMilestone <= _config.forfeitWindowYears && remainingPotential > 0)
            {
                // Scale forfeit chance by biological time passed using the formula: 1 - (1 - p)^n
                float baseForfeitP = _config.forfeitBaseChance * remainingPotential;
                float scaledForfeitChance = 1f - Mathf.Pow(1f - baseForfeitP, weeksAgedThisInterval);

                if (_random.Value() < scaledForfeitChance)
                {
                    Log.Info($"{member.firstName} failed to reach their full potential for the {stage} stage.");
                    gene.ForfeitPotential(stage);
                    return;
                }
            }

            float finalChance = CalculateStatGainChance(member, gene, stage, currentAgeYears, yearsToMilestone, remainingPotential, weeksAgedThisInterval);
            
            if (_random.Value() < finalChance)
            {
                // Character successfully improved a skill.
                AwardStatGain(member, gene, stage);
            }
        }

        /// <summary>
        /// Calculates the final probability for a stat gain, incorporating stress, life stage, and catch-up modifiers.
        /// </summary>
        internal float CalculateStatGainChance(FamilyMember member, DevelopmentGene gene, LifeStage stage, 
            int currentAgeYears, int yearsToMilestone, int remainingPotential, int weeksAged)
        {
            // Start with configured baseline.
            // The settings UI stores this value as a percentage.
            float chance = _config.baseStatGainChance / 100f;

            // Apply childhood growth multiplier if applicable.
            if (stage == LifeStage.PreAdult)
            {
                chance *= _config.childGrowthMultiplier;
            }

            // Apply Stress/Trauma influence.
            // Formula maps trauma (0..100) to a multiplier based on 'maxStressInfluence'.
            float stressModifier = 0f;
            if (member.stats != null && member.stats.trauma != null)
            {
                float traumaNormalized = member.stats.trauma.NormalizedValue; // 0.0 to 1.0
                stressModifier = _config.maxStressInfluence * (1f - (2f * traumaNormalized)); 
            }
            chance *= (1f + stressModifier);

            // Apply Catch-Up Bonus: Increases probability if behind on potential near milestones.
            if (yearsToMilestone <= _config.catchUpWindowYears && remainingPotential > 0)
            {
                float catchUpBonus = _config.catchUpBonusPerPoint * remainingPotential * (_config.catchUpWindowYears - yearsToMilestone + 1);
                chance += catchUpBonus;
            }

            // Scale biological chance by the number of weeks passed in this aging tick.
            float p = Mathf.Clamp01(chance);
            return 1f - Mathf.Pow(1f - p, weeksAged);
        }

        /// <summary>
        /// Selects a stat, applies XP, and handles the 'Spark' breakthrough effect.
        /// </summary>
        private void AwardStatGain(FamilyMember member, DevelopmentGene gene, LifeStage stage)
        {
            // Pick a random stat weighted by the character's genetic profile.
            BaseStats.StatType statType = PickWeightedStat(gene);
            
            int gainAmount = 1;
            bool sparkTriggered = false;

            // Calculate Spark Chance (Bonus breakthroughs occur more often at low stress).
            float traumaNormalized = (member.stats?.trauma != null) ? member.stats.trauma.NormalizedValue : 0.5f;
            float sparkChance = _config.sparkBaseChance + (_config.maxStressInfluence * (1f - (2f * traumaNormalized)));
            
            if (_random.Value() < Mathf.Clamp01(sparkChance))
            {
                gainAmount *= _config.sparkMultiplier;
                sparkTriggered = true;
                Log.Info($"SPARK! {member.firstName} had a developmental breakthrough (+{gainAmount} {statType})!");
            }

            // Grant the experience increase.
            ApplyStatGain(member, statType, gainAmount);
            
            // Manage potential consumption.
            // Adult sparks provide a 'free' extra point that does not count against their biological potential.
            int potentialConsumed = (sparkTriggered && stage == LifeStage.PostAdult) ? 1 : gainAmount;
            gene.RecordGain(stage, potentialConsumed);

            if (sparkTriggered) ShowSparkMessage(member, statType);
            
            // Milestone check
            _milestoneManager?.ReportSkillGain(member, statType);
        }

        /// <summary>
        /// Directly increases the character's stat experience by exactly the amount needed for N levels.
        /// </summary>
        private void ApplyStatGain(FamilyMember member, BaseStats.StatType statType, int amount)
        {
            if (member.BaseStats == null || amount <= 0) return;

            BaseStat stat = GetStatByType(member.BaseStats, statType);
            if (stat == null) return;

            // Find the experience needed for exactly 'amount' more levels.
            // Use reflection to get the private ExpLevel array from BaseStat if it's static, 
            // but it's public/internal enough in the decompiled code (actually it's private static).
            // We can calculate it or use reflection to reach 'ExpLevel'.
            
            int currentLevel = stat.Level;
            int targetLevel = Math.Min(stat.LevelCap, currentLevel + amount);
            
            // Replicating the ExpLevel array logic from BaseStat.cs
            int[] expTable = { 0, 100, 200, 400, 600, 900, 1200, 1600, 2000, 2500, 3000, 3600, 4200, 4900, 5600, 6400, 7200, 8100, 9000, 10000, 11000 };
            
            if (targetLevel >= expTable.Length) targetLevel = expTable.Length - 1;
            
            int targetExp = expTable[targetLevel];
            int xpToAdd = targetExp - stat.Exp;

            if (xpToAdd > 0)
            {
                stat.IncreaseExp(xpToAdd);
                Log.Debug($"Granted {xpToAdd} XP to {statType} for {member.firstName} to reach level {targetLevel}.");
            }
        }

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
        /// Selects a stat based on genetic weights and handles 'Skill Fatigue' to prevent repetitive rolls.
        /// </summary>
        private BaseStats.StatType PickWeightedStat(DevelopmentGene gene)
        {
            float strW = gene.StrengthWeight;
            float dexW = gene.DexterityWeight;
            float intW = gene.IntelligenceWeight;
            float chaW = gene.CharismaWeight;
            float perW = gene.PerceptionWeight;

            // Preserve the intended 25% strength bias before weighted selection.
            strW *= 1.25f;

            // Apply skill fatigue: significantly reduces the chance of gaining the same stat twice in a row.
            if (gene.HasLastGainedStat)
            {
                float fatigueFactor = 0.3f; // 70% reduction for the last seen stat
                switch (gene.LastGainedStat)
                {
                    case BaseStats.StatType.Strength: strW *= fatigueFactor; break;
                    case BaseStats.StatType.Dexterity: dexW *= fatigueFactor; break;
                    case BaseStats.StatType.Intelligence: intW *= fatigueFactor; break;
                    case BaseStats.StatType.Charisma: chaW *= fatigueFactor; break;
                    case BaseStats.StatType.Perception: perW *= fatigueFactor; break;
                }
            }

            float totalWeight = strW + dexW + intW + chaW + perW;
            if (totalWeight <= 0f) return BaseStats.StatType.Strength;

            float roll = _random.Value() * totalWeight;
            float cumulative = 0f;
            BaseStats.StatType selected;
            
            if (roll < (cumulative += strW)) selected = BaseStats.StatType.Strength;
            else if (roll < (cumulative += dexW)) selected = BaseStats.StatType.Dexterity;
            else if (roll < (cumulative += intW)) selected = BaseStats.StatType.Intelligence;
            else if (roll < (cumulative += chaW)) selected = BaseStats.StatType.Charisma;
            else selected = BaseStats.StatType.Perception;

            // Update fatigue state.
            gene.LastGainedStat = selected;
            gene.HasLastGainedStat = true;

            return selected;
        }

        public void SetScheduler(DialogueScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        private void ShowSparkMessage(FamilyMember member, BaseStats.StatType statType)
        {
            if (_sparkMessages.TryGetValue(statType, out string[] messages))
            {
                string message = messages[_random.Range(0, messages.Length)];
                if (_scheduler != null) _scheduler.Enqueue(member, message, false, DialogueScheduler.Priority.Reactive);
                else
                {
                    try
                    {
                        member.Say(message);
                    }
                    catch (System.Exception ex)
                    {
                        if (Log.IsDebugEnabled) Log.Debug($"Failed to show development speech line: {ex.Message}");
                    }
                }
            }
        }

        private LifeStage DetermineLifeStage(int ageYears)
        {
            if (ageYears < _config.adultAgeYears) return LifeStage.PreAdult;
            if (ageYears < (_config.elderAgeYears - PRE_ELDER_WINDOW_YEARS)) return LifeStage.PostAdult;
            if (ageYears < _config.elderAgeYears) return LifeStage.PreElder;
            return LifeStage.PostElder;
        }

        private int GetYearsToMilestone(int currentAgeYears, LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.PreAdult: return Math.Max(0, _config.adultAgeYears - currentAgeYears);
                case LifeStage.PostAdult: return Math.Max(0, (_config.elderAgeYears - PRE_ELDER_WINDOW_YEARS) - currentAgeYears);
                case LifeStage.PreElder: return Math.Max(0, _config.elderAgeYears - currentAgeYears);
                case LifeStage.PostElder: return 10; // No hard milestone for elders anymore
                default: return 10;
            }
        }
    }
}
