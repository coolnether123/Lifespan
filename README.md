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
Characters now grow and learn naturally over time based on a unique **Development Gene**:
- **Child Growth**: Children have a high potential for learning (4x faster than adults) and can gain significant stats before adulthood.
- **Adult Development**: Adults grow slower but steady.
- **Spark Mechanic**: Happy characters (low stress) have a chance to experience a "Spark" — a breakthrough that grants double stat points instantly.
- **Skill Fatigue**: Characters are encouraged to be well-rounded. Gaining a specific stat reduces the chance of gaining that same stat again immediately.
- **Use It or Lose It**: Characters have a genetic potential cap. If they don't develop their skills before reaching the next life stage (e.g. becoming an Elder), that potential is lost forever.

### Child-to-Adult Transition
When a child reaches adulthood:
- Character mesh automatically swaps from boy/girl to man/woman
- Physical appearance is preserved (colors, features)
- Stats are recalculated for adult caps
- Journal entry is created to commemorate the event
- Movement speed and behaviors update

### Elder Illness System
Once characters reach elder age (default: 65 years), they become susceptible to age-related illnesses:

#### Available Illnesses (all configurable):
1. **Dementia** - Reduces Intelligence stat
2. **Arthritis** - Reduces movement speed
3. **Heart Disease** - Weekly chance of heart attacks (can be fatal)
4. **Frailty** - Reduces Strength stat
5. **Respiratory Issues** - Affects stamina

#### Illness Mechanics:
- Base 3% weekly chance (configurable)
- Modified by current health (lower health = higher chance)
- Illnesses stack (characters can have multiple)
- Each illness applies permanent effects
- All illnesses persist across saves

### Death from Old Age
At the death risk threshold (default: 75 years):
- Base 1% weekly death chance (configurable)
- Increases by 0.5% per year over threshold
- Modified by:
  - Current health (lower health = higher risk)
  - Number of active illnesses
  - Trauma/stress levels
- Death cause is recorded (old age or specific illness)
- Journal entry created upon death

## Configuration (New!)

All settings are now fully integrated into the game's **Mod Manager UI**. You can adjust these values on the fly without restarting the game.

### Life Stages
- **Adulthood Age**: Age at which children become adults. (Default: 18)
    - *Constraint:* Must be strictly greater than "Default Child Start Age".
- **Elderly Age**: Age at which characters become susceptible to illness and death. (Default: 60)
    - *Constraint:* Must be strictly greater than "Adulthood Age".

### Aging Speed
- **Weeks Between Aging**: How real-time weeks correspond to biological aging checks. (Default: 1 week)
- **Biological Weeks Per Tick**: How much older a character gets per check. (Default: 52 weeks / 1 year)

### Visuals
- **Hair Greying**: Toggle to enable dynamic hair color fading based on genetics and age.

### Elder Illnesses
Completely toggle individual illnesses on/off:
- **Dementia**: Reduces Intelligence.
- **Heart Disease**: Risk of heart attacks in high stress.
- **Arthritis**: Reduces movement speed.
- **Frailty**: Reduces Strength.
- **Respiratory Issues**: Affects stamina.

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

## Events

The mod publishes the following events for inter-mod communication:

- `Lifespan.CharacterAgedUp` - Weekly age update
- `Lifespan.BecameAdult` - Child became adult
- `Lifespan.BecameElder` - Reached elder threshold
- `Lifespan.ElderIllnessAcquired` - Acquired an age-related illness
- `Lifespan.DiedOfOldAge` - Died from old age

### Example Event Subscription:
```csharp
using ModAPI.Events;

ModEventBus.Subscribe<BecameAdultEventArgs>("Lifespan.BecameAdult", args =>
{
    MMLog.Info($"Character {args.FamilyMemberId} became an adult at age {args.AgeYears}");
});
```

## Installation

1. Ensure Sheltered ModAPI v1.0.1 is installed
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

Created using the Sheltered Mod Loader ModAPI v1.1

## License

This mod is provided as-is for use with Sheltered.
