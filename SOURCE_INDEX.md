# 선별 소스 안내

원본 프로젝트에서 면접 코드 리뷰 가치가 높은 파일을 선별했습니다. 이 저장소는 독립 빌드가 아니라 코드 검토용입니다.

| 영역 | 파일 | 확인할 내용 |
| --- | --- | --- |
| 스테이지 | [StageManager.cs](Source/Core/StageManager.cs) | 6개 스테이지 메타데이터, 제작형·절차형 구성, 안전 스폰 |
| 저장 | [SaveSystem.cs](Source/Core/SaveSystem.cs) | 스토리 축, 클래스 마이그레이션, 장비와 초기화 경계 |
| 업그레이드 | [UpgradeDatabase.cs](Source/Core/UpgradeDatabase.cs) | 비용, 효과, 클래스 정의와 추천 |
| 무기 부착 | [WeaponAttachmentProfile.cs](Source/Core/WeaponAttachmentProfile.cs) | 지오메트리 해시, 무기별 프로필과 JSON/CSV 증거 |
| 동료 | [CatCompanionDirector.cs](Source/Core/CatCompanionDirector.cs) | 포메이션, 스폰, 지원 대상과 복제 시각 요소 |
| 클래스 | [PlayerClassRuntime.cs](Source/Classes/PlayerClassRuntime.cs) | 런타임 클래스·외형 전환과 접지 |
| 근접 전투 | [MeleeCatCombatRuntime.cs](Source/Classes/MeleeCatCombatRuntime.cs) | 콤보, 방패 돌진, 스킬과 타격 지오메트리 |
| 총기 전투 | [GunCatCombatRuntime.cs](Source/Classes/GunCatCombatRuntime.cs) | 조준, 탄약, 투사체 풀과 회전·난사 스킬 |
| 전투 단계 | [BuildingBreakDirector.cs](Source/BuildingBreak/BuildingBreakDirector.cs) | 목표 생명주기와 중복 단계 전환 방지 |
| 전투 경제 | [BuildingBreakPlayerProgress.cs](Source/BuildingBreak/BuildingBreakPlayerProgress.cs) | 클래스별 성장, 피해·골드, 세션 소모품 |
| 원격 아바타 | [CoopNetworkPlayerAvatar.cs](Source/Coop/CoopNetworkPlayerAvatar.cs) | Photon 직렬화와 원격 표현 복구 |
| 이름표 | [CoopNetworkNameplate.cs](Source/Coop/CoopNetworkNameplate.cs) | 거리 크기, 가림, 가독 범위 |
| 스킬 QA | [ClassSkillVfxPlayModeTests.cs](Tests/PlayMode/ClassSkillVfxPlayModeTests.cs) | 공개 입력 스킬 이벤트와 제한된 VFX 풀 |
| 이동 QA | [Stage4ClassGroundBridgeAmmoDropPlayModeTests.cs](Tests/PlayMode/Stage4ClassGroundBridgeAmmoDropPlayModeTests.cs) | 3개 클래스 접지와 다리 이동 |
| 입력·성장 QA | [ClassProgressionAndInputPlayModeTests.cs](Tests/PlayMode/ClassProgressionAndInputPlayModeTests.cs) | 클래스 분리, 성장, 방패 입력, 줍기와 슬라이더 |

## 면접관용 10분 검토 동선

1. README의 1분 요약과 실패 공개를 확인합니다.
2. MeleeCatCombatRuntime.cs와 GunCatCombatRuntime.cs를 비교합니다.
3. BuildingBreakDirector.cs의 중복 전환 방지를 확인합니다.
4. CoopNetworkPlayerAvatar.cs에서 원격 상태와 복구 경로를 확인합니다.
5. SaveSystem.cs의 영구·세션 상태 경계를 보고 QA 보고서와 연결합니다.

## 제가 코드 리뷰에서 설명할 부분

- 전투 판정을 호·구·캡슐로 나눈 이유
- 호스트만 스테이지 결과를 확정하도록 만든 이유
- 원격 표현에서 연속 상태와 이벤트 상태를 구분하는 방법
- 클래스별 저장키 마이그레이션과 롤백 전략

