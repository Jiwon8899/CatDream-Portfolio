# 스토리 협동 잔여 검증 최종 보고서

- 대상: Unity 6000.3.6f1 / StandaloneWindows64
- 검증일: 2026-08-15
- 결론: 기능 검증 PART 1~7, 9~10은 통과. PART 8 UI 해상도는 통과했으나 성능 1% low는 기존 이슈 구간보다 낮아 `Failed`이다.
- Unity 컴파일: Error 0
- 실제 플레이 로그의 hard exception: 최종 증거 세트 12개 모두 0

| # | 항목 | 변경 파일/오브젝트 | 컴파일 | 검증 방법 | 결과 |
|---|---|---|---|---|---|
| 1 | 원격 스킬 1~4 시각화 | `StoryCoopRuntimeBridge`, `CatSkillEffectRuntime`, `CoopNetworkSyncManager` | Passed | 2인 빌드, 스킬별 10회 | Passed |
| 2 | 츄르폭탄 수동/자동 원격 시각화와 Host 판정 | `SecondDevelopmentRuntime`, `ChuruBombAirstrikeRuntime`, `CoopNetworkSyncManager` | Passed | 수동 10회, 자동 3회 | Passed |
| 3 | 원격 이동 애니메이션 상태/클립 | `CoopNetworkPlayerAvatar` | Passed | Idle/BackWalk/RunForward/Jump/Dash 대조 | Passed |
| 4 | 동시 동일 물건 타격과 예측 롤백 | `CoopNetworkSyncManager` | Passed | 20회 동시 요청, 강제 reject 3회 | Passed |
| 5 | 개인 업그레이드 격리 | 개발 검증기 | Passed | Host Lv.3, Client Lv.0 주입 후 수치/타수 대조 | Passed |
| 6 | 닉네임 거리/가림 | `CoopNetworkNameplate` | Passed | 5/20/40/45m와 벽 가림 | Passed |
| 7 | Stage 1 완주·기여도·다음으로 2회 | 개발 검증기 | Passed | 2인 정식 UI 흐름 | Passed |
| 8 | 참가자/방장 강제 종료 후 생존 진행 | `CoopNetService` (`PlayerTtl=0` 명시) | Passed | 프로세스 강제 종료 2방향 | Passed |
| 9 | 1인 완주·다음으로·이어하기 | `StorySoloFollowupBuildVerifier` | Passed | 정식 UI와 실제 Continue 클릭 | Passed |
| 10 | 다해상도 | 개발 검증기 요청 해상도 재적용 | Passed | 1600×900, 1280×720, 1280×1024 각 2인·2회차 | Passed |
| 11 | 성능 | 개발 전용 ProfilerRecorder | Passed | Stage4 2인 3회 중앙값, Stage5 1인/2인 비교 | **Failed** |
| 12 | Development/Release 빌드·종료 | 빌드 산출물 | Passed | 개발 종료 인자, Release UI 실제 클릭 | Passed |

## PART 1. 스킬·츄르폭탄 원격 시각화

| 항목 | 소유자 결과 | 원격 결과 | 공유 판정 |
|---|---:|---:|---|
| 스킬 1 | 10/10 | 10/10 표시 | Passed |
| 스킬 2 | 10/10 | 10/10 표시 | Passed |
| 스킬 3 | 10/10 | 10/10 표시 | Passed |
| 스킬 4 | 10/10 | 10/10 표시 | Passed |
| 츄르폭탄 수동 | 10/10 | 10회 표시 | 공유 잔여량 9 감소(주변 환경 파괴 포함) |
| 츄르폭탄 자동 | 3/3 | 4회 관측(주기 폭격 1회 포함) | Passed |

시각화는 Photon 이벤트 173(스킬), 174(폭탄)로 variant와 자동 여부만 전달하고, 파괴/보상은 기존 Host 권한 경로를 유지한다. 스킬 수치·쿨다운·이동 속도는 변경하지 않았다.

## PART 2. 이동 애니메이션 대조

