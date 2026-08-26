# Class Skill External VFX Verification

Date: 2026-08-26
Unity: 6000.3.6f1 / URP 17.3

## Implemented presentation map

| Class | Skill | Presentation | External layers |
|---|---:|---|---|
| Melee | 1 | Crescent empowered slash | Magic Hit 2, Basic Hit 7, Unity Sparks |
| Melee | 2 | Heavenfall ground impact | Basic Hit 8, Magic Hit 2, Unity Smoke/Sparks |
| Melee | 3 | Tempest spin | 3x Shadow Hit, Unity Sparks |
| Melee | 4 | Thunder thrust | Lightning Hit Blue, Magic Hit 2, Unity Lightning |
| Gun | 1 | Twin rapid flash | 2x Basic Hit 7, Unity Sparks |
| Gun | 2 | Deadeye radial detonation | Basic Hit 8, Lightning Hit Blue, Unity Smoke |
| Gun | 3 | 360-degree storm cylinder | 4x Lightning Hit Blue, Unity Sparks |
| Gun | 4 | Skyfire dual barrage | 4x Fire Hit, Unity Sparks/Smoke |

All compositions add bounded pooling, motion, scale punch, HDR bloom, a short point-light
pulse, and external-source markers. The legacy runtime type name is retained for save
compatibility, but no Blender-authored class-skill model is loaded or packaged.

## Idle correction

The zero-speed locomotion child in `Assets/Animations/MeleeCat/MeleeCat.controller` now
uses the compatible walk motion at 0.12x instead of the T-pose-like idle clip.

Final standalone measurements:

- Left hand below upper arm: 0.3094 world units
- Right hand below upper arm: 0.3093 world units
- Natural idle gate: Passed

## External sources

- Hit Effects FREE by Matthew Guz, Unity Asset Store Standard EULA.
- Unity Visual Effect Graph Samples, Unity Companion License.
- Runtime-addressable copies and license notices are under
  `Assets/Resources/SkillVFX/External`.
- Obsolete, unreferenced `Assets/Resources/SkillVFX/Blender` class-skill FBX assets were
  removed before the final clean build.

## Verification

- PlayMode public-input test:
  `BlenderSkillVfxPlayModeTests.PublicKeyboardAndMouseFlow_PlaysAllEightExternalEffects`
  - Result: Passed (1/1)
  - Covers actual keyboard keys 1-4, melee skill-1 left click follow-up, all eight event
    codes, external prefab counts, 48-object bounded pool, source markers, post-processing,
    and natural idle bone geometry.
- Final Windows64 development build:
  `Artifacts/ClassSkillExternalVFX/Build/CatDream.exe`
  - Result: Succeeded, 0 errors.
- Final standalone executable verification:
  `Artifacts/ClassSkillExternalVFX/FinalStandaloneVerification-20260826-234004/skill_vfx_build_report.json`
  - Result: Passed; process exit code 0.
  - Blender model count: 0
  - Hit Effects prefabs: 6
  - Unity VFX Graph prefabs: 3
  - Eight skill entries passed; invalid shader: false for all entries
  - Runtime error-pattern matches: 0

