# 경제 SLG PvP 권위 서버 0.3.0

이 서버는 Unity 클라이언트가 보낸 행동을 그대로 믿지 않고, 서버가 명령 유효성 검사·행동력 소비·시장 거래·생산·채굴지 이벤트·승패 판정을 직접 처리하는 턴제 권위 서버다. SQL은 사용하지 않는다.

## 현재 지원 범위

- 한 경기당 2~4명
- 6자리 초대 코드 기반 다중 방 생성·참가·방장 시작
- 방별 256비트 세션 토큰과 SHA-256 해시 저장
- 시장 구매·판매 명령과 플레이어별 행동력 검증
- 방별 80×48 결정론 지도와 부대 이동·광산 점령·성 점령·공성 명령
- 지도 이동 경로·점령 진행·성벽/식량·수도 멸망을 포함한 재접속 월드
- 모든 참가자가 준비하면 한 턴 정산
- 미준비 플레이어 자동 준비 처리(기본 120초)
- 요청 ID 기반 재전송 멱등성
- 실제 월드 상태에서 계산한 SHA-256 상태 해시
- 7월 1일(181일차)부터 60일(2개월) 연속으로 상대 경제력 합계의 3배 달성 시 승리
- 12월 30일(360일차) 종료 시 경제력 순위로 승패 판정
- 5턴마다 채굴지 생성, 경과 턴에 따른 채광률 감소와 최소 생산량
- 서버 재시작 시 JSON 저널 재생으로 경기 복구

방마다 독립된 권위 시뮬레이션과 저널을 사용한다. 시작 전에는 2명 이상이 필요하고, 시작 후 새 참가자는 차단된다.

## 저장 방식(SQL 없음)

`PVP_DATA_DIR`에 다음 파일을 쓴다.

- `rooms/<room-code>.room.json`: 방 메타데이터와 세션 토큰의 SHA-256 해시
- `matches/<match-id>.journal.jsonl`: 명령, 준비, 턴 정산을 순서대로 즉시 추가하는 원장
- `matches/<match-id>.snapshot.json`: 운영 확인과 백업을 위한 최신 원자적 스냅샷

재시작 시 JSONL 원장을 처음부터 결정론적으로 재생하고, 저장된 턴·리비전·상태 해시가 재계산 결과와 같은지 검증한다. 원장은 매 기록마다 디스크에 플러시하므로 작은 규모의 2~4인 턴제 게임에는 충분하다. 대규모 동시 경기로 확장할 때는 SQL 대신 경기별 압축 스냅샷 + 분할 저널 또는 오브젝트 스토리지로 교체할 수 있도록 저장 계층이 분리되어 있다.

## 환경 변수

발급된 세션 토큰은 절대 Unity 씬, ScriptableObject, Git 저장소에 넣지 않는다. 서버는 방 생성·참가 응답에서 평문 토큰을 한 번만 반환하고 저장 파일에는 해시만 남긴다.

| 변수 | 설명 | 기본값 |
|---|---|---|
| `PVP_DATA_DIR` | JSON 저널/스냅샷 경로 | 앱 데이터 경로 |
| `PVP_MAX_ROOMS` | 동시에 보관할 활성·대기 방 수 | `16` |
| `PVP_TURN_TIMEOUT_SECONDS` | 턴 제한시간, 15~3600초 | `120` |
| `PVP_URLS` | Kestrel 수신 주소 | `http://127.0.0.1:5100` |

## API

방 생성과 참가를 제외한 API는 해당 방에서 발급된 `Authorization: Bearer <token>`을 요구한다.

- `GET /health`: 서버 버전과 상태 확인
- `POST /api/v1/rooms`: 방 생성, 방장 세션 발급
- `POST /api/v1/rooms/{roomCode}/join`: 초대 코드로 참가, 참가자 세션 발급
- `GET /api/v1/rooms/{roomCode}`: 대기실·방 상태 조회
- `POST /api/v1/rooms/{roomCode}/start`: 방장이 2~4인 경기 시작
- `GET /api/v1/rooms/{roomCode}/match`: 재접속용 월드와 본인 상태 조회
- `POST /api/v1/rooms/{roomCode}/commands`: 시장 또는 지도 행동 제출
- `POST /api/v1/rooms/{roomCode}/ready`: 해당 턴 행동 확정

명령과 준비 요청에는 `requestId`, `protocolVersion`, `matchId`, `expectedRevision`을 보낸다. 네트워크 오류 후 같은 요청을 재전송하면 서버는 중복 실행하지 않고 기존 결과를 돌려준다. 같은 `requestId`에 다른 내용을 넣으면 충돌로 거부한다.

