using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Application.PvP;
using Game.Domain.Common;

namespace Game.Server;

public sealed class PvpMatchRuntime
{
    private sealed record CachedCommandResponse(
        string RequestHash,
        CommandResponse Response);

    private sealed record CachedReadyResponse(
        string RequestHash,
        ReadyResponse Response);

    public const string ServerVersion = "0.3.0";

    private readonly object _gate = new();
    private readonly PvpMatchId _matchId;
    private readonly PvpTurnCoordinator _coordinator;
    private readonly AuthoritativeSimulationRuntime _simulation;
    private readonly JsonMatchJournal _journal;
    private readonly Dictionary<string, AuthenticatedPlayer> _playersByTokenHash;
    private readonly Dictionary<string, AuthenticatedPlayer> _playersById;
    private readonly IReadOnlyList<PvpPlayerSlot> _slots;
    private readonly TimeSpan _turnTimeout;
    private readonly Dictionary<(string PlayerId, string RequestId), CachedCommandResponse>
        _commandCache = new();
    private readonly Dictionary<(string PlayerId, string RequestId), CachedReadyResponse>
        _readyCache = new();
    private bool _isReplaying;
    private DateTimeOffset _turnDeadlineUtc;

    private PvpMatchRuntime(
        string matchId,
        IReadOnlyList<ServerPlayerConfiguration> players,
        string dataDirectory)
    {
        if (players.Count < 2 || players.Count > 4)
            throw new InvalidOperationException("PVP 참가자는 2~4명이어야 합니다.");

        _matchId = new PvpMatchId(matchId);
        _playersByTokenHash = new Dictionary<string, AuthenticatedPlayer>(
            StringComparer.Ordinal);
        _playersById = new Dictionary<string, AuthenticatedPlayer>(
            StringComparer.Ordinal);
        var slots = new List<PvpPlayerSlot>(players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            ServerPlayerConfiguration configured = players[i];
            ValidatePlayerConfiguration(configured);
            var authenticated = new AuthenticatedPlayer(
                configured.PlayerId,
                configured.CompanyId);
            string tokenHash = configured.TokenIsSha256Hash
                ? configured.Token.ToUpperInvariant()
                : HashToken(configured.Token);

            if (!_playersByTokenHash.TryAdd(tokenHash, authenticated) ||
                !_playersById.TryAdd(configured.PlayerId, authenticated))
            {
                throw new InvalidOperationException(
                    "PvP 플레이어 ID 또는 토큰이 중복되었습니다.");
            }

            slots.Add(new PvpPlayerSlot(
                i,
                new PvpPlayerId(configured.PlayerId),
                new CompanyId(configured.CompanyId),
                configured.DisplayName));
        }

        _slots = slots;
        _coordinator = new PvpTurnCoordinator(
            _matchId,
            slots,
            new PvpMatchRules(
                minPlayers: 2,
                maxPlayers: 4,
                maxActionPointsPerPlayer: 5,
                maxCommandsPerPlayer: 16));
        _simulation = new AuthoritativeSimulationRuntime(
            slots,
            _matchId.Value);
        _journal = new JsonMatchJournal(dataDirectory, _matchId.Value);
        _turnTimeout = TimeSpan.FromSeconds(GetTurnTimeoutSeconds());
        _turnDeadlineUtc = DateTimeOffset.UtcNow + _turnTimeout;
        ReplayJournal();
        _turnDeadlineUtc = DateTimeOffset.UtcNow + _turnTimeout;
    }

    public static PvpMatchRuntime FromEnvironment()
    {
        string matchId = Environment.GetEnvironmentVariable("PVP_MATCH_ID")?.Trim();
        if (string.IsNullOrWhiteSpace(matchId))
            matchId = "dev-match-001";

        string dataDirectory = Environment.GetEnvironmentVariable("PVP_DATA_DIR")?.Trim();
        if (string.IsNullOrWhiteSpace(dataDirectory))
            dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");

        return new PvpMatchRuntime(
            matchId,
            LoadPlayersFromEnvironment(),
            dataDirectory);
    }

    public static PvpMatchRuntime Create(
        string matchId,
        IReadOnlyList<ServerPlayerConfiguration> players,
        string dataDirectory) =>
        new(matchId, players, dataDirectory);

    public bool IsFinished
    {
        get
        {
            lock (_gate)
                return _simulation.IsFinished;
        }
    }

