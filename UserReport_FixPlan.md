# Lifespan User Report - Fix Plan (Lifespan vs ModAPI)

## Scope
This document maps the reported issues to concrete fix tasks, split by ownership:
- **Lifespan mod fixes** (can be done in this repo)
- **ModAPI fixes** (upstream framework work)

It also includes priorities, technical root causes, and test/acceptance criteria.

## Status Note
- You mentioned the **ModRandom seed changing on reload** has already been fixed in ModAPI. Keep this in verification scope, but do not treat it as open unless repro still occurs after the patch.

## Implementation Status (February 22, 2026)
- Completed in Lifespan code:
  - hydration/load guards for age initialization
  - child-to-adult gameplay-first transition + visual retry queue
  - expedition dialogue pacing and low-anxiety filtering
  - unified journal insertion path
  - settings redesign for Simple/Advanced modes, clearer labels, and stronger cross-field clamps
  - multi-field difficulty presets (death + growth + illness knobs)
- Left to ModAPI (unchanged here):
  - settings layout engine, control overlap/font sizing, and category row packing
  - framework-level simple/advanced UI behavior bugs (if present on older ModAPI builds)

## Priority Summary
- **P0 (stability/data integrity):** age persistence and reload consistency, child transition reliability.
- **P1 (gameplay correctness):** newborn movement soft-lock behavior, dialogue frequency/context accuracy.
- **P2 (UX/polish):** settings clarity, journal parity, layout/readability improvements.

## Lifespan Fixes

### 1) Ages randomize after save reload (P0)
**Report:** family member ages re-randomize every reload.

**Likely root causes in Lifespan:**
- `AgeTracker.Reset()` replaces `_saveContainer` with a new object after registration (`Lifespan/AgeTracker.cs:57`, `Lifespan/AgeTracker.cs:58`, `Lifespan/AgeTracker.cs:68`).
- Registration is only done in constructor; resetting to a new container can break linkage with ModAPI save hydration.
- `BaseCharacter_SaveLoadCharacter_Patch` calls `Tracker.GetAgeWeeks(member)` during load (`Lifespan/AgingPatches.cs:96`), which lazily initializes random age if data is not hydrated yet (`Lifespan/AgeTracker.cs:269`, `Lifespan/AgeTracker.cs:281`).

**Fixes:**
- Do not reassign `_saveContainer` in `Reset()`; clear its lists in place.
- Add explicit `isHydrated` guard in `AgeTracker`.
- In `BaseCharacter_SaveLoadCharacter_Patch`, skip age-dependent logic until tracker has loaded save data.
- Add diagnostics: one log line for `container registered`, `hydrated`, `fresh init path taken`.

**Acceptance criteria:**
- Same save loaded 10 times keeps identical ages.
- No member gets freshly initialized during load unless truly new.

### 2) Age 0 character becomes immobile and blocks equipment (P1)
**Report:** age 0 can get stuck and block critical tiles/equipment.

**Current behavior:**
- Newborn movement is hard-zeroed in walk speed patch (`Lifespan/ChildCapabilityPatches.cs:25`, `Lifespan/ChildCapabilityPatches.cs:37`).
- Newborns rely on care jobs; no fallback relocation/unblock system.

**Fixes:**
- Replace absolute immobility with one of:
  - very low crawl speed, or
  - periodic auto-reposition to nearest safe idle node when path-blocking, or
  - "carry to crib/bed" auto-job if available.
- Add anti-soft-lock check: if newborn occupies interactable hotspot >N seconds, trigger unblock behavior.

**Acceptance criteria:**
- Newborn never permanently blocks essential interactions (beds, airlock, workstations).

### 3) Starting ages feel too random / family relationship realism (P1)
**Report:** initial ages can break roleplay plausibility.

**Current behavior:**
- Initial generation uses hardcoded child range `10-17` (`Lifespan/AgeTracker.cs:32`, `Lifespan/AgeTracker.cs:33`, `Lifespan/AgeTracker.cs:98`).
- Adults use broad distribution with cap `18-90` (`Lifespan/AgeTracker.cs:36`, `Lifespan/AgeTracker.cs:37`, `Lifespan/AgeTracker.cs:115`).

**Fixes:**
- Add explicit config toggles for:
  - `UseRoleplayFamilyAgeModel`
  - `InitialChildMin/Max`
  - `InitialAdultMin/Max`
  - optional parent-child gap constraints.
- Keep current random model as optional preset, not forced default.

**Acceptance criteria:**
- "Default family" generation respects configured realism constraints.

### 4) Rare cases where kids do not become adults on threshold (P1)
**Report:** rare non-transition at adulthood age.

**Likely causes:**
- Transition pipeline can fail and swallow exception at top-level (`Lifespan/ChildTransitionManager.cs`, `TransitionToAdult` try/catch).
- Mesh swap fallback may revert to child in failure path.

**Fixes:**
- Add transition state machine with retry queue (`Pending`, `AppliedData`, `AppliedVisuals`, `Completed`).
- If visuals fail, keep gameplay adulthood flag and retry visuals next frame/week.
- Emit warning telemetry for transition failures with member id/age.

**Acceptance criteria:**
- Any child >= adulthood threshold is guaranteed adult in gameplay state.

### 5) Family speech bubbles are too frequent / thematically wrong (P1)
**Report:** too much talking; lines imply someone is missing/expedition/lost while safe.

**Current behavior:**
- Away observation chance is frame-based (`_random.Value() < 0.005f`) in update loop (`Lifespan/ExpeditionDialogueManager.cs:60`).
- Content includes high-anxiety/"lost" style lines in expedition dialogue packs (`Lifespan/Dialogue/Content/ExpeditionDialogue.cs`).

