using Game.Application.PvP;

namespace Game.Presentation
{
    public static class PvpKoreanFormatter
    {
        public static string Operation(PvpOperationCode code)
        {
            switch (code)
            {
                case PvpOperationCode.Accepted:
                    return "요청 승인";
                case PvpOperationCode.MatchFinished:
                    return "이미 종료된 대전입니다.";
                case PvpOperationCode.NotPlanning:
                    return "계획 단계에서만 명령을 제출할 수 있습니다.";
                case PvpOperationCode.NotLocked:
                    return "모든 플레이어의 준비가 필요합니다.";
                case PvpOperationCode.NotResolving:
                    return "현재 턴을 정산 중이 아닙니다.";
                case PvpOperationCode.WrongMatch:
                    return "다른 대전의 명령입니다.";
                case PvpOperationCode.WrongTurn:
                    return "현재 턴과 일치하지 않는 명령입니다.";
                case PvpOperationCode.UnknownPlayer:
                    return "대전 참가자를 찾을 수 없습니다.";
                case PvpOperationCode.PlayerDisconnected:
                    return "연결이 끊긴 플레이어입니다.";
                case PvpOperationCode.PlayerEliminated:
                    return "이미 탈락한 플레이어입니다.";
                case PvpOperationCode.PlayerAlreadyReady:
                    return "준비 완료 후에는 명령을 변경할 수 없습니다.";
                case PvpOperationCode.CompanyOwnershipMismatch:
                    return "소유하지 않은 회사에는 명령할 수 없습니다.";
                case PvpOperationCode.SequenceMismatch:
                    return "명령 순서가 맞지 않습니다. 재동기화가 필요합니다.";
                case PvpOperationCode.DuplicateCommand:
                    return "이미 처리된 명령입니다.";
                case PvpOperationCode.InvalidPayload:
                    return "명령 데이터가 올바르지 않습니다.";
                case PvpOperationCode.InsufficientActionPoints:
                    return "남은 행동력이 부족합니다.";
                case PvpOperationCode.CommandLimitExceeded:
                    return "한 턴 최대 명령 수를 초과했습니다.";
                case PvpOperationCode.NoCommandsToCancel:
                    return "취소할 명령이 없습니다.";
                case PvpOperationCode.NotLastCommand:
                    return "가장 마지막 명령만 취소할 수 있습니다.";
                case PvpOperationCode.InvalidStateHash:
                    return "서버 상태 체크섬이 올바르지 않습니다.";
                case PvpOperationCode.ProtocolMismatch:
                    return "클라이언트와 서버의 대전 프로토콜 버전이 다릅니다.";
                case PvpOperationCode.AuthenticationMismatch:
                    return "로그인한 플레이어와 명령 제출자가 일치하지 않습니다.";
                case PvpOperationCode.StaleRevision:
                    return "서버 상태가 더 최신입니다. 대전 상태를 다시 동기화합니다.";
                case PvpOperationCode.DuplicateRequestConflict:
                    return "같은 요청 ID에 서로 다른 명령이 전송되었습니다.";
                case PvpOperationCode.UnsupportedRequest:
                    return "서버가 지원하지 않는 대전 요청입니다.";
                default:
                    return "알 수 없는 대전 오류입니다.";
            }
        }

        public static string Phase(PvpMatchPhase phase)
        {
            switch (phase)
            {
                case PvpMatchPhase.Lobby:
                    return "대전 대기실";
                case PvpMatchPhase.Planning:
                    return "명령 계획";
                case PvpMatchPhase.Locked:
                    return "명령 잠금";
                case PvpMatchPhase.Resolving:
                    return "턴 정산";
                case PvpMatchPhase.Finished:
                    return "대전 종료";
                default:
                    return "알 수 없음";
            }
        }
    }
}