    public bool TryAuthenticate(
        HttpRequest request,
        out AuthenticatedPlayer player)
    {
        player = default;
        string authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string token = authorization[prefix.Length..].Trim();
        if (token.Length < 32 || token.Length > 512)
            return false;

        return _playersByTokenHash.TryGetValue(HashToken(token), out player);
    }

    public CommandResponse Submit(
        AuthenticatedPlayer player,
        SubmitCommandRequest request)
    {
        lock (_gate)
        {
            return ApplySubmit(player, request, persist: true);
        }
    }

    public ReadyResponse MarkReady(
        AuthenticatedPlayer player,
        ReadyRequest request)
    {
        lock (_gate)
        {
            return ApplyReady(player, request, persist: true);
        }
    }

    public ReconnectResponse GetReconnectState(AuthenticatedPlayer player)
    {
        lock (_gate)
        {
            PvpMatchSnapshot snapshot = _coordinator.CreateSnapshot();
            IReadOnlyList<PvpCommandEnvelope> pending =
                _coordinator.GetPendingCommands(new PvpPlayerId(player.PlayerId));

            return new ReconnectResponse(
                snapshot.MatchId.Value,
                player.PlayerId,
                snapshot.Turn.Value,
                snapshot.Phase.ToString(),
                snapshot.Revision,
                snapshot.LastAuthoritativeStateHash,
                _turnDeadlineUtc.ToString("O"),
                snapshot.Players.Select(item => new PlayerStateResponse(
                    item.SlotIndex,
                    item.PlayerId.Value,
                    item.CompanyId.Value,
                    item.IsConnected,
                    item.IsReady,
                    item.IsEliminated,
                    item.SpentActionPoints,
                    item.ExpectedSequence)).ToArray(),
                pending.Select(item => new PendingCommandResponse(
                    item.CommandId,
                    item.Turn.Value,
                    item.Sequence,
                    item.Kind.ToString(),
                    item.Payload.RegionId.Value,
                    item.Payload.ResourceId?.Value,
                    item.Payload.Quantity,
                    item.Payload.LimitPrice)).ToArray(),
                _simulation.CreateWorldView(new CompanyId(player.CompanyId)));
        }
    }

    public void ProcessTurnTimeout(DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            if (_coordinator.Phase != PvpMatchPhase.Planning ||
                utcNow < _turnDeadlineUtc)
            {
                return;
            }

            PvpMatchSnapshot snapshot = _coordinator.CreateSnapshot();
            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                PvpPlayerSnapshot playerState = snapshot.Players[i];
                if (playerState.IsReady || playerState.IsEliminated)
                    continue;
                if (!_playersById.TryGetValue(playerState.PlayerId.Value, out var player))
                    continue;

                var timeoutReady = new ReadyRequest(
                    $"timeout_{snapshot.Turn.Value}_{player.PlayerId}",
                    PvpProtocol.CurrentVersion,
                    _matchId.Value,
                    snapshot.Turn.Value,
                    snapshot.Revision,
                    playerState.ExpectedSequence);
                ApplyReady(player, timeoutReady, persist: true);

                if (_coordinator.Phase != PvpMatchPhase.Planning)
                    break;
            }