**Fixes:**
- Convert dialogue triggers to time-budgeted cooldowns per speaker/topic, not per-frame probability.
- Add context gate: only use "away" lines if member has been away >X minutes and no active radio contact.
- Add per-category intensity control in config (`Low/Normal/High`).

**Acceptance criteria:**
- Normal shelter operation does not produce noticeable dialogue spam.
- No expedition-loss implication for recently departed/safe away members.

### 6) Journal entries look slightly off from vanilla (P2)
**Report:** font/size/spacing may differ versus vanilla.

**Current behavior:**
- Entries are inserted through reflected `InsertJournalEntry(text, "", false)` in multiple managers (`Lifespan/MilestoneManager.cs:253`, `Lifespan/DialogueScheduler.cs:104`, `Lifespan/ElderIllnessManager.cs`).

**Fixes:**
- Standardize all journal insertion through one wrapper utility.
- Verify exact parameter pattern and formatting used by vanilla-equivalent entries.
- Normalize punctuation/capitalization templates.

**Acceptance criteria:**
- Lifespan milestone and illness entries are visually indistinguishable from vanilla entry style.

### 7) Settings terminology is hard to understand (P2)
**Report:** terms like spark/catch-up/forfeit/death multiplier unclear.

**Fixes:**
- Rename labels to player-facing language, keep technical terms in optional detail tooltip.
- Add one "Gameplay impact" sentence per advanced setting.
- Add an in-repo settings glossary in README.

**Acceptance criteria:**
- First-time user can configure pace/death/illness without external explanation.

### 8) Difficulty only affects one slider (P2)
**Report:** difficulty control seems redundant.

**Current behavior:**
- Difficulty presets currently exist on one field (`deathProbabilityMultiplier`) via `ModSettingPreset` (`Lifespan/LifespanConfig.cs:144-147`).

**Fixes:**
- Either remove preset labeling, or make a real composite preset profile (death + illness + development tuning).

**Acceptance criteria:**
- Difficulty preset causes meaningful multi-parameter behavior changes, or no misleading preset control exists.

### 9) Stage threshold sliders can be set inconsistently (P2)
**Report:** elderly can be set lower than adulthood.

**Current behavior:**
- Has validate methods (`Lifespan/LifespanConfig.cs:19`, `Lifespan/LifespanConfig.cs:22`, `Lifespan/LifespanConfig.cs:252`, `Lifespan/LifespanConfig.cs:261`) plus clamp, but UX can still feel loose depending on panel behavior.

**Fixes:**
- Add hard runtime invariants when applying config.
- Provide immediate inline error text when threshold relation invalid.

**Acceptance criteria:**
- User cannot save or run with `elder <= adult`.

## ModAPI Fixes

### 1) ModRandom reload seed stability (P0 -> verify)
**Status:** you stated this is already fixed.

**Verification required:**
- Confirm seed is stable for same save slot across reloads and not regenerated during settings/UI opens.

### 2) Simple/Advanced mode filter bug in settings panel (P1)
**Existing analysis doc:** `ModAPI_SimpleAdvanced_Bug.md`

**Problem:**
- Toggle visibility checks `Mode`, but filtering uses `ShowInSimpleView/ShowInAdvancedView` flags; mapping is not applied in one code path.

**Fix:**
- In `SettingsController.CreateDefinition(...)`, map `Mode` to visibility flags consistently.
- Optionally make panel toggle visibility depend on effective visibility flags.

### 3) Settings layout/readability constraints (P2)
**Report:** overlapping text, small font/sliders, overcrowded two-column layout, headings mixed with controls.

**Needed upstream capabilities in ModAPI:**
- Responsive one-column fallback for dense mods.
- Header rows that consume full width and do not share row with fields.
- Auto-wrap/ellipsis rules preventing label-toggle overlap.
- Configurable base font scale and control scale per panel.
- Better pagination/group packing so a category can stay on a single page when possible.

### 4) Cross-field constraint UX support (P2)
**Problem:** mods can validate in code, but UI support for linked sliders and inline relation constraints is limited.

**Needed features:**
- Declarative relational constraints (`A < B`, `B >= A+1`).
- Dynamic min/max binding between controls.
- Inline validation message next to offending field.

### 5) Better hooks for condition display integration (P2)
**Report:** illnesses should be visible in UI, not only journal.

**Needed ModAPI support:**
- Extensible character status panel hooks (badges/icons/tooltip rows).
- Lightweight API for per-character condition chips with localization.

## Ownership Matrix
- **Lifespan only:** persistence container lifecycle, age hydration timing guards, newborn anti-soft-lock behavior, transition retry reliability, dialogue pacing/context, terminology/docs.
- **ModAPI only:** settings layout engine improvements, simple/advanced mode filtering fix, linked-control UX, character panel extension hooks.
- **Shared verify:** seed stability (ModAPI fix) + Lifespan load order/hydration integration.

## Recommended Execution Order
1. Fix Lifespan persistence/hydration and verify seed-stability behavior end-to-end.
2. Fix newborn soft-lock and child transition reliability.
3. Rebalance dialogue scheduler + expedition context gating.
4. Ship settings text cleanup and glossary.
5. Upstream ModAPI UI/layout/visibility fixes.

## Regression Test Plan
- Save/load loop test: same slot loaded repeatedly, ages identical.
- Child lifecycle test: newborn -> mobile -> child -> adult with no stuck state.
- Transition fault injection test: mesh swap failure still yields gameplay adulthood.
- Dialogue rate test: max speech bubbles per 10 minutes bounded.
- Settings UX test: no overlaps at common resolutions, relational constraints enforced.
