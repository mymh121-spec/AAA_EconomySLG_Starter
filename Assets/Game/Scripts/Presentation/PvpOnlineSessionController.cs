using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Game.Application.PvP;
using UnityEngine;

namespace Game.Presentation
{
    public enum PvpRealtimeConnectionState
    {
        Stopped,
        Connecting,
        Connected,
        Reconnecting
    }

    public sealed class PvpOnlineSessionController : MonoBehaviour
    {
        private sealed class RealtimeNotification
        {
            public PvpRealtimeConnectionState State;
            public string Error;
        }

        private const string HiveProviderTypeName =
            "Game.HiveIntegration.HiveSdkMatchmakingProvider, Assembly-CSharp";
        private const int MaximumStreamMessageBytes = 2 * 1024 * 1024;

        [Header("PvP 서버")]
        [SerializeField] private string serverEndpoint =
            "http://127.0.0.1:5200";

        [Header("HIVE 매칭")]
        [SerializeField, Range(3f, 10f)]
        private float hivePollingSeconds = 5f;

        private PvpHttpClient _client;
        private CancellationTokenSource _lifetime;
        private IPvpMatchmakingProvider _matchmakingProvider;
        private CancellationTokenSource _matchmakingLifetime;
        private int _activeHiveMatchId;
        private readonly ConcurrentQueue<string> _streamMessages =
            new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<RealtimeNotification>
            _streamNotifications =
                new ConcurrentQueue<RealtimeNotification>();
        private CancellationTokenSource _streamLifetime;
        private Task _streamTask;
        private string _sessionAccessToken = string.Empty;
        private string _sessionRoomCode = string.Empty;
        private string _lastStreamId = string.Empty;
        private long _lastStreamVersion = -1;

