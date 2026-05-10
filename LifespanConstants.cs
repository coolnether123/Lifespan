using System;

namespace Lifespan
{
    /// <summary>
    /// Centralized repository for non-configurable constants used across the Lifespan mod.
    /// Ensures consistency and follows DRY principles for AI navigation and interaction logic.
    /// </summary>
    public static class LifespanConstants
    {
        // --- Navigation & AI ---
        
        /// <summary> Distance at which a caregiver is considered to have arrived at their target. </summary>
        public const float ArrivalDistance = 1.0f;
        
        /// <summary> Distance threshold to trigger a pathfinding update if the target (child) moves. </summary>
        public const float UpdateTargetDistance = 2.0f;

        // --- Biological ---
        
        /// <summary> Standard weeks in a year for biological aging calculations. </summary>
        public const int WeeksPerYear = 52;
        
        /// <summary> Age at which childhood acceleration strictly terminates. </summary>
        public const int ChildhoodAccelerationStopAge = 10;
    }
}
