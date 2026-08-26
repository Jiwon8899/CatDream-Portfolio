# 냥발스럽게 — portfolio case studies

## 1. Three classes with different interaction rhythms

**Design contribution:** The Basic, Melee, and Gun cats were planned as distinct ways to interact with the same destructible world rather than simple stat skins.

- Melee uses combo buffering, directional arcs, shield grab/dash, and close-range area skills.
- Gun uses aim state, ammo, pooled projectiles, radial fire, and a dual-wield barrage.
- Class state is isolated in progression/save logic so one class's purchases do not leak into another.

**Evidence:** [melee runtime](Source/Classes/MeleeCatCombatRuntime.cs), [gun runtime](Source/Classes/GunCatCombatRuntime.cs), [class runtime](Source/Classes/PlayerClassRuntime.cs), and the [VFX report](Evidence/CLASS_SKILL_EXTERNAL_VFX_REPORT.md).

## 2. Destruction as a progression and multiplayer system

**Design contribution:** I co-designed a loop where smashing objects earns currency, unlocks upgrades, and culminates in a building-destruction mode with escalating targets.

**Technical direction:** The director separates the stage ladder from networking so solo and multiplayer can share the same rules. Host-authoritative stage transitions use a last-applied guard to prevent two simultaneous killing blows from advancing twice. Player progression tracks class-specific levels, damage, critical hits, earned gold, and session-only consumables.

**Evidence:** [building director](Source/BuildingBreak/BuildingBreakDirector.cs), [player progression](Source/BuildingBreak/BuildingBreakPlayerProgress.cs), and [room capture](media/02_building_break_room.png).

## 3. Remote state that remains visually meaningful

**Problem:** A networked cat is more than a position. Remote players need stable locomotion, attack variants, held items, skill effects, giant-scale changes, companions, weapons, and visibility repair after animator changes.

**Decision:** Serialize compact gameplay state, apply visual changes on the remote proxy, and send important held-item state through a reliable repair path. Nameplates scale by distance, hide behind occlusion, and disappear beyond the readable range.

**Recorded QA:** Two-process evidence compares owner and remote Idle, BackWalk, RunForward, Jump, and Dash states. Skills 1–4 were repeated ten times, and forced client/host exits were tested for survival and master handoff.

**Evidence:** [network avatar](Source/Coop/CoopNetworkPlayerAvatar.cs), [nameplate](Source/Coop/CoopNetworkNameplate.cs), and [story/co-op report](Evidence/STORY_COOP_FINAL_REPORT.md).

## 4. Persistence boundaries and migration

**Problem:** Story solo, story co-op, three class upgrade sets, equipment, stage progress, settings, and temporary battle items should not all persist in the same way.

**Decision:** Use explicit key prefixes and story axes, migrate older shared upgrades into per-class keys, persist player-owned progression, and keep match-only bombs out of saves. Validation restores captured values after destructive test runs when evidence exists.

**Evidence:** [SaveSystem.cs](Source/Core/SaveSystem.cs), [BuildingBreakPlayerProgress.cs](Source/BuildingBreak/BuildingBreakPlayerProgress.cs), and the upgrade-cycle section of the [co-op regression report](Evidence/COOP_REGRESSION_REPORT.md).

## 5. Weapon attachment as measurable geometry

**Problem:** Different props and weapons cannot share one hand offset. Cat growth, animation, mesh bounds, pivots, and long-weapon shapes can all produce body penetration while still appearing “attached.”

**Decision:** Give every weapon a stable geometry signature and its own attachment profile. Record hand side, object type, pose, clearance result, correction attempts, and screenshot paths; export the result to JSON/CSV.

**Evidence:** [WeaponAttachmentProfile.cs](Source/Core/WeaponAttachmentProfile.cs). This repository does not claim a fresh all-weapon pass; it shows the validation-oriented data model.

## 6. QA that preserves failures

The project reports intentionally retain failed or unresolved results:

- A two-player failure screen generated more participant rows than the actual room size.
- A Stage 4 multiplayer performance sample missed its 1% low target.
- Some original save values could not be safely restored because a failed run occurred before the baseline snapshot.

Keeping these results matters. My workflow uses `Passed`, `Failed`, `NeedsReview`, and `NotValidated` so uncertainty does not become a portfolio claim.

## What I would improve next

- Make result-screen extension creation idempotent and assert exact participant-row count
- Profile and reduce Stage 4 co-op main-thread/physics/renderer cost
- Split large runtime classes into typed combat, presentation, persistence, and network services
- Replace remaining reflection-heavy validation helpers with explicit test adapters
- Establish a warning budget and CI subsets for EditMode/PlayMode tests
- Produce a smaller redistributable vertical slice without licensed source assets
