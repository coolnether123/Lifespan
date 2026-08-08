using System;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Shared normalization for biological age and per-week probability calculations.
    /// Keeping these rules in one place prevents save/API/gameplay paths from drifting.
    /// </summary>
    internal static class LifespanMath
    {
        public static int NormalizeAgeWeeks(int ageWeeks)
        {
            return NormalizeAgeWeeks((long)ageWeeks);
        }

        public static int NormalizeAgeWeeks(long ageWeeks)
        {
            if (ageWeeks <= 0) return 0;
            return ageWeeks >= int.MaxValue ? int.MaxValue : (int)ageWeeks;
        }

        public static float NormalizeHealthFraction(int health, int maxHealth)
        {
            if (maxHealth <= 0) return 0f;
            return Mathf.Clamp01((float)health / maxHealth);
        }

        public static float ProbabilityAtLeastOnce(float perWeekProbability, int elapsedWeeks)
        {
            float normalizedProbability = Mathf.Clamp01(perWeekProbability);
            int normalizedWeeks = Math.Max(1, elapsedWeeks);
            if (normalizedWeeks == 1 || normalizedProbability >= 1f)
            {
                return normalizedProbability;
            }

            return Mathf.Clamp01(1f - (float)Math.Pow(1f - normalizedProbability, normalizedWeeks));
        }
    }
}
