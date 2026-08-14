using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Game.Presentation
{
    public sealed class PvpHttpClient : IDisposable
    {
        private readonly string _endpoint;
        private readonly string _accessToken;
        private readonly string _roomCode;
        private bool _disposed;

        public PvpHttpClient(
            string endpoint,
            string accessToken = null,
            string roomCode = null)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri uri))
                throw new ArgumentException("올바른 서버 주소가 필요합니다.", nameof(endpoint));
            if (uri.Scheme != Uri.UriSchemeHttps &&
                !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            {
                throw new ArgumentException(
                    "공개 서버는 HTTPS만 허용합니다. HTTP는 로컬 SSH 터널에서만 사용할 수 있습니다.",
                    nameof(endpoint));
            }
            if (accessToken != null && accessToken.Length < 32)
                throw new ArgumentException("32자 이상의 접속 토큰이 필요합니다.", nameof(accessToken));
            if (!string.IsNullOrWhiteSpace(roomCode) && roomCode.Trim().Length != 6)
                throw new ArgumentException("6자리 방 코드가 필요합니다.", nameof(roomCode));

            _endpoint = endpoint.TrimEnd('/');
            _accessToken = accessToken ?? string.Empty;
            _roomCode = roomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        }

        public static async Task<PvpRoomSessionDto> CreateRoomAsync(
            string endpoint,
            string displayName,
            int maxPlayers,
            CancellationToken cancellationToken = default)
        {
            using var client = new PvpHttpClient(endpoint);
            return await client.SendAsync<PvpRoomSessionDto>(
                UnityWebRequest.kHttpVerbPOST,
                "/api/v1/rooms",
                JsonUtility.ToJson(new PvpCreateRoomDto
                {
                    displayName = displayName,
                    maxPlayers = maxPlayers
                }),
                cancellationToken);
        }

        public static async Task<PvpRoomSessionDto> JoinRoomAsync(
            string endpoint,
            string roomCode,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            string normalizedCode = roomCode?.Trim().ToUpperInvariant();
            using var client = new PvpHttpClient(endpoint);
            return await client.SendAsync<PvpRoomSessionDto>(
                UnityWebRequest.kHttpVerbPOST,
                $"/api/v1/rooms/{normalizedCode}/join",
                JsonUtility.ToJson(new PvpJoinRoomDto
                {
                    displayName = displayName
                }),
                cancellationToken);
        }

        public Task<PvpRoomStateDto> GetRoomAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureRoomCode();
            return SendAsync<PvpRoomStateDto>(
                UnityWebRequest.kHttpVerbGET,
                $"/api/v1/rooms/{_roomCode}",
                null,
                cancellationToken);
        }

        public Task<PvpRoomStateDto> StartRoomAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureRoomCode();
            return SendAsync<PvpRoomStateDto>(
                UnityWebRequest.kHttpVerbPOST,
                $"/api/v1/rooms/{_roomCode}/start",
                null,
                cancellationToken);
        }

        public Task<PvpReconnectDto> GetMatchAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync<PvpReconnectDto>(
                UnityWebRequest.kHttpVerbGET,
                MatchPath("match"),
                null,
                cancellationToken);
        }

        public Task<PvpCommandResponseDto> SubmitCommandAsync(
            PvpSubmitCommandDto command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            return SendAsync<PvpCommandResponseDto>(
                UnityWebRequest.kHttpVerbPOST,
                MatchPath("commands"),
                JsonUtility.ToJson(command),
                cancellationToken,
                allowConflictResponse: true);
        }

        public Task<PvpReadyResponseDto> MarkReadyAsync(
            PvpReadyRequestDto ready,
            CancellationToken cancellationToken = default)
        {
            if (ready == null)
                throw new ArgumentNullException(nameof(ready));

            return SendAsync<PvpReadyResponseDto>(
                UnityWebRequest.kHttpVerbPOST,
                MatchPath("ready"),
                JsonUtility.ToJson(ready),
                cancellationToken,
                allowConflictResponse: true);
        }

        public void Dispose()
        {
            _disposed = true;
        }

        private async Task<T> SendAsync<T>(
            string method,
            string path,
            string json,
            CancellationToken cancellationToken,
            bool allowConflictResponse = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PvpHttpClient));

            using var request = new UnityWebRequest(_endpoint + path, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            }

            if (!string.IsNullOrWhiteSpace(_accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + _accessToken);
            request.SetRequestHeader("Accept", "application/json");
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            using CancellationTokenRegistration registration =
                cancellationToken.Register(request.Abort);
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            string responseText = request.downloadHandler?.text ?? string.Empty;
            bool acceptedHttpStatus = request.responseCode >= 200 &&
                request.responseCode < 300;
            bool acceptedConflict = allowConflictResponse &&
                request.responseCode == 409;

            if (!acceptedHttpStatus && !acceptedConflict)
            {
                throw new PvpHttpException(
                    request.responseCode,
                    string.IsNullOrWhiteSpace(responseText)
                        ? request.error
                        : responseText);
            }
            if (string.IsNullOrWhiteSpace(responseText))
                throw new PvpHttpException(request.responseCode, "서버 응답이 비어 있습니다.");

            T response = JsonUtility.FromJson<T>(responseText);
            if (response == null)
                throw new PvpHttpException(request.responseCode, "서버 JSON 응답을 읽지 못했습니다.");
            return response;
        }

        private string MatchPath(string operation)
        {
            return string.IsNullOrWhiteSpace(_roomCode)
                ? $"/api/v1/{operation}"
                : $"/api/v1/rooms/{_roomCode}/{operation}";
        }

        private void EnsureRoomCode()
        {
            if (string.IsNullOrWhiteSpace(_roomCode))
                throw new InvalidOperationException("방 코드가 설정되지 않았습니다.");
        }
    }

    public sealed class PvpHttpException : Exception
    {
        public long StatusCode { get; }

        public PvpHttpException(long statusCode, string message)
            : base(string.IsNullOrWhiteSpace(message)
                ? "PvP 서버 통신에 실패했습니다."
                : message)
        {
            StatusCode = statusCode;
        }
    }

    [Serializable]
    public sealed class PvpCreateRoomDto
    {
        public string displayName;
        public int maxPlayers;
    }

    [Serializable]
    public sealed class PvpJoinRoomDto
    {
        public string displayName;
    }

    [Serializable]
    public sealed class PvpRoomSessionDto
    {
        public string roomCode;
        public string playerId;
        public string companyId;
        public string accessToken;
        public bool isHost;
        public PvpRoomStateDto room;
    }

    [Serializable]
    public sealed class PvpRoomStateDto
    {
        public string roomCode;
        public string matchId;
        public string status;
        public int maxPlayers;
        public string createdAtUtc;
        public string lastActivityUtc;
        public PvpRoomPlayerDto[] players;
    }

    [Serializable]
    public sealed class PvpRoomPlayerDto
    {
        public int slot;
        public string playerId;
        public string displayName;
        public bool isHost;
        public bool connected;
    }

    [Serializable]
    public sealed class PvpSubmitCommandDto
    {
        public string requestId;
        public int protocolVersion = 1;
        public string matchId;
        public int expectedRevision;
        public string commandId;
        public int turn;
        public int sequence;
        public string kind;
        public string regionId;
        public string resourceId;
        public string targetCompanyId;
        public string targetId;
        public double quantity;
        public double limitPrice;
        public int targetX;
        public int targetY;
        public string action;
    }

    [Serializable]
    public sealed class PvpReadyRequestDto
    {
        public string requestId;
        public int protocolVersion = 1;
        public string matchId;
        public int turn;
        public int expectedRevision;
        public int lastSequence;
    }

    [Serializable]
    public sealed class PvpCommandResponseDto
    {
        public string requestId;
        public bool accepted;
        public string code;
        public string message;
        public int expectedSequence;
        public int revision;
        public int turn;
        public bool isReplay;
    }

    [Serializable]
    public sealed class PvpReadyResponseDto
    {
        public string requestId;
        public bool accepted;
        public string code;
        public string message;
        public int revision;
        public int turn;
        public bool turnResolved;
        public string commandHash;
        public string stateHash;
        public string turnDeadlineUtc;
        public PvpWorldStateDto world;
        public bool isReplay;
    }

    [Serializable]
    public sealed class PvpReconnectDto
    {
        public string matchId;
        public string playerId;
        public int turn;
        public string phase;
        public int revision;
        public string stateHash;
        public string turnDeadlineUtc;
        public PvpPlayerStateDto[] players;
        public PvpPendingCommandDto[] ownPendingCommands;
        public PvpWorldStateDto world;
    }

    [Serializable]
    public sealed class PvpPlayerStateDto
    {
        public int slot;
        public string playerId;
        public string companyId;
        public bool connected;
        public bool ready;
        public bool eliminated;
        public int spentActionPoints;
        public int expectedSequence;
    }

    [Serializable]
    public sealed class PvpPendingCommandDto
    {
        public string commandId;
        public int turn;
        public int sequence;
        public string kind;
        public string regionId;
        public string resourceId;
        public double quantity;
        public double limitPrice;
    }

    [Serializable]
    public sealed class PvpWorldStateDto
    {
        public int turn;
        public int calendarDay;
        public PvpMarketStateDto[] markets;
        public PvpPublicCompanyStateDto[] companies;
        public PvpOwnCompanyStateDto ownCompany;
        public PvpResourceSiteStateDto[] resourceSites;
        public PvpMapWorldStateDto map;
        public bool isFinished;
        public string winnerCompanyId;
    }

    [Serializable]
    public sealed class PvpMarketStateDto
    {
        public string regionId;
        public string resourceId;
        public string displayName;
        public double currentPrice;
        public double supply;
        public double demand;
        public double marketStock;
    }

    [Serializable]
    public sealed class PvpPublicCompanyStateDto
    {
        public string companyId;
        public string displayName;
        public bool isEliminated;
        public double economicPower;
    }

    [Serializable]
    public sealed class PvpOwnCompanyStateDto
    {
        public string companyId;
        public double cash;
        public double debt;
        public bool isBankrupt;
        public PvpInventoryStateDto[] inventory;
    }

    [Serializable]
    public sealed class PvpInventoryStateDto
    {
        public string resourceId;
        public double onHand;
        public double reserved;
    }

    [Serializable]
    public sealed class PvpResourceSiteStateDto
    {
        public string siteId;
        public string regionId;
        public string resourceId;
        public int discoveryTurn;
        public double currentOutput;
        public double minimumOutput;
        public bool isActive;
    }

    [Serializable]
    public sealed class PvpMapWorldStateDto
    {
        public int width;
        public int height;
        public int seed;
        public bool wrapHorizontally;
        public int fixedStepsPerTurn;
        public int currentEconomicDay;
        public int[] terrain;
        public PvpMapUnitStateDto[] units;
        public PvpMapMineStateDto[] mines;
        public PvpMapCastleStateDto[] castles;
    }

    [Serializable]
    public sealed class PvpMapCoordinateDto
    {
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class PvpMapUnitStateDto
    {
        public string unitId;
        public string ownerCompanyId;
        public string archetype;
        public int x;
        public int y;
        public int destinationX;
        public int destinationY;
        public int movementProgress;
        public int movementStepsPerTile;
        public int remainingTiles;
        public int stamina;
        public int maxStamina;
        public int soldiers;
        public double attackPower;
        public double defensePower;
        public double morale;
        public double fatigue;
        public PvpMapCoordinateDto[] plannedPath;
    }

    [Serializable]
    public sealed class PvpMapMineStateDto
    {
        public int x;
        public int y;
        public string kind;
        public string ownerCompanyId;
        public string capturingCompanyId;
        public int captureProgress;
        public int captureRequired;
    }

    [Serializable]
    public sealed class PvpMapCastleStateDto
    {
        public int x;
        public int y;
        public string ownerCompanyId;
        public string originalOwnerCompanyId;
        public string capturingCompanyId;
        public bool isCapital;
        public bool isDestroyed;
        public string role;
        public string conflictKind;
        public string siegeAction;
        public string occupationPolicy;
        public int captureProgress;
        public int captureRequired;
        public int wallDurability;
        public int maxWallDurability;
        public int foodSupply;
        public int maxFoodSupply;
        public int garrisonUnitCount;
    }
}
