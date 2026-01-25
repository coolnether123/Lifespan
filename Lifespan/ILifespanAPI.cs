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
        /// Gets the list of active illnesses for a character.
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
        /// Manually increments the age of an NPC (non-family member).
        /// Useful for other mods managing their own lifecycle (e.g. pregnancy).
        /// </summary>
        int IncrementNPCAge(BaseCharacter character, int weeks);

        /// <summary>
        /// Fired when an NPC's age is explicitly incremented via IncrementNPCAge.
        /// </summary>
        event System.Action<BaseCharacter, int> OnCharacterAged;
    }
}
