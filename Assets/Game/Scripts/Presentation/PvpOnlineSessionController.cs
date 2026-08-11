using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Application.PvP;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class PvpOnlineSessionController : MonoBehaviour
    {
        private const string HiveProviderTypeName =
            "Game.HiveIntegration.HiveSdkMatchmakingProvider, Assembly-CSharp";

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

        public bool IsConnected { get; private set; }
        public bool IsRequestRunning { get; private set; }
        public bool IsMatchmakingRequestRunning { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public PvpReconnectDto CurrentState { get; private set; }
        public PvpMatchmakingSnapshot CurrentMatchmaking { get; private set; }
        public string ServerEndpoint => serverEndpoint;
        public bool IsHiveMatchmakingAvailable =>
            EnsureMatchmakingProvider().IsAvailable;

        public event Action<PvpReconnectDto> StateChanged;
        public event Action<PvpMatchmakingSnapshot> MatchmakingChanged;
        public event Action<string> ErrorRaised;

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
            if (IsRequestRunning || IsMatchmakingRequestRunning)
                return false;

            DisposeClient();
            _lifetime = new CancellationTokenSource();
            IsRequestRunning = true;

            try
            {
                _client = new PvpHttpClient(serverEndpoint, accessToken);
                await RefreshAsync(_lifetime.Token);
                IsConnected = true;
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

            CurrentState = await _client.GetMatchAsync(cancellationToken);
            StateChanged?.Invoke(CurrentState);
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
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _client?.Dispose();
            _client = null;
            IsConnected = false;
            CurrentState = null;
        }
    }
}