| 입력 | 소유자 상태/클립 | 원격 상태/클립 | 일치 |
|---|---|---|---|
| 정지 | Idle / Idle | Idle / Idle | Yes |
| 후진 걷기 | BackWalk / BackWalk | BackWalk / BackWalk | Yes |
| 전진 달리기 | RunForward / RunForward | RunForward / RunForward | Yes |
| 점프 | Jump / Jump | Jump / Jump | Yes |
| 대시 | RunForward / RunForward, dash=True | RunForward / RunForward, dash=True | Yes |

연속 3프레임 캡처는 `Main2P_Final2/Host`, `Main2P_Final2/Client`에 저장했다. 전환은 즉시 정지로 튀지 않고 상태가 유지됐다.

## PART 3. 동시 타격·롤백

- 동일 오브젝트에 같은 라운드/프레임으로 20회 동시 요청.
- Host 기여도 증가: 20/20 정확히 1회.
- 중복 증가: 0/20.
- 중복 재화: 0.
- 일부 라운드 잔여량 변화가 0 또는 2인 것은 주변 자동 파괴가 같은 틱에 섞인 값이며, 대상별 Host 기여도는 모든 라운드 정확히 +1이었다.
- 개발 전용으로 다음 3개 요청을 명시적 reject하여 롤백을 재현했다.
- 롤백 관측: 3/3, 최종 alpha 1.00, `연결 불안정` 표시 True, 오브젝트 재출현 확인.
- 개발 주입 코드는 `DEVELOPMENT_BUILD || UNITY_EDITOR`에서만 컴파일된다.

## PART 4. Stage 1 완주·강제 종료

### 2회 완주

| 회차 | 목표 | Host 기여 | Client 기여 | 합계 | 재화 | 다음으로 |
|---|---:|---:|---:|---:|---|---|
| 1 | 114 | 104 | 10 | 114 | 182125 → 185037 | Host 활성, Client 비활성+대기 안내 |
| 2 | 253 | 247 | 6 | 253 | 185037 → 191077 | Host 활성, Client 비활성+대기 안내 |

- 회차 1: 양쪽 Stage 2 동시 진입.
- 회차 2: 양쪽 Stage 3 동시 진입.
- 결과 재화는 양쪽 로그에서 동일하게 관측됐다.

### 강제 종료

| 종료 대상 | 생존자 | 승계 | Stage1 완주 | 저장 |
|---|---|---|---|---|
| 참가자 Client | Host | Host 유지 | 114 파괴 | save=1, currency=183581 |
| 방장 Host | Client | `is_master=True`로 승계 | 114 파괴 | save=1, currency=183581 |

첫 Host 강제 종료 재현은 90초 동안 인원 1 전환이 없어 Failed였다. `RoomOptions.PlayerTtl=0`을 명시한 뒤 같은 방식으로 다시 실행했고 `OnMasterClientSwitched`와 인원 1 전환이 즉시 발생해 통과했다. `EmptyRoomTtl`은 변경하지 않았다.

## PART 5. 업그레이드 개인 적용

| 역할 | base_damage 레벨 | 펀치 피해 | 공격 거리 | 동일 체력 대상 예상/실제 타수 |
|---|---:|---:|---:|---:|
| Host | 3 | 26.00 | 2.20 | 6 / 6 |
| Client | 0 | 22.40 | 2.20 | 7 / 7 |

Host만 구매한 효과가 Client에 전파되지 않았다. 협동 종료 후 검증기 저장값 복원 로그를 확인했다.

## PART 6. 닉네임

| 목표 거리 | 실거리 | 표시 | 배율 | 가림 |
|---:|---:|---|---:|---|
| 5m | 5.00 | True | 0.70 | False |
| 20m | 20.00 | True | 1.50 | False |
| 40m | 39.50 | True | 1.50 | False |
| 45m | 45.00 | False | 0.00 | False |
| 벽 뒤 | - | False | - | True |