        public bool IsConnected { get; private set; }
        public bool IsRequestRunning { get; private set; }
        public bool IsMatchmakingRequestRunning { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public PvpReconnectDto CurrentState { get; private set; }
        public PvpRoomSessionDto CurrentRoomSession { get; private set; }
        public PvpRoomStateDto CurrentRoom { get; private set; }
        public PvpMatchmakingSnapshot CurrentMatchmaking { get; private set; }
        public PvpRealtimeConnectionState RealtimeConnectionState
            { get; private set; } = PvpRealtimeConnectionState.Stopped;
        public string LastRealtimeError { get; private set; } = string.Empty;
        public DateTimeOffset? LastRealtimeMessageUtc { get; private set; }
        public string ServerEndpoint => serverEndpoint;
        public string RoomCode => CurrentRoom?.roomCode ?? string.Empty;
        public bool IsRoomHost => CurrentRoomSession?.isHost == true;
        public bool IsHiveMatchmakingAvailable =>
            EnsureMatchmakingProvider().IsAvailable;

        public event Action<PvpReconnectDto> StateChanged;
        public event Action<PvpRoomStateDto> RoomChanged;
        public event Action<PvpMatchmakingSnapshot> MatchmakingChanged;
        public event Action<PvpRealtimeConnectionState>
            RealtimeConnectionChanged;
        public event Action<string> ErrorRaised;

        private void Update()
        {
            RealtimeNotification latestNotification = null;
            while (_streamNotifications.TryDequeue(
                       out RealtimeNotification notification))
            {
                latestNotification = notification;
            }
            if (latestNotification != null)
            {
                RealtimeConnectionState = latestNotification.State;
                LastRealtimeError = latestNotification.Error ?? string.Empty;
                RealtimeConnectionChanged?.Invoke(
                    RealtimeConnectionState);
            }

            string latestMessage = null;
            while (_streamMessages.TryDequeue(out string message))
                latestMessage = message;
            if (!string.IsNullOrWhiteSpace(latestMessage))
                ApplyRealtimeMessage(latestMessage);
        }

        public bool ConfigureServerEndpoint(string endpoint)
        {
            if (IsConnected || IsRequestRunning)
            {
                SetError("연결 중에는 서버 주소를 변경할 수 없습니다.");
                return false;
            }
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            {
                SetError("올바른 서버 주소를 입력해야 합니다.");
                return false;
            }

            serverEndpoint = endpoint.TrimEnd('/');
            LastError = string.Empty;
            return true;
        }

        public async Task<bool> ConnectAsync(string accessToken)
        {
            return await ConnectAsync(string.Empty, accessToken);
        }

        public async Task<bool> ConnectAsync(
            string roomCode,
            string accessToken)
        {
            if (IsRequestRunning || IsMatchmakingRequestRunning)
                return false;

            DisposeClient();
            _lifetime = new CancellationTokenSource();
            IsRequestRunning = true;

            try
            {
                _client = new PvpHttpClient(serverEndpoint, accessToken, roomCode);
                _sessionAccessToken = accessToken;
                _sessionRoomCode = roomCode?.Trim().ToUpperInvariant() ??
                    string.Empty;
                await RefreshAsync(_lifetime.Token);
                IsConnected = true;
                if (!string.IsNullOrWhiteSpace(roomCode))
                {
                    CurrentRoomSession = new PvpRoomSessionDto
                    {
                        roomCode = roomCode.Trim().ToUpperInvariant(),
                        accessToken = string.Empty,
                        isHost = false
                    };
                    CurrentRoom = await _client.GetRoomAsync(_lifetime.Token);
                    RoomChanged?.Invoke(CurrentRoom);
                }
                StartRealtimeStream();
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                DisposeClient();
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> CreateRoomAsync(
            string displayName,
            int maxPlayers)
        {
            if (IsRequestRunning || IsMatchmakingRequestRunning)
                return false;

            DisposeClient();
            _lifetime = new CancellationTokenSource();
            IsRequestRunning = true;
            try
            {
                PvpRoomSessionDto session = await PvpHttpClient.CreateRoomAsync(
                    serverEndpoint,
                    displayName,
                    maxPlayers,
                    _lifetime.Token);
                BeginRoomSession(session);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                DisposeClient();
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> JoinRoomAsync(
            string roomCode,
            string displayName)
        {
            if (IsRequestRunning || IsMatchmakingRequestRunning)
                return false;

            DisposeClient();
            _lifetime = new CancellationTokenSource();
            IsRequestRunning = true;
            try
            {
                PvpRoomSessionDto session = await PvpHttpClient.JoinRoomAsync(
                    serverEndpoint,
                    roomCode,
                    displayName,
                    _lifetime.Token);
                BeginRoomSession(session);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                DisposeClient();
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> RefreshRoomAsync()
        {
            if (_client == null || CurrentRoomSession == null || IsRequestRunning)
                return false;

            IsRequestRunning = true;
            try
            {
                CurrentRoom = await _client.GetRoomAsync(_lifetime.Token);
                RoomChanged?.Invoke(CurrentRoom);
                if (string.Equals(CurrentRoom.status, "Active", StringComparison.Ordinal))
                {
                    await RefreshAsync(_lifetime.Token);
                    IsConnected = true;
                    StartRealtimeStream();
                }
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> StartRoomAsync()
        {
            if (_client == null || CurrentRoomSession == null ||
                !CurrentRoomSession.isHost || IsRequestRunning)
            {
                return false;
            }

            IsRequestRunning = true;
            try
            {
                CurrentRoom = await _client.StartRoomAsync(_lifetime.Token);
                RoomChanged?.Invoke(CurrentRoom);
                if (!string.Equals(CurrentRoom.status, "Active", StringComparison.Ordinal))
                    return false;

                await RefreshAsync(_lifetime.Token);
                IsConnected = true;
                StartRealtimeStream();
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> SubmitMarketOrderAsync(
            bool isBuy,
            string resourceId,
            double quantity,
            double limitPrice)
        {
            if (!CanSendRequest() ||
                string.IsNullOrWhiteSpace(resourceId) ||
                quantity <= 0d ||
                limitPrice <= 0d)
            {
                return false;
            }

            PvpPlayerStateDto ownPlayer = FindOwnPlayer();
            if (ownPlayer == null)
            {
                SetError("서버 상태에서 내 플레이어 슬롯을 찾지 못했습니다.");
                return false;
            }

            IsRequestRunning = true;
            try
            {
                string id = Guid.NewGuid().ToString("N");
                var request = new PvpSubmitCommandDto
                {
                    requestId = "request_" + id,
                    matchId = CurrentState.matchId,
                    expectedRevision = CurrentState.revision,
                    commandId = "command_" + id,
                    turn = CurrentState.turn,
                    sequence = ownPlayer.expectedSequence,
                    kind = isBuy ? "MarketBuy" : "MarketSell",
                    regionId = "capital",
                    resourceId = resourceId,
                    quantity = quantity,
                    limitPrice = limitPrice
                };

                PvpCommandResponseDto response = await _client.SubmitCommandAsync(
                    request,
                    _lifetime.Token);
                if (!response.accepted)
                {
                    SetError(response.message);
                    await RefreshAsync(_lifetime.Token);
                    return false;
                }

                await RefreshAsync(_lifetime.Token);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task<bool> MarkReadyAsync()
        {
            if (!CanSendRequest())
                return false;

            PvpPlayerStateDto ownPlayer = FindOwnPlayer();
            if (ownPlayer == null)
            {
                SetError("서버 상태에서 내 플레이어 슬롯을 찾지 못했습니다.");
                return false;
            }

            IsRequestRunning = true;
            try
            {
                var request = new PvpReadyRequestDto
                {
                    requestId = "ready_" + Guid.NewGuid().ToString("N"),
                    matchId = CurrentState.matchId,
                    turn = CurrentState.turn,
                    expectedRevision = CurrentState.revision,
                    lastSequence = ownPlayer.expectedSequence
                };
                PvpReadyResponseDto response = await _client.MarkReadyAsync(
                    request,
                    _lifetime.Token);
                if (!response.accepted)
                {
                    SetError(response.message);
                    await RefreshAsync(_lifetime.Token);
                    return false;
                }

                await RefreshAsync(_lifetime.Token);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        public async Task RefreshAsync(
            CancellationToken cancellationToken = default)
        {
            if (_client == null)
            {
                throw new InvalidOperationException(
                    "PvP 서버에 먼저 연결해야 합니다.");
            }

            PvpReconnectDto state = await _client.GetMatchAsync(
                cancellationToken);
            TryApplyAuthoritativeState(
                state,
                state.streamId,
                state.stateVersion);
        }

        public async Task<PvpMatchmakingSnapshot> FindHiveMatchAsync(
            int matchId,
            int point,
            string extraData)
        {
            if (IsMatchmakingRequestRunning)
                return CurrentMatchmaking;

            var request = new PvpMatchmakingRequest(
                matchId,
                point,
                extraData);
            IPvpMatchmakingProvider provider =
                EnsureMatchmakingProvider();
            var lifetime = new CancellationTokenSource();
            _matchmakingLifetime = lifetime;
            _activeHiveMatchId = matchId;
            IsMatchmakingRequestRunning = true;

            try
            {
                PublishMatchmaking(new PvpMatchmakingSnapshot(
                    provider.ProviderName,
                    matchId,
                    PvpMatchmakingStatus.Initializing,
                    "HIVE 연결 모듈을 준비하고 있습니다."));

                PvpMatchmakingSnapshot snapshot =
                    await provider.RequestAsync(request, lifetime.Token);
                PublishMatchmaking(snapshot);

                while (snapshot.Status == PvpMatchmakingStatus.Searching)
                {
                    int delayMilliseconds = Mathf.RoundToInt(
                        Mathf.Clamp(hivePollingSeconds, 3f, 10f) * 1000f);
                    await Task.Delay(delayMilliseconds, lifetime.Token);
                    snapshot = await provider.GetStatusAsync(
                        matchId,
                        lifetime.Token);
                    PublishMatchmaking(snapshot);
                }

                if (snapshot.Status == PvpMatchmakingStatus.Unavailable ||
                    snapshot.Status == PvpMatchmakingStatus.Cancelled ||
                    snapshot.Status == PvpMatchmakingStatus.Idle)
                {
                    _activeHiveMatchId = 0;
                }

                return snapshot;
            }
            catch (OperationCanceledException)
            {
                var cancelled = new PvpMatchmakingSnapshot(
                    provider.ProviderName,
                    matchId,
                    PvpMatchmakingStatus.Cancelled,
                    "HIVE 매칭 확인을 중단했습니다.");
                PublishMatchmaking(cancelled);
                return cancelled;
            }
            catch (Exception exception)
            {
                var failed = new PvpMatchmakingSnapshot(
                    provider.ProviderName,
                    matchId,
                    PvpMatchmakingStatus.Failed,
                    exception.Message);
                PublishMatchmaking(failed);
                SetError(exception.Message);
                return failed;
            }
            finally
            {
                if (ReferenceEquals(_matchmakingLifetime, lifetime))
                {
                    _matchmakingLifetime.Dispose();
                    _matchmakingLifetime = null;
                }
                IsMatchmakingRequestRunning = false;
            }
        }

        public async Task<PvpMatchmakingSnapshot> CancelHiveMatchmakingAsync()
        {
            int matchId = _activeHiveMatchId;
            if (matchId <= 0)
            {
                return CurrentMatchmaking ?? new PvpMatchmakingSnapshot(
                    "HIVE Matchmaking",
                    0,
                    PvpMatchmakingStatus.Idle,
                    "진행 중인 HIVE 매칭이 없습니다.");
            }

            _matchmakingLifetime?.Cancel();
            IPvpMatchmakingProvider provider =
                EnsureMatchmakingProvider();
            try
            {
                PvpMatchmakingSnapshot snapshot =
                    await provider.CancelAsync(
                        matchId,
                        CancellationToken.None);
                PublishMatchmaking(snapshot);
                return snapshot;
            }
            catch (Exception exception)
            {
                var failed = new PvpMatchmakingSnapshot(
                    provider.ProviderName,
                    matchId,
                    PvpMatchmakingStatus.Failed,
                    exception.Message);
                PublishMatchmaking(failed);
                SetError(exception.Message);
                return failed;
            }
            finally
            {
                _activeHiveMatchId = 0;
            }
        }

        public void Disconnect()
        {
            if (_activeHiveMatchId > 0)
                _ = CancelHiveMatchmakingAsync();
            else
                _matchmakingLifetime?.Cancel();

            DisposeClient();
        }

        private bool CanSendRequest()
        {
            if (!IsConnected || _client == null || CurrentState == null)
            {
                SetError("PvP 서버에 연결되어 있지 않습니다.");
                return false;
            }
            if (IsRequestRunning)
            {
                SetError("이전 서버 요청을 처리하고 있습니다.");
                return false;
            }

            return true;
        }

        private PvpPlayerStateDto FindOwnPlayer()
        {
            if (CurrentState?.players == null)
                return null;

            for (int i = 0; i < CurrentState.players.Length; i++)
            {
                if (CurrentState.players[i].playerId == CurrentState.playerId)
                    return CurrentState.players[i];
            }

            return null;
        }

        private void SetError(string message)
        {
            LastError = string.IsNullOrWhiteSpace(message)
                ? "PvP 요청에 실패했습니다."
                : message;
            ErrorRaised?.Invoke(LastError);
            Debug.LogWarning(LastError);
        }

        private void PublishMatchmaking(PvpMatchmakingSnapshot snapshot)
        {
            CurrentMatchmaking = snapshot;
            MatchmakingChanged?.Invoke(snapshot);
        }

        public async Task<bool> SubmitMapOrderAsync(
            PvpCommandKind kind,
            string unitId,
            int targetX,
            int targetY,
            string action = "")
        {
            if (!CanSendRequest() ||
                string.IsNullOrWhiteSpace(unitId) ||
                (kind != PvpCommandKind.MoveUnit &&
                 kind != PvpCommandKind.OccupyResourceSite &&
                 kind != PvpCommandKind.OccupyCastle &&
                 kind != PvpCommandKind.StartSiege &&
                 kind != PvpCommandKind.CancelOrder))
            {
                return false;
            }

            PvpPlayerStateDto ownPlayer = FindOwnPlayer();
            if (ownPlayer == null)
            {
                SetError("서버 상태에서 내 플레이어 슬롯을 찾지 못했습니다.");
                return false;
            }

            IsRequestRunning = true;
            try
            {
                string id = Guid.NewGuid().ToString("N");
                var request = new PvpSubmitCommandDto
                {
                    requestId = "map_request_" + id,
                    matchId = CurrentState.matchId,
                    expectedRevision = CurrentState.revision,
                    commandId = "map_command_" + id,
                    turn = CurrentState.turn,
                    sequence = ownPlayer.expectedSequence,
                    kind = kind.ToString(),
                    regionId = "map",
                    targetId = unitId,
                    targetX = targetX,
                    targetY = targetY,
                    action = action ?? string.Empty
                };

                PvpCommandResponseDto response =
                    await _client.SubmitCommandAsync(
                        request,
                        _lifetime.Token);
                if (!response.accepted)
                {
                    SetError(response.message);
                    await RefreshAsync(_lifetime.Token);
                    return false;
                }

                await RefreshAsync(_lifetime.Token);
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                SetError(exception.Message);
                return false;
            }
            finally
            {
                IsRequestRunning = false;
            }
        }

        private void BeginRoomSession(PvpRoomSessionDto session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.accessToken) ||
                string.IsNullOrWhiteSpace(session.roomCode))
            {
                throw new InvalidOperationException("서버가 유효한 방 세션을 발급하지 않았습니다.");
            }

            string accessToken = session.accessToken;
            _sessionAccessToken = accessToken;
            _sessionRoomCode = session.roomCode.Trim().ToUpperInvariant();
            _client = new PvpHttpClient(serverEndpoint, accessToken, session.roomCode);
            session.accessToken = string.Empty;
            CurrentRoomSession = session;
            CurrentRoom = session.room;
            RoomChanged?.Invoke(CurrentRoom);
        }

        private IPvpMatchmakingProvider EnsureMatchmakingProvider()
        {
            if (_matchmakingProvider != null)
                return _matchmakingProvider;

            Type providerType = Type.GetType(HiveProviderTypeName, false);
            if (providerType != null &&
                typeof(IPvpMatchmakingProvider).IsAssignableFrom(providerType))
            {
                try
                {
                    _matchmakingProvider =
                        (IPvpMatchmakingProvider)Activator.CreateInstance(
                            providerType);
                    return _matchmakingProvider;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "HIVE 매칭 어댑터 생성 실패: " + exception.Message);
                }
            }

            _matchmakingProvider =
                new UnavailablePvpMatchmakingProvider(
                    "HIVE Matchmaking",
                    "HIVE 연결 어댑터를 찾지 못했습니다. 프로젝트를 동기화한 " +
                    "뒤 Unity를 다시 실행하세요.");
            return _matchmakingProvider;
        }

        private void OnDestroy()
        {
            _matchmakingLifetime?.Cancel();
            _matchmakingLifetime?.Dispose();
            _matchmakingLifetime = null;
            _matchmakingProvider?.Dispose();
            _matchmakingProvider = null;
            DisposeClient();
        }

        private void DisposeClient()
        {
            StopRealtimeStream();
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _client?.Dispose();
            _client = null;
            IsConnected = false;
            CurrentState = null;
            CurrentRoomSession = null;
            CurrentRoom = null;
            _sessionAccessToken = string.Empty;
            _sessionRoomCode = string.Empty;
            _lastStreamId = string.Empty;
            _lastStreamVersion = -1;
        }

        private void StartRealtimeStream()
        {
            if (!IsConnected ||
                _lifetime == null ||
                string.IsNullOrWhiteSpace(_sessionAccessToken) ||
                string.IsNullOrWhiteSpace(_sessionRoomCode))
            {
                return;
            }

            StopRealtimeStream();
            LastRealtimeMessageUtc = null;
            _streamLifetime = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetime.Token);
            _streamTask = RunRealtimeStreamAsync(
                CreateStreamUri(_sessionRoomCode),
                _sessionAccessToken,
                _streamLifetime.Token);
        }

        private void StopRealtimeStream()
        {
            CancellationTokenSource lifetime = _streamLifetime;
            Task streamTask = _streamTask;
            lifetime?.Cancel();
            _streamLifetime = null;
            _streamTask = null;
            if (lifetime != null)
            {
                if (streamTask == null)
                {
                    lifetime.Dispose();
                }
                else
                {
                    _ = streamTask.ContinueWith(
                        _ => lifetime.Dispose(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }
            while (_streamMessages.TryDequeue(out _))
            {
            }
            while (_streamNotifications.TryDequeue(out _))
            {
            }
            RealtimeConnectionState = PvpRealtimeConnectionState.Stopped;
            LastRealtimeError = string.Empty;
            LastRealtimeMessageUtc = null;
        }

        private async Task RunRealtimeStreamAsync(
            Uri streamUri,
            string accessToken,
            CancellationToken cancellationToken)
        {
            int reconnectAttempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                QueueRealtimeNotification(
                    reconnectAttempt == 0
                        ? PvpRealtimeConnectionState.Connecting
                        : PvpRealtimeConnectionState.Reconnecting,
                    string.Empty);
                try
                {
                    using var socket = new ClientWebSocket();
                    socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                    socket.Options.SetRequestHeader(
                        "Authorization",
                        "Bearer " + accessToken);
                    await socket.ConnectAsync(streamUri, cancellationToken)
                        .ConfigureAwait(false);
                    reconnectAttempt = 0;
                    QueueRealtimeNotification(
                        PvpRealtimeConnectionState.Connected,
                        string.Empty);

                    while (!cancellationToken.IsCancellationRequested &&
                           socket.State == WebSocketState.Open)
                    {
                        string json = await ReceiveStreamMessageAsync(
                                socket,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (json == null)
                        {
                            throw new WebSocketException(
                                "서버가 실시간 상태 스트림을 종료했습니다.");
                        }
                        _streamMessages.Enqueue(json);
                    }
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    reconnectAttempt++;
                    QueueRealtimeNotification(
                        PvpRealtimeConnectionState.Reconnecting,
                        exception.Message);
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                int delaySeconds = Math.Min(
                    10,
                    1 << Math.Min(3, Math.Max(0, reconnectAttempt - 1)));
                try
                {
                    await Task.Delay(
                            TimeSpan.FromSeconds(delaySeconds),
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private static async Task<string> ReceiveStreamMessageAsync(
            ClientWebSocket socket,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[16 * 1024];
            using var content = new MemoryStream();
            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidDataException(
                        "PvP 상태 스트림은 텍스트 JSON만 허용합니다.");
                }

                content.Write(buffer, 0, result.Count);
                if (content.Length > MaximumStreamMessageBytes)
                {
                    throw new InvalidDataException(
                        "PvP 상태 스트림 메시지가 허용 크기를 초과했습니다.");
                }
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(
                        content.GetBuffer(),
                        0,
                        checked((int)content.Length));
                }
            }
        }

        private void ApplyRealtimeMessage(string json)
        {
            PvpStreamMessageDto message;
            try
            {
                message = JsonUtility.FromJson<PvpStreamMessageDto>(json);
            }
            catch (Exception exception)
            {
                LastRealtimeError = exception.Message;
                return;
            }

            if (message?.state == null ||
                !string.Equals(message.type, "state", StringComparison.Ordinal) ||
                (CurrentState != null &&
                 !string.Equals(
                     CurrentState.matchId,
                     message.state.matchId,
                     StringComparison.Ordinal)))
            {
                LastRealtimeError =
                    "서버 실시간 상태 메시지가 올바르지 않습니다.";
                return;
            }

            LastRealtimeError = string.Empty;
            LastRealtimeMessageUtc = DateTimeOffset.UtcNow;
            TryApplyAuthoritativeState(
                message.state,
                message.streamId,
                message.version);
        }

        private bool TryApplyAuthoritativeState(
            PvpReconnectDto state,
            string streamId,
            long stateVersion)
        {
            if (state == null)
                return false;

            string normalizedStreamId = streamId ?? string.Empty;
            if (!string.IsNullOrEmpty(normalizedStreamId))
            {
                if (!string.Equals(
                        _lastStreamId,
                        normalizedStreamId,
                        StringComparison.Ordinal))
                {
                    _lastStreamId = normalizedStreamId;
                    _lastStreamVersion = -1;
                }
                if (stateVersion <= _lastStreamVersion)
                    return false;

                _lastStreamVersion = stateVersion;
                state.streamId = normalizedStreamId;
                state.stateVersion = stateVersion;
            }
            else if (CurrentState != null &&
                     string.Equals(
                         CurrentState.matchId,
                         state.matchId,
                         StringComparison.Ordinal) &&
                     (state.turn < CurrentState.turn ||
                      (state.turn == CurrentState.turn &&
                       state.revision < CurrentState.revision)))
            {
                return false;
            }

            CurrentState = state;
            StateChanged?.Invoke(CurrentState);
            return true;
        }

        private void QueueRealtimeNotification(
            PvpRealtimeConnectionState state,
            string error)
        {
            _streamNotifications.Enqueue(new RealtimeNotification
            {
                State = state,
                Error = error ?? string.Empty
            });
        }

        private Uri CreateStreamUri(string roomCode)
        {
            var builder = new UriBuilder(serverEndpoint)
            {
                Scheme = serverEndpoint.StartsWith(
                    "https://",
                    StringComparison.OrdinalIgnoreCase)
                    ? "wss"
                    : "ws"
            };
            if ((builder.Scheme == "ws" && builder.Port == 80) ||
                (builder.Scheme == "wss" && builder.Port == 443))
            {
                builder.Port = -1;
            }
            string prefix = builder.Path.TrimEnd('/');
            builder.Path = prefix + "/api/v1/rooms/" +
                roomCode + "/stream";
            builder.Query = string.Empty;
            builder.Fragment = string.Empty;
            return builder.Uri;
        }
    }
}
