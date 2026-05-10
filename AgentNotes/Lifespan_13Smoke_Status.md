# Lifespan 1.3 Final Smoke Status

Date: 2026-05-10
Branch: `main`
Repo state: refreshed with `git fetch --all --prune`; branch is aligned with `origin/main`.

## Verification

- Read project guidance from the user-provided AGENTS instructions. No repo-local `AGENTS.md` was present.
- Read `README.md`, `UserReport_FixPlan.md`, and `ModAPI_SimpleAdvanced_Bug.md`.
- Inspected recent 1.3 compatibility commits covering save container ownership, weekly aging service extraction, external aging API, child care/dialogue boundaries, and settings/test updates.
- Built with Visual Studio MSBuild:
  - `Lifespan.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal`
  - Result: success.
- Ran the NUnit assembly with Visual Studio Test Platform:
  - `vstest.console.exe Lifespan.Tests\bin\Debug\net472\Lifespan.Tests.dll /InIsolation`
  - Result: 63 discovered, 60 passed, 3 skipped. The skipped tests are already marked disabled in the repo for stale DLL issues.
- Checked patched private members and method names against the shared decompiled Sheltered source:
  - `BaseCharacter.SaveLoadCharacter(SaveData)`
  - `NpcVisitManager.CreateNpcVisitor(NpcVisitor.NpcType, FamilySpawner.CharacterAttributes, Vector3)`
  - `FamilyManager.AdoptNpc(NpcVisitor)`
  - `familyId`, `m_npcId`, `m_child`, `m_characterMeshId`, `m_callbackAction`

## Smoke Findings

- Save/load: Age data uses a registered ModAPI save container and preserves registered list instances during copy/hydration. Load-time age reads are guarded until hydration completes.
- Aging tick/week processing: Weekly aging is centralized in `WeeklyAgingService`, skips duplicate weeks, respects API cancellation, and includes external live-character aging.
- Child care boundaries: Newborns are slow instead of hard-immobile; care jobs preserve legacy type strings and save/load behavior; expedition departure jobs are not blocked.
- Dialogue boundaries: Dialogue is budgeted and queued. Expedition-away observations are delayed and low-anxiety filtered early in an expedition.
- External aging API: Live external characters are tracked separately from saved external age records, cancellation is respected, and events publish concrete characters.
- ModAPI/ShelteredAPI 1.3 references: Project references ModAPI, ShelteredAPI, 0Harmony, UnityEngine, and Assembly-CSharp. No Dev-1.4 references were found.
- Settings: Runtime validation clamps invalid thresholds. Simple/Advanced UI behavior remains an upstream ModAPI concern documented in `ModAPI_SimpleAdvanced_Bug.md`.
- Actor identity: No new Relationship Manager, Gene Manager, or full actor identity system was added. Lifespan still uses vanilla family/NPC IDs with a small live external-character cache for API events.
- Broad silent catches: Core save/load and weekly compatibility paths log failures. Empty `catch {}` blocks were removed from the mod source during this smoke pass.

## Fixes Made

- Fixed `ILifespanAPI.SetConfiguration` so API-provided settings copy into the shared `LifespanConfig` instance instead of replacing only the API's reference. This keeps weekly aging, child development, illness, death, and other managers aligned with API-driven settings changes.
- Added `LifespanConfig.CopyFrom(...)` and a regression test confirming the shared config instance is updated and clamped.
- Added logging to former silent fallback catches in child transition reflection, hair greying, dialogue speech fallbacks, and radio death biome-name fallback.

## Future Integration Notes

- When SMM ActorSystem is available, consider replacing the obituary name fallback and the API live external-character reference cache with ActorSystem-backed identity lookups. Do not build that system inside Lifespan.
- Keep Relationship Manager and Gene Manager ownership outside Lifespan. Lifespan should continue owning only aging, life-stage transitions, elder illness/death, and growth timing.
- Leave ModAPI settings layout/simple-advanced filtering fixes upstream; Lifespan should continue providing settings metadata only.

## Outcome

Final 1.3 smoke pass completed. The project builds, tests pass except for the existing skipped tests, one clear external settings API bug was fixed, and future integration boundaries are documented.