자기 닉네임은 생성하지 않고 원격 아바타에만 표시한다. 전용 재검증 `Nameplate2P_Final`은 양쪽 모두 FAIL 0이다.

## PART 7. 1인 완주·이어하기

- 타이틀 → 게임 시작 → 모드 선택 → 1인 선택 → 하우스 → 침대 → Stage1 정식 진입.
- Stage1 114/114, 결과 재화 182125 → 187487, 다음으로 활성.
- Stage2 진입 후 타이틀로 돌아가 `이어하기`를 실제 클릭.
- 이어하기 후 하우스 진입, 재화/해금 스테이지 유지, 인원 선택 패널 미노출.
- 최신 `Solo1P_Clean2`와 성능 포함 `Solo1P_Performance` 모두 FAIL 0 / PASS.

## PART 8. 다해상도·성능

### 해상도

| 해상도 | 인원 선택 | 로비 | 방 만들기 | 대기실 | 결과/다음으로 2회 | 결과 |
|---|---|---|---|---|---|---|
| 1920×1080 | Passed | Passed | Passed | Passed | Passed | Passed |
| 1600×900 | Passed | Passed | Passed | Passed | Passed | Passed |
| 1280×720 | Passed | Passed | Passed | Passed | Passed | Passed |
| 1280×1024 (5:4) | Passed | Passed | Passed | Passed | Passed | Passed |

1280×1024 이미지를 직접 확인했으며 핵심 패널·버튼의 화면 밖 잘림은 없었다. `ScreenCapture.CaptureScreenshot` 직후 자동 클릭이 이어지는 일부 증거에는 다음 화면이 겹쳐 찍히는 캡처 타이밍 현상이 있다. 게임 UI 배치 실패와 구분하며 후속 증거 도구 개선 항목으로 남긴다.

### Stage4 2인 3회 중앙값

| 역할 | 3회 평균 FPS | 중앙값 | 3회 1% low | 중앙값 | Draw Call 중앙값 |
|---|---|---:|---|---:|---:|
| Host | 30.5 / 32.7 / 36.3 | 32.7 | 4.6 / 4.8 / 5.2 | 4.8 | 328 |
| Client | 30.6 / 32.9 / 36.1 | 32.9 | 4.7 / 4.7 / 5.1 | 4.7 | 524 |

기존 Stage4 1% low 10~14 구간보다 악화되어 Failed이다.

### Stage5 1인 대 2인

| 조건 | 평균 FPS | 1% low | Draw Call |
|---|---:|---:|---:|
| 1인 최신 실측 | 45.0 | 11.2 | 651 |
| 2인 Host 기존 실측 | 41.2 | 9.1 | [미확인] |
| 2인 Client 기존 실측 | 39.3 | 7.7 | [미확인] |

2인 동기화 시 평균 FPS와 1% low가 모두 낮아지므로 협동 동기화가 성능을 악화시키는 것으로 판정한다. 이번 지시 범위에서는 동기화/렌더 최적화 수치를 변경하지 않았다.

## PART 9. 종료 처리

- Development 전용 `-validationQuit true`: 로그 `development_validation_quit exit=0`, 정상 종료.
- Release: 타이틀 `게임 종료` 버튼 클릭 → 확인 다이얼로그 `네` 클릭 → 프로세스 `ExitCode 0`.
- Release `CatPrototype.Runtime.dll`: `StorySoloFollowupBuildVerifier`, `DevelopmentValidationQuitProbe` 문자열/타입 미포함 확인.
- 종료 전/확인 다이얼로그 스크린샷: `ReleaseExit/title_ready.png`, `ReleaseExit/after_exit_click.png`.

## PART 10. 빌드

| 빌드 | 경로 | 실제 폴더 크기 | 최종 소요시간 | 에러 | 경고 |
|---|---|---:|---:|---:|---:|
| Development | `Builds/StoryCoopFollowupDev/GomyammiStoryCoopFollowup.exe` | 2.412 GiB | 70.98초 | 0 | 168 |
| Release | `Builds/StoryCoopFollowupRelease/GomyammiStoryCoopFollowup.exe` | 2.345 GiB | 72.02초 | 0 | 129 |

