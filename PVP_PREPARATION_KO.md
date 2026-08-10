# PvP 준비 아키텍처

## 목표

현재 PvP는 실제 네트워크 접속을 붙인 상태가 아니라, 어떤 서버 기술을 선택해도 재사용할 수 있는 **서버 권위형 동시 턴 규약**까지 구현한 상태다. 클라이언트는 결과를 결정하지 않고 명령만 제출하며, 서버만 턴을 정산한다.

```text
[클라이언트 A 계획] ─┐
                     ├→ [서버 명령 검증·저장]
[클라이언트 B 계획] ─┘
                              ↓ 모두 준비 완료
                    [명령 잠금·결정론적 정렬]
                              ↓
                    [서버 SimulationEngine 정산]
                              ↓
                    [상태 Revision·SHA-256 생성]
                         ┌────┴────┐
                    [A 동기화] [B 동기화]
```

## 현재 구현된 규칙

- 기본 2인, 규칙상 2~4인까지 확장 가능
- 각 플레이어는 한 회사만 소유한다.
- 플레이어별 행동력은 기본 5이며 서로 공유하지 않는다.
- 명령마다 `MatchId`, `PlayerId`, `CompanyId`, `Turn`, `Sequence`, `CommandId`를 포함한다.
- 서버는 다른 회사 조작, 잘못된 턴, 명령 순서 누락, 중복·재전송, 행동력 초과, 잘못된 Payload를 거절한다.
- 준비 완료 후에는 해당 플레이어가 명령을 변경할 수 없다.
- 모든 생존 플레이어가 준비되면 명령을 잠근다.
- 명령은 플레이어 슬롯 순서 → Sequence 순서 → CommandId 순서로 정렬한다.
- 잠긴 명령 패키지는 정규화된 문자열로 변환한 뒤 SHA-256 체크섬을 생성한다.
- 서버 정산 후 `Revision`과 권위 상태 해시를 저장하고 다음 턴으로 넘어간다.
- 연결이 끊겨도 플레이어 슬롯과 제출 명령은 유지하며 재접속 스냅샷을 만들 수 있다.
- 상대방의 미제출 명령은 제공하지 않고, `GetPendingCommands(playerId)`로 자기 명령만 복원한다.

## 행동력 비용

| 명령 | 행동력 |
|---|---:|
| 시장 구매·판매 | 1 |
| 생산 계획 변경 | 1 |
| 운송 지시 | 1 |
| 연구 시작 | 1 |
| 미션 시작 | 2 |
| 시설 건설 | 3 |
| 공격·방어 | 3 |

비용은 클라이언트가 보내지 않는다. 서버의 `IPvpCommandRulePolicy`가 명령 종류를 보고 계산한다.

## 주요 코드

- `PvpCommandEnvelope`: 네트워크로 전달할 불변 명령 봉투
- `PvpCommandPayload`: 명령 대상·자원·수량·가격 데이터
- `DefaultPvpCommandRulePolicy`: 서버 행동력 비용과 Payload 검증
- `PvpTurnCoordinator`: 소유권·순서·준비·잠금·턴 전환 관리
- `PvpTurnPackage`: 정렬 완료된 한 턴 권위 명령 묶음
- `PvpChecksum`: 명령 패키지 SHA-256 계산
- `PvpMatchSnapshot`: 재접속용 매치 상태
- `PvpMarketCommandTranslator`: PvP 시장 명령을 기존 `ITurnCommand`로 변환
- `PvpKoreanFormatter`: 클라이언트에 표시할 한국어 거절 사유
- `IPvpTransport`: 실제 소켓·SDK를 감추는 비동기 전송 포트
- `IPvpMessageCodec`: JSON·MessagePack·Protobuf 교체 지점
- `PvpNetworkEnvelope`: 프로토콜 버전·Revision·상관관계 ID를 포함한 전송 봉투
- `PvpAuthoritativeGateway`: 인증 신원·Revision·멱등성 검증 후 Coordinator로 라우팅
- `PvpPeerContext`: 로그인 서버가 확정한 플레이어 신원. Payload의 PlayerId보다 우선한다.

## 서버가 맡아야 하는 일

1. 로그인 토큰을 검증하고 `PlayerId`를 서버 세션에서 주입한다.
2. 클라이언트가 보낸 `PlayerId`, `CompanyId`, 행동력 비용을 신뢰하지 않는다.
3. `PvpTurnCoordinator.SubmitCommand()`로 모든 명령을 검증한다.
4. 턴 제한 시간이 끝나면 미접속 플레이어의 정책을 적용한다.
5. 잠긴 패키지를 기존 턴 명령으로 변환한다.
6. 서버 인스턴스의 `SimulationEngine`에서 한 번만 정산한다.
7. 전체 권위 상태를 정규화해 상태 해시를 만든다.
8. 결과와 새 `Revision`을 저장한 뒤 클라이언트에 배포한다.

