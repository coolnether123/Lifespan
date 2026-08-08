using System;
using System.Reflection;
using ModAPI.Spine;
using ModAPI.Attributes;

namespace Lifespan
{
    /// <summary>
    /// Configuration container for the Lifespan mod.
    /// Supports the ModAPI Spine settings framework for in-game UI generation.
    /// </summary>
    [ModConfiguration]
    public class LifespanConfig
    {
        private const string CatLife = "01 Life Stages";
        private const string CatAging = "02 Aging Pace";
        private const string CatDevelopment = "03 Child Development";
        private const string CatGrowth = "04 Growth Tuning";
        private const string CatRisk = "05 Death and Illness";
        private const string CatJournal = "06 Journal and Dialogue";
        private const string CatVisualNpc = "07 Visuals and NPC Ages";
        private const string CatDebug = "08 Debug";

        // ====================================================================
        // LIFE STAGES
        // ====================================================================

        [ModSetting("Realistic Starting Family Ages", Tooltip = "When enabled, day-1 family generation uses stricter child/adult age ranges.", Category = CatLife, Mode = SettingMode.Simple, SortOrder = 1)]
        public bool useRoleplayFamilyAgeModel = true;

        [ModSetting("Adulthood Age (years)", Tooltip = "Age when children transition to adults. Must be higher than Child Start Age Max.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 2, ValidateMethod = "CheckAdultAge")]
        public int adultAgeYears = 18;

        [ModSetting("Elder Age (years)", Tooltip = "Age when elder illness and natural death checks begin. Must be higher than Adulthood Age.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 3, ValidateMethod = "CheckElderAge")]
        public int elderAgeYears = 60;

        [ModSetting("Recruited Child Average Age", Tooltip = "Average age for newly recruited children.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 100, StepSize = 1, SortOrder = 10)]
        public int initialChildAgeYears = 10;

        [ModSetting("Recruited Adult Average Age", Tooltip = "Average age for newly recruited adults.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 100, StepSize = 1, SortOrder = 11)]
        public int initialAdultAgeYears = 30;

        [ModSetting("Child Start Age Min", Tooltip = "Minimum generated age for child starts.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 0, MaxValue = 30, StepSize = 1, SortOrder = 12, ValidateMethod = "CheckChildMinAge")]
        public int initialChildAgeMinYears = 10;

        [ModSetting("Child Start Age Max", Tooltip = "Maximum generated age for child starts. Must be below Adulthood Age.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 13, ValidateMethod = "CheckChildMaxAge")]
        public int initialChildAgeMaxYears = 17;

        [ModSetting("Adult Start Age Min", Tooltip = "Minimum generated age for adult starts.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 100, StepSize = 1, SortOrder = 14)]
        public int initialAdultAgeMinYears = 18;

        [ModSetting("Adult Start Age Max", Tooltip = "Maximum generated age for adult starts.", Category = CatLife, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 100, StepSize = 1, SortOrder = 15)]
        public int initialAdultAgeMaxYears = 90;

        // ====================================================================
        // AGING PACE
        // ====================================================================

        [ModSetting("Aging Tick Interval (weeks)", Tooltip = "How often biological aging runs. Higher values = slower aging checks.", Category = CatAging, Mode = SettingMode.Simple, MinValue = 1, MaxValue = 52, StepSize = 1, SortOrder = 20)]
        public int agingIntervalWeeks = 1;

        [ModSetting("Biological Weeks Per Tick", Tooltip = "How much age is added each aging tick. 52 = 1 biological year.", Category = CatAging, Mode = SettingMode.Simple, MinValue = 1, MaxValue = 520, StepSize = 1, SortOrder = 21)]
        public int weeksAgedPerInterval = 52;

        // ====================================================================
        // CHILD DEVELOPMENT
        // ====================================================================

        [ModSetting("Enable Child Development", Tooltip = "Enable newborn/child/teen capability rules.", Category = CatDevelopment, Mode = SettingMode.Simple, SortOrder = 30)]
        public bool enableChildDevelopment = true;

        [ModSetting("Mobility Age (years)", Tooltip = "Age when children become mobile.", Category = CatDevelopment, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 10, StepSize = 1, SortOrder = 31, ValidateMethod = "CheckMobileAge")]
        public int mobileAgeYears = 1;

        [ModSetting("Work Age (years)", Tooltip = "Age when children can perform normal jobs.", Category = CatDevelopment, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 15, StepSize = 1, SortOrder = 32, ValidateMethod = "CheckChildJobAge")]
        public int childJobAgeYears = 6;

        [ModSetting("Expedition Age With Escort", Tooltip = "Minimum age for expeditions when accompanied by a capable adult.", Category = CatDevelopment, Mode = SettingMode.Advanced, MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 33, ValidateMethod = "CheckExpeditionAccompaniedAge")]
        public int expeditionMinAgeAccompanied = 10;

        [ModSetting("Expedition Age Solo", Tooltip = "Minimum age to lead or go on expeditions without an escort.", Category = CatDevelopment, Mode = SettingMode.Advanced, MinValue = 5, MaxValue = 18, StepSize = 1, SortOrder = 34, ValidateMethod = "CheckExpeditionSoloAge")]
        public int expeditionMinAgeSolo = 13;

        [ModSetting("Enable Childhood Fast Aging", Tooltip = "Children age 2x biologically until the cutoff age.", Category = CatDevelopment, Mode = SettingMode.Simple, SortOrder = 35)]
        public bool enableAcceleratedChildhood = true;

        [ModSetting("Fast Aging Cutoff Age", Tooltip = "Ages below this value use childhood fast aging.", Category = CatDevelopment, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 18, StepSize = 1, SortOrder = 36)]
        public int childhoodAccelerationCutoffAge = 10;

        // ====================================================================
        // GROWTH TUNING
        // ====================================================================

        [ModSetting("Skill Gain Chance (%/week)", Tooltip = "Base chance for a stat gain per biological week.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0.01f, MaxValue = 5.0f, SortOrder = 40)]
        [ModSettingPreset("Easy", 0.35f)]
        [ModSettingPreset("Medium", 0.25f)]
        [ModSettingPreset("Hard", 0.18f)]
        public float baseStatGainChance = 0.25f;

        [ModSetting("Stress Impact On Growth", Tooltip = "How strongly trauma changes growth chance (0-1).", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 41)]
        public float maxStressInfluence = 0.3f;

        [ModSetting("Late-Growth Bonus Per Missing Point", Tooltip = "Bonus chance near milestones when a character is behind their potential.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 42)]
        public float catchUpBonusPerPoint = 0.05f;

        [ModSetting("Late-Growth Bonus Window (years)", Tooltip = "How many years before a milestone the late-growth bonus can apply.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0, MaxValue = 10, StepSize = 1, SortOrder = 43)]
        public int catchUpWindowYears = 3;

        [ModSetting("Potential Loss Chance Per Missing Point", Tooltip = "Chance to lose unused potential near milestone deadlines.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 44)]
        [ModSettingPreset("Easy", 0.05f)]
        [ModSettingPreset("Medium", 0.10f)]
        [ModSettingPreset("Hard", 0.15f)]
        public float forfeitBaseChance = 0.10f;

        [ModSetting("Potential Loss Window (years)", Tooltip = "How many years before a milestone potential-loss checks begin.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0, MaxValue = 10, StepSize = 1, SortOrder = 45)]
        public int forfeitWindowYears = 1;

        [ModSetting("Breakthrough Chance (0-1)", Tooltip = "Chance for an extra-large stat gain event.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 0.5f, SortOrder = 46)]
        public float sparkBaseChance = 0.05f;

        [ModSetting("Breakthrough Gain Multiplier", Tooltip = "Multiplier applied when a breakthrough triggers.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 10, SortOrder = 47)]
        public int sparkMultiplier = 2;

        [ModSetting("Child Growth Multiplier", Tooltip = "Multiplier for growth chance during pre-adult years.", Category = CatGrowth, Mode = SettingMode.Advanced, MinValue = 1f, MaxValue = 10f, SortOrder = 48)]
        [ModSettingPreset("Easy", 5.0f)]
        [ModSettingPreset("Medium", 4.0f)]
        [ModSettingPreset("Hard", 3.0f)]
        public float childGrowthMultiplier = 4.0f;

        // ====================================================================
        // DEATH & ILLNESS
        // ====================================================================

        [ModSetting("Natural Death Difficulty", Tooltip = "Higher values increase age-related death risk.", Category = CatRisk, Mode = SettingMode.Simple, MinValue = 0.1f, MaxValue = 5.0f, SortOrder = 50)]
        [ModSettingPreset("Easy", 0.5f)]
        [ModSettingPreset("Medium", 1.0f)]
        [ModSettingPreset("Hard", 2.0f)]
        public float deathProbabilityMultiplier = 1.0f;

        [ModSetting("Enable Natural Death", Tooltip = "Allow death from old age checks.", Category = CatRisk, Mode = SettingMode.Simple, SortOrder = 51)]
        public bool enableNaturalDeath = true;

        [ModSetting("Elder Illness Chance (%/week)", Tooltip = "Base weekly chance for an elder to develop an illness.", Category = CatRisk, Mode = SettingMode.Simple, MinValue = 0f, MaxValue = 5.0f, SortOrder = 52)]
        [ModSettingPreset("Easy", 0.06f)]
        [ModSettingPreset("Medium", 0.10f)]
        [ModSettingPreset("Hard", 0.14f)]
        public float elderIllnessBaseChance = 0.1f;

        [ModSetting("Enable Dementia", Tooltip = "Allow dementia illness rolls.", Category = CatRisk, Mode = SettingMode.Advanced, SortOrder = 53)]
        public bool enableDementia = true;

        [ModSetting("Enable Heart Disease", Tooltip = "Allow heart disease illness rolls.", Category = CatRisk, Mode = SettingMode.Advanced, SortOrder = 54)]
        public bool enableHeartDisease = true;

        [ModSetting("Enable Arthritis", Tooltip = "Allow arthritis illness rolls.", Category = CatRisk, Mode = SettingMode.Advanced, SortOrder = 55)]
        public bool enableArthritis = true;

        [ModSetting("Enable Frailty", Tooltip = "Allow frailty illness rolls.", Category = CatRisk, Mode = SettingMode.Advanced, SortOrder = 56)]
        public bool enableFrailty = true;

        [ModSetting("Enable Respiratory Illness", Tooltip = "Allow respiratory illness rolls.", Category = CatRisk, Mode = SettingMode.Advanced, SortOrder = 57)]
        public bool enableRespiratory = true;

        [ModSetting("Base Elder Death Chance (%/week)", Tooltip = "Base weekly natural death chance once elder age is reached.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1.0f, StepSize = 0.001f, SortOrder = 60)]
        [ModSettingPreset("Easy", 0.03f)]
        [ModSettingPreset("Medium", 0.05f)]
        [ModSettingPreset("Hard", 0.07f)]
        public float deathBaseProbability = 0.05f;

        [ModSetting("Death Chance Growth (%/year)", Tooltip = "Additional weekly death chance per year past elder age.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 0.1f, StepSize = 0.001f, SortOrder = 61)]
        [ModSettingPreset("Easy", 0.005f)]
        [ModSettingPreset("Medium", 0.008f)]
        [ModSettingPreset("Hard", 0.012f)]
        public float deathProbabilityIncreasePerYear = 0.008f;

        [ModSetting("Low-Health Death Impact", Tooltip = "How strongly low HP raises natural death risk.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 20f, SortOrder = 62)]
        [ModSettingPreset("Easy", 7.0f)]
        [ModSettingPreset("Medium", 10.0f)]
        [ModSettingPreset("Hard", 14.0f)]
        public float healthImpactFactor = 10f;

        [ModSetting("Illness Progress Min Delay (years)", Tooltip = "Minimum years before a mild illness can worsen.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 10, SortOrder = 63)]
        public int illnessStageMinYears = 4;

        [ModSetting("Illness Progress Max Delay (years)", Tooltip = "Maximum years before a mild illness can worsen.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 20, SortOrder = 64)]
        public int illnessStageMaxYears = 8;

        [ModSetting("Dementia Intelligence Multiplier", Tooltip = "Intelligence retained under severe dementia (0.5 = 50%).", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 65)]
        public float dementiaIntModifier = 0.5f;

        [ModSetting("Arthritis Dexterity Multiplier", Tooltip = "Dexterity retained under severe arthritis.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 66)]
        public float arthritisSpeedModifier = 0.6f;

        [ModSetting("Frailty Strength Multiplier", Tooltip = "Strength retained under severe frailty.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 1f, SortOrder = 67)]
        public float frailtyStrModifier = 0.6f;

        [ModSetting("Heart Attack Chance (%/week)", Tooltip = "Weekly heart attack chance while severe heart disease is active.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 25.0f, SortOrder = 68)]
        [ModSettingPreset("Easy", 3.0f)]
        [ModSettingPreset("Medium", 5.0f)]
        [ModSettingPreset("Hard", 7.5f)]
        public float heartDiseaseAttackChance = 5.0f;

        [ModSetting("Heart Attack Damage", Tooltip = "Damage dealt by a heart attack event.", Category = CatRisk, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 100f, SortOrder = 69)]
        public float heartAttackDamage = 20.0f;

        // ====================================================================
        // JOURNAL & DIALOGUE
        // ====================================================================

        [ModSetting("Enable Journal Milestones", Tooltip = "Record birthdays and major life events in the bunker journal.", Category = CatJournal, Mode = SettingMode.Simple, SortOrder = 70)]
        public bool enableJournalEntries = true;

        [ModSetting("Reactive Speech Min Delay", Tooltip = "Minimum delay before queued reactive speech/journal lines fire.", Category = CatJournal, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 30f, SortOrder = 71)]
        public float surgeMinDelay = 3.0f;

        [ModSetting("Reactive Speech Max Delay", Tooltip = "Maximum delay before queued reactive speech/journal lines fire.", Category = CatJournal, Mode = SettingMode.Advanced, MinValue = 0f, MaxValue = 60f, SortOrder = 72)]
        public float surgeMaxDelay = 8.0f;

        // ====================================================================
        // VISUALS & NPC AGES
        // ====================================================================

        [ModSetting("Enable Hair Greying", Tooltip = "Hair gradually turns grey/white with age.", Category = CatVisualNpc, Mode = SettingMode.Simple, SortOrder = 80)]
        public bool enableHairGreying = true;

        [ModSetting("Hair Greying Speed", Tooltip = "Higher values make visible greying transitions complete faster.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 0.01f, MaxValue = 5f, SortOrder = 81)]
        public float hairGreyingLerpSpeed = 0.5f;

        [ModSetting("Hair Greying Max Seconds", Tooltip = "Safety timeout for one greying transition.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 1f, MaxValue = 300f, SortOrder = 82)]
        public float hairGreyingMaxDuration = 30f;

        [ModSetting("Explorer Age Mean", Tooltip = "Average age of explorers encountered outside.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 83)]
        public int explorerMeanAge = 28;

        [ModSetting("Explorer Age Spread", Tooltip = "Standard deviation for explorer ages.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 84)]
        public int explorerStdDev = 10;

        [ModSetting("Recruit Age Mean", Tooltip = "Average age for shelter recruits.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 85)]
        public int recruiterMeanAge = 35;

        [ModSetting("Recruit Age Spread", Tooltip = "Standard deviation for recruit ages.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 86)]
        public int recruiterStdDev = 15;

        [ModSetting("Trader Age Mean", Tooltip = "Average age for traders.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 18, MaxValue = 80, StepSize = 1, SortOrder = 87)]
        public int traderMeanAge = 40;

        [ModSetting("Trader Age Spread", Tooltip = "Standard deviation for trader ages.", Category = CatVisualNpc, Mode = SettingMode.Advanced, MinValue = 1, MaxValue = 30, StepSize = 1, SortOrder = 88)]
        public int traderStdDev = 12;

        // ====================================================================
        // DEBUG
        // ====================================================================

        [ModSetting("Verbose Logging", Tooltip = "Enable detailed logging for troubleshooting.", Category = CatDebug, Mode = SettingMode.Advanced, SortOrder = 90)]
        public bool verboseLogging = false;

        [ModSetting("Enable Debug Keys (F7)", Tooltip = "Allow debug key shortcuts for testing.", Category = CatDebug, Mode = SettingMode.Advanced, SortOrder = 91)]
        public bool enableDebugKeys = false;

        /// <summary>
        /// Validates and clamps configuration values to ensure logical consistency.
        /// </summary>
        public void ValidateAndClamp()
        {
            initialChildAgeMinYears = ClampInt(initialChildAgeMinYears, 0, 30);
            initialChildAgeMaxYears = ClampInt(initialChildAgeMaxYears, initialChildAgeMinYears, 30);

            // Keep room for an elder threshold while honoring the cross-field invariant.
            adultAgeYears = ClampInt(adultAgeYears, 1, 99);
            adultAgeYears = Math.Max(adultAgeYears, initialChildAgeMaxYears + 1);
            adultAgeYears = Math.Min(adultAgeYears, 99);
            elderAgeYears = ClampInt(elderAgeYears, adultAgeYears + 1, 100);

            initialAdultAgeMinYears = ClampInt(initialAdultAgeMinYears, 18, 100);
            initialAdultAgeMaxYears = ClampInt(initialAdultAgeMaxYears, initialAdultAgeMinYears, 100);

            int maxChildStart = Math.Min(initialChildAgeMaxYears, adultAgeYears - 1);
            initialChildAgeYears = ClampInt(initialChildAgeYears, initialChildAgeMinYears, Math.Max(initialChildAgeMinYears, maxChildStart));
            initialAdultAgeYears = ClampInt(initialAdultAgeYears, initialAdultAgeMinYears, initialAdultAgeMaxYears);

            mobileAgeYears = ClampInt(mobileAgeYears, 1, Math.Max(1, adultAgeYears - 1));
            childJobAgeYears = ClampInt(childJobAgeYears, mobileAgeYears, Math.Max(mobileAgeYears, adultAgeYears - 1));
            expeditionMinAgeAccompanied = ClampInt(expeditionMinAgeAccompanied, childJobAgeYears, Math.Max(childJobAgeYears, adultAgeYears));
            expeditionMinAgeSolo = ClampInt(expeditionMinAgeSolo, expeditionMinAgeAccompanied, Math.Max(expeditionMinAgeAccompanied, adultAgeYears));
            childhoodAccelerationCutoffAge = ClampInt(childhoodAccelerationCutoffAge, 1, adultAgeYears);

            agingIntervalWeeks = ClampInt(agingIntervalWeeks, 1, 52);
            weeksAgedPerInterval = ClampInt(weeksAgedPerInterval, 1, 520);

            baseStatGainChance = ClampFloat(baseStatGainChance, 0.01f, 5.0f);
            maxStressInfluence = ClampFloat(maxStressInfluence, 0f, 1f);
            catchUpBonusPerPoint = ClampFloat(catchUpBonusPerPoint, 0f, 1f);
            catchUpWindowYears = ClampInt(catchUpWindowYears, 0, 10);
            forfeitBaseChance = ClampFloat(forfeitBaseChance, 0f, 1f);
            forfeitWindowYears = ClampInt(forfeitWindowYears, 0, 10);
            sparkBaseChance = ClampFloat(sparkBaseChance, 0f, 0.5f);
            sparkMultiplier = ClampInt(sparkMultiplier, 1, 10);
            childGrowthMultiplier = ClampFloat(childGrowthMultiplier, 1f, 10f);

            elderIllnessBaseChance = ClampFloat(elderIllnessBaseChance, 0f, 5.0f);
            illnessStageMinYears = ClampInt(illnessStageMinYears, 1, 10);
            illnessStageMaxYears = ClampInt(illnessStageMaxYears, illnessStageMinYears, 20);
            dementiaIntModifier = ClampFloat(dementiaIntModifier, 0f, 1f);
            arthritisSpeedModifier = ClampFloat(arthritisSpeedModifier, 0f, 1f);
            frailtyStrModifier = ClampFloat(frailtyStrModifier, 0f, 1f);
            heartDiseaseAttackChance = ClampFloat(heartDiseaseAttackChance, 0f, 25.0f);
            heartAttackDamage = ClampFloat(heartAttackDamage, 0f, 100f);

            deathProbabilityMultiplier = ClampFloat(deathProbabilityMultiplier, 0.1f, 5.0f);
            deathBaseProbability = ClampFloat(deathBaseProbability, 0f, 1.0f);
            deathProbabilityIncreasePerYear = ClampFloat(deathProbabilityIncreasePerYear, 0f, 0.1f);
            healthImpactFactor = ClampFloat(healthImpactFactor, 0f, 20f);

            surgeMinDelay = ClampFloat(surgeMinDelay, 0f, 30f);
            surgeMaxDelay = ClampFloat(surgeMaxDelay, surgeMinDelay, 60f);

            hairGreyingLerpSpeed = ClampFloat(hairGreyingLerpSpeed, 0.01f, 5f);
            hairGreyingMaxDuration = ClampFloat(hairGreyingMaxDuration, 1f, 300f);

            explorerMeanAge = ClampInt(explorerMeanAge, 18, 80);
            explorerStdDev = ClampInt(explorerStdDev, 1, 30);
            recruiterMeanAge = ClampInt(recruiterMeanAge, 18, 80);
            recruiterStdDev = ClampInt(recruiterStdDev, 1, 30);
            traderMeanAge = ClampInt(traderMeanAge, 18, 80);
            traderStdDev = ClampInt(traderStdDev, 1, 30);
        }

        public void CopyFrom(LifespanConfig source)
        {
            if (source == null) return;

            if (ReferenceEquals(this, source))
            {
                ValidateAndClamp();
                return;
            }

            foreach (FieldInfo field in typeof(LifespanConfig).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                field.SetValue(this, field.GetValue(source));
            }

            ValidateAndClamp();
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
                return val > initialChildAgeMaxYears;
            }
            return true;
        }

        public bool CheckChildMinAge(object newVal)
        {
            if (newVal is int val)
            {
                return val <= initialChildAgeMaxYears;
            }
            return true;
        }

        public bool CheckChildMaxAge(object newVal)
        {
            if (newVal is int val)
            {
                return val >= initialChildAgeMinYears && val < adultAgeYears;
            }
            return true;
        }

        public bool CheckMobileAge(object newVal)
        {
            if (newVal is int val)
            {
                return val <= childJobAgeYears && val < adultAgeYears;
            }
            return true;
        }

        public bool CheckChildJobAge(object newVal)
        {
            if (newVal is int val)
            {
                return val >= mobileAgeYears && val <= expeditionMinAgeAccompanied;
            }
            return true;
        }

        public bool CheckExpeditionAccompaniedAge(object newVal)
        {
            if (newVal is int val)
            {
                return val >= childJobAgeYears && val <= expeditionMinAgeSolo;
            }
            return true;
        }

        public bool CheckExpeditionSoloAge(object newVal)
        {
            if (newVal is int val)
            {
                return val >= expeditionMinAgeAccompanied && val <= adultAgeYears;
            }
            return true;
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float ClampFloat(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
