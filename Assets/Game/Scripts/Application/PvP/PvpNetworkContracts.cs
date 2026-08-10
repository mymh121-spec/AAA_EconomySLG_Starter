using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Application.PvP
{
    public static class PvpProtocol
    {
        public const int CurrentVersion = 1;
        public const int MaxFrameBytes = 256 * 1024;
        public const int DefaultRequestCacheCapacity = 2048;
    }

    public enum PvpTransportState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Faulted
    }

    public enum PvpDeliveryMode
    {
        ReliableOrdered,
        ReliableUnordered,
        Unreliable
    }

    public enum PvpNetworkMessageKind
    {
        Hello,
        ClientRequest,
        ServerResponse,
        Snapshot,
        TurnPackage,
        Ping,
        Pong
    }

    public sealed class PvpConnectionOptions
    {
        public string Endpoint { get; }
        public string AccessToken { get; }

        public PvpConnectionOptions(string endpoint, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("서버 주소가 필요합니다.", nameof(endpoint));

            Endpoint = endpoint.Trim();
            AccessToken = accessToken ?? string.Empty;
        }
    }

    public sealed class PvpNetworkEnvelope
    {
        private readonly byte[] _payload;

        public int ProtocolVersion { get; }
        public string MessageId { get; }
        public string CorrelationId { get; }
        public PvpNetworkMessageKind Kind { get; }
        public PvpMatchId MatchId { get; }
        public PvpPlayerId PlayerId { get; }
        public int Turn { get; }
        public int Revision { get; }
        public byte[] Payload => CopyPayload();

        public PvpNetworkEnvelope(
            int protocolVersion,
            string messageId,
            string correlationId,
            PvpNetworkMessageKind kind,
            PvpMatchId matchId,
            PvpPlayerId playerId,
            int turn,
            int revision,
            byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(messageId))
                throw new ArgumentException("메시지 ID가 필요합니다.", nameof(messageId));
            if (payload != null && payload.Length > PvpProtocol.MaxFrameBytes)
                throw new ArgumentOutOfRangeException(nameof(payload));

            ProtocolVersion = protocolVersion;
            MessageId = messageId.Trim();
            CorrelationId = correlationId ?? string.Empty;
            Kind = kind;
            MatchId = matchId;
            PlayerId = playerId;
            Turn = turn;
            Revision = revision;
            _payload = payload == null
                ? Array.Empty<byte>()
                : (byte[])payload.Clone();
        }

        public byte[] CopyPayload() => (byte[])_payload.Clone();
    }

    public interface IPvpMessageCodec
    {
        byte[] Encode(PvpNetworkEnvelope envelope);

        bool TryDecode(
            byte[] bytes,
            out PvpNetworkEnvelope envelope,
            out string error);
    }

    public interface IPvpTransport : IDisposable
    {
        PvpTransportState State { get; }

        event Action<PvpNetworkEnvelope> MessageReceived;
        event Action<PvpTransportState> StateChanged;

        Task ConnectAsync(
            PvpConnectionOptions options,
            CancellationToken cancellationToken);

        Task SendAsync(
            PvpNetworkEnvelope envelope,
            PvpDeliveryMode deliveryMode,
            CancellationToken cancellationToken);

        Task DisconnectAsync(CancellationToken cancellationToken);
    }
}
