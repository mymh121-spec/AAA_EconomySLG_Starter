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
        private bool _disposed;

        public PvpHttpClient(string endpoint, string accessToken)
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
            if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length < 32)
                throw new ArgumentException("32자 이상의 접속 토큰이 필요합니다.", nameof(accessToken));

            _endpoint = endpoint.TrimEnd('/');
            _accessToken = accessToken;
        }

        public Task<PvpReconnectDto> GetMatchAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync<PvpReconnectDto>(
                UnityWebRequest.kHttpVerbGET,
                "/api/v1/match",
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
                "/api/v1/commands",
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
                "/api/v1/ready",
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
}
