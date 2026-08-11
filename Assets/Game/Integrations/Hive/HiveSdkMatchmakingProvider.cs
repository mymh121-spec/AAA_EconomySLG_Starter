using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Application.PvP;

#if HIVE_MATCHMAKING_ENABLED
using hive;
#endif

namespace Game.HiveIntegration
{
    // HIVE SDK는 asmdef가 없는 Assembly-CSharp로 설치됩니다. 게임 코어의
    // asmdef가 외부 SDK를 직접 참조하지 않도록 이 어댑터만 기본 어셈블리에
    // 둡니다. Presentation은 IPvpMatchmakingProvider만 사용합니다.
    public sealed class HiveSdkMatchmakingProvider
        : IPvpMatchmakingProvider
    {
        private const string MissingSdkMessage =
            "HIVE 연결 모듈이 비활성 상태입니다. HIVE Unity Interface와 " +
            "Windows 패키지를 설치한 뒤 HIVE_MATCHMAKING_ENABLED를 " +
            "Standalone 정의 기호에 추가하세요.";

        private bool _initialized;
        private bool _disposed;

        public string ProviderName => "HIVE Matchmaking";

#if HIVE_MATCHMAKING_ENABLED
        public bool IsAvailable => !_disposed;
#else
        public bool IsAvailable => false;
#endif

        public Task<PvpMatchmakingSnapshot> InitializeAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

#if HIVE_MATCHMAKING_ENABLED
            if (!_initialized)
            {
                HIVEUnityPlugin.InitPlugin();
                _initialized = true;
            }

            return Task.FromResult(new PvpMatchmakingSnapshot(
                ProviderName,
                0,
                PvpMatchmakingStatus.Idle,
                "HIVE 연결 모듈을 초기화했습니다."));
#else
            return Task.FromResult(PvpMatchmakingSnapshot.Unavailable(
                ProviderName,
                MissingSdkMessage));
#endif
        }

        public async Task<PvpMatchmakingSnapshot> RequestAsync(
            PvpMatchmakingRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            ThrowIfDisposed();

            PvpMatchmakingSnapshot initialized =
                await InitializeAsync(cancellationToken);
            if (initialized.Status == PvpMatchmakingStatus.Unavailable)
                return initialized;

#if HIVE_MATCHMAKING_ENABLED
            var completion = NewCompletionSource();
            MatchMaking.requestMatchMaking(
                request.MatchId,
                request.Point,
                request.ExtraData,
                (result, data) =>
                {
                    completion.TrySetResult(ConvertResult(
                        result,
                        data,
                        request.MatchId));
                });
            return await AwaitWithCancellation(
                completion.Task,
                cancellationToken);
#else
            return PvpMatchmakingSnapshot.Unavailable(
                ProviderName,
                MissingSdkMessage);
#endif
        }

        public async Task<PvpMatchmakingSnapshot> GetStatusAsync(
            int matchId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (matchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matchId));