## 네트워크 메시지 초안

```text
JoinMatchRequest
  MatchId, 인증 토큰

SubmitCommandRequest
  CommandId, MatchId, Turn, Sequence, Kind, Payload

SubmitCommandResponse
  OperationCode, ExpectedSequence, Revision

ReadyRequest
  MatchId, Turn, 마지막 Sequence

TurnLockedEvent
  MatchId, Turn, CommandHash

TurnResolvedEvent
  MatchId, Turn, Revision, StateHash, 공개 결과

ReconnectSnapshot
  MatchSnapshot, 자기 PendingCommands, 공개 게임 상태, 자기 회사 상세 상태
```

## 정보 공개 정책

- 모두 공개: 턴, 시장 가격, 시장 공급·수요, 전쟁 결과, 공개 거점
- 자기만 공개: 현금, 창고 상세 재고, 예약 명령, 연구 진행, 비공개 미션
- 상대에게 집계 공개: 경제력, 알려진 공장, 공개 주문, 정찰로 확인된 정보
- 서버 전용: AI 내부 점수, 숨겨진 미션, Random Seed, 검증용 전체 상태

서버는 플레이어별 응답 DTO를 따로 만들어야 하며, 전체 월드 객체를 그대로 직렬화하면 안 된다.

## 재접속과 시간 제한 정책 권장안

- 계획 제한 시간: 초기 권장 120초
- 연결 종료 후 유예 시간: 30초
- 유예 시간 안에 복귀하면 자기 PendingCommands와 ExpectedSequence 복구
- 제한 시간 종료 시 이미 제출된 명령만 확정하고 자동 준비
- 명령이 없으면 방어·현상 유지로 처리
- 2턴 연속 미접속 시 AI 대행 여부는 방 생성 옵션으로 결정

시간 제한은 경제 시뮬레이션의 결정론적 코드에 넣지 않는다. 서버의 매치 스케줄러가 UTC 시간으로 관리한다.

## 실제 네트워크 연결 전 남은 작업

- 서버용 프로젝트 또는 Headless Unity 실행 환경 결정
- `IPvpMessageCodec` 구현체로 명령·스냅샷 직렬화 포맷 결정
- `IPvpTransport` 구현체로 NGO·Mirror·Photon·WebSocket 중 하나 연결
- `PvpTurnPackage` 전체를 `SimulationEngine`의 권위 명령 배치로 실행하는 어댑터 구현
- 시장 외 생산·건설·연구·미션·전쟁 PvP 명령 Translator 구현
- 전체 월드 상태 Canonical Snapshot과 StateHash 구현
- DB에 매치 Revision, 명령 로그, 결과 스냅샷 저장
- 서버 턴 정산 실패 시 롤백 및 재시도
- 낙관적 UI와 서버 거절 시 되돌리기
- 패킷 유실·중복·순서 뒤바뀜·재접속 통합 테스트
- 서버 부하 테스트와 악성 Payload 퍼징

## 중요한 보안 원칙

- 클라이언트의 현금·재고·가격·승패 결과를 신뢰하지 않는다.
- 클라이언트는 다른 플레이어의 `CompanyId`로 명령할 수 없다.
- 동일 `CommandId`는 매치 전체에서 한 번만 처리한다.
- Sequence가 끊기면 서버가 예상 번호를 돌려주고 클라이언트는 재동기화한다.
- 명령 패키지 해시와 정산 후 상태 해시는 목적이 다르므로 둘 다 저장한다.
- PvP에서도 시장 가격을 직접 수정하는 명령은 허용하지 않는다.

## 서버 구현 전제

현재 코드는 전송 계층과 DB에 의존하지 않는다. 이후 WebSocket, 전용 게임 서버, REST 기반 비동기 턴 등 어떤 방식을 선택해도 `PvpTurnCoordinator`, `PvpAuthoritativeGateway`, 명령 규약은 그대로 사용할 수 있다.

실제 서버 어댑터는 로그인 토큰을 검증한 뒤 `PvpPeerContext`를 생성해야 한다. `PvpAuthoritativeGateway`는 인증된 신원과 요청의 PlayerId가 다르면 거절하고, 같은 요청이 재전송되면 캐시된 동일 응답을 반환한다. 같은 RequestId를 다른 Payload로 재사용하면 공격 또는 클라이언트 버그로 보고 거절한다.
