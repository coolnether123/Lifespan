using System;

namespace Lifespan
{
    [Serializable]
    public class LifespanConfig
    {
        public int adultAgeYears = 18;
        public int elderAgeYears = 60;
        public int maxAgeYears = 200; // Effectively uncapped, statistics will handle it
        public bool verboseLogging = false;
        public bool enableDebugKeys = false; // Toggle for F7 day advance

        // Initial ages when characters are first tracked
        public int initialChildAgeYears = 10;
        public int initialAdultAgeYears = 30;

        // Aging speed settings
        public int agingIntervalWeeks = 1;      // How many game weeks pass between aging checks
        public int weeksAgedPerInterval = 52;    // 1 Game Week = 1 Year of Aging

        public float elderIllnessBaseChance = 0.001f;
        public float deathProbabilityMultiplier = 1.0f;

        public bool enableDementia = true;
        public bool enableArthritis = true;
        public bool enableHeartDisease = true;
        public bool enableFrailty = true;
        public bool enableRespiratory = true;

        public float dementiaIntModifier = 0.5f;
        public float arthritisSpeedModifier = 0.7f;
        public float frailtyStrModifier = 0.6f;
        public float heartDiseaseAttackChance = 0.02f;
        public float heartAttackDamage = 60f;

        public float? baseStatGainChance = 0.0025f;
        public float? maxStressInfluence = 0.30f;

        public float hairGreyingLerpSpeed = 0.5f;
        public float hairGreyingMaxDuration = 30f;
        
        public float surgeMinDelay = 3.0f;
        public float surgeMaxDelay = 8.0f;
        
        public float deathBaseProbability = 0.0005f;
        
        public int preAdultMinPotential = 6;
        public int preAdultMaxPotential = 10;

        // NEW: NPC Age Ranges by Context
        // Explorers: Often young adults 18-45. Mean 28, stdDev 10.
        public int explorerMeanAge = 28;
        public int explorerStdDev = 10;
        
        // Recruits: Mix of all ages. Mean 35, stdDev 15.
        public int recruiterMeanAge = 35;
        public int recruiterStdDev = 15;
        
        // Traders: Often older, experienced. Mean 40, stdDev 12.
        public int traderMeanAge = 40;
        public int traderStdDev = 12;

        public void ValidateAndClamp()
        {
            adultAgeYears = Math.Max(1, adultAgeYears);
            elderAgeYears = Math.Max(adultAgeYears, elderAgeYears);
            maxAgeYears = Math.Max(elderAgeYears, maxAgeYears);
            
            agingIntervalWeeks = Math.Max(1, agingIntervalWeeks);
            weeksAgedPerInterval = Math.Max(1, weeksAgedPerInterval);

            elderIllnessBaseChance = Math.Max(0f, Math.Min(1f, elderIllnessBaseChance));
            heartAttackDamage = Math.Max(0f, Math.Min(100f, heartAttackDamage));
            deathProbabilityMultiplier = Math.Max(0f, Math.Min(10f, deathProbabilityMultiplier));
            
            explorerMeanAge = Math.Max(1, explorerMeanAge);
            recruiterMeanAge = Math.Max(1, recruiterMeanAge);
            traderMeanAge = Math.Max(1, traderMeanAge);
        }
    }
}
