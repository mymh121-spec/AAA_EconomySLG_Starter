# HIVE 멀티플레이 연결 설계

최종 갱신: 2026-08-11

## 이번 단계의 범위

이번 단계는 보안·알림·결제보다 **플레이어 연결과 매칭 흐름**을 먼저 만든다.

```text
[Unity 여러 명 플레이]
        |
        v
[HIVE 개인 매칭]
  - 매치 ID
  - 매칭 점수
  - 추가 정보(최대 256자)
        |
        | 3~10초 간격 상태 확인(현재 5초)
        v
[matchingId + 참가자 목록]
        |
        v
[기존 C# 권위 게임 서버]
  - 서버 주소
  - 플레이어별 접속 토큰
  - 턴/경제/승패 최종 판정
```

HIVE는 참가자를 찾고 묶는 역할을 맡는다. 실제 시장, 생산, 전쟁, 턴 정산은 현재 `Server/Game.Server`가 계속 맡는다. HIVE 매칭 결과만으로 게임 상태를 클라이언트가 결정하지 않는다.

## 구현된 코드

- `PvpMatchmakingContracts.cs`: HIVE와 다른 매칭 서비스가 공통으로 따르는 요청, 상태, 참가자, Provider 계약
- `HiveSdkMatchmakingProvider.cs`: HIVE `requestMatchMaking`, `getRequestingStatus`, `deleteRequesting` 어댑터
- `PvpOnlineSessionController.cs`: HIVE 상태를 5초마다 확인하고 매칭 성공 후 기존 서버 연결 단계로 넘기는 오케스트레이션
- `GameModeSelectionController.cs`: 한국어 HIVE 매치 ID, 점수, 추가 정보, 시작/취소 UI
- `HiveConnectionSetup.cs`: Unity 메뉴에서 HIVE 연결 코드 활성화/비활성화 및 설치 상태 확인

HIVE SDK가 없을 때는 어댑터가 `Unavailable`을 반환하므로 현재 프로젝트가 컴파일되고 직접 서버 연결도 계속 동작한다. 외부 SDK는 게임의 Domain/Application asmdef에 침투하지 않는다.

## 공식 SDK 설치

현재 공식 릴리스 페이지에 표시된 Unity Interface와 Windows 플랫폼 패키지를 사용한다. 이 문서를 작성한 시점의 다운로드 기본값은 Interface `26.5.0`, Windows `26.4.0`이다. 설치 전에는 반드시 공식 릴리스 페이지에서 최신 호환 버전을 다시 확인한다.

1. Unity를 닫는다.
2. 프로젝트 루트의 `PREPARE_HIVE_CONNECTION_SDK.cmd`를 실행한다.
3. 내려받은 폴더에서 `Hive_SDK_Unity_Interface.unitypackage`를 먼저 가져온다.
4. 이어서 Windows 플랫폼용 `.unitypackage`를 가져온다.
5. Unity 메뉴 `게임 > HIVE 연결 > HIVE SDK 설치 상태 확인`을 누른다.
6. 정상 설치가 확인되면 `게임 > HIVE 연결 > HIVE 매칭 활성화`를 누른다.

SDK는 `Assets/Hive_SDK_v4`에 설치된다. 게임 어댑터는 HIVE SDK의 기본 `Assembly-CSharp` 위치를 고려해 `Assets/Game/Integrations/Hive`에 격리했다.

공식 문서:

- Unity 설치: https://developers.hiveplatform.ai/en/latest-version/dev/overview/getting-started/install/unity-install/
- Unity 설치 후 초기화: https://developers.hiveplatform.ai/en/latest-version/dev/overview/getting-started/post-install/unity-post-install/
- 개인 매칭: https://developers.hiveplatform.ai/en/latest/dev/matchmaking/individual-matching/
- Unity 릴리스: https://developers.hiveplatform.ai/en/latest/releases/unity/

## HIVE 콘솔 준비

실제 매칭 호출 전 다음 외부 설정이 필요하다.

1. HIVE App Center에 프로젝트와 AppID를 등록한다.
2. HIVE 콘솔의 Matchmaking 메뉴에서 개인 매칭을 만든다.
3. 콘솔에서 발급된 정수 `matchId`를 게임 UI의 `HIVE 매치 ID`에 입력한다.
4. 랜덤 매칭 또는 점수 기반 매칭과 참가 인원을 정한다. HIVE 매칭 자체의 총 참가자 한도는 16명이며 현재 게임 서버는 2~4명만 지원한다.
5. SDK 설정 파일에 AppID, Zone, 필요한 키를 적용한다.

## 로그인은 나중이지만 피할 수 없는 최소 조건

로그인 화면과 계정 UX는 다음 단계로 미뤘다. 다만 HIVE Matchmaking은 HIVE `playerId`가 있는 인증 세션을 전제로 하므로 **실제 외부 매칭 테스트에는 최소 HIVE SDK 초기화와 로그인 1회가 반드시 필요하다**. HIVE SDK v25 이상에서는 보안 키 설정도 로그인에 필요하다.

현재 Provider는 Unity 플러그인 초기화까지만 담당한다. 로그인 구현 전에는 UI와 상태 전환, SDK 미설치 폴백, 기존 서버 직접 연결을 검증할 수 있고, 실제 HIVE 상대 검색은 로그인 연결 후 활성화된다.

## 현재 서버와의 연결 방식

현재는 HIVE 매칭을 시작하기 전에 기존 게임 서버 주소와 32자 이상 토큰을 입력한다. 매칭이 성공하면 동일한 값으로 `GET /api/v1/match`를 호출해 권위 서버 세션에 들어간다. 개발용 2~4인 고정 경기에는 사용할 수 있지만, 불특정 다수의 자동 방 배정은 아직 아니다.

다음 서버 단계는 다음 순서가 적절하다.

1. `MatchRegistry`로 여러 경기를 동시에 보관한다.
2. HIVE `matchingId`를 내부 `PvpMatchId`에 연결한다.
3. 매칭 완료 콜백 또는 서버 조회 결과로 빈 게임 서버/방을 배정한다.
4. `/api/v1/hive/session`에서 플레이어별 짧은 게임 접속 토큰을 발급한다.
5. Unity는 토큰을 저장하지 않고 해당 경기 종료 시 HIVE 매칭 삭제를 요청한다.

HIVE Certification Key를 사용하는 서버 API를 붙일 때 그 키는 Unity 클라이언트나 Git에 넣지 않고 C# 서버 환경 변수에만 둔다.

## 플레이 화면 사용법

1. `여러 명이서 하기`를 선택한다.
2. 기존 권위 서버 주소와 플레이어 토큰을 입력한다.
3. HIVE 콘솔의 매치 ID, 현재 실력/경제력에 대응하는 점수, 표시용 추가 정보를 입력한다.
4. `HIVE에서 상대 찾고 서버 연결`을 누른다.
5. 매칭 중에는 `HIVE 매칭 취소`로 요청을 삭제할 수 있다.
6. SDK 또는 로그인 준비 전에는 `직접 서버 연결`로 기존 로컬/VPS 테스트를 계속한다.

## 완료 기준

- HIVE SDK 미설치 상태에서 Unity 컴파일과 직접 서버 연결이 깨지지 않는다.
- HIVE 활성 상태에서 요청, 5초 폴링, 성공, 시간 초과, 취소 상태가 한국어 UI에 표시된다.
- 매칭 성공 뒤에도 경제 정산 권한은 C# 서버에만 있다.
- Domain/Application은 HIVE 타입을 직접 참조하지 않는다.

