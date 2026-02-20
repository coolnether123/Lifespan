using System;
using ModAPI.Spine;
using ModAPI.Attributes;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Configuration container for the Lifespan mod.
    /// Supports the ModAPI Spine settings framework for in-game UI generation.
    /// </summary>
    [ModConfiguration]
    public class LifespanConfig
    {
        // ====================================================================
        // LIFE STAGES
        // ====================================================================

        [ModSetting("Adulthood Threshold (Years)", Tooltip = "The age when children become adults. Must be greater than Default Child Start Age.", Category = "Life Stages", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 10, ValidateMethod = "CheckAdultAge")]
        public int adultAgeYears = 18;

        [ModSetting("Elder Threshold (Years)", Tooltip = "The age when natural death risks begin. Must be greater than Adulthood Age.", Category = "Life Stages", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 11, ValidateMethod = "CheckElderAge")]
        public int elderAgeYears = 60;

        [ModSetting("Recruited Child Start Age (Years)", Tooltip = "The starting age for newly recruited children (e.g. from radio broadcasts).", Category = "Life Stages", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 12)]
        public int initialChildAgeYears = 10;

        [ModSetting("Recruited Adult Start Age (Years)", Tooltip = "The average starting age for newly recruited adults.", Category = "Life Stages", MinValue = 18, MaxValue = 100, StepSize = 1, SortOrder = 13)]
        public int initialAdultAgeYears = 30;

        // ====================================================================
        // CHILD DEVELOPMENT & EXPEDITIONS
        // ====================================================================

        [ModSetting("Enable Child Development", Tooltip = "Enables stages (Newborn, Toddler, Teen) with specific restrictions.", Category = "Development", SortOrder = 40)]
        public bool enableChildDevelopment = true;

        [ModSetting("Mobility Age (Years)", Tooltip = "Age (years) when children become mobile. Before this, they are immobile and need care.", Category = "Development", MinValue = 1, MaxValue = 10, StepSize = 1, SortOrder = 41)]
        public int mobileAgeYears = 1;

        [ModSetting("Child Work Age (Years)", Tooltip = "Age (years) when children can start doing jobs/tasks.", Category = "Development", MinValue = 1, MaxValue = 15, StepSize = 1, SortOrder = 42)]
        public int childJobAgeYears = 6;

        [ModSetting("Expedition Age: With Escort (Years)", Tooltip = "Min age (years) to go on expeditions with an adult.", Category = "Development", MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 43)]
        public int expeditionMinAgeAccompanied = 10;

        [ModSetting("Expedition Age: Solo (Years)", Tooltip = "Min age (years) to go on expeditions alone/lead a party.", Category = "Development", MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 44)]
        public int expeditionMinAgeSolo = 13;

        [ModSetting("Enable Childhood Fast Aging (2x)", Tooltip = "If true, children age 2x as fast until they reach the cutoff age.", Category = "Development", SortOrder = 45)]
        public bool enableAcceleratedChildhood = true;

        [ModSetting("Childhood Fast Aging Cutoff (Years)", Tooltip = "Ages below this value use 2x biological aging. Default 10 => ages 0-9 are accelerated.", Category = "Development", MinValue = 1, MaxValue = 18, StepSize = 1, SortOrder = 46)]
        public int childhoodAccelerationCutoffAge = 10;

        // ====================================================================
        // AGING SPEED
        // ====================================================================

        [ModSetting("Aging Tick Interval (In-Game Weeks)", Tooltip = "Higher values slow down biological aging check frequency.", Category = "Aging", MinValue = 1, MaxValue = 52, StepSize = 1, SortOrder = 14)]
        public int agingIntervalWeeks = 1;

        [ModSetting("Biological Aging Per Tick (Weeks)", Tooltip = "How many weeks a character ages every interval. (52 = 1 year).", Category = "Aging", MinValue = 1, MaxValue = 520, StepSize = 1, SortOrder = 15)]
        public int weeksAgedPerInterval = 52;

        // ====================================================================
        // GROWTH & DEVELOPMENT
        // ====================================================================

        [ModSetting("Base Skill Gain Chance (% / Biological Week)", Tooltip = "Baseline probability (%) of a skill increase per week.", Category = "Development", MinValue = 0.01f, MaxValue = 5.0f, SortOrder = 50)]
        public float baseStatGainChance = 0.25f;

        [ModSetting("Trauma Impact On Growth (0-1)", Tooltip = "How much trauma penalizes stat growth (Default: 30%).", Category = "Development", MinValue = 0f, MaxValue = 1f, SortOrder = 51)]
        public float maxStressInfluence = 0.3f;

        [ModSetting("Catch-Up Bonus Per Missing Point", Tooltip = "Extra gain chance added for each missing potential point near a milestone.", Category = "Development", MinValue = 0f, MaxValue = 1f, SortOrder = 52)]
        public float catchUpBonusPerPoint = 0.05f;

        [ModSetting("Catch-Up Window (Years)", Tooltip = "How many years before a milestone the catch-up bonus starts.", Category = "Development", MinValue = 0, MaxValue = 10, StepSize = 1, SortOrder = 53)]
        public int catchUpWindowYears = 3;

        [ModSetting("Potential Forfeit Chance Per Missing Point", Tooltip = "Chance that unresolved potential is forfeited near milestone deadlines.", Category = "Development", MinValue = 0f, MaxValue = 1f, SortOrder = 54)]
        public float forfeitBaseChance = 0.10f;

        [ModSetting("Potential Forfeit Window (Years)", Tooltip = "How many years before a milestone potential forfeit checks begin.", Category = "Development", MinValue = 0, MaxValue = 10, StepSize = 1, SortOrder = 55)]
        public int forfeitWindowYears = 1;

        [ModSetting("Spark Chance (0-1)", Tooltip = "Chance for a double stat gain (breakthrough).", Category = "Development", MinValue = 0f, MaxValue = 0.5f, SortOrder = 56)]
        public float sparkBaseChance = 0.05f;

        [ModSetting("Spark Gain Multiplier", Tooltip = "Multiplier when a spark occurs.", Category = "Development", MinValue = 1, MaxValue = 10, SortOrder = 57)]
        public int sparkMultiplier = 2;

        [ModSetting("Child Growth Multiplier", Tooltip = "Multiplier for stat gains during childhood.", Category = "Development", MinValue = 1f, MaxValue = 10f, SortOrder = 58)]
        public float childGrowthMultiplier = 4.0f;

        // ====================================================================
        // ELDER ILLNESS
        // ====================================================================

        [ModSetting("Enable Dementia", Tooltip = "Enables the Dementia illness, which reduces Intelligence.", Category = "Elder Illness", SortOrder = 70)]
        public bool enableDementia = true;

        [ModSetting("Enable Heart Disease", Tooltip = "Enables Heart Disease, carrying a risk of heart attacks in high stress.", Category = "Elder Illness", SortOrder = 71)]
        public bool enableHeartDisease = true;

        [ModSetting("Enable Arthritis", Tooltip = "Enables Arthritis, which slows down move speed.", Category = "Elder Illness", SortOrder = 72)]
        public bool enableArthritis = true;

        [ModSetting("Enable Frailty", Tooltip = "Enables Frailty, completely preventing expeditions and reducing Strength.", Category = "Elder Illness", SortOrder = 73)]
        public bool enableFrailty = true;

        [ModSetting("Enable Respiratory Illness", Tooltip = "Enables Respiratory issues, reducing oxygen efficiency.", Category = "Elder Illness", SortOrder = 74)]
        public bool enableRespiratory = true;

        [ModSetting("Illness Contract Chance (% / Week)", Tooltip = "Probability (%) of an elder falling ill each week.", Category = "Elder Illness", MinValue = 0f, MaxValue = 5.0f, SortOrder = 75)]
        public float elderIllnessBaseChance = 0.1f;

        [ModSetting("Illness Progression Min Delay (Years)", Tooltip = "Minimum years before an illness can progress to the next stage.", Category = "Elder Illness", MinValue = 1, MaxValue = 10, SortOrder = 76)]
        public int illnessStageMinYears = 4;

        [ModSetting("Illness Progression Max Delay (Years)", Tooltip = "Maximum years before an illness moves to the next stage.", Category = "Elder Illness", MinValue = 1, MaxValue = 20, SortOrder = 77)]
        public int illnessStageMaxYears = 8;

        [ModSetting("Dementia Stat Multiplier (Intelligence)", Tooltip = "Final intelligence multiplier while severe dementia is active (0.5 = 50% retained).", Category = "Elder Illness", MinValue = 0f, MaxValue = 1f, SortOrder = 80)]
        public float dementiaIntModifier = 0.5f;

        [ModSetting("Arthritis Stat Multiplier (Dexterity)", Tooltip = "Final dexterity multiplier while severe arthritis is active (0.6 = 60% retained).", Category = "Elder Illness", MinValue = 0f, MaxValue = 1f, SortOrder = 81)]
        public float arthritisSpeedModifier = 0.6f;

        [ModSetting("Frailty Stat Multiplier (Strength)", Tooltip = "Final strength multiplier while severe frailty is active (0.6 = 60% retained).", Category = "Elder Illness", MinValue = 0f, MaxValue = 1f, SortOrder = 82)]
        public float frailtyStrModifier = 0.6f;

        [ModSetting("Heart Attack Chance (% / Week)", Tooltip = "Weekly chance of a heart attack if suffering from Heart Disease and High Stress.", Category = "Elder Illness", MinValue = 0f, MaxValue = 25.0f, SortOrder = 78)]
        public float heartDiseaseAttackChance = 5.0f;

        [ModSetting("Heart Attack Damage (HP)", Tooltip = "Amount of HP damage dealt by a single heart attack.", Category = "Elder Illness", MinValue = 0f, MaxValue = 100f, SortOrder = 79)]
        public float heartAttackDamage = 20.0f;

        // ====================================================================
        // NATURAL DEATH
        // ====================================================================

        [ModSetting("Death Risk Multiplier", Tooltip = "Affects the average lifespan. Easy=0.5x, Medium=1.0x, Hard=2.0x.", Category = "Death", MinValue = 0.1f, MaxValue = 5.0f, SortOrder = 1)]
        [ModSettingPreset("Easy", 0.5f)]
        [ModSettingPreset("Medium", 1.0f)]
        [ModSettingPreset("Hard", 2.0f)]
        public float deathProbabilityMultiplier = 1.0f;

        [ModSetting("Base Elder Death Chance (% / Week)", Tooltip = "Base chance (0-100%) of death per week once past elder threshold.", Category = "Death", MinValue = 0f, MaxValue = 1.0f, StepSize = 0.001f, SortOrder = 2)]
        public float deathBaseProbability = 0.05f;

        [ModSetting("Death Chance Growth (% / Year Past Elder)", Tooltip = "How much the death chance increases each year past elder threshold.", Category = "Death", MinValue = 0f, MaxValue = 0.1f, StepSize = 0.001f, SortOrder = 3)]
        public float deathProbabilityIncreasePerYear = 0.008f;

        [ModSetting("Low-Health Death Risk Multiplier Strength", Tooltip = "How strongly low HP increases natural death probability.", Category = "Death", MinValue = 0f, MaxValue = 20f, SortOrder = 4)]
        public float healthImpactFactor = 10f;

        // ====================================================================
        // VISUALS & JOURNAL
        // ====================================================================

        [ModSetting("Hair Greying Transition Speed", Tooltip = "Higher values make hair color transition faster each frame.", Category = "Visuals", MinValue = 0.01f, MaxValue = 5f, SortOrder = 90)]
        public float hairGreyingLerpSpeed = 0.5f;

        [ModSetting("Hair Greying Max Transition Time (Seconds)", Tooltip = "Safety cap for a single greying transition.", Category = "Visuals", MinValue = 1f, MaxValue = 300f, SortOrder = 91)]
        public float hairGreyingMaxDuration = 30f;

        [ModSetting("Enable Hair Greying", Tooltip = "If true, hair will gradually turn grey/white as characters age.", Category = "Visuals", SortOrder = 89)]
        public bool enableHairGreying = true;

        [ModSetting("Enable Natural Death", Tooltip = "If true, characters can die of old age.", Category = "Death", SortOrder = 5)]
        public bool enableNaturalDeath = true;

        [ModSetting("Enable Journal Milestones", Tooltip = "If true, major life events and birthdays are recorded in the bunker journal.", Category = "Journal", SortOrder = 100)]
        public bool enableJournalEntries = true;

        [ModSetting("Reactive Speech Min Delay (Seconds)", Tooltip = "Minimum delay before queued reactive speech/journal lines fire.", Category = "Journal", MinValue = 0f, MaxValue = 30f, SortOrder = 101)]
        public float surgeMinDelay = 3.0f;

        [ModSetting("Reactive Speech Max Delay (Seconds)", Tooltip = "Maximum delay before queued reactive speech/journal lines fire.", Category = "Journal", MinValue = 0f, MaxValue = 60f, SortOrder = 102)]
        public float surgeMaxDelay = 8.0f;

        // ====================================================================
        // NPC AGES
        // ====================================================================

        [ModSetting("Explorer Age Mean (Years)", Tooltip = "Average age of explorers encountered outside.", Category = "NPC Ages", MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 110)]
        public int explorerMeanAge = 28;

        [ModSetting("Explorer Age Spread (StdDev)", Tooltip = "Higher values create more age variation for explorers.", Category = "NPC Ages", MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 111)]
        public int explorerStdDev = 10;

        [ModSetting("Recruit Age Mean (Years)", Tooltip = "Average age for shelter recruits.", Category = "NPC Ages", MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 112)]
        public int recruiterMeanAge = 35;

        [ModSetting("Recruit Age Spread (StdDev)", Tooltip = "Higher values create more age variation for recruits.", Category = "NPC Ages", MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 113)]
        public int recruiterStdDev = 15;

        [ModSetting("Trader Age Mean (Years)", Tooltip = "Average age for trader NPCs.", Category = "NPC Ages", MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 114)]
        public int traderMeanAge = 40;

        [ModSetting("Trader Age Spread (StdDev)", Tooltip = "Higher values create more age variation for traders.", Category = "NPC Ages", MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 115)]
        public int traderStdDev = 12;

        // ====================================================================
        // DEBUG
        // ====================================================================

        [ModSetting("Enable Verbose Logging", Tooltip = "Spams the log file with detailed step-by-step aging info. Only use if debugging.", Category = "Debug", SortOrder = 120)]
        public bool verboseLogging = false;

        [ModSetting("Enable Debug Keys (F7)", Tooltip = "Enables F7 to force age-up and test death.", Category = "Debug", SortOrder = 121)]
        public bool enableDebugKeys = false;

        [ModSetting("Custom Random Seed (0 = Auto)", Tooltip = "Forces a specific random seed for genetic rolls and illness. Set to 0 to use standard save-based randomness.", Category = "Debug", SortOrder = 122)]
        public int customSeed = 0;

        /// <summary>
        /// Validates and clamps configuration values to ensure logical consistency.
        /// </summary>
        public void ValidateAndClamp()
        {
            adultAgeYears = Math.Max(1, adultAgeYears);
            elderAgeYears = Math.Max(adultAgeYears + 1, elderAgeYears);

            agingIntervalWeeks = Math.Max(1, agingIntervalWeeks);
            weeksAgedPerInterval = Math.Max(1, weeksAgedPerInterval);
            childhoodAccelerationCutoffAge = Math.Max(1, childhoodAccelerationCutoffAge);

            elderIllnessBaseChance = Math.Max(0f, Math.Min(100f, elderIllnessBaseChance));
            baseStatGainChance = Math.Max(0f, Math.Min(100f, baseStatGainChance));
            heartDiseaseAttackChance = Math.Max(0f, Math.Min(100f, heartDiseaseAttackChance));
            deathBaseProbability = Math.Max(0f, Math.Min(100f, deathBaseProbability));

            heartAttackDamage = Math.Max(0f, Math.Min(100f, heartAttackDamage));
            deathProbabilityMultiplier = Math.Max(0f, Math.Min(10f, deathProbabilityMultiplier));
            illnessStageMaxYears = Math.Max(illnessStageMinYears, illnessStageMaxYears);
            catchUpWindowYears = Math.Max(0, catchUpWindowYears);
            forfeitWindowYears = Math.Max(0, forfeitWindowYears);
            surgeMinDelay = Math.Max(0f, surgeMinDelay);
            surgeMaxDelay = Math.Max(surgeMinDelay, surgeMaxDelay);

            explorerMeanAge = Math.Max(1, explorerMeanAge);
            explorerStdDev = Math.Max(1, explorerStdDev);
            recruiterMeanAge = Math.Max(1, recruiterMeanAge);
            recruiterStdDev = Math.Max(1, recruiterStdDev);
            traderMeanAge = Math.Max(1, traderMeanAge);
            traderStdDev = Math.Max(1, traderStdDev);
        }

        public bool CheckElderAge(object newVal)
        {
            if (newVal is int val)
            {
                return val > adultAgeYears;
            }
            return true;
        }

        public bool CheckAdultAge(object newVal)
        {
            if (newVal is int val)
            {
                return val > initialChildAgeYears;
            }
            return true;
        }
    }
}
