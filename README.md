# Lifespan Mod for Sheltered
**Created by coolnether123**

A detailed aging system mod that brings realistic aging mechanics to Sheltered, progressing characters from childhood through adulthood and into old age. Now featuring full NPC support and an expanded API for inter-mod compatibility.

## Features

### Core Aging System
- **Weekly Age Tracking**: Characters age by **1 Year** every **1 in-game Week** (default configuration).
- **Persistent Data**: Age information is saved per-save slot and persists across sessions.
- **Automatic Child-to-Adult Transitions**: Children automatically become adults at the configurable threshold.

### Aging Pacing (Default)
The default configuration is tuned for "Fast Aging" to make generational gameplay viable in a standard playthrough:
- **1 Game Week = 1 Year of Age.**
- **Expected Lifespans:**
  - **Newborns:** ~550 Days to old age.
  - **Recruits (Age 30):** ~300 Days to old age.
  - **Elders (Age 60):** ~100-150 Days until death risk becomes high.
- *Note:* Players can adjust `weeksAgedPerInterval` in the config file to slow this down (e.g., set to `1` for realistic time) or speed it up.

### Expanded Age Service (New!)
- **NPC Age Generation**: Context-aware age generation for non-family characters.
  - **Explorers**: Typically young adults (18-45).
  - **Recruits**: Wide range of ages seeking shelter.
  - **Traders**: Often older, more experienced individuals.
  - **Wanderers**: Generic fallback distribution.
- **Seamless Recruitment**: When an NPC is recruited into the family, their age data is automatically transferred to their new family member profile.
- **Inter-Mod Support**: Built-in support for pregnancy mods to register newborns (starting at age 0) and manage their growth.

### Natural Development System
Characters now grow and learn naturally over time based on Lifespan's saved growth-potential profile:
- **Child Growth**: Children have a high potential for learning (4x faster than adults) and can gain significant stats before adulthood.
- **Adult Development**: Adults grow slower but steady.
- **Spark Mechanic**: Happy characters (low stress) have a chance to experience a "Spark" — a breakthrough that grants double stat points instantly.
- **Skill Fatigue**: Characters are encouraged to be well-rounded. Gaining a specific stat reduces the chance of gaining that same stat again immediately.
- **Use It or Lose It**: Characters have a growth-potential cap. If they don't develop their skills before reaching the next life stage (e.g. becoming an Elder), that potential is lost forever.

### Child-to-Adult Transition
When a child reaches adulthood:
- Character mesh automatically swaps from boy/girl to man/woman
- Physical appearance is preserved (colors, features)
- Stats are recalculated for adult caps
- Journal entry is created to commemorate the event
- Movement speed and behaviors update

### Elder Illness System
Once characters reach elder age (default: 60 years), they become susceptible to age-related illnesses:

#### Available Illnesses (all configurable):
1. **Dementia** - Reduces Intelligence stat
2. **Arthritis** - Reduces movement speed
3. **Heart Disease** - Weekly chance of heart attacks (can be fatal)
4. **Frailty** - Reduces Strength stat
5. **Respiratory Issues** - Affects stamina

#### Illness Mechanics:
- Base 0.1% weekly chance (configurable)
- Modified by current health (lower health = higher chance)
- Illnesses stack (characters can have multiple)
- Each illness applies permanent effects
- All illnesses persist across saves

### Death from Old Age
At the death risk threshold (default: 60 years):
- Base 0.05% weekly death chance (configurable)
- Increases by 0.008% per year over threshold
- Modified by:
  - Current health (lower health = higher risk)
  - Number of active illnesses
  - Trauma/stress levels
- Death cause is recorded (old age or specific illness)
- Journal entry created upon death

## Configuration

All settings are exposed through the shared ShelteredAPI 2.0 settings UI with:
- **Simple View**: Core gameplay controls only.
- **Advanced View**: Full tuning options and debug controls.

### Simple View (Recommended Start)
- Life stages: Adulthood and Elder age
- Child development: broad enable/disable and fast-childhood toggle
- Aging pace: interval and biological weeks per tick
- Core risk: natural death difficulty, natural death toggle, elder illness chance
- Journal and visuals toggles

### Advanced View
- Starting age generation ranges and averages
- Detailed child progression gates (movement/work/expedition ages)
- Detailed growth tuning and milestone catch-up logic
- Illness progression timings, per-illness toggles, and severe-effect multipliers
- NPC age distribution controls
- Debug options

### Settings Glossary
- **Breakthrough Chance** (formerly "Spark"): chance for a bigger-than-normal stat gain event.
- **Late-Growth Bonus** (formerly "Catch-Up Bonus"): extra growth chance when a character is behind potential near a milestone.
- **Potential Loss Chance** (formerly "Forfeit Chance"): chance to permanently lose unused growth potential near milestone deadlines.
- **Natural Death Difficulty** (formerly "Death Risk Multiplier"): scales age-based death probability higher or lower.

---

## API for Other Mods

The Lifespan mod exposes a public API for other mods to interact with the aging system.

### Example Usage:
```csharp
using ModAPI.Core;

// Get the Lifespan API
if (ModAPIRegistry.TryGetAPI<ILifespanAPI>("com.lifespan.api", out var lifespanAPI))
{
    // Get a character's age
    int ageYears = lifespanAPI.GetCharacterAgeYears(member);
    
    // Check if they're an elder
    bool isElder = lifespanAPI.IsElder(member);
    
    // Get their illnesses
    var illnesses = lifespanAPI.GetActiveIllnesses(member);
    
    // Add a custom illness
    lifespanAPI.AddIllness(member, "lifespan.illness.dementia");
}
```

