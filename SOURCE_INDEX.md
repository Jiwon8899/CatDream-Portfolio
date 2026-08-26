# Selected source index

These files are unchanged excerpts from the original project. They are provided for code review, not as a standalone build.

| Area | File | Review focus |
|---|---|---|
| Stage flow | [StageManager.cs](Source/Core/StageManager.cs) | Six-stage metadata, authored/procedural setup, spawn safety |
| Persistence | [SaveSystem.cs](Source/Core/SaveSystem.cs) | Story axes, class migration, equipment, reset boundaries |
| Upgrade data | [UpgradeDatabase.cs](Source/Core/UpgradeDatabase.cs) | Costs, effects, class definitions, recommendations |
| Attachments | [WeaponAttachmentProfile.cs](Source/Core/WeaponAttachmentProfile.cs) | Geometry hashing, per-weapon profiles, JSON/CSV evidence |
| Companions | [CatCompanionDirector.cs](Source/Core/CatCompanionDirector.cs) | Formation, spawning, support targeting, cloned visuals |
| Class selection | [PlayerClassRuntime.cs](Source/Classes/PlayerClassRuntime.cs) | Runtime visual/class switching and grounding |
| Melee combat | [MeleeCatCombatRuntime.cs](Source/Classes/MeleeCatCombatRuntime.cs) | Combos, shield dash, skills, hit geometry |
| Gun combat | [GunCatCombatRuntime.cs](Source/Classes/GunCatCombatRuntime.cs) | Aiming, ammo, projectile pool, spin/barrage skills |
| Battle stages | [BuildingBreakDirector.cs](Source/BuildingBreak/BuildingBreakDirector.cs) | Target lifecycle and duplicate-transition protection |
| Battle economy | [BuildingBreakPlayerProgress.cs](Source/BuildingBreak/BuildingBreakPlayerProgress.cs) | Per-class upgrades, damage/gold, session consumables |
| Remote avatar | [CoopNetworkPlayerAvatar.cs](Source/Coop/CoopNetworkPlayerAvatar.cs) | Photon serialization and remote presentation repair |
| Nameplates | [CoopNetworkNameplate.cs](Source/Coop/CoopNetworkNameplate.cs) | distance scaling, occlusion, readable range |
| Skill QA | [ClassSkillVfxPlayModeTests.cs](Tests/PlayMode/ClassSkillVfxPlayModeTests.cs) | public-input skill events and bounded VFX pool |
| Traversal QA | [Stage4ClassGroundBridgeAmmoDropPlayModeTests.cs](Tests/PlayMode/Stage4ClassGroundBridgeAmmoDropPlayModeTests.cs) | three-class grounding and bridge traversal |
| Input/progression QA | [ClassProgressionAndInputPlayModeTests.cs](Tests/PlayMode/ClassProgressionAndInputPlayModeTests.cs) | class isolation, growth, shield input, pickup and slider behavior |
