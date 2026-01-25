using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Represents the genetic potential for stat development across life stages.
    /// Each character gets a unique gene that determines their growth potential.
    /// </summary>
    [Serializable]
    public class DevelopmentGene
    {
        // ========================================
        // LIFE STAGE POTENTIALS
        // These define how many stat points CAN be gained in each stage
        // ========================================
        
        /// <summary>Pre-Adult stage: childhood to adulthood (age 0-18)</summary>
        public int PreAdultPotential;
        
        /// <summary>Post-Adult stage: adulthood to elder (age 18-60)</summary>
        public int PostAdultPotential;
        
        /// <summary>Pre-Elder stage: approaching elder threshold (age 55-60)</summary>
        public int PreElderPotential;
        
        /// <summary>Post-Elder stage: after becoming elder (age 60+)</summary>
        public int PostElderPotential;

        // ========================================
        // TRACKING - How many gains have been awarded
        // ========================================
        public int PreAdultGainsAwarded;
        public int PostAdultGainsAwarded;
        public int PreElderGainsAwarded;
        public int PostElderGainsAwarded;

        // ========================================
        // FORFEIT FLAGS - If potential was lost due to bad luck
        // ========================================
        public bool PreAdultPotentialForfeited;
        public bool PostAdultPotentialForfeited;
        public bool PreElderPotentialForfeited;
        public bool PostElderPotentialForfeited;

        // ========================================
        // STAT DISTRIBUTION - Which stats this gene favors
        // Values are weights (higher = more likely to be picked)
        // ========================================
        public float StrengthWeight = 1.0f;
        public float DexterityWeight = 1.0f;
        public float IntelligenceWeight = 1.0f;
        public float CharismaWeight = 1.0f;
        public float PerceptionWeight = 1.0f;

        // ========================================
        // SKILL FATIGUE - Tracks consecutive gains
        // ========================================
        public BaseStats.StatType LastGainedStat;
        public bool HasLastGainedStat = false;

        /// <summary>
        /// Generates a random development gene for a new character.
        /// </summary>
        public static DevelopmentGene GenerateRandom(System.Random rng)
        {
            var gene = new DevelopmentGene();

            // Pre-Adult: Children have high growth potential (2-6 stat points)
            gene.PreAdultPotential = rng.Next(2, 7);

            // Post-Adult: Adults have moderate growth (1-4 stat points)
            gene.PostAdultPotential = rng.Next(1, 5);

            // Pre-Elder: Slight boost before decline (0-2 stat points)
            gene.PreElderPotential = rng.Next(0, 3);

            // Post-Elder: Minimal growth in old age (0-1 stat points)
            gene.PostElderPotential = rng.Next(0, 2);

            // Randomize stat weights (some genes favor certain stats)
            gene.StrengthWeight = 0.5f + (float)rng.NextDouble();
            gene.DexterityWeight = 0.5f + (float)rng.NextDouble();
            gene.IntelligenceWeight = 0.5f + (float)rng.NextDouble();
            gene.CharismaWeight = 0.5f + (float)rng.NextDouble();
            gene.PerceptionWeight = 0.5f + (float)rng.NextDouble();

            return gene;
        }

        /// <summary>
        /// Gets remaining potential for a life stage.
        /// </summary>
        public int GetRemainingPotential(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.PreAdult:
                    return PreAdultPotentialForfeited ? 0 : Math.Max(0, PreAdultPotential - PreAdultGainsAwarded);
                case LifeStage.PostAdult:
                    return PostAdultPotentialForfeited ? 0 : Math.Max(0, PostAdultPotential - PostAdultGainsAwarded);
                case LifeStage.PreElder:
                    return PreElderPotentialForfeited ? 0 : Math.Max(0, PreElderPotential - PreElderGainsAwarded);
                case LifeStage.PostElder:
                    return PostElderPotentialForfeited ? 0 : Math.Max(0, PostElderPotential - PostElderGainsAwarded);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Records a stat gain for a life stage.
        /// </summary>
        public void RecordGain(LifeStage stage, int amount = 1)
        {
            switch (stage)
            {
                case LifeStage.PreAdult:
                    PreAdultGainsAwarded += amount;
                    break;
                case LifeStage.PostAdult:
                    PostAdultGainsAwarded += amount;
                    break;
                case LifeStage.PreElder:
                    PreElderGainsAwarded += amount;
                    break;
                case LifeStage.PostElder:
                    PostElderGainsAwarded += amount;
                    break;
            }
        }

        /// <summary>
        /// Forfeits remaining potential for a life stage.
        /// </summary>
        public void ForfeitPotential(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.PreAdult:
                    PreAdultPotentialForfeited = true;
                    break;
                case LifeStage.PostAdult:
                    PostAdultPotentialForfeited = true;
                    break;
                case LifeStage.PreElder:
                    PreElderPotentialForfeited = true;
                    break;
                case LifeStage.PostElder:
                    PostElderPotentialForfeited = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Life stages for development tracking.
    /// </summary>
    public enum LifeStage
    {
        PreAdult,   // Age 0-17 (childhood)
        PostAdult,  // Age 18-54 (prime adulthood)
        PreElder,   // Age 55-59 (approaching elder)
        PostElder   // Age 60+ (elder years)
    }

    /// <summary>
    /// Serializable entry for saving development genes.
    /// </summary>
    [Serializable]
    public class DevelopmentGeneEntry
    {
        public int id;
        public DevelopmentGene gene;
    }
}
