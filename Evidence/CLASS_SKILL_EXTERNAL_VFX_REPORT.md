# 클래스 스킬 외부 VFX 검증

- 날짜: 2026-08-26
- 환경: Unity 6000.3.6f1 / URP 17.3

## 구현한 표현 구성

| 클래스 | 스킬 | 표현 | 외부 효과 레이어 |
| --- | ---: | --- | --- |
| 근접 | 1 | 강화 초승달 베기 | Magic Hit 2, Basic Hit 7, Unity Sparks |
| 근접 | 2 | 낙하 지면 충격 | Basic Hit 8, Magic Hit 2, Unity Smoke/Sparks |
| 근접 | 3 | 회전 폭풍 | Shadow Hit 3개, Unity Sparks |
| 근접 | 4 | 번개 찌르기 | Lightning Hit Blue, Magic Hit 2, Unity Lightning |
| 총기 | 1 | 쌍발 속사 섬광 | Basic Hit 7 두 개, Unity Sparks |
| 총기 | 2 | 원형 정밀 폭발 | Basic Hit 8, Lightning Hit Blue, Unity Smoke |
| 총기 | 3 | 360도 탄환 폭풍 | Lightning Hit Blue 네 개, Unity Sparks |
| 총기 | 4 | 양손 공중 난사 | Fire Hit 네 개, Unity Sparks/Smoke |

모든 구성에 최대 개수가 제한된 풀링, 움직임, 크기 반동, HDR Bloom, 짧은 포인트 라이트와 외부 출처 표식을 적용했습니다. 저장 호환성을 위해 과거 런타임 타입 이름은 유지했지만 Blender 제작 클래스 스킬 모델은 로드하거나 빌드에 포함하지 않았습니다.

## Idle 자세 수정

Assets/Animations/MeleeCat/MeleeCat.controller의 속도 0 이동 자식은 T 포즈처럼 보이는 Idle 클립 대신 호환되는 걷기 모션을 0.12배속으로 사용하도록 수정했습니다.

독립 실행 측정:

- 왼손이 위팔보다 아래: 0.3094 월드 단위
- 오른손이 위팔보다 아래: 0.3093 월드 단위
- 자연스러운 Idle 판정: Passed

## 외부 출처

- Hit Effects FREE — Matthew Guz, Unity Asset Store Standard EULA
- Unity Visual Effect Graph Samples — Unity Companion License
- 런타임 주소 지정 복사본과 라이선스 고지는 원본 프로젝트의 Assets/Resources/SkillVFX/External에 보관
- 참조되지 않는 과거 Blender 클래스 스킬 FBX는 최종 클린 빌드 전에 제거

## 검증 결과

- PlayMode 공개 입력 테스트 1/1 Passed
  - 실제 키보드 1~4, 근접 스킬 1의 좌클릭 후속타, 스킬 이벤트 8개, 외부 프리팹 수, 최대 48개 풀, 출처 표식, 후처리와 Idle 뼈 구조를 확인
- Windows64 Development 빌드 성공, 오류 0건
- 독립 실행 검증 Passed, 프로세스 종료 코드 0
  - Blender 모델 0개
  - Hit Effects 프리팹 6개
  - Unity VFX Graph 프리팹 3개
  - 스킬 8개 항목 통과, 잘못된 셰이더 없음
  - 런타임 오류 패턴 0건

