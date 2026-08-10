# 경제 SLG PvP 권위 서버 0.2.0

이 서버는 Unity 클라이언트가 보낸 행동을 그대로 믿지 않고, 서버가 명령 유효성 검사·행동력 소비·시장 거래·생산·채굴지 이벤트·승패 판정을 직접 처리하는 턴제 권위 서버다. SQL은 사용하지 않는다.

## 현재 지원 범위

- 한 경기당 2~4명
- 시장 구매·판매 명령과 플레이어별 행동력 검증
- 모든 참가자가 준비하면 한 턴 정산
- 미준비 플레이어 자동 준비 처리(기본 120초)
- 요청 ID 기반 재전송 멱등성
- 실제 월드 상태에서 계산한 SHA-256 상태 해시
- 턴 15부터 2턴 연속으로 상대 경제력 합계의 3배 달성 시 승리
- 턴 30 종료 시 경제력 순위로 승패 판정
- 5턴마다 채굴지 생성, 경과 턴에 따른 채광률 감소와 최소 생산량
- 서버 재시작 시 JSON 저널 재생으로 경기 복구

현재 구현은 설정된 단일 경기 인스턴스다. 여러 방을 동시에 운영하는 로비·매치 레지스트리는 다음 단계다.

## 저장 방식(SQL 없음)

`PVP_DATA_DIR`에 다음 파일을 쓴다.

- `<match-id>.journal.jsonl`: 명령, 준비, 턴 정산을 순서대로 즉시 추가하는 원장
- `<match-id>.snapshot.json`: 운영 확인과 백업을 위한 최신 원자적 스냅샷

재시작 시 JSONL 원장을 처음부터 결정론적으로 재생하고, 저장된 턴·리비전·상태 해시가 재계산 결과와 같은지 검증한다. 원장은 매 기록마다 디스크에 플러시하므로 작은 규모의 2~4인 턴제 게임에는 충분하다. 대규모 동시 경기로 확장할 때는 SQL 대신 경기별 압축 스냅샷 + 분할 저널 또는 오브젝트 스토리지로 교체할 수 있도록 저장 계층이 분리되어 있다.

## 환경 변수

필수 토큰은 절대 Unity 씬, ScriptableObject, Git 저장소에 넣지 않는다.

| 변수 | 설명 | 기본값 |
|---|---|---|
| `PVP_PLAYER1_TOKEN` | 1번 플레이어 Bearer 토큰 | 필수 |
| `PVP_PLAYER2_TOKEN` | 2번 플레이어 Bearer 토큰 | 필수 |
| `PVP_PLAYER3_TOKEN` | 3번 플레이어 토큰 | 없음 |
| `PVP_PLAYER4_TOKEN` | 4번 플레이어 토큰 | 없음 |
| `PVP_PLAYERS_FILE` | 2~4인 구성 JSON 파일 경로 | 없음 |
| `PVP_MATCH_ID` | 경기 ID | `dev-match-001` |
| `PVP_DATA_DIR` | JSON 저널/스냅샷 경로 | 앱 데이터 경로 |
| `PVP_TURN_TIMEOUT_SECONDS` | 턴 제한시간, 15~3600초 | `120` |
| `PVP_URLS` | Kestrel 수신 주소 | `http://127.0.0.1:5100` |

`PVP_PLAYERS_FILE`을 쓰면 토큰 환경 변수 대신 다음처럼 2~4명을 구성할 수 있다.

```json
[
  { "playerId": "player-1", "companyId": "company-player-1", "displayName": "청람 산업", "token": "긴-임의-토큰" },
  { "playerId": "player-2", "companyId": "company-player-2", "displayName": "백호 물류", "token": "긴-임의-토큰" }
]
```

## API

모든 게임 API는 `Authorization: Bearer <token>`을 요구한다.

- `GET /health`: 버전, 경기 상태, 턴, 리비전 확인
- `GET /api/v1/match`: 재접속용 공개 월드와 본인 비공개 상태 조회
- `POST /api/v1/commands`: 시장 구매/판매 행동 제출
- `POST /api/v1/ready`: 해당 턴 행동 확정

명령과 준비 요청에는 `requestId`, `protocolVersion`, `matchId`, `expectedRevision`을 보낸다. 네트워크 오류 후 같은 요청을 재전송하면 서버는 중복 실행하지 않고 기존 결과를 돌려준다. 같은 `requestId`에 다른 내용을 넣으면 충돌로 거부한다.

## 로컬 빌드

```powershell
dotnet build .\Server\Game.Server\Game.Server.csproj -c Release
dotnet publish .\Server\Game.Server\Game.Server.csproj -c Release -r linux-x64 --self-contained true
```

## VPS 배포

배포 산출물:

- `game-server-0.2.0-linux-x64.tar.gz`
- SHA-256: `c9817117cebc3e0e54ea8ec634ca7d6e45ee403e1355bba58c93d6341de62322`

서버에서 다음 위치로 업로드한 뒤 배포 스크립트를 실행한다.

```bash
/home/economyslg/apps/economy-slg/incoming/game-server-0.2.0-linux-x64.tar.gz
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

`PvpOnlineSessionController`를 온라인 세션 전용 GameObject에 한 번만 붙인다. 엔드포인트만 직렬화하고, 로그인이나 개발 콘솔에서 얻은 토큰은 런타임에 `ConnectAsync(token)`으로 주입한다. 씬 전환 뒤에도 유지하려면 별도 부트스트랩 객체가 소유하게 하고 중복 생성을 막는다.

클라이언트가 표시하는 가격·재고·현금은 `GET /api/v1/match` 또는 명령/준비 응답의 `world`를 기준으로 갱신한다. 로컬 시뮬레이션 결과를 온라인 경기의 최종값으로 사용하면 안 된다.

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