지도 명령은 `kind`에 `MoveUnit`, `OccupyResourceSite`, `OccupyCastle`, `StartSiege`, `CancelOrder` 중 하나를 사용한다. `targetId`에는 서버가 발급한 부대 ID를, 목표가 필요한 명령에는 `targetX`와 `targetY`를 보낸다. `StartSiege`의 `action`은 현재 `Assault`를 지원한다. 서버는 부대 소유권, 목표 좌표, 경로, 행동력을 다시 검증한다.

## 로컬 빌드

```powershell
D:\dotnet\dotnet.exe build .\Server\Game.Server\Game.Server.csproj -c Release
D:\dotnet\dotnet.exe publish .\Server\Game.Server\Game.Server.csproj -c Release -r linux-x64 --self-contained true
```

Windows에서는 저장소 루트의 `RUN_PVP_SERVER.cmd`를 실행하면 `D:\dotnet`과 `D:\AAA_EconomySLG\ServerData`를 우선 사용한다.

실제 API 통합 스모크 테스트:

```powershell
.\Validation\PvpRoomApiSmoke.ps1
D:\dotnet\dotnet.exe run --project .\Validation\PvpMapAuthoritySmoke\PvpMapAuthoritySmoke.csproj
```

테스트 서버 데이터와 로그는 `D:\AAA_EconomySLG\ServerTests` 아래의 실행별 격리 폴더에 저장된다.

## VPS 배포

기존 0.2.0 배포 묶음은 단일 매치 API이므로 0.3.0 방 API 배포 전에 새 Linux 산출물을 만들어야 한다.

서버에서 다음 위치로 업로드한 뒤 배포 스크립트를 실행한다.

```bash
/home/economyslg/apps/economy-slg/incoming/game-server-0.3.0-linux-x64.tar.gz
bash /home/economyslg/apps/economy-slg/deploy_user.sh
```

현재 Windows 작업 폴더에서는 아래 한 명령으로 업로드, Bash 문법 검사, 배포, 원격 한 턴 검증까지 실행할 수 있다.

```powershell
.\Server\Deploy\deploy_from_windows.ps1
```

스크립트는 기존 토큰을 보존하고, 새 서버 실패 시 이전 릴리스로 자동 복구한다. 서버는 기본적으로 `127.0.0.1:5100`에만 바인딩하므로 인터넷에 5100 포트를 직접 개방하지 않는다.

## 접속 방식

개발 중에는 SSH 터널이 가장 단순하다.

```powershell
ssh -N -L 5200:127.0.0.1:5100 economyslg@101.79.19.253
```

Unity 엔드포인트는 `http://127.0.0.1:5200`으로 둔다. 출시 환경에서는 Caddy 또는 Nginx에서 TLS를 종료하고 `https://게임도메인`을 127.0.0.1:5100으로 프록시한다. Unity 클라이언트는 공인 주소의 평문 HTTP 연결을 거부하도록 구현되어 있다.

## Unity 연결

기본 멀티플레이 화면에서 표시 이름과 2~4인 정원을 입력해 방을 만들거나 6자리 초대 코드로 참가한다. Unity는 생성·참가 응답의 세션 토큰을 `PlayerPrefs`나 파일에 저장하지 않고 현재 프로세스 메모리에만 유지한다. 방장은 참가자가 2명 이상일 때 경기를 시작할 수 있고, 참가자는 `방 상태 갱신`으로 시작된 경기에 진입한다.

`PvpOnlineSessionController`는 온라인 세션 전용 GameObject에 한 번만 둔다. 개발용 직접 연결은 방 코드와 세션 토큰을 함께 받으며, 일반 플레이에서는 방 생성·참가 UI가 세션을 자동 구성한다.

클라이언트가 표시하는 가격·재고·현금과 지도·부대·광산·성 상태는 방별 `GET /api/v1/rooms/{roomCode}/match` 또는 명령/준비 응답의 `world`를 기준으로 갱신한다. Unity 지도에서 아군 부대를 선택하고 목표 칸을 고른 뒤 이동/점령, 강습, 취소 버튼으로 서버 명령을 보낼 수 있다. 로컬 시뮬레이션 결과를 온라인 경기의 최종값으로 사용하면 안 된다.

## 운영 점검

```bash
curl --fail http://127.0.0.1:5100/health
tail -n 100 /home/economyslg/apps/economy-slg/logs/server.log
ls -lh /home/economyslg/apps/economy-slg/data
```

토큰은 다음처럼 서버 내부에서만 확인한다.

```bash
sudo -u economyslg grep '^PVP_PLAYER' /home/economyslg/apps/economy-slg/config/pvp.env
```

운영 백업은 서버를 멈춘 뒤 `data` 디렉터리 전체를 복사하는 것이 가장 안전하다. 실행 중 백업해야 한다면 먼저 스냅샷과 저널을 같은 시점으로 묶는 운영 기능을 추가한다.
