# ModAPI Simple/Advanced View Bug (Spine Settings)

## Summary
The `SIMPLE`/`ADVANCED` view buttons can appear, but switching modes may not change the visible settings list.

## Observed Behavior
- Buttons are shown in the settings panel.
- Toggling between `SIMPLE` and `ADVANCED` does not change which settings are displayed.

## Expected Behavior
- `SIMPLE` should show only settings marked for Simple/Both.
- `ADVANCED` should show settings marked for Advanced/Both (and optionally Simple, depending design).

## Repro Conditions
1. A mod defines settings with `ModSettingAttribute.Mode` values.
2. At least one setting is `Simple` and one is `Advanced` (so buttons are shown).
3. Open mod settings and switch mode.
4. The list does not change.

## Root Cause (Code Path)
### 1) Toggle visibility logic uses `Mode`
`ModAPI/UI/ModSettingsPanel.cs`
- `showToggles` is computed from `allDefs.Any(d => d.Mode == SettingMode.Simple)` and `allDefs.Any(d => d.Mode == SettingMode.Advanced)`.
- This part works and shows buttons.

### 2) Actual filtering uses view flags, not `Mode`
`ModAPI/UI/ModSettingsPanel.cs`
- Filtering is done through `SettingsHierarchy.GetFlattenedForView(...)`, which checks:
  - `SettingDefinition.ShowInSimpleView`
  - `SettingDefinition.ShowInAdvancedView`

`ModAPI/Spine/SettingsHierarchy.cs`
- `IsVisibleInView` returns `ShowInSimpleView` or `ShowInAdvancedView`.

### 3) `SettingsController` never maps `Mode` into those flags
`ModAPI/Spine/SettingsController.cs`
- `CreateDefinition(...)` sets `def.Mode`, but does not assign:
  - `def.ShowInSimpleView`
  - `def.ShowInAdvancedView`
- Defaults in `SettingDefinition` are both `true`, so both views render the same list.

## Why It Happens In Practice
There are two mapping implementations in the codebase:
- `SpineSettingsHelper` has a mode->visibility mapping.
- `SettingsController` path (used by normal mods via provider scan) does not.

If a mod uses `SettingsController` definitions, mode switching is effectively non-functional for filtering.

## Upstream Fix Required
Implement mode-to-visibility mapping in `SettingsController.CreateDefinition(...)` (or centralize mapping so all paths use one function).

Suggested mapping:
- `SettingMode.Advanced` -> `ShowInSimpleView = false`, `ShowInAdvancedView = true`
- `SettingMode.Simple` -> `ShowInSimpleView = true`, `ShowInAdvancedView = true` (or false if strict split is intended)
- `SettingMode.Both` -> `ShowInSimpleView = true`, `ShowInAdvancedView = true`

## Optional Cleanup
In `ModSettingsPanel`, toggle visibility should ideally consider effective visibility flags, not only raw `Mode`, to avoid edge-case mismatches.

## Temporary Workaround For Mods
Do not rely on `Mode` filtering until fixed upstream.
Use dependency-based gating (`DependsOnId` + `ControlsChildVisibility`) for an "advanced options" section if needed.
