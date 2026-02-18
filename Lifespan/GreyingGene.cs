using System;
using UnityEngine;
using ModAPI.Core;

namespace Lifespan
{
    /// <summary>
    /// Represents the genetic factors determining how a character's hair greys over time.
    /// Based on real-world statistics: 
    /// - Variable start ages (20s to 60s)
    /// - Variable duration (Fast <10y, Avg 10y, Slow 40y+)
    /// - Variable intensity (Salt & Pepper vs Full White)
    /// </summary>
    [Serializable]
    public class GreyingGene
    {
        public int StartAge;
        public int DurationYears;
        public float MaxCoverage; // 0.0 to 1.0 (1.0 = White, 0.5 = Salt & Pepper)
        
        // Caches for optimization
        private float _slope;

        public GreyingGene(int startAge, int duration, float maxCoverage)
        {
            StartAge = startAge;
            DurationYears = Mathf.Max(1, duration); // Prevent division by zero
            MaxCoverage = Mathf.Clamp01(maxCoverage);
            _slope = MaxCoverage / (float)DurationYears;
        }

        public float GetGreyFactor(float currentAgeYears)
        {
            if (currentAgeYears < StartAge) return 0f;
            if (DurationYears <= 0) return MaxCoverage;

            float yearsActive = currentAgeYears - StartAge;
            float progress = yearsActive / DurationYears;
            
            // Allow progress to go slightly above 1.0 internally before clamping 
            // to ensure we hit the MaxCoverage exactly.
            float coverage = Mathf.Clamp(progress, 0f, 1f) * MaxCoverage;
            
            return coverage;
        }

        /// <summary>
        /// Generates a randomized gene based on statistical guidelines.
        /// </summary>
        public static GreyingGene GenerateRandom(ModRandomStream rng)
        {
            // 1. Determine if they grey at all (1 in 10 do not grey significantly by 60)
            // We'll treat this as 'StartAge > 70' or MaxCoverage very low.
            bool willGrey = rng.Value() > 0.10f; 
            if (!willGrey)
            {
                return new GreyingGene(99, 10, 0f);
            }

            // 2. Determine Start Age
            // Most start in mid-30s to late 40s.
            // Early onset: 20s. Late onset: 50s+.
            // Using a weighted approach.
            int startAge = 35 + rng.Range(-10, 20); // 25 to 55 range roughly

            // 3. Determine Duration
            // Average: 10 years.
            // Fast: < 10.
            // Slow: 20-40+.
            int durationType = rng.Range(0, 100);
            int duration;
            if (durationType < 20) // 20% Fast
            {
                duration = rng.Range(3, 10);
            }
            else if (durationType < 60) // 40% Average
            {
                duration = rng.Range(10, 16);
            }
            else // 40% Slow
            {
                duration = rng.Range(20, 45);
            }

            // 4. Determine Max Coverage (Intensity)
            // Most go full white (1.0), some stay salt-and-pepper (0.5 - 0.9)
            float maxCoverage = 1.0f;
            if (rng.Value() < 0.30f) // 30% chance of incomplete transition
            {
                maxCoverage = 0.4f + (rng.Value() * 0.5f); // 0.4 to 0.9
            }

            return new GreyingGene(startAge, duration, maxCoverage);
        }
    }
}