            PvpMatchmakingSnapshot initialized =
                await InitializeAsync(cancellationToken);
            if (initialized.Status == PvpMatchmakingStatus.Unavailable)
                return initialized;

#if HIVE_MATCHMAKING_ENABLED
            var completion = NewCompletionSource();
            MatchMaking.getRequestingStatus(matchId, (result, data) =>
            {
                completion.TrySetResult(ConvertResult(
                    result,
                    data,
                    matchId));
            });
            return await AwaitWithCancellation(
                completion.Task,
                cancellationToken);
#else
            return PvpMatchmakingSnapshot.Unavailable(
                ProviderName,
                MissingSdkMessage);
#endif
        }

        public async Task<PvpMatchmakingSnapshot> CancelAsync(
            int matchId,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (matchId <= 0)
                throw new ArgumentOutOfRangeException(nameof(matchId));

            PvpMatchmakingSnapshot initialized =
                await InitializeAsync(cancellationToken);
            if (initialized.Status == PvpMatchmakingStatus.Unavailable)
                return initialized;

#if HIVE_MATCHMAKING_ENABLED
            var completion = NewCompletionSource();
            MatchMaking.deleteRequesting(matchId, result =>
            {
                if (!result.isSuccess())
                {
                    completion.TrySetResult(CreateFailure(
                        matchId,
                        result));
                    return;
                }

                completion.TrySetResult(new PvpMatchmakingSnapshot(
                    ProviderName,
                    matchId,
                    PvpMatchmakingStatus.Cancelled,
                    "HIVE 매칭 요청을 취소했습니다."));
            });
            return await AwaitWithCancellation(
                completion.Task,
                cancellationToken);
#else
            return PvpMatchmakingSnapshot.Unavailable(
                ProviderName,
                MissingSdkMessage);
#endif
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private static TaskCompletionSource<PvpMatchmakingSnapshot>
            NewCompletionSource()
        {
            return new TaskCompletionSource<PvpMatchmakingSnapshot>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task<PvpMatchmakingSnapshot>
            AwaitWithCancellation(
                Task<PvpMatchmakingSnapshot> operation,
                CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return await operation;

            var cancelled = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(
                () => cancelled.TrySetResult(true)))
            {
                Task completed = await Task.WhenAny(
                    operation,
                    cancelled.Task);
                if (completed != operation)
                    throw new OperationCanceledException(cancellationToken);
            }

            return await operation;
        }

#if HIVE_MATCHMAKING_ENABLED
        private PvpMatchmakingSnapshot ConvertResult(
            ResultAPI result,
            MatchMaking.MatchMakingData data,
            int requestedMatchId)
        {
            if (result == null || !result.isSuccess())
                return CreateFailure(requestedMatchId, result);
            if (data == null)
            {
                return new PvpMatchmakingSnapshot(
                    ProviderName,
                    requestedMatchId,
                    PvpMatchmakingStatus.Failed,
                    "HIVE가 빈 매칭 정보를 반환했습니다.");
            }

            PvpMatchmakingStatus status =
                PvpMatchmakingStatusMapper.FromExternalStatus(
                    data.matchingStatus,
                    data.requestStatus);
            var players = new List<PvpMatchedPlayer>();

            if (data.matchingPlayerInfoList != null)
            {
                for (int i = 0; i < data.matchingPlayerInfoList.Count; i++)
                {
                    MatchMaking.MatchingResultPlayerInfo player =
                        data.matchingPlayerInfoList[i];
                    AddPlayer(players, player, -1);
                }
            }

            if (data.matchingTeamInfoList != null)
            {
                for (int i = 0; i < data.matchingTeamInfoList.Count; i++)
                {
                    MatchMaking.MatchingResultTeamInfo team =
                        data.matchingTeamInfoList[i];
                    if (team?.playerInfos == null)
                        continue;

                    for (int playerIndex = 0;
                        playerIndex < team.playerInfos.Count;
                        playerIndex++)
                    {
                        AddPlayer(
                            players,
                            team.playerInfos[playerIndex],
                            team.teamIndex);
                    }
                }
            }

            int matchId = data.requestMatchId > 0
                ? data.requestMatchId
                : requestedMatchId;
            return new PvpMatchmakingSnapshot(
                ProviderName,
                matchId,
                status,
                BuildStatusMessage(status, players.Count),
                data.matchingId,
                players);
        }

        private static void AddPlayer(
            ICollection<PvpMatchedPlayer> target,
            MatchMaking.MatchingResultPlayerInfo player,
            int teamIndex)
        {
            if (player == null)
                return;

            target.Add(new PvpMatchedPlayer(
                player.playerId,
                player.point,
                player.extraData,
                teamIndex));
        }

        private PvpMatchmakingSnapshot CreateFailure(
            int matchId,
            ResultAPI result)
        {
            string details = result == null
                ? "결과 정보 없음"
                : $"{result.errorCode}: {result.errorMessage}";
            return new PvpMatchmakingSnapshot(
                ProviderName,
                matchId,
                PvpMatchmakingStatus.Failed,
                "HIVE 매칭 요청에 실패했습니다. " + details);
        }
#endif

        private static string BuildStatusMessage(
            PvpMatchmakingStatus status,
            int playerCount)
        {
            switch (status)
            {
                case PvpMatchmakingStatus.Searching:
                    return "HIVE에서 비슷한 조건의 참가자를 찾고 있습니다.";
                case PvpMatchmakingStatus.Matched:
                    return $"HIVE 매칭 완료: {playerCount}명이 연결되었습니다.";
                case PvpMatchmakingStatus.TimedOut:
                    return "HIVE 매칭 시간이 초과되었습니다. 취소 후 다시 시도하세요.";
                case PvpMatchmakingStatus.Idle:
                    return "진행 중인 HIVE 매칭이 없습니다.";
                default:
                    return "HIVE 매칭 상태를 확인했습니다.";
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HiveSdkMatchmakingProvider));
        }
    }
}
