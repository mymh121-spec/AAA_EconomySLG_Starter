using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Presentation
{
    public sealed class PvpOnlineSessionController : MonoBehaviour
    {
        [Header("PvP 서버")]
        [SerializeField] private string serverEndpoint =
            "http://127.0.0.1:5200";

        private PvpHttpClient _client;
        private CancellationTokenSource _lifetime;

        public bool IsConnected { get; private set; }
        public bool IsRequestRunning { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public PvpReconnectDto CurrentState { get; private set; }
        public string ServerEndpoint => serverEndpoint;

        public event Action<PvpReconnectDto> StateChanged;
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
            if (IsRequestRunning)
                return false;

            DisposeClient();
            _lifetime = new CancellationTokenSource();
            _client = new PvpHttpClient(serverEndpoint, accessToken);
            IsRequestRunning = true;

            try
            {
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
                throw new InvalidOperationException("PvP 서버에 먼저 연결해야 합니다.");

            CurrentState = await _client.GetMatchAsync(cancellationToken);
            StateChanged?.Invoke(CurrentState);
        }

        public void Disconnect()
        {
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

        private void OnDestroy()
        {
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
