# Lifespan

Lifespan adds biological ages, child development, elder illness, and natural death to Sheltered. It stores age and health-history data in each save and exposes the data through `com.lifespan.api`.

Version 1.1.4 by coolnether123. Requires Sheltered Mod Manager 2.0, ModAPI 2.0, and ShelteredAPI 2.0.

## Aging

By default, the aging service runs once per in-game week and adds 52 biological weeks. Children below age 10 receive twice that increment when childhood fast aging is enabled.

The default life-stage thresholds are:

- adulthood at 18 years
- elder status at 60 years
- child mobility at 1 year
- regular work at 6 years
- escorted expeditions at 10 years
- solo expeditions at 13 years

You can change the interval, biological increment, life-stage ages, and childhood acceleration in the ShelteredAPI settings panel. Lifespan catches up missed eligible intervals after a save loads without processing the same interval twice.

## Character development

Children and adults can gain stats from a saved development-potential profile. Growth chance depends on life stage, trauma, unused potential, and the configured milestone windows. A breakthrough multiplies one stat gain. Repeating the same stat receives a lower selection weight.

When a child reaches adulthood, Lifespan changes the child mesh to an adult mesh and updates the capabilities that depend on age.

Young children have dedicated care jobs for food, water, toileting, cleaning, and sleep. The jobs use the game's pantry and water resources. A cancelled feeding job returns carried food when the game allows it.

## Elder illness and natural death

At the configured elder age, weekly checks can add dementia, arthritis, heart disease, frailty, or respiratory illness. Illnesses can progress from mild to severe states and persist with the save.

Natural-death checks use age, health, trauma, active illnesses, and the configured difficulty values. Both elder illness and natural death can be disabled in settings.

## NPC ages

Lifespan generates ages for explorers, recruits, traders, and other external characters. The adoption patch transfers a tracked NPC age to the resulting family member when the game uses `FamilyManager.AdoptNpc`.

A mod that creates or adopts characters through another path must register or transfer the age through the API.

## Configure the mod

ShelteredAPI exposes common controls in the simple settings view and the full set in the advanced view.

The simple view includes aging pace, childhood development, natural-death controls, elder illness chance, journal entries, and hair greying. The advanced view includes generated-age ranges, growth tuning, child capability ages, illness progression, severe illness effects, NPC distributions, and debug controls.

## Use the API

The mod registers `ILifespanAPI` as `com.lifespan.api`.

```csharp
using Lifespan;
using ModAPI.Core;

ILifespanAPI lifespan;
if (ModAPIRegistry.TryGetAPI<ILifespanAPI>("com.lifespan.api", out lifespan))
{
    int ageYears = lifespan.GetCharacterAgeYears(member);
    bool isElder = lifespan.IsElder(member);
}
```

Ages are integer biological weeks, and 52 weeks equal one biological year. Age setters clamp negative values to zero. The getter also returns zero for an untracked character, so the public API does not distinguish an unknown age from a newborn.

Family records load at session start. Call age-dependent APIs after the load event. An early family lookup returns zero and does not create a record.

### Track an external character

Call `GenerateAgeForNPC` or `SetCharacterAgeWeeks(BaseCharacter, int)` while the character object is loaded. Lifespan keeps the saved age when the object unloads, but bulk aging and `OnCharacterAged` resume only after another live object is registered for that stable character ID.

```csharp
lifespan.GenerateAgeForNPC(visitor, AgeContext.NPC_Trader);

lifespan.OnBeforeCharacterAged += character =>
{
    return !IsInCryostasis(character);
};

lifespan.OnCharacterAged += (character, newAgeWeeks) =>
{
    RecordExternalAge(character, newAgeWeeks);
};

lifespan.UpdateExternalCharacters(1);
```

Every `OnBeforeCharacterAged` subscriber must return `true` for the increment to proceed. An exception from a subscriber cancels that character's increment. `OnCharacterAged` runs after storage, and subscriber exceptions do not roll the age back.

`GetActiveIllnesses` returns a snapshot. Use `AddIllness` and `RemoveIllness` to change saved illness state. Treat the object from `GetConfiguration` as read-only and pass a complete configuration to `SetConfiguration` when you need to update it.

## Events

Lifespan publishes these ModAPI events:

- `Lifespan.CharacterAgedUp` after the weekly family aging pipeline stores a new age
- `Lifespan.CharacterAgeGenerated` after Lifespan generates an age
- `Lifespan.AgeInitialized` after a family member receives an initial age

The `OnCharacterAged` API event covers explicit and external-character increments. It is separate from `Lifespan.CharacterAgedUp`.

## Install the mod

1. Install Sheltered Mod Manager with ModAPI 2.0 and ShelteredAPI 2.0.
2. Copy the `Lifespan` mod folder into the game's `mods` directory.
3. Enable **Lifespan** in Sheltered Mod Manager.
4. Review the aging interval and biological increment before starting a long save.

## Save warning

Keep Lifespan enabled for any save that uses its age data. Saving with the mod disabled removes access to ages, illness history, development potential, and recorded natural deaths. Re-enabling the mod can then initialize survivors from their current child or adult state instead of restoring the missing history.

## Known limits

- Lifespan stores age in weeks, not individual days.
- Severe illness stat changes do not have a cure system.
- Child and adult transitions use the game's existing character meshes.

## License

Lifespan is available under the [MIT License](LICENSE).
