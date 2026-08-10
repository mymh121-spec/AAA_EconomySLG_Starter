using System;
using System.Collections.Generic;

namespace Game.Application.PvP
{
    public enum PvpClientRequestKind
    {
        SubmitCommand,
        CancelLastCommand,
        MarkReady,
        Reconnect,
        Ping
    }

    public sealed class PvpPeerContext
    {
        public string ConnectionId { get; }
        public PvpPlayerId AuthenticatedPlayerId { get; }

        public PvpPeerContext(
            string connectionId,
            PvpPlayerId authenticatedPlayerId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("연결 ID가 필요합니다.", nameof(connectionId));

            ConnectionId = connectionId.Trim();
            AuthenticatedPlayerId = authenticatedPlayerId;
        }
    }

    public sealed class PvpClientRequest
    {
        public int ProtocolVersion { get; }
        public string RequestId { get; }
        public PvpClientRequestKind Kind { get; }
        public PvpMatchId MatchId { get; }
        public PvpPlayerId PlayerId { get; }
        public int ExpectedRevision { get; }
        public PvpCommandEnvelope Command { get; }
        public string CommandId { get; }

        public PvpClientRequest(
            int protocolVersion,
            string requestId,
            PvpClientRequestKind kind,
            PvpMatchId matchId,
            PvpPlayerId playerId,
            int expectedRevision,
            PvpCommandEnvelope command = null,
            string commandId = null)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("요청 ID가 필요합니다.", nameof(requestId));