### External Character Aging

External characters are tracked with both saved age records and live runtime references. Use `GenerateAgeForNPC` or `SetCharacterAgeWeeks(BaseCharacter, int)` when an NPC is created so later bulk aging can access the concrete character object.

```csharp
lifespanAPI.GenerateAgeForNPC(visitor, AgeContext.NPC_Trader);

lifespanAPI.OnBeforeCharacterAged += character =>
{
    // Return false to skip this character for this aging pass.
    return !IsInCryostasis(character);
};

lifespanAPI.OnCharacterAged += (character, newAgeWeeks) =>
{
    // character is always the concrete character aged by this API event.
};

lifespanAPI.UpdateExternalCharacters(1);
```

Saved external age records without a currently loaded `BaseCharacter` are preserved, but they are not bulk-aged or emitted through `OnCharacterAged` until a live character is registered again.

### Compatibility contract

The public API is registered as `com.lifespan.api` and is compiled against ModAPI/ShelteredAPI 2.0.0.0. Family and integration mods should depend on the `ILifespanAPI` interface rather than Lifespan implementation classes.

- Ages are integer biological weeks; one biological year is 52 weeks. Negative values passed to an age setter are stored as 0. Age 0 is a valid newborn age, while an untracked character also reads as 0 through the non-`Try` getter.
- Family age records are hydrated at session load. Call age-dependent APIs after the load/session event; a family lookup made before hydration returns 0 and does not create a record. Fresh family members are initialized once after hydration, based on their child/adult status and current configuration.
- `GenerateAgeForNPC` is idempotent for a stable character ID. For external NPCs, the live character object must be registered with `GenerateAgeForNPC` or `SetCharacterAgeWeeks(BaseCharacter)` for weekly bulk aging and `OnCharacterAged` delivery. The ID must remain stable while the NPC exists; names are not identity keys.
- The vanilla `FamilyManager.AdoptNpc(NpcVisitor)` path transfers the NPC's saved age to the resulting `FamilyMember` and removes the external record. Mods that replace or bypass that adoption path must transfer the age explicitly to the new family member.
- `OnBeforeCharacterAged` runs once per character; every subscribed handler must return `true` for aging to proceed. A handler exception cancels that character's increment. `OnCharacterAged` fires only after the new age is stored, and subscriber exceptions do not undo the stored age.
- The family weekly pipeline publishes `Lifespan.CharacterAgedUp`; the `OnCharacterAged` API event covers explicit/API external-character increments. These are separate hooks.
- `GetActiveIllnesses` returns a snapshot. Use `AddIllness` and `RemoveIllness` for changes. Stable built-in IDs are the `lifespan.illness.*` constants; custom IDs are persisted but have no built-in gameplay effect.
- `GetConfiguration` should be treated as read-only. Use `SetConfiguration` to apply a complete configuration; it clamps cross-field invariants and persists through the settings provider when available.

## Ownership Boundaries

- Lifespan owns aging, life-stage transitions, elder illness, natural death, and growth-potential timing.
- Gene Manager owns trait and gene systems. Lifespan may read vanilla traits for aging effects, but it should not become the owner of trait assignment or gene management.
- Family Expansion owns conception, pregnancy, birth, and postpartum handoff. Lifespan only consumes newborn/age integration calls.
- Shared settings UI rendering is owned by ShelteredAPI 2.0. Lifespan only provides settings definitions.

## Events

The mod publishes the following events for inter-mod communication:

- `Lifespan.CharacterAgedUp` - Weekly age update
- `Lifespan.CharacterAgeGenerated` - Age generated for a family member or external character
- `Lifespan.AgeInitialized` - Family age initialized for the first time

### Example Event Subscription:
```csharp
using ModAPI.Events;

ModEventBus.Subscribe<CharacterAgedUpArgs>("Lifespan.CharacterAgedUp", args =>
{
    MMLog.Info($"Character {args.FamilyMemberId} aged to {args.NewAgeWeeks / 52} years");
});
```

## Installation

1. Ensure Sheltered Mod Manager is installed with ModAPI/ShelteredAPI 2.0.
2. Copy the `Lifespan` folder to `Sheltered/mods/`
3. Enable the mod in the Mod Manager
4. **Recommended**: Configure your preferred aging speed in the Settings menu before starting a long playthrough.

## Compatibility

- **Save Compatibility**: Age data is stored per-save. **CRITICAL WARNING**: Disabling the mod mid-playthrough will PERMANENTLY WIPE generational records (death history, illnesses). Any characters who have aged up will reset to young-adult or child baselines if the mod is re-enabled. Ensure this mod remains active for the duration of your save session.
- **New Saves**: Characters will be initialized with appropriate ages based on their child/adult status (approx. 10y for kids, 30y for adults).
- **Existing Saves**: When first loaded, characters will be assigned default ages based on their current status.

## Known Limitations

- Age is tracked in weeks, not individual days
- Stat modifications from illnesses are not fully reversible (no cure system yet)
- Child meshes are limited to game's existing boy/girl models

## Future Plans

- Cure/treatment system for elder illnesses
- Custom events for milestone birthdays

## Credits

Created for the Sheltered Mod Loader with ModAPI/ShelteredAPI 2.0.

## License

This mod is provided as-is for use with Sheltered.
