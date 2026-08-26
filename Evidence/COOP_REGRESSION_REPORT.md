# CoopFix13 잔여 검증 회수 최종 보고서

검증 기준: Unity 6000.3.6f1 / StandaloneWindows64 Development 빌드. 2026-08-15.

결론: PART 1, 2, 3, 4, 6, 7과 PART 5의 랭킹/HUD는 통과했다. PART 5 실패 결과 화면은 4개 해상도 모두 도달했지만 2인 방에서 기여도 행이 3~4개 생성되는 동일 결함이 반복되어 **Failed**다. 지시서의 “동일 오류 3회 이후 해당 세부 항목 Failed 처리” 규칙에 따라 추가 수정 없이 후속 작업으로 남겼다.

| # | 항목 | 변경 파일/오브젝트 | 컴파일 | 검증 방법 | 결과 |
|---|---|---|---|---|---|
| 1 | 업그레이드 종단·재시작 영속성 | `CoopFix13FollowupUpgradeVerifier.cs`(Development 전용) | 성공 | 빌드 2회 OS 프로세스 + 2 vs AI + 스토리 복귀 | **Passed** |
| 2 | 스토리 협동 업그레이드 반영 | `MultiplayerUIBuildVerifier_Followup.cs` 검증 분기 | 성공 | 빌드 2인스턴스, Host Lv.3 / Client Lv.0 비교 | **Passed** |
| 3 | 스테이지1→2→3 연속 진행 | 검증 배선만 변경 | 성공 | 빌드 2인스턴스, 두 결과/컷신/전환 | **Passed** |
| 4 | 공격 20회 렌더러 프레임 계수 | `MultiplayerUIBuildVerifier_Followup.cs`, `MultiplayerUIBuildVerifier.cs` | 성공 | 하우스·스테이지4, 양쪽 20공격×3프레임 CSV | **Passed** |
| 5 | 1인 스토리 종단 회귀 | 기존 게임 코드 변경 없음 | 성공 | 정식 UI 종단 + 실제 Input System 조작 | **Passed** |
| 6 | 랭킹 탭 3해상도 | 검증 배선만 변경 | 성공 | 2인스턴스, 싱글/멀티 제목·데이터 전환 | **Passed** |
| 7 | 파괴 대전 HUD 3해상도 | `CoopItems10BuildVerifier.cs` 검증 타임아웃 | 성공 | 2 vs AI, 180초 게임 시뮬레이션·실제 캡처 | **Passed** |
| 8 | 실패 결과 화면 4해상도 | 기존 결과 UI 관찰 | 성공 | 시간 초과 유도, 2인스턴스 | **Failed** — 기여도 행 중복 |
| 9 | BattleNetwork 장시간 루프 | `CoopItems10BuildVerifier.cs` | 성공 | 원인 대조 + 45초 실시간 가드 + 2 vs AI 대체 | **Passed** |
| 10 | Development 빌드/로그 | `Build/CoopFix13Followup.exe` | 성공 | 최종 빌드 + 양쪽 로그 전수 검색 | **Passed** |

## PART 1 — 업그레이드 종단

| 수치 | 스토리 구매 후 | 파괴 대전 2 vs AI | 스토리 복귀 | 게임 재실행 |
|---|---:|---:|---:|---:|
| 이동 속도 | 7.80 | 6.00 | 7.80 | 7.80 |
| 공격력 | 22.00 | 28.00 | 22.00 | 22.00 |
| 공격 범위 | 1.84 | 1.80 | 1.84 | 1.84 |
| 쿨다운 | 0.196 | 0.35 | 0.196 | 0.196 |
| 동료 | 3 | 0 | 3 | 3 |
| 공중 무기 | 3 | 0 | 3 | 3 |
| 해금 스킬 | 4 | 1 | 4 | 4 |
| 츄르폭탄 | 5 | 0 | 5 | 5 |
| 재화 | 384,484 | 384,484 | 384,484 | 384,490 |

- 개발 빌드에서 원본 334,484에 50,000을 임시 주입했다.
- 재실행 후 +6은 장면 로드 직후 동료가 주변 물건을 파괴해 획득한 값이다. 차감은 없었고 업그레이드·폭탄·동료·공중 무기 수치는 완전 일치했다.
- 검증 종료 후 원본 재화 334,484, 폭탄 20개, 전체 업그레이드 레벨을 복원했다. `originalSaveRestored=true`.
- 파괴 대전에서 실제 T/E 입력 후 `UpgradePanel` 비활성: `upgradePanelBlocked=true`.
- 스토리 협동 별도 2인 검증: Host `base_damage Lv.3`, 피해 15.60, 8타 파괴 / Client Lv.0, 피해 12.00, 11타 파괴. 양쪽 `PASS story_coop_followup_upgrades`.
- 증거: `Part1_UpgradeCycle_Final2/upgrade_cycle_report.json`, `Part1_StoryCoopUpgrades_Rerun/`.

