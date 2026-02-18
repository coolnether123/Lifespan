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

        [ModSetting("Adulthood Age", Tooltip = "The age when children become adults. Must be greater than Default Child Start Age.", Category = "Constant Settings", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 10, ValidateMethod = "CheckAdultAge")]
        public int adultAgeYears = 18;

        [ModSetting("Elderly Age", Tooltip = "The age when natural death risks begin. Must be greater than Adulthood Age.", Category = "Constant Settings", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 11, ValidateMethod = "CheckElderAge")]
        public int elderAgeYears = 60;

        [ModSetting("Default Child Start Age", Tooltip = "The starting age for newly recruited children (e.g. from radio broadcasts).", Category = "Constant Settings", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 12)]
        public int initialChildAgeYears = 10;

        [ModSetting("Default Adult Start Age", Tooltip = "The starting age for newly recruited adults.", Category = "Constant Settings", MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 13)]
        public int initialAdultAgeYears = 30;

        // ====================================================================
        // CHILD DEVELOPMENT & EXPEDITIONS
        // ====================================================================

        [ModSetting("Enable Child Development", Tooltip = "Enables stages (Newborn, Toddler, Teen) with specific restrictions.", Category = "Development", SortOrder = 40)]
        public bool enableChildDevelopment = true;

        [ModSetting("Mobile Age", Tooltip = "Age (years) when children become mobile. Before this, they are immobile and need care.", Category = "Development", MinValue = 1, MaxValue = 10, StepSize = 1, SortOrder = 41)]
        public int mobileAgeYears = 1;

        [ModSetting("Child Job Age", Tooltip = "Age (years) when children can start doing jobs/tasks.", Category = "Development", MinValue = 1, MaxValue = 15, StepSize = 1, SortOrder = 42)]
        public int childJobAgeYears = 6;

        [ModSetting("Expedition (Accompanied) Age", Tooltip = "Min age (years) to go on expeditions with an adult.", Category = "Development", MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 43)]
        public int expeditionMinAgeAccompanied = 10;

        [ModSetting("Expedition (Solo) Age", Tooltip = "Min age (years) to go on expeditions alone/lead a party.", Category = "Development", MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 44)]
        public int expeditionMinAgeSolo = 13;

        [ModSetting("Accelerated Childhood Aging", Tooltip = "If true, children age 2x as fast until they reach a certain age.", Category = "Development", SortOrder = 45)]
        public bool enableAcceleratedChildhood = true;

        [ModSetting("Acceleration Cutoff Age", Tooltip = "The age until which accelerated aging applies (Default: 14).", Category = "Development", MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 46)]
        public int childhoodAccelerationCutoffAge = 14;

        // ====================================================================
        // AGING SPEED
        // ====================================================================

        [ModSetting("Weeks Between Aging Ticks", Tooltip = "Higher values slow down biological aging check frequency.", Category = "Constant Settings", MinValue = 1, MaxValue = 52, StepSize = 1, SortOrder = 14)]
        public int agingIntervalWeeks = 1;

        [ModSetting("Biological Weeks Per Tick", Tooltip = "How many weeks a character ages every interval. (52 = 1 year).", Category = "Constant Settings", MinValue = 1, MaxValue = 520, StepSize = 1, SortOrder = 15)]
        public int weeksAgedPerInterval = 52; 

        // ====================================================================
        // GROWTH & DEVELOPMENT
        // ====================================================================

        [ModSetting("Base Skill Gain Chance (%)", Tooltip = "Baseline probability (%) of a skill increase per week.", Category = "Constant Settings", MinValue = 0.01f, MaxValue = 5.0f, SortOrder = 16)]
        public float baseStatGainChance = 0.25f;

        [ModSetting("Trauma Influence", Tooltip = "How much trauma penalizes stat growth (Default: 30%).", Category = "Constant Settings", MinValue = 0f, MaxValue = 1f, SortOrder = 17)]
        public float maxStressInfluence = 0.3f;

        // Hidden / Internal config for now
        public float catchUpBonusPerPoint = 0.05f;
        public int catchUpWindowYears = 3;
        public float forfeitBaseChance = 0.10f;
        public int forfeitWindowYears = 1;

        [ModSetting("Spark Probability", Tooltip = "Chance for a double stat gain (breakthrough).", Category = "Constant Settings", MinValue = 0f, MaxValue = 0.5f, SortOrder = 18)]
        public float sparkBaseChance = 0.05f; 

        [ModSetting("Spark Multiplier", Tooltip = "Multiplier when a spark occurs.", Category = "Constant Settings", MinValue = 1, MaxValue = 10, SortOrder = 19)]
        public int sparkMultiplier = 2;

        [ModSetting("Child Growth Multiplier", Tooltip = "Multiplier for stat gains during childhood.", Category = "Constant Settings", MinValue = 1f, MaxValue = 10f, SortOrder = 20)]
        public float childGrowthMultiplier = 4.0f; 

        // ====================================================================
        // ELDER ILLNESS
        // ====================================================================
        [ModSetting("Dementia Enabled", Tooltip = "Enables the Dementia illness, which reduces Intelligence.", Category = "Constant Settings", SortOrder = 21)]
        public bool enableDementia = true;

        [ModSetting("Heart Disease Enabled", Tooltip = "Enables Heart Disease, carrying a risk of heart attacks in high stress.", Category = "Constant Settings", SortOrder = 22)]
        public bool enableHeartDisease = true;

        [ModSetting("Arthritis Enabled", Tooltip = "Enables Arthritis, which slows down move speed.", Category = "Constant Settings", SortOrder = 23)]
        public bool enableArthritis = true;

        [ModSetting("Frailty Enabled", Tooltip = "Enables Frailty, completely preventing expeditions and reducing Strength.", Category = "Constant Settings", SortOrder = 24)]
        public bool enableFrailty = true;

        [ModSetting("Respiratory Issues Enabled", Tooltip = "Enables Respiratory issues, reducing oxygen efficiency.", Category = "Constant Settings", SortOrder = 25)]
        public bool enableRespiratory = true;

        [ModSetting("Illness Contract Chance (%)", Tooltip = "Probability (%) of an elder falling ill each week.", Category = "Constant Settings", MinValue = 0f, MaxValue = 5.0f, SortOrder = 26)]
        public float elderIllnessBaseChance = 0.1f; 

        [ModSetting("Min Years Between Stages", Tooltip = "Minimum years before an illness can progress to the next stage.", Category = "Constant Settings", MinValue = 1, MaxValue = 10, SortOrder = 27)]
        public int illnessStageMinYears = 4;

        [ModSetting("Max Years Between Stages", Tooltip = "Maximum years before an illness moves to the next stage.", Category = "Constant Settings", MinValue = 1, MaxValue = 20, SortOrder = 28)]
        public int illnessStageMaxYears = 8;
        
        // Modifiers (Advanced, could expose if desired)
        public float dementiaIntModifier = 0.5f;
        public float arthritisSpeedModifier = 0.6f;
        public float frailtyStrModifier = 0.6f;
        
        [ModSetting("Heart Attack Chance (%)", Tooltip = "Weekly chance of a heart attack if suffering from Heart Disease and High Stress.", Category = "Constant Settings", MinValue = 0f, MaxValue = 25.0f, SortOrder = 29)]
        public float heartDiseaseAttackChance = 5.0f;

        [ModSetting("Heart Attack Damage", Tooltip = "Amount of HP damage dealt by a single heart attack.", Category = "Constant Settings", MinValue = 0f, MaxValue = 100f, SortOrder = 30)]
        public float heartAttackDamage = 20.0f;

        // ====================================================================
        // NATURAL DEATH
        // ====================================================================
        
        [ModSetting("Difficulty (Death Multiplier)", Tooltip = "Affects the average lifespan. Easy=0.5x, Medium=1.0x, Hard=2.0x.", Category = "Difficulty", MinValue = 0.1f, MaxValue = 5.0f, SortOrder = 1)]
        [ModSettingPreset("Easy", 0.5f)]
        [ModSettingPreset("Medium", 1.0f)]
        [ModSettingPreset("Hard", 2.0f)]
        public float deathProbabilityMultiplier = 1.0f; 

        [ModSetting("Base Death Probability (%)", Tooltip = "Base chance (0-100%) of death per week once past elder threshold.", Category = "Constant Settings", MinValue = 0f, MaxValue = 1.0f, StepSize = 0.001f, SortOrder = 19)]
        public float deathBaseProbability = 0.05f; // 0.05% per week at elder threshold

        [ModSetting("Death Probability Increase Per Year (%)", Tooltip = "How much the death chance increases each year past elder threshold.", Category = "Constant Settings", MinValue = 0f, MaxValue = 0.1f, StepSize = 0.001f, SortOrder = 20)]
        public float deathProbabilityIncreasePerYear = 0.008f; // 0.008% increase per year 

        /// <summary>How significantly low health increases the probability of natural death.</summary>
        public float healthImpactFactor = 10f;

        // ====================================================================
        // VISUALS & DEBUG
        // ====================================================================

        public float hairGreyingLerpSpeed = 0.5f;
        public float hairGreyingMaxDuration = 30f;
        
        [ModSetting("Enable Hair Greying", Tooltip = "If true, hair will gradually turn grey/white as characters age.", Category = "Constant Settings", SortOrder = 20)]
        public bool enableHairGreying = true;

        [ModSetting("Enable Natural Death", Tooltip = "If true, characters can die of old age.", Category = "Constant Settings", SortOrder = 31)]
        public bool enableNaturalDeath = true;

        [ModSetting("Enable Journal Milestones", Tooltip = "If true, major life events and birthdays are recorded in the bunker journal.", Category = "Constant Settings", SortOrder = 32)]
        public bool enableJournalEntries = true;

        public float surgeMinDelay = 3.0f;
        public float surgeMaxDelay = 8.0f;

        // ====================================================================
        // NPC
        // ====================================================================

        [ModSetting("Mean Explorer Age", Tooltip = "Average age of NPCs found exploring. (Bell Curve)", Category = "Constant Settings", MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 22)]
        public int explorerMeanAge = 28;
        public int explorerStdDev = 10;
        public int recruiterMeanAge = 35;
        public int recruiterStdDev = 15;
        public int traderMeanAge = 40;
        public int traderStdDev = 12;

        // ====================================================================
        // DEBUG
        // ====================================================================
        [ModSetting("Enable Verbose Logging", Tooltip = "Spams the log file with detailed step-by-step aging info. Only use if debugging.", Category = "Constant Settings", SortOrder = 23)]
        public bool verboseLogging = false;

        [ModSetting("Enable Debug Keys (F7)", Tooltip = "Enables F7 to force age-up and test death.", Category = "Constant Settings", SortOrder = 24)]
        public bool enableDebugKeys = false;

        [ModSetting("Custom Seed", Tooltip = "Forces a specific random seed for genetic rolls and illness. Set to 0 to use standard save-based randomness.", Category = "Constant Settings", SortOrder = 25)]
        public int customSeed = 0;

        /// <summary>
        /// Validates and clamps configuration values to ensure logical consistency.
        /// </summary>
        public void ValidateAndClamp()
        {
            adultAgeYears = Math.Max(1, adultAgeYears);
            // Ensure Elder comes after Adult
            elderAgeYears = Math.Max(adultAgeYears + 1, elderAgeYears);
            
            agingIntervalWeeks = Math.Max(1, agingIntervalWeeks);
            weeksAgedPerInterval = Math.Max(1, weeksAgedPerInterval);

            // Bounds check 0-100 for percentage inputs
            elderIllnessBaseChance = Math.Max(0f, Math.Min(100f, elderIllnessBaseChance));
            baseStatGainChance = Math.Max(0f, Math.Min(100f, baseStatGainChance));
            heartDiseaseAttackChance = Math.Max(0f, Math.Min(100f, heartDiseaseAttackChance));
            deathBaseProbability = Math.Max(0f, Math.Min(100f, deathBaseProbability));
            
            heartAttackDamage = Math.Max(0f, Math.Min(100f, heartAttackDamage));
            deathProbabilityMultiplier = Math.Max(0f, Math.Min(10f, deathProbabilityMultiplier));
            
            explorerMeanAge = Math.Max(1, explorerMeanAge);
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
                // Ensure Adult Age is logically higher than the starting child age
                return val > initialChildAgeYears;
            }
            return true;
        }
    }
}