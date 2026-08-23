using System;

namespace Lifespan
{
    /// <summary>
    /// Non-configurable distances and biological units shared by Lifespan systems.
    /// </summary>
    public static class LifespanConstants
    {
        // --- Navigation & AI ---
        
        /// <summary>Distance at which a caregiver reaches the target.</summary>
        public const float ArrivalDistance = 1.0f;
        
        /// <summary>Distance a child must move before the caregiver updates the path.</summary>
        public const float UpdateTargetDistance = 2.0f;

        // --- Biological ---
        
        /// <summary>Biological weeks in one year.</summary>
        public const int WeeksPerYear = 52;
        
        /// <summary>Age at which the fixed childhood acceleration rule stops.</summary>
        public const int ChildhoodAccelerationStopAge = 10;
    }
}