## PART 2 — 스테이지1→2→3 연속 진행

| 진입/전환 | Host | Client | 결과 UI 잔존 | 컷신 | 목표 |
|---|---|---|---|---|---:|
| 스테이지1 결과 | 패널 1, 다음 활성 | 패널 1, 다음 비활성·대기 안내 | 정상 | image 10 양쪽 | 114 |
| 1→2 | remote=1 | remote=1 조건 통과 | `stale_result=False`, 패널 0 | 양쪽 확인 | 252 |
| 스테이지2 결과 | 패널 1, 다음 활성 | 패널 1, 다음 비활성·대기 안내 | 정상 | image 11 양쪽 | 253 파괴 집계 |
| 2→3 | remote=1 | remote=1 조건 통과 | `stale_result=False`, 패널 0 | 양쪽 확인 | 324 |

Client의 마지막 로그 시점에는 Host가 검증 종료 후 먼저 Disconnect하여 `remote=0`으로 출력됐지만, 그 직전 `completion_cycle_2_next` 대기 조건 자체가 remote=1을 요구했고 통과했다. 두 전환과 세 스테이지 진입은 Passed다.

증거: `Part2_Completion_Rerun/`.

## PART 3 — 공격 20회 렌더러 계수

캐릭터 렌더러 집합은 공격 전 고정했다. 집은 물건이 손 소켓 아래로 재부모화되어 추가되는 Renderer는 캐릭터 중복이 아니므로 계수에서 제외했다.

| 공간 | 관찰 화면 | 공격 수 | 프레임 수 | 로컬 활성 | 원격 활성 | 결과 |
|---|---|---:|---:|---:|---:|---|
| 하우스 | Host | 20 | 60 | 1→1 | 1→1 | Passed |
| 하우스 | Client | 20 | 60 | 1→1 | 1→1 | Passed |
| 스테이지4 | Host | 20 | 60 | 1→1 | 1→1 | Passed |
| 스테이지4 | Client | 20 | 60 | 1→1 | 1→1 | Passed |

대상: punch 3종, grab/equip, Sword A/B, 스킬 1~4, 추가 punch 9회. CSV 4개와 각 화면의 최초 공격 연속 3프레임 캡처를 `Part3_RendererFrames_Final/`에 저장했다.

## PART 4 — 1인 스토리 종단 회귀

- 정식 경로: Title → 게임시작 → 모드 선택 → 스토리 → 1인 → 인트로/하우스 → 침대/스테이지 선택 → 스테이지1 → 결과 → 스테이지2.
- 스테이지1 `114/114`, 결과 패널 정상, 기여도 행 없음, 싱글 랭킹 표시.
- 실제 Input System 입력: 자동 플레이 비활성, WASD, 맨손 입력 3회(최대 적중 6), 우클릭 파지, F 장착, 스킬 1~4, R 츄르폭탄, 설정 품질 왕복. 런타임 오류 0.
- 거대냥은 실제 스킬 4 입력으로 발동했고, 공중 무기는 저장 업그레이드 상태에서 동작했다. 별도 정합 수치 재측정은 선행 CoopFix13의 확정 Passed 근거를 유지했다(재작업 금지 항목).
- 실제 이어하기 클릭 후 하우스로 직접 진입했으며 인원 선택 패널은 열리지 않았다.
- 저장 대조: 결과 전후 재화 326,020→331,382, 이어하기 후 331,382 유지, 해금 스테이지 6 유지.
- 스테이지5 성능 표본: 평균 48.6 FPS / 1% low 11.1 / 평균 Draw Call 649.
- 증거: `Part4_Solo/`, `Part4_Controls/stage1_actual_input_run.json`.

## PART 5 — 해상도 확장

| 해상도 | 랭킹 싱글/멀티 | 파괴 대전 HUD | 실패 화면 프레임/버튼 | 실패 기여도 행 |
|---|---|---|---|---|
| 1920×1080 | 선행 Passed | 선행 Passed | 도달, Host 활성/Client 비활성·대기 | **Failed: 3~4행** |
| 1600×900 | Passed | Passed | 도달, 버튼 상태 Passed | **Failed: 3~4행** |
| 1280×720 | Passed | Passed | 도달, 버튼 상태 Passed | **Failed: 4행** |
| 1280×1024 | Passed | Passed | 도달, 버튼 상태 Passed | **Failed: 3~4행** |