경고는 주로 기존 음수 Scale BoxCollider 및 기존 Stage 런타임 경고다. 최종 로그 세트에서 NullReference/MissingReference/Unhandled Exception/Crash는 0이다.

## 변경 파일

- `Assets/Scripts/StoryCoopRuntimeBridge.cs`
- `Assets/Scripts/CatSkillEffectRuntime.cs`
- `Assets/Scripts/SecondDevelopmentRuntime.cs`
- `Assets/Scripts/ChuruBombAirstrikeRuntime.cs`
- `Assets/Scripts/Coop/Net/CoopNetworkPlayerAvatar.cs`
- `Assets/Scripts/Coop/Net/CoopNetworkNameplate.cs`
- `Assets/Scripts/Coop/Net/CoopNetworkSyncManager.cs`
- `Assets/Scripts/Coop/Net/CoopNetService.cs`
- `Assets/Scripts/Coop/Net/MultiplayerUIBuildVerifier.cs`
- `Assets/Scripts/Coop/Net/MultiplayerUIBuildVerifier_Followup.cs`
- `Assets/Scripts/StorySoloFollowupBuildVerifier.cs`

## 결정표 적용

- 완주 시간이 길어지는 문제: Stage1 목표 114를 사용.
- Stage1 자동 완료: 목표-5까지 개발용 Host 채움, 마지막은 실제 양쪽 파괴 입력.
- 동시 타격: 같은 라운드 키를 같은 프레임에 양쪽 전송.
- 롤백 재현: Release에 없는 개발 전용 reject 스위치 사용.
- 업그레이드 차이: Host만 base_damage Lv.3, Client Lv.0 주입.
- 성능 튐: Stage4 3회 값의 중앙값 사용.
- 해상도: 독립 빌드 프로세스와 실제 요청 해상도 재적용.

## PlayerPrefs 복원

- 검증기가 저장한 currency/unlocked stage/growth/upgrade/best score/pending clear/HasSave를 각 실행 종료 시 복원.
- 최종 Standalone 재화는 작업 시작 기준 로그 값 `182125`로 명시 복원하고 레지스트리 값을 확인했다.
- 초기에 실패한 실행이 사전 스냅샷 전에 변경한 것으로 보이는 츄르폭탄 수량/해금과 검증용 닉네임의 원래 값은 증거가 없어 추측 복원하지 않았다. 이 둘의 완전한 원상복구 여부는 `[미확인]`이다.

## Failed / 후속 작업

1. **성능 Failed**: Stage4 2인 중앙값 Host 32.7/4.8, Client 32.9/4.7. 기존 10~14 FPS의 1% low보다 낮다.
2. **PlayerPrefs 일부 미확인**: 최초 실패 실행 이전의 츄르폭탄/닉네임 값이 남아 있지 않아 안전하게 복원할 근거가 없다.
3. **증거 캡처 타이밍**: 비동기 `CaptureScreenshot` 직후 자동 전환 시 다음 화면이 일부 겹친다. 검증기는 `WaitForEndOfFrame` 기반 캡처 큐로 개선할 수 있다.
4. 초기 통합 실행의 nameplate/완주 timeout FAIL 3개는 전용 재검증 `Nameplate2P_Final`, `Completion2P`에서 FAIL 0으로 해소했다.

## 증거 경로

- 종합 2인: `ValidationReports/StoryCoopFollowup/Main2P_Final2/`
- 완주 2회: `ValidationReports/StoryCoopFollowup/Completion2P/`
- 닉네임: `ValidationReports/StoryCoopFollowup/Nameplate2P_Final/`
- 강제 종료: `ForcedExitClient/`, `ForcedExitHost_Retry/`
- 1인: `Solo1P_Clean2/`, `Solo1P_Performance/`
- 해상도: `Resolution1600x900_Final/`, `Resolution1280x720/`, `Resolution1280x1024/`
- Release 종료: `ReleaseExit/`
