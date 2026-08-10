# 실시간 경제 SLG 개발 인계 README

## 현재 방향

이 프로젝트는 Unity 6.3 LTS와 C#으로 개발하는 기업 중심 경제 시뮬레이션 + 실시간 SLG다. 플레이어는 국가가 아니라 기업 또는 조직을 운영한다.

Hive Platform은 최종적으로 사용하지만 현재는 SDK를 설치하지 않는다. 로그인, 변조 방지, 알림, 매치메이킹, 채팅, 순위 기능은 게임 본체가 완성된 후 연결한다.

## 이번 단계 완료 내용

- 기존 수동 턴 종료 방식 앞에 고정 스텝 실시간 시계를 추가했다.
- 일시정지와 1배속, 2배속, 3배속, 4배속, 5배속을 지원한다.
- 기본 속도에서 게임 하루는 현실 60초다.
- 5배속에서 게임 하루는 현실 12초다.
- 경제, 생산, 소비, 시장, AI, 사건, 승패 판정은 게임 내 자정마다 기존 하루 정산 파이프라인으로 처리한다.
- 프레임 정지 후 과도한 캐치업으로 게임이 멈추지 않도록 프레임당 고정 스텝 상한을 적용했다.
- 동적 싱글플레이 HUD에서 `턴 종료` 버튼을 제거하고 일시정지 및 1~5배속 버튼으로 교체했다.
- 기존 Unity UI용 `TurnHudPresenter`도 실시간 시간과 배속 표시 방식으로 전환했다.
- 캠페인은 현재 30일이며 경제력 3배 조건은 15일부터 확인한다.
- 5일마다 자원지 이벤트가 발생하는 기존 규칙은 실시간 기준 5게임일마다 실행된다.
- 맵은 80×48 평면 월드이며 동쪽과 서쪽이 연결된다.

## 주요 코드

- 실시간 시계: `Assets/Game/Scripts/Application/Turn/RealtimeSimulationClock.cs`
- 시뮬레이션 연결: `Assets/Game/Scripts/Presentation/SimulationBootstrapper.cs`
- 모드 선택과 실시간 HUD: `Assets/Game/Scripts/Presentation/GameModeSelectionController.cs`
- 기존 씬 HUD 호환: `Assets/Game/Scripts/Presentation/TurnHudPresenter.cs`
- 실시간 설정: `Assets/Game/Scripts/Data/Settings/SimulationSettingsAsset.cs`
- 런타임 설정 에셋: `Assets/Game/Resources/SimulationSettings.asset`
- 맵 규칙: `WORLD_MAP_RULES_KO.md`

## 다음 우선순위

1. 지도 칸 선택 후 실제 행동 메뉴 표시
2. 플레이어 유닛 생성, 선택, 가로 래핑 이동
3. 광산 점령 및 소유권 변경
4. 공장과 창고 건설
5. 생산·시장·회사 현황 HUD
6. 실시간 AI 이동과 점령
7. 전투와 수도 함락
8. 저장·불러오기
9. 튜토리얼과 게임 밸런스
10. 게임 본체 완성 후 Hive Platform 연결

## 반드시 지킬 규칙

- 실제 게임 규칙은 `MonoBehaviour.Update`에 작성하지 않는다.
- Unity Update는 입력과 시간 전달만 담당한다.
- 경제 가격은 이벤트가 직접 설정하지 않는다.
- 생산, 소비, 재고, 운송, AI 거래, 플레이어 거래가 가격을 변화시킨다.
- 이동과 사거리 계산은 가로 래핑을 지원하는 `GridMapLayout` API를 사용한다.
- 싱글은 로컬 권위 시뮬레이션, 멀티는 서버 권위 시뮬레이션으로 유지한다.
- Hive SDK 타입을 Domain 또는 Application 계층에 직접 넣지 않는다.
- 화면과 데이터는 최대한 한국어로 표시한다.
- 기존 테스트와 사용자 변경을 보존한다.

## 다음 작업용 복사 프롬프트

```text
아래 Unity 프로젝트의 개발을 계속하라.

프로젝트 경로:
C:\Users\andrew\Documents\Codex\2026-08-01\15-aaa-unity-c-slg-slg\work\AAA_EconomySLG_Starter

먼저 REALTIME_GAME_CONTINUATION_README_KO.md, WORLD_MAP_RULES_KO.md,
NEXT_STEPS_KO.txt와 git status를 읽어라.

게임은 기업 중심 경제 시뮬레이션 + HOI4식 일시정지 가능한 실시간 SLG다.
현재 일시정지 및 1~5배속 고정 스텝 시계와 하루 단위 경제 정산이 구현되어 있다.
Hive Platform은 나중에 사용하며 지금은 로그인, 변조 방지, 알림, 매칭 SDK를 설치하지 마라.

다음 목표는 지도 상호작용의 실제 게임플레이 구현이다.

1. 지도 칸을 선택하면 한국어 행동 패널을 표시한다.
2. 플레이어 본사 또는 소유 칸에서 유닛을 생성할 수 있게 한다.
3. 유닛 선택 후 목적지를 클릭하면 이동 명령을 내린다.
4. 이동은 실시간 고정 스텝으로 진행하고 80×48 맵의 좌우 래핑 최단 경로를 사용한다.
5. 광산에 도착하면 점령 진행도가 증가하고 완료 시 소유권과 해당 기업의 생산량이 변한다.
6. AI 기업도 같은 명령과 규칙을 사용한다.
7. MonoBehaviour 남용 없이 Domain/Application에 규칙을 두고 Presentation은 표시와 입력만 담당한다.
8. 싱글플레이 30일 캠페인, 15일부터 경제력 3배 확인, 수도 함락·파산 패배 규칙을 유지한다.
9. EditMode 테스트와 독립 컴파일 검증을 추가한다.
10. 완료 후 변경 파일, 검증 결과, 다음 작업을 한국어로 정리한다.

코드를 조사만 하지 말고 안전한 범위에서 실제 구현과 검증까지 진행하라.
```

## 검증 명령

```powershell
dotnet run --project Validation\CoreSimulationSmoke\CoreSimulationSmoke.csproj --nologo
dotnet build Validation\UnityCoreCompile\UnityCoreCompile.csproj --nologo --verbosity:minimal
git diff --check
```

Unity 배치 실행은 이 PC의 헤드리스 Editor 라이선스가 활성화되지 않으면 코드와 무관하게 종료 코드 198로 실패할 수 있다. 이 경우 Unity 모듈 참조 독립 컴파일과 에디터 Console을 사용해 확인한다.
