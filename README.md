# 냥발스럽게 · CatDream

> AI-assisted destruction action prototype · Unity 6 · three cat classes · story/co-op validation

![냥발스럽게 title screen](media/01_title.png)

`냥발스럽게` is a Unity 6 action prototype in which cats grow stronger by smashing objects across Seoul-inspired stages. It combines story progression, three playable classes, upgrade/save systems, recruitable companions, and a cooperative building-destruction mode.

This is a recruiter-oriented source showcase. The private working project is not reproduced here because it contains third-party packages, service configuration, generated builds, and more than 130GB of assets and validation artifacts.

## My role

- **Co-planning (about 50%)** with a teammate at **Surge Games / 서지게임즈**
- Co-designed the destruction loop, Seoul stage progression, class identities, upgrades, companion growth, and cooperative modes
- Converted features and bugs into goal-based tasks with observable acceptance criteria
- Directed and reviewed an AI-assisted implementation workflow in Unity
- Led hands-on QA through public keyboard/mouse input, two-process multiplayer runs, screenshots, logs, numeric checks, and regression reports

AI accelerated implementation. I am not claiming every line was manually typed; the portfolio focuses on the decisions, integration work, reproducible validation, and code that resulted from that workflow.

## Project snapshot

| Item | Detail |
|---|---|
| Engine | Unity 6000.3.6f1, URP 17.3, C# |
| Genre | Third-person destruction action / cooperative score attack |
| Content | Six Seoul-themed stages, story route, building-destruction battle |
| Classes | Basic, Melee, Gun |
| Systems | combat skills, upgrades, save migration, companions, weapon attachment |
| Multiplayer | Photon-based room flow, remote avatar state, host-authoritative outcomes |
| QA | PlayMode suites, development-build probes, two-process host/client evidence |

## Start here

- [Portfolio case studies](PORTFOLIO.md)
- [Selected source index](SOURCE_INDEX.md)
- [Class-skill VFX verification](Evidence/CLASS_SKILL_EXTERNAL_VFX_REPORT.md)
- [Story/co-op final report](Evidence/STORY_COOP_FINAL_REPORT.md)
- [Co-op regression report](Evidence/COOP_REGRESSION_REPORT.md)

### Recommended code tour

1. [MeleeCatCombatRuntime.cs](Source/Classes/MeleeCatCombatRuntime.cs) — combo buffering, shield dash, four skills, arc/sphere/capsule damage
2. [GunCatCombatRuntime.cs](Source/Classes/GunCatCombatRuntime.cs) — ammo, aiming, pooled projectiles, spin and barrage skills
3. [BuildingBreakDirector.cs](Source/BuildingBreak/BuildingBreakDirector.cs) — host-driven stage ladder and duplicate-transition guard
4. [BuildingBreakPlayerProgress.cs](Source/BuildingBreak/BuildingBreakPlayerProgress.cs) — class-isolated upgrades, economy, migration, session-only bombs
5. [CoopNetworkPlayerAvatar.cs](Source/Coop/CoopNetworkPlayerAvatar.cs) — remote animation, held items, skills, companions, and reliable state repair
6. [SaveSystem.cs](Source/Core/SaveSystem.cs) — story axes, upgrade migration, equipment, stage and settings persistence
7. [WeaponAttachmentProfile.cs](Source/Core/WeaponAttachmentProfile.cs) — stable geometry signatures and per-weapon validation records

## Gameplay evidence

### Cooperative building-destruction room

![Cooperative building-destruction room](media/02_building_break_room.png)

### Melee-class skill composition

![Melee class external VFX](media/03_melee_skill.png)

### Gun-class barrage

![Gun class barrage](media/04_gun_skill.png)

### Battle HUD and skills

![Building destruction battle HUD](media/05_battle_hud.png)

### Authored final-stage boss intro

![Stage 6 boss intro](media/06_stage6_boss.png)

## Recorded verification

The included evidence was produced in the original project and is dated; this curated repository is not a runnable Unity checkout.

- Eight class skills: public-input PlayMode test Passed; Windows development build succeeded with 0 build errors; standalone verifier Passed
- Grounding and traversal: three classes passed Stage 4 spawn/bridge checks, with maximum recorded ground error of 0.01m
- Multiplayer: host/client timer agreement, room recreation/rejoin, forfeit outcome, and class-introduction/VFX checks recorded as Passed
- Story co-op: remote skills, movement states, simultaneous-hit authority, rollback, upgrades, stage completion, forced host/client exit recovery, and multiple resolutions were exercised

## Known limitations

- A dated co-op report found duplicate contribution rows on the failure-result screen. That subcase remains **Failed** in the evidence instead of being hidden.
- A story co-op performance run recorded poor Stage 4 two-player 1% lows and marked the performance target **Failed**.
- The original build reports contain non-blocking warnings that still need cleanup.
- This source selection cannot be compiled alone; current standalone status here is **NotValidated**.

## Rights and reuse

Screenshots contain licensed third-party models, effects, fonts, and environment assets. Their original files, Photon/PlayFab settings, local packages, builds, and credentials are excluded. See [RIGHTS.md](RIGHTS.md).