            ProtocolVersion = protocolVersion;
            RequestId = requestId.Trim();
            Kind = kind;
            MatchId = matchId;
            PlayerId = playerId;
            ExpectedRevision = expectedRevision;
            Command = command;
            CommandId = commandId ?? string.Empty;
        }
    }

    public sealed class PvpServerResponse
    {
        public string RequestId { get; }
        public PvpOperationResult Result { get; }
        public PvpMatchSnapshot Snapshot { get; }
        public IReadOnlyList<PvpCommandEnvelope> OwnPendingCommands { get; }
        public bool IsReplay { get; }

        public PvpServerResponse(
            string requestId,
            PvpOperationResult result,
            PvpMatchSnapshot snapshot,
            IReadOnlyList<PvpCommandEnvelope> ownPendingCommands,
            bool isReplay = false)
        {
            RequestId = requestId ?? string.Empty;
            Result = result;
            Snapshot = snapshot;
            OwnPendingCommands = ownPendingCommands ??
                Array.Empty<PvpCommandEnvelope>();
            IsReplay = isReplay;
        }

        public PvpServerResponse AsReplay()
        {
            return new PvpServerResponse(
                RequestId,
                Result,
                Snapshot,
                OwnPendingCommands,
                true);
        }
    }

    public interface IPvpAuthoritativeGateway
    {
        PvpServerResponse Handle(
            PvpPeerContext peer,
            PvpClientRequest request);
    }

    public sealed class PvpAuthoritativeGateway : IPvpAuthoritativeGateway
    {
        private readonly struct RequestCacheKey : IEquatable<RequestCacheKey>
        {
            public PvpPlayerId PlayerId { get; }
            public string RequestId { get; }

            public RequestCacheKey(PvpPlayerId playerId, string requestId)
            {
                PlayerId = playerId;
                RequestId = requestId;
            }

            public bool Equals(RequestCacheKey other)
            {
                return PlayerId.Equals(other.PlayerId) &&
                    string.Equals(RequestId, other.RequestId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is RequestCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PlayerId.GetHashCode() * 397) ^
                        StringComparer.Ordinal.GetHashCode(RequestId);
                }
            }
        }

        private sealed class CachedResponse
        {
            public string RequestHash;
            public PvpServerResponse Response;
        }

        private readonly PvpTurnCoordinator _coordinator;
        private readonly int _cacheCapacity;
        private readonly Dictionary<RequestCacheKey, CachedResponse> _cache =
            new Dictionary<RequestCacheKey, CachedResponse>();
        private readonly Queue<RequestCacheKey> _cacheOrder =
            new Queue<RequestCacheKey>();

        public PvpAuthoritativeGateway(
            PvpTurnCoordinator coordinator,
            int cacheCapacity = PvpProtocol.DefaultRequestCacheCapacity)
        {
            _coordinator = coordinator ??
                throw new ArgumentNullException(nameof(coordinator));
            _cacheCapacity = Math.Max(32, cacheCapacity);
        }

        public PvpServerResponse Handle(
            PvpPeerContext peer,
            PvpClientRequest request)
        {
            if (peer == null || request == null)
            {
                return Reject(
                    request?.RequestId,
                    PvpOperationCode.InvalidPayload,
                    null);
            }

            if (request.ProtocolVersion != PvpProtocol.CurrentVersion)
            {
                return Reject(
                    request.RequestId,
                    PvpOperationCode.ProtocolMismatch,
                    null);
            }

            if (!request.MatchId.Equals(_coordinator.MatchId))
            {
                return Reject(
                    request.RequestId,
                    PvpOperationCode.WrongMatch,
                    null);
            }

            if (!peer.AuthenticatedPlayerId.Equals(request.PlayerId))
            {
                return Reject(
                    request.RequestId,
                    PvpOperationCode.AuthenticationMismatch,
                    null);
            }

            var key = new RequestCacheKey(request.PlayerId, request.RequestId);
            string requestHash = PvpChecksum.ComputeClientRequest(request);

            if (_cache.TryGetValue(key, out var cached))
            {
                if (!string.Equals(
                    cached.RequestHash,
                    requestHash,
                    StringComparison.Ordinal))
                {
                    return Reject(
                        request.RequestId,
                        PvpOperationCode.DuplicateRequestConflict,
                        request.PlayerId);
                }

                return cached.Response.AsReplay();
            }

            if (RequiresCurrentRevision(request.Kind) &&
                request.ExpectedRevision != _coordinator.Revision)
            {
                return Cache(
                    key,
                    requestHash,
                    Reject(
                        request.RequestId,
                        PvpOperationCode.StaleRevision,
                        request.PlayerId));
            }

            PvpOperationResult result;
            switch (request.Kind)
            {
                case PvpClientRequestKind.SubmitCommand:
                    result = Submit(request);
                    break;
                case PvpClientRequestKind.CancelLastCommand:
                    result = _coordinator.CancelLastCommand(
                        request.PlayerId,
                        request.CommandId);
                    break;
                case PvpClientRequestKind.MarkReady:
                    result = _coordinator.MarkReady(request.PlayerId);
                    break;
                case PvpClientRequestKind.Reconnect:
                    result = _coordinator.SetConnected(request.PlayerId, true);
                    break;
                case PvpClientRequestKind.Ping:
                    result = PvpOperationResult.Accepted();
                    break;
                default:
                    result = new PvpOperationResult(
                        PvpOperationCode.UnsupportedRequest);
                    break;
            }

            var response = new PvpServerResponse(
                request.RequestId,
                result,
                _coordinator.CreateSnapshot(),
                _coordinator.GetPendingCommands(request.PlayerId));

            return Cache(key, requestHash, response);
        }

        private PvpOperationResult Submit(PvpClientRequest request)
        {
            if (request.Command == null ||
                !request.Command.PlayerId.Equals(request.PlayerId) ||
                !request.Command.MatchId.Equals(request.MatchId))
            {
                return new PvpOperationResult(
                    PvpOperationCode.AuthenticationMismatch);
            }

            return _coordinator.SubmitCommand(request.Command);
        }

        private PvpServerResponse Reject(
            string requestId,
            PvpOperationCode code,
            PvpPlayerId? playerId)
        {
            IReadOnlyList<PvpCommandEnvelope> pending =
                Array.Empty<PvpCommandEnvelope>();

            if (playerId.HasValue)
            {
                pending = _coordinator.GetPendingCommands(
                    playerId.Value);
            }

            return new PvpServerResponse(
                requestId,
                new PvpOperationResult(code),
                _coordinator.CreateSnapshot(),
                pending);
        }

        private PvpServerResponse Cache(
            RequestCacheKey key,
            string requestHash,
            PvpServerResponse response)
        {
            while (_cache.Count >= _cacheCapacity && _cacheOrder.Count > 0)
            {
                RequestCacheKey expired = _cacheOrder.Dequeue();
                _cache.Remove(expired);
            }

            _cache[key] = new CachedResponse
            {
                RequestHash = requestHash,
                Response = response
            };
            _cacheOrder.Enqueue(key);
            return response;
        }

        private static bool RequiresCurrentRevision(PvpClientRequestKind kind)
        {
            return kind == PvpClientRequestKind.SubmitCommand ||
                kind == PvpClientRequestKind.CancelLastCommand ||
                kind == PvpClientRequestKind.MarkReady;
        }
    }
}