            if (_coordinator.Phase == PvpMatchPhase.Planning)
                _turnDeadlineUtc = utcNow + _turnTimeout;
        }
    }

    private CommandResponse ApplySubmit(
        AuthenticatedPlayer player,
        SubmitCommandRequest request,
        bool persist)
    {
        string requestHash = JsonMatchJournal.ComputeRequestHash(request);
        var cacheKey = (player.PlayerId, request.RequestId ?? string.Empty);

        if (_commandCache.TryGetValue(cacheKey, out CachedCommandResponse? cached))
        {
            if (!string.Equals(cached.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return RejectedCommand(
                    request.RequestId,
                    PvpOperationCode.DuplicateRequestConflict,
                    "같은 RequestId가 다른 내용으로 재사용되었습니다.");
            }

            return cached.Response with { IsReplay = true };
        }

        if (!Enum.TryParse(request.Kind, true, out PvpCommandKind kind) ||
            !IsSupportedCommand(kind))
        {
            return CacheCommand(cacheKey, requestHash, RejectedCommand(
                request.RequestId,
                PvpOperationCode.UnsupportedRequest,
                "현재 서버가 지원하지 않는 명령입니다."));
        }

        CommandResponse? validationFailure =
            ValidateSubmitRequest(request, kind);
        if (validationFailure != null)
            return CacheCommand(cacheKey, requestHash, validationFailure);

        PvpCommandEnvelope command;
        try
        {
            ResourceId? resourceId = null;
            if (!string.IsNullOrWhiteSpace(request.ResourceId))
                resourceId = new ResourceId(request.ResourceId);
            CompanyId? targetCompanyId = null;
            if (!string.IsNullOrWhiteSpace(request.TargetCompanyId))
                targetCompanyId = new CompanyId(request.TargetCompanyId);

            command = new PvpCommandEnvelope(
                request.CommandId,
                _matchId,
                new PvpPlayerId(player.PlayerId),
                new CompanyId(player.CompanyId),
                new TurnNumber(request.Turn),
                request.Sequence,
                kind,
                new PvpCommandPayload(
                    new RegionId(string.IsNullOrWhiteSpace(request.RegionId)
                        ? "map"
                        : request.RegionId),
                    resourceId,
                    targetCompanyId,
                    request.TargetId,
                    request.Quantity,
                    request.LimitPrice,
                    request.TargetX,
                    request.TargetY,
                    request.Action));
        }
        catch (ArgumentException exception)
        {
            return CacheCommand(cacheKey, requestHash, RejectedCommand(
                request.RequestId,
                PvpOperationCode.InvalidPayload,
                exception.Message));
        }

        PvpOperationCode authoritativeValidation =
            _simulation.ValidateCommand(command, out string validationReason);
        if (authoritativeValidation != PvpOperationCode.Accepted)
        {
            return CacheCommand(cacheKey, requestHash, RejectedCommand(
                request.RequestId,
                authoritativeValidation,
                validationReason));
        }

        PvpOperationResult result = _coordinator.SubmitCommand(command);
        var response = new CommandResponse(
            request.RequestId,
            result.Success,
            result.Code.ToString(),
            KoreanMessage(result.Code),
            result.ExpectedSequence,
            _coordinator.Revision,
            _coordinator.CurrentTurn.Value);

        if (result.Success && persist && !_isReplaying)
        {
            try
            {
                _journal.AppendCommand(
                    player,
                    request,
                    _coordinator.Revision,
                    _coordinator.CurrentTurn.Value);
            }
            catch
            {
                _coordinator.CancelLastCommand(
                    new PvpPlayerId(player.PlayerId),
                    command.CommandId);
                throw;
            }
        }

        return CacheCommand(cacheKey, requestHash, response);
    }

    private ReadyResponse ApplyReady(
        AuthenticatedPlayer player,
        ReadyRequest request,
        bool persist)
    {
        string requestHash = JsonMatchJournal.ComputeRequestHash(request);
        var cacheKey = (player.PlayerId, request.RequestId ?? string.Empty);

        if (_readyCache.TryGetValue(cacheKey, out CachedReadyResponse? cached))
        {
            if (!string.Equals(cached.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return RejectedReady(
                    request.RequestId,
                    PvpOperationCode.DuplicateRequestConflict,
                    "같은 RequestId가 다른 내용으로 재사용되었습니다.",
                    player);
            }

            return cached.Response with { IsReplay = true };
        }

        ReadyResponse? validationFailure = ValidateReadyRequest(player, request);
        if (validationFailure != null)
            return CacheReady(cacheKey, requestHash, validationFailure);

        if (persist && !_isReplaying)
        {
            _journal.AppendReady(
                player,
                request,
                _coordinator.Revision,
                _coordinator.CurrentTurn.Value,
                string.Empty);
        }

        PvpOperationResult ready = _coordinator.MarkReady(
            new PvpPlayerId(player.PlayerId));
        if (!ready.Success)
        {
            return CacheReady(cacheKey, requestHash, RejectedReady(
                request.RequestId,
                ready.Code,
                KoreanMessage(ready.Code),
                player));
        }

        bool resolved = false;
        string commandHash = string.Empty;
        string stateHash = _coordinator.LastAuthoritativeStateHash;

        if (_coordinator.Phase == PvpMatchPhase.Locked)
        {
            PvpOperationResult begin = _coordinator.TryBeginResolution(
                out PvpTurnPackage package);
            if (!begin.Success)
            {
                return CacheReady(cacheKey, requestHash, RejectedReady(
                    request.RequestId,
                    begin.Code,
                    KoreanMessage(begin.Code),
                    player));
            }

            _simulation.Resolve(package);
            commandHash = package.CommandHash;
            stateHash = _simulation.ComputeStateHash(_coordinator.Revision + 1);

            for (int i = 0; i < _slots.Count; i++)
            {
                PvpPlayerSlot slot = _slots[i];
                if (_simulation.IsCompanyEliminated(slot.CompanyId))
                    _coordinator.SetEliminated(slot.PlayerId, true);
            }

            PvpOperationResult complete = _coordinator.CompleteResolution(
                stateHash,
                _simulation.IsFinished);
            if (!complete.Success)
            {
                throw new InvalidOperationException(
                    $"권위 턴 완료에 실패했습니다: {complete.Code}");
            }

            resolved = true;
        }

        if (resolved && !_simulation.IsFinished)
            _turnDeadlineUtc = DateTimeOffset.UtcNow + _turnTimeout;

        var response = new ReadyResponse(
            request.RequestId,
            true,
            PvpOperationCode.Accepted.ToString(),
            resolved ? "서버가 권위 턴을 정산했습니다." : "준비 완료. 다른 플레이어를 기다립니다.",
            _coordinator.Revision,
            _coordinator.CurrentTurn.Value,
            resolved,
            commandHash,
            stateHash,
            _turnDeadlineUtc.ToString("O"),
            _simulation.CreateWorldView(new CompanyId(player.CompanyId)));

        if (persist && !_isReplaying && resolved)
        {
            _journal.AppendResolution(
                _coordinator.Revision,
                _coordinator.CurrentTurn.Value,
                stateHash);
            _journal.SaveSnapshot(new
            {
                matchId = _matchId.Value,
                revision = _coordinator.Revision,
                turn = _coordinator.CurrentTurn.Value,
                stateHash,
                savedAtUtc = DateTimeOffset.UtcNow,
                world = _simulation.CreateWorldView(new CompanyId(string.Empty))
            });
        }

        return CacheReady(cacheKey, requestHash, response);
    }

    private CommandResponse? ValidateSubmitRequest(
        SubmitCommandRequest request,
        PvpCommandKind kind)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.RequestId.Length > 128 ||
            string.IsNullOrWhiteSpace(request.CommandId) ||
            request.CommandId.Length > 128)
        {
            return RejectedCommand(
                request?.RequestId ?? string.Empty,
                PvpOperationCode.InvalidPayload,
                "요청 ID와 명령 ID를 확인하십시오.");
        }
        if (request.ProtocolVersion != PvpProtocol.CurrentVersion)
            return RejectedCommand(request.RequestId, PvpOperationCode.ProtocolMismatch, "프로토콜 버전이 다릅니다.");
        if (!string.Equals(request.MatchId, _matchId.Value, StringComparison.Ordinal))
            return RejectedCommand(request.RequestId, PvpOperationCode.WrongMatch, "매치 ID가 다릅니다.");
        if (request.ExpectedRevision != _coordinator.Revision)
            return RejectedCommand(request.RequestId, PvpOperationCode.StaleRevision, "서버 Revision과 다릅니다.");
        if (request.Turn < 1 || request.Turn != _coordinator.CurrentTurn.Value)
            return RejectedCommand(request.RequestId, PvpOperationCode.WrongTurn, "현재 턴과 다른 명령입니다.");
        if (kind is PvpCommandKind.MarketBuy or PvpCommandKind.MarketSell)
        {
            if (string.IsNullOrWhiteSpace(request.RegionId) ||
                request.RegionId.Length > 64 ||
                string.IsNullOrWhiteSpace(request.ResourceId) ||
                request.ResourceId.Length > 64 ||
                request.Quantity <= 0m ||
                request.LimitPrice <= 0m)
            {
                return RejectedCommand(
                    request.RequestId,
                    PvpOperationCode.InvalidPayload,
                    "시장 명령의 지역·자원·수량·가격을 확인하십시오.");
            }
        }
        else if (string.IsNullOrWhiteSpace(request.TargetId) ||
                 request.TargetId.Length > 128)
        {
            return RejectedCommand(
                request.RequestId,
                PvpOperationCode.InvalidPayload,
                "지도 명령에는 대상 부대 ID가 필요합니다.");
        }
        return null;
    }

    private static bool IsSupportedCommand(PvpCommandKind kind) =>
        kind is PvpCommandKind.MarketBuy or
            PvpCommandKind.MarketSell or
            PvpCommandKind.MoveUnit or
            PvpCommandKind.OccupyResourceSite or
            PvpCommandKind.OccupyCastle or
            PvpCommandKind.StartSiege or
            PvpCommandKind.CancelOrder;

    private ReadyResponse? ValidateReadyRequest(
        AuthenticatedPlayer player,
        ReadyRequest request)
    {
        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            request.RequestId.Length > 128)
        {
            return RejectedReady(
                request?.RequestId ?? string.Empty,
                PvpOperationCode.InvalidPayload,
                "RequestId가 필요합니다.",
                player);
        }
        if (request.ProtocolVersion != PvpProtocol.CurrentVersion)
            return RejectedReady(request.RequestId, PvpOperationCode.ProtocolMismatch, "프로토콜 버전이 다릅니다.", player);
        if (!string.Equals(request.MatchId, _matchId.Value, StringComparison.Ordinal))
            return RejectedReady(request.RequestId, PvpOperationCode.WrongMatch, "매치 ID가 다릅니다.", player);
        if (request.ExpectedRevision != _coordinator.Revision)
            return RejectedReady(request.RequestId, PvpOperationCode.StaleRevision, "서버 Revision과 다릅니다.", player);
        if (request.Turn < 1 || request.Turn != _coordinator.CurrentTurn.Value)
            return RejectedReady(request.RequestId, PvpOperationCode.WrongTurn, "현재 턴과 다른 준비 요청입니다.", player);

        PvpPlayerSnapshot? playerState = _coordinator.CreateSnapshot().Players
            .FirstOrDefault(item => item.PlayerId.Value == player.PlayerId);
        if (playerState == null || request.LastSequence != playerState.ExpectedSequence)
            return RejectedReady(request.RequestId, PvpOperationCode.SequenceMismatch, "마지막 명령 순서가 서버와 다릅니다.", player);
        return null;
    }

    private CommandResponse RejectedCommand(
        string requestId,
        PvpOperationCode code,
        string message)
    {
        return new CommandResponse(
            requestId ?? string.Empty,
            false,
            code.ToString(),
            message,
            0,
            _coordinator.Revision,
            _coordinator.CurrentTurn.Value);
    }

    private ReadyResponse RejectedReady(
        string requestId,
        PvpOperationCode code,
        string message,
        AuthenticatedPlayer player)
    {
        return new ReadyResponse(
            requestId ?? string.Empty,
            false,
            code.ToString(),
            message,
            _coordinator.Revision,
            _coordinator.CurrentTurn.Value,
            false,
            string.Empty,
            _coordinator.LastAuthoritativeStateHash,
            _turnDeadlineUtc.ToString("O"),
            _simulation.CreateWorldView(new CompanyId(player.CompanyId)));
    }

    private CommandResponse CacheCommand(
        (string PlayerId, string RequestId) key,
        string requestHash,
        CommandResponse response)
    {
        _commandCache[key] = new CachedCommandResponse(requestHash, response);
        return response;
    }

    private ReadyResponse CacheReady(
        (string PlayerId, string RequestId) key,
        string requestHash,
        ReadyResponse response)
    {
        _readyCache[key] = new CachedReadyResponse(requestHash, response);
        return response;
    }

    private void ReplayJournal()
    {
        IReadOnlyList<MatchJournalEntry> entries = _journal.Load();
        if (entries.Count == 0)
            return;

        _isReplaying = true;
        try
        {
            for (int i = 0; i < entries.Count; i++)
            {
                MatchJournalEntry entry = entries[i];
                if (!_playersById.TryGetValue(entry.PlayerId, out var player))
                {
                    if (entry.Type == "resolution")
                    {
                        if (entry.Revision != _coordinator.Revision ||
                            entry.Turn != _coordinator.CurrentTurn.Value ||
                            !string.Equals(
                                entry.StateHash,
                                _coordinator.LastAuthoritativeStateHash,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                "저널 재생 중 권위 상태 해시가 일치하지 않습니다.");
                        }

                        continue;
                    }

                    throw new InvalidDataException($"저널의 플레이어를 찾을 수 없습니다: {entry.PlayerId}");
                }

                if (entry.Type == "command" && entry.Command != null)
                {
                    CommandResponse result = ApplySubmit(player, entry.Command, persist: false);
                    if (!result.Accepted)
                        throw new InvalidDataException($"명령 저널 재생 실패: {result.Code}");
                }
                else if (entry.Type == "ready" && entry.Ready != null)
                {
                    ReadyResponse result = ApplyReady(player, entry.Ready, persist: false);
                    if (!result.Accepted)
                        throw new InvalidDataException($"준비 저널 재생 실패: {result.Code}");
                    if (!string.IsNullOrWhiteSpace(entry.StateHash) &&
                        result.TurnResolved &&
                        !string.Equals(entry.StateHash, result.StateHash, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("저널 재생 중 상태 해시가 일치하지 않습니다.");
                    }
                }
                else
                {
                    throw new InvalidDataException($"알 수 없는 저널 형식입니다: {entry.Type}");
                }
            }
        }
        finally
        {
            _isReplaying = false;
        }
    }

    private static IReadOnlyList<ServerPlayerConfiguration> LoadPlayersFromEnvironment()
    {
        string? file = Environment.GetEnvironmentVariable("PVP_PLAYERS_FILE")?.Trim();
        if (!string.IsNullOrWhiteSpace(file))
        {
            string json = File.ReadAllText(file, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<ServerPlayerConfiguration>>(
                       json,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       }) ?? throw new InvalidDataException("PVP 플레이어 설정 파일이 비어 있습니다.");
        }

        var players = new List<ServerPlayerConfiguration>(4);
        for (int i = 1; i <= 4; i++)
        {
            string variable = $"PVP_PLAYER{i}_TOKEN";
            string? token = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(token))
            {
                if (i <= 2)
                    throw new InvalidOperationException($"환경 변수 {variable}에 32자 이상의 토큰이 필요합니다.");
                continue;
            }

            players.Add(new ServerPlayerConfiguration(
                $"player_{i}",
                $"company_{i}",
                $"플레이어 {i}",
                token));
        }

        return players;
    }

    private static void ValidatePlayerConfiguration(ServerPlayerConfiguration player)
    {
        if (string.IsNullOrWhiteSpace(player.PlayerId) || player.PlayerId.Length > 64 ||
            string.IsNullOrWhiteSpace(player.CompanyId) || player.CompanyId.Length > 64 ||
            string.IsNullOrWhiteSpace(player.Token) || player.Token.Length < 32 || player.Token.Length > 512)
        {
            throw new InvalidOperationException("PvP 플레이어 설정이 올바르지 않습니다.");
        }
        if (player.TokenIsSha256Hash &&
            (player.Token.Length != 64 || !player.Token.All(Uri.IsHexDigit)))
        {
            throw new InvalidOperationException("PvP 토큰 해시가 올바르지 않습니다.");
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static int GetTurnTimeoutSeconds()
    {
        string? raw = Environment.GetEnvironmentVariable("PVP_TURN_TIMEOUT_SECONDS");
        return int.TryParse(raw, out int seconds)
            ? Math.Clamp(seconds, 15, 3600)
            : 120;
    }

    private static string KoreanMessage(PvpOperationCode code) => code switch
    {
        PvpOperationCode.Accepted => "요청을 처리했습니다.",
        PvpOperationCode.WrongTurn => "현재 턴과 다른 명령입니다.",
        PvpOperationCode.UnknownPlayer => "등록되지 않은 플레이어입니다.",
        PvpOperationCode.PlayerAlreadyReady => "이미 준비 완료했습니다.",
        PvpOperationCode.CompanyOwnershipMismatch => "다른 회사는 조작할 수 없습니다.",
        PvpOperationCode.SequenceMismatch => "명령 순서가 맞지 않습니다.",
        PvpOperationCode.DuplicateCommand => "이미 처리한 명령입니다.",
        PvpOperationCode.InvalidPayload => "명령 데이터가 올바르지 않습니다.",
        PvpOperationCode.InsufficientActionPoints => "행동력이 부족합니다.",
        PvpOperationCode.CommandLimitExceeded => "턴 명령 제한을 초과했습니다.",
        PvpOperationCode.NotPlanning => "현재는 계획 단계가 아닙니다.",
        PvpOperationCode.MatchFinished => "매치가 종료되었습니다.",
        _ => $"요청을 거절했습니다: {code}"
    };
}
