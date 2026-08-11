using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Application.PvP
{
    public enum PvpMatchmakingStatus
    {
        Unavailable,
        Idle,
        Initializing,
        Searching,
        Matched,
        TimedOut,
        Cancelled,
        Failed
    }

    public sealed class PvpMatchmakingRequest
    {
        public const int MaxExtraDataLength = 256;

        public int MatchId { get; }
        public int Point { get; }
        public string ExtraData { get; }

        public PvpMatchmakingRequest(
            int matchId,
            int point,
            string extraData)
        {
            if (matchId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(matchId),
                    "HIVE 콘솔에 등록한 1 이상의 매치 ID가 필요합니다.");
            if (point < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(point),
                    "매칭 점수는 0 이상이어야 합니다.");

            string normalizedExtraData = extraData ?? string.Empty;
            if (normalizedExtraData.Length > MaxExtraDataLength)
            {
                throw new ArgumentException(
                    $"추가 정보는 {MaxExtraDataLength}자 이하여야 합니다.",
                    nameof(extraData));
            }

            MatchId = matchId;
            Point = point;
            ExtraData = normalizedExtraData;
        }
    }

    public sealed class PvpMatchedPlayer
    {
        public long PlayerId { get; }
        public int Point { get; }
        public string ExtraData { get; }
        public int TeamIndex { get; }

        public PvpMatchedPlayer(
            long playerId,
            int point,
            string extraData,
            int teamIndex = -1)
        {
            PlayerId = playerId;
            Point = point;
            ExtraData = extraData ?? string.Empty;
            TeamIndex = teamIndex;
        }
    }

    public sealed class PvpMatchmakingSnapshot
    {
        private readonly PvpMatchedPlayer[] _players;

        public string ProviderName { get; }
        public int MatchId { get; }
        public string ExternalMatchingId { get; }
        public PvpMatchmakingStatus Status { get; }
        public string Message { get; }
        public IReadOnlyList<PvpMatchedPlayer> Players => _players;
        public bool IsTerminal =>
            Status == PvpMatchmakingStatus.Matched ||
            Status == PvpMatchmakingStatus.TimedOut ||
            Status == PvpMatchmakingStatus.Cancelled ||
            Status == PvpMatchmakingStatus.Failed ||
            Status == PvpMatchmakingStatus.Unavailable;

        public PvpMatchmakingSnapshot(
            string providerName,
            int matchId,
            PvpMatchmakingStatus status,
            string message,
            string externalMatchingId = "",
            IReadOnlyList<PvpMatchedPlayer> players = null)
        {
            ProviderName = string.IsNullOrWhiteSpace(providerName)
                ? "알 수 없는 매칭 서비스"
                : providerName.Trim();
            MatchId = matchId;
            Status = status;
            Message = message ?? string.Empty;
            ExternalMatchingId = externalMatchingId ?? string.Empty;

            if (players == null || players.Count == 0)
            {
                _players = Array.Empty<PvpMatchedPlayer>();
                return;
            }

            _players = new PvpMatchedPlayer[players.Count];
            for (int i = 0; i < players.Count; i++)
                _players[i] = players[i];
        }

        public static PvpMatchmakingSnapshot Unavailable(
            string providerName,
            string message)
        {
            return new PvpMatchmakingSnapshot(
                providerName,
                0,
                PvpMatchmakingStatus.Unavailable,
                message);
        }
    }

    public static class PvpMatchmakingStatusMapper
    {
        public static PvpMatchmakingStatus FromExternalStatus(
            string matchingStatus,
            string requestStatus = "")
        {
            switch (matchingStatus?.Trim())
            {
                case "matchingInProgress":
                    return PvpMatchmakingStatus.Searching;
                case "matched":
                    return PvpMatchmakingStatus.Matched;
                case "timeout":
                    return PvpMatchmakingStatus.TimedOut;
            }

            return string.Equals(
                requestStatus?.Trim(),
                "requested",
                StringComparison.OrdinalIgnoreCase)
                ? PvpMatchmakingStatus.Searching
                : PvpMatchmakingStatus.Idle;
        }
    }

    public interface IPvpMatchmakingProvider : IDisposable
    {
        string ProviderName { get; }
        bool IsAvailable { get; }

        Task<PvpMatchmakingSnapshot> InitializeAsync(
            CancellationToken cancellationToken);

        Task<PvpMatchmakingSnapshot> RequestAsync(
            PvpMatchmakingRequest request,
            CancellationToken cancellationToken);

        Task<PvpMatchmakingSnapshot> GetStatusAsync(
            int matchId,
            CancellationToken cancellationToken);

        Task<PvpMatchmakingSnapshot> CancelAsync(
            int matchId,
            CancellationToken cancellationToken);
    }

    public sealed class UnavailablePvpMatchmakingProvider
        : IPvpMatchmakingProvider
    {
        private readonly string _message;

        public string ProviderName { get; }
        public bool IsAvailable => false;

        public UnavailablePvpMatchmakingProvider(
            string providerName,
            string message)
        {
            ProviderName = providerName ?? "HIVE Matchmaking";
            _message = message ?? "매칭 서비스를 사용할 수 없습니다.";
        }

        public Task<PvpMatchmakingSnapshot> InitializeAsync(
            CancellationToken cancellationToken)
        {
            return CompletedUnavailable();
        }

        public Task<PvpMatchmakingSnapshot> RequestAsync(
            PvpMatchmakingRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            return CompletedUnavailable();
        }

        public Task<PvpMatchmakingSnapshot> GetStatusAsync(
            int matchId,
            CancellationToken cancellationToken)
        {
            return CompletedUnavailable();
        }

        public Task<PvpMatchmakingSnapshot> CancelAsync(
            int matchId,
            CancellationToken cancellationToken)
        {
            return CompletedUnavailable();
        }

        public void Dispose()
        {
        }

        private Task<PvpMatchmakingSnapshot> CompletedUnavailable()
        {
            return Task.FromResult(PvpMatchmakingSnapshot.Unavailable(
                ProviderName,
                _message));
        }
    }
}