- 랭킹: 각 해상도 양쪽에서 `싱글`/`멀티` 제목이 실제로 바뀌고 `passed=True`. 버튼과 10개 기록은 화면 안에 유지됐다.
- HUD: 좌상단 시간/잔여, 중앙 상단 바, 우상단 미니맵, 효과 패널이 화면 안에 유지됐다. 3종 모두 `local_build_report.json status=Passed`.
- 1280×1024: 5:4에서도 주요 HUD와 랭킹 패널은 잘리지 않았다.
- 실패 화면: 실제 방 인원은 2명이지만 `ParticipantRow` 활성 계수가 Host 3~4, Client 4로 증가했다. 캡처에서도 Host 닉네임 중복 행이 보인다.
- 반복 원인 근거: `MultiplayerGameplayUI.BuildResultExtension`이 기존 `MultiplayerResultExtension` 존재 여부 확인 없이 새 확장과 행을 생성한다. 실패 상태 갱신 중 중복 호출되면 기존 행이 남는다.
- 동일 결함을 4해상도에서 재현하여 이 세부 항목은 지시서 규칙대로 Failed 처리했다.

증거: `Part5_Ranking/`, `Part5_BattleHUD/`, `Part5_Failure/`.

## PART 6 — BattleNetwork 장시간 루프

확정 원인은 검증기 `CoopItems10BuildVerifier`가 `while (Time.time < simulatedDeadline)`만 사용한 것이다. `Time.timeScale=8`이어도 Host에서 AI, 낙하 물건, 두 진영 물리 계산량이 커지면 게임 시간 진행이 매우 느려져 실시간 상한 없이 장시간 대기했다. 프로덕션 코드는 변경하지 않았다.

- 검증기에 45초 실시간 상한을 추가하고 `simulationTimedOut`, `simulationRealtimeSeconds`를 보고하도록 했다.
- 지시서 결정표대로 BattleNetwork 하네스 자체 수정 대신 2 vs AI 경로로 대체 검증했다.
- 1600×900 / 1280×720 / 1280×1024 모두 180초 게임 시뮬레이션이 실시간 22.51~22.53초에 끝났고 `simulationTimedOut=false`, 예외 0.
- 각 실행에서 파괴 200, 아이템 드롭 22(11%), 지면 미스 0, 10종 효과 적용, 스킬 4개 호출을 확인했다.

## 변경 파일

- `Assets/Scripts/Coop/CoopItems10BuildVerifier.cs` — 검증 루프 실시간 가드와 보고 필드, 실행 해상도 인자 준수.
- `Assets/Scripts/Coop/Net/MultiplayerUIBuildVerifier_Followup.cs` — 연속 결과/랭킹/실패/업그레이드/렌더러 검증 분기.
- `Assets/Scripts/Coop/Net/MultiplayerUIBuildVerifier.cs` — 하우스 렌더러 검증 진입 배선.
- `Assets/Scripts/CoopFix13FollowupUpgradeVerifier.cs` — Development 전용 업그레이드 종단·재시작·원복 검증기.

모든 추가 검증 코드는 `DEVELOPMENT_BUILD || UNITY_EDITOR` 조건부다. 확정된 13개 프로덕션 수치와 HUD 규격은 변경하지 않았다.

## 빌드 및 로그

- 최종 빌드: `ValidationReports/CoopFix13Followup/Build/CoopFix13Followup.exe`
- 구성: StandaloneWindows64 Development
- 최종 빌드 작업: `build-9737ea42f2`
- 결과: Succeeded, 42.779초, 보고 크기 3,710.52 MB, errors 0, warnings 16.
- 디스크 실제 폴더: 368파일, 3,891,128,346 bytes(3.624 GiB).
- PART 1~5의 채택 로그 전수 검색: NullReference / IndexOutOfRange / ArgumentOutOfRange / InvalidOperation / error CS / crash 0.
- 모든 최종 2인스턴스 프로세스 정상 종료. 실패 화면 검증도 결과를 기록한 뒤 종료 코드 0.
- Unity Editor 최종 콘솔 Error 0, `Assets/Scenes/TitleScene.unity` 저장 완료.

## Failed / 후속 작업

1. **실패 결과 화면의 기여도 행 중복**: `MultiplayerResultExtension` 생성 경로를 idempotent하게 만들고, 2인 방에서 활성 `ParticipantRow`가 정확히 2인지 4해상도 재검증이 필요하다.
2. 실패 화면 자체는 4해상도 모두 도달했고 잘림·말줄임·Host/Client 버튼 권한은 정상이다. 실패 판정 범위는 중복 행 한 항목이다.

