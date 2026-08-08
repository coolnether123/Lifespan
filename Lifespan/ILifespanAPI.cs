using System.Collections.Generic;

namespace Lifespan
{
    /// <summary>
    /// Context for age generation when a character is first encountered.
    /// </summary>
    public enum AgeContext
    {
        Unknown,
        FamilyMember,
        ExplorerEncounter,
        ShelterRecruit,
        NPC_Trader,
        NPC_Wanderer,
        NewbornFromPregnancy,
        Custom // For other mods to define their own logic if needed
    }

    public enum ChildStage
    {
        Newborn,    // Immobile, needs care
        Child,      // Mobile, simple jobs
        PreTeen,    // Expeditions with adult
        Teen,       // Full expeditions
        Adult,      // Full capabilities
        Elder       // Full capabilities
    }

    /// <summary>
    /// Public API interface for the Lifespan mod.
    /// Other mods can access this via ModAPIRegistry.
    /// </summary>
    public interface ILifespanAPI
    {
        // ===== EXISTING FAMILY MEMBER API =====

        /// <summary>
        /// Gets the age of a character in weeks.
        /// </summary>
        int GetCharacterAgeWeeks(FamilyMember member);

        /// <summary>
        /// Gets the age of a character in years.
        /// </summary>
        int GetCharacterAgeYears(FamilyMember member);

        /// <summary>
        /// Sets the age of a character in weeks.
        /// </summary>
        void SetCharacterAgeWeeks(FamilyMember member, int weeks);

        /// <summary>
        /// Sets the age of any character in weeks.
        /// </summary>
        void SetCharacterAgeWeeks(BaseCharacter character, int weeks);

        /// <summary>
        /// Checks if a character is an elder (past elder threshold).
        /// </summary>
        bool IsElder(FamilyMember member);

        /// <summary>
        /// Gets a snapshot of the active illnesses for a character. Treat the returned
        /// list as read-only; use AddIllness or RemoveIllness to change saved state.
        /// </summary>
        List<string> GetActiveIllnesses(FamilyMember member);

        /// <summary>
        /// Adds an illness to a character.
        /// </summary>
        void AddIllness(FamilyMember member, string illnessId);

        /// <summary>
        /// Removes an illness from a character.
        /// </summary>
        void RemoveIllness(FamilyMember member, string illnessId);

        /// <summary>
        /// Gets the current configuration settings.
        /// </summary>
        LifespanConfig GetConfiguration();

        /// <summary>
        /// Sets the configuration settings (will be persisted).
        /// </summary>
        void SetConfiguration(LifespanConfig config);

        // ===== NEW GENERIC / NPC API =====

        /// <summary>
        /// Generates and tracks age for any character (including NPCs) based on context.
        /// Use this for new encounters, recruits, or newborns.
        /// </summary>
        int GenerateAgeForNPC(BaseCharacter character, AgeContext context);

        /// <summary>
        /// Gets the age of any character (FamilyMember or NPC) in weeks.
        /// Returns 0 if not tracked.
        /// </summary>
        int GetCharacterAgeWeeks(BaseCharacter character);

        /// <summary>
        /// Gets live external characters registered with the aging API in this runtime.
        /// Save records without a currently loaded character object are not included.
        /// </summary>
        List<BaseCharacter> GetTrackedExternalCharacters();

        /// <summary>
        /// Manually increments the age of a character.
        /// Useful for other mods managing their own lifecycle (e.g. pregnancy).
        /// Returns the unchanged age when OnBeforeCharacterAged cancels the increment.
        /// </summary>
        int IncrementCharacterAge(BaseCharacter character, int weeks);

        /// <summary>
        /// Backward-compatible alias for IncrementCharacterAge.
        /// </summary>
        int IncrementNPCAge(BaseCharacter character, int weeks);

        /// <summary>
        /// Fired after this API increments a concrete character's age.
        /// </summary>
        event System.Action<BaseCharacter, int> OnCharacterAged;
        
        /// <summary>
        /// Fired BEFORE a character is aged.
        /// Return FALSE to cancel the aging process for this character (e.g. for cryostasis).
        /// </summary>
        event System.Func<BaseCharacter, bool> OnBeforeCharacterAged;

        /// <summary>
        /// Iterates through all live tracked external characters (NPCs) and increments their age by the specified weeks.
        /// This respects OnBeforeCharacterAged cancellation and only fires OnCharacterAged with a non-null character.
        /// </summary>
        void UpdateExternalCharacters(int weeks);

        /// <summary>
        /// Sets the initial genetic development potential for a character (usually a newborn).
        /// This controls how many stats they can gain during childhood (Pre-Adult stage).
        /// </summary>
        void SetInitialDevelopmentPotential(FamilyMember member, int potential);

        /// <summary>
        /// Gets the current developmental stage of a child.
        /// </summary>
        ChildStage GetChildStage(FamilyMember member);

        // ===== POPULATION STATISTICS API =====

        /// <summary>
        /// Calculates the total population count tracked by the AgeTracker, 
        /// optionally filtered by a predicate on the character details.
        /// Useful for Faction mods to determine colony size.
        /// </summary>
        int CalculateTotalPopulation(System.Func<BaseCharacter, bool> filter = null);

        /// <summary>
        /// Gets the number of tracked characters within a specific age range (in years).
        /// </summary>
        int GetPopulationInAgeRange(int minAgeYears, int maxAgeYears);

        /// <summary>
        /// Retrieves a list of all FamilyMembers currently in a specific developmental stage.
        /// </summary>
        List<FamilyMember> GetCharactersByStage(ChildStage stage);
    }
}
