using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Server;

public enum PvpRoomStatus
{
    Lobby,
    Active,
    Finished
}

public sealed record PvpRoomPlayerPersistence(
    int Slot,
    string PlayerId,
    string CompanyId,
    string DisplayName,
    string TokenHash,
    bool IsHost,
    bool Connected);

public sealed record PvpRoomPersistence(
    string RoomCode,
    string MatchId,
    PvpRoomStatus Status,
    int MaxPlayers,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc,
    IReadOnlyList<PvpRoomPlayerPersistence> Players);

public sealed record RoomOperationResult<T>(
    bool Success,
    int StatusCode,
    T? Value,
    ApiError? Error)
{
    public static RoomOperationResult<T> Ok(T value, int statusCode = 200) =>
        new(true, statusCode, value, null);

    public static RoomOperationResult<T> Fail(
        int statusCode,
        string code,
        string message) =>
        new(false, statusCode, default, new ApiError(code, message));
}

public sealed class PvpRoomRegistry
{
    private sealed class RoomEntry
    {
        public required string RoomCode { get; init; }
        public required string MatchId { get; init; }
        public required int MaxPlayers { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required List<PvpRoomPlayerPersistence> Players { get; init; }
        public required DateTimeOffset LastActivityUtc { get; set; }
        public required PvpRoomStatus Status { get; set; }
        public PvpMatchRuntime? Runtime { get; set; }
    }

    private const string InviteAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int InviteCodeLength = 6;
    private readonly object _gate = new();
    private readonly Dictionary<string, RoomEntry> _rooms =
        new(StringComparer.Ordinal);
    private readonly string _dataDirectory;
    private readonly string _roomsDirectory;
    private readonly string _matchesDirectory;
    private readonly int _maxRooms;

    public PvpRoomRegistry(string dataDirectory, int maxRooms = 16)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
            throw new ArgumentException("PvP 데이터 디렉터리가 필요합니다.", nameof(dataDirectory));
        if (maxRooms < 1 || maxRooms > 1024)
            throw new ArgumentOutOfRangeException(nameof(maxRooms));

        _dataDirectory = Path.GetFullPath(dataDirectory);
        _roomsDirectory = Path.Combine(_dataDirectory, "rooms");
        _matchesDirectory = Path.Combine(_dataDirectory, "matches");
        _maxRooms = maxRooms;
        Directory.CreateDirectory(_roomsDirectory);
        Directory.CreateDirectory(_matchesDirectory);
        LoadRooms();
    }

    public static PvpRoomRegistry FromEnvironment()
    {
        string? configuredDirectory =
            Environment.GetEnvironmentVariable("PVP_DATA_DIR")?.Trim();
        string dataDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : configuredDirectory;
        string? configuredMax =
            Environment.GetEnvironmentVariable("PVP_MAX_ROOMS")?.Trim();
        int maxRooms = int.TryParse(configuredMax, out int parsed)
            ? Math.Clamp(parsed, 1, 1024)
            : 16;
        return new PvpRoomRegistry(dataDirectory, maxRooms);
    }

    public RoomOperationResult<RoomSessionResponse> CreateRoom(
        CreateRoomRequest request)
    {
        if (request == null)
        {
            return RoomOperationResult<RoomSessionResponse>.Fail(
                400, "잘못된요청", "방 생성 요청이 필요합니다.");
        }
        if (!TryNormalizeDisplayName(request?.DisplayName, out string displayName))
        {
            return RoomOperationResult<RoomSessionResponse>.Fail(
                400, "잘못된이름", "표시 이름은 공백·제어 문자를 제외한 1~24자여야 합니다.");
        }
        if (request.MaxPlayers is < 2 or > 4)
        {
            return RoomOperationResult<RoomSessionResponse>.Fail(
                400, "잘못된정원", "방 정원은 2~4명이어야 합니다.");
        }

        lock (_gate)
        {
            int retainedRoomCount = _rooms.Values.Count(room =>
                room.Status != PvpRoomStatus.Finished);
            if (retainedRoomCount >= _maxRooms)
            {
                return RoomOperationResult<RoomSessionResponse>.Fail(
                    503, "방한도초과", "현재 생성 가능한 방 수를 초과했습니다.");
            }

            string roomCode = GenerateUniqueRoomCode();
            string token = GenerateSessionToken();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var host = CreatePlayer(0, displayName, token, isHost: true);
            var room = new RoomEntry
            {
                RoomCode = roomCode,
                MatchId = $"match_{roomCode}_{Guid.NewGuid():N}",
                Status = PvpRoomStatus.Lobby,
                MaxPlayers = request.MaxPlayers,
                CreatedAtUtc = now,
                LastActivityUtc = now,
                Players = new List<PvpRoomPlayerPersistence> { host }
            };
            _rooms.Add(roomCode, room);
            SaveRoom(room);

            return RoomOperationResult<RoomSessionResponse>.Ok(
                CreateSession(room, host, token), 201);
        }
    }

    public RoomOperationResult<RoomSessionResponse> JoinRoom(
        string roomCode,
        JoinRoomRequest request)
    {
        if (!TryNormalizeRoomCode(roomCode, out string normalizedCode))
        {
            return RoomOperationResult<RoomSessionResponse>.Fail(
                404, "방없음", "초대 코드에 해당하는 방을 찾을 수 없습니다.");
        }
        if (!TryNormalizeDisplayName(request?.DisplayName, out string displayName))
        {
            return RoomOperationResult<RoomSessionResponse>.Fail(
                400, "잘못된이름", "표시 이름은 공백·제어 문자를 제외한 1~24자여야 합니다.");
        }

        lock (_gate)
        {
            if (!_rooms.TryGetValue(normalizedCode, out RoomEntry? room))
            {
                return RoomOperationResult<RoomSessionResponse>.Fail(
                    404, "방없음", "초대 코드에 해당하는 방을 찾을 수 없습니다.");
            }
            if (room.Status != PvpRoomStatus.Lobby)
            {
                return RoomOperationResult<RoomSessionResponse>.Fail(
                    409, "참가마감", "이미 시작했거나 종료된 방에는 참가할 수 없습니다.");
            }
            if (room.Players.Count >= room.MaxPlayers)
            {
                return RoomOperationResult<RoomSessionResponse>.Fail(
                    409, "방가득참", "방 정원이 가득 찼습니다.");
            }
            if (room.Players.Any(player => string.Equals(
                    player.DisplayName, displayName, StringComparison.OrdinalIgnoreCase)))
            {
                return RoomOperationResult<RoomSessionResponse>.Fail(
                    409, "이름중복", "방 안에서 같은 표시 이름을 사용할 수 없습니다.");
            }

            string token = GenerateSessionToken();
            var player = CreatePlayer(
                room.Players.Count,
                displayName,
                token,
                isHost: false);
            room.Players.Add(player);
            room.LastActivityUtc = DateTimeOffset.UtcNow;
            SaveRoom(room);
            return RoomOperationResult<RoomSessionResponse>.Ok(
                CreateSession(room, player, token), 201);
        }
    }

    public RoomOperationResult<RoomStateResponse> GetRoom(
        string roomCode,
        HttpRequest request)
    {
        lock (_gate)
        {
            if (!TryAuthenticateLocked(roomCode, request, out RoomEntry? room, out _))
                return UnauthorizedOrMissing<RoomStateResponse>(roomCode);

            room.LastActivityUtc = DateTimeOffset.UtcNow;
            SaveRoom(room);
            return RoomOperationResult<RoomStateResponse>.Ok(CreateState(room));
        }
    }

    public RoomOperationResult<RoomStateResponse> StartRoom(
        string roomCode,
        HttpRequest request)
    {
        lock (_gate)
        {
            if (!TryAuthenticateLocked(
                    roomCode, request, out RoomEntry? room, out PvpRoomPlayerPersistence? player))
            {
                return UnauthorizedOrMissing<RoomStateResponse>(roomCode);
            }
            if (!player.IsHost)
            {
                return RoomOperationResult<RoomStateResponse>.Fail(
                    403, "방장아님", "경기는 방장만 시작할 수 있습니다.");
            }
            if (room.Status != PvpRoomStatus.Lobby)
            {
                return RoomOperationResult<RoomStateResponse>.Fail(
                    409, "시작불가", "대기 중인 방만 시작할 수 있습니다.");
            }
            if (room.Players.Count < 2)
            {
                return RoomOperationResult<RoomStateResponse>.Fail(
                    409, "인원부족", "경기를 시작하려면 2명 이상이 필요합니다.");
            }

            room.Runtime = CreateRuntime(room);
            room.Status = PvpRoomStatus.Active;
            room.LastActivityUtc = DateTimeOffset.UtcNow;
            SaveRoom(room);
            return RoomOperationResult<RoomStateResponse>.Ok(CreateState(room));
        }
    }

    public bool TryGetMatch(
        string roomCode,
        HttpRequest request,
        out AuthenticatedPlayer player,
        out PvpMatchRuntime runtime,
        out ApiError error,
        out int statusCode)
    {
        lock (_gate)
        {
            player = default;
            runtime = null!;
            error = ApiError.Unauthorized();
            statusCode = 401;

            if (!TryAuthenticateLocked(
                    roomCode, request, out RoomEntry? room, out PvpRoomPlayerPersistence? roomPlayer))
            {
                if (!TryNormalizeRoomCode(roomCode, out string normalized) ||
                    !_rooms.ContainsKey(normalized))
                {
                    error = new ApiError("방없음", "초대 코드에 해당하는 방을 찾을 수 없습니다.");
                    statusCode = 404;
                }
                return false;
            }
            if (room.Status != PvpRoomStatus.Active || room.Runtime == null)
            {
                error = new ApiError("경기없음", "아직 경기가 시작되지 않았습니다.");
                statusCode = 409;
                return false;
            }

            room.LastActivityUtc = DateTimeOffset.UtcNow;
            player = new AuthenticatedPlayer(roomPlayer.PlayerId, roomPlayer.CompanyId);
            runtime = room.Runtime;
            return true;
        }
    }

    public void RecordMatchActivity(string roomCode, bool isFinished)
    {
        lock (_gate)
        {
            if (!TryNormalizeRoomCode(roomCode, out string normalized) ||
                !_rooms.TryGetValue(normalized, out RoomEntry? room))
            {
                return;
            }

            room.LastActivityUtc = DateTimeOffset.UtcNow;
            if (isFinished)
                room.Status = PvpRoomStatus.Finished;
            SaveRoom(room);
        }
    }

    public IReadOnlyList<PvpMatchRuntime> GetActiveRuntimes()
    {
        lock (_gate)
        {
            return _rooms.Values
                .Where(room => room.Status == PvpRoomStatus.Active && room.Runtime != null)
                .Select(room => room.Runtime!)
                .ToArray();
        }
    }

    private void LoadRooms()
    {
        foreach (string path in Directory.EnumerateFiles(_roomsDirectory, "*.room.json"))
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            PvpRoomPersistence persisted = JsonSerializer.Deserialize<PvpRoomPersistence>(
                json, JsonOptions) ?? throw new InvalidDataException(
                $"PvP 방 파일을 읽을 수 없습니다: {path}");
            if (!TryNormalizeRoomCode(persisted.RoomCode, out string roomCode) ||
                persisted.MaxPlayers is < 2 or > 4 ||
                persisted.Players.Count is < 1 or > 4)
            {
                throw new InvalidDataException($"PvP 방 파일이 올바르지 않습니다: {path}");
            }

            var room = new RoomEntry
            {
                RoomCode = roomCode,
                MatchId = persisted.MatchId,
                Status = persisted.Status,
                MaxPlayers = persisted.MaxPlayers,
                CreatedAtUtc = persisted.CreatedAtUtc,
                LastActivityUtc = persisted.LastActivityUtc,
                Players = persisted.Players.ToList()
            };
            if (room.Status == PvpRoomStatus.Active)
                room.Runtime = CreateRuntime(room);
            _rooms.Add(roomCode, room);
        }
    }

    private PvpMatchRuntime CreateRuntime(RoomEntry room)
    {
        return PvpMatchRuntime.Create(
            room.MatchId,
            room.Players.Select(player => new ServerPlayerConfiguration(
                player.PlayerId,
                player.CompanyId,
                player.DisplayName,
                player.TokenHash,
                TokenIsSha256Hash: true)).ToArray(),
            _matchesDirectory);
    }

    private bool TryAuthenticateLocked(
        string roomCode,
        HttpRequest request,
        out RoomEntry room,
        out PvpRoomPlayerPersistence player)
    {
        room = null!;
        player = null!;
        if (!TryNormalizeRoomCode(roomCode, out string normalized) ||
            !_rooms.TryGetValue(normalized, out RoomEntry? foundRoom) ||
            !TryReadBearerToken(request, out string token))
        {
            return false;
        }

        string tokenHash = HashToken(token);
        PvpRoomPlayerPersistence? foundPlayer = foundRoom.Players.FirstOrDefault(
            item => string.Equals(item.TokenHash, tokenHash, StringComparison.Ordinal));
        if (foundPlayer == null)
            return false;

        room = foundRoom;
        player = foundPlayer;
        return true;
    }

    private RoomOperationResult<T> UnauthorizedOrMissing<T>(string roomCode)
    {
        if (!TryNormalizeRoomCode(roomCode, out string normalized) ||
            !_rooms.ContainsKey(normalized))
        {
            return RoomOperationResult<T>.Fail(
                404, "방없음", "초대 코드에 해당하는 방을 찾을 수 없습니다.");
        }
        return RoomOperationResult<T>.Fail(
            401, "인증실패", "이 방에서 발급한 유효한 Bearer 토큰이 필요합니다.");
    }

    private string GenerateUniqueRoomCode()
    {
        var code = new char[InviteCodeLength];
        for (int attempt = 0; attempt < 256; attempt++)
        {
            for (int i = 0; i < code.Length; i++)
                code[i] = InviteAlphabet[RandomNumberGenerator.GetInt32(InviteAlphabet.Length)];
            string candidate = new(code);
            if (!_rooms.ContainsKey(candidate))
                return candidate;
        }
        throw new InvalidOperationException("고유한 PvP 초대 코드를 생성하지 못했습니다.");
    }

    private void SaveRoom(RoomEntry room)
    {
        var persisted = new PvpRoomPersistence(
            room.RoomCode,
            room.MatchId,
            room.Status,
            room.MaxPlayers,
            room.CreatedAtUtc,
            room.LastActivityUtc,
            room.Players);
        string path = Path.Combine(_roomsDirectory, $"{room.RoomCode}.room.json");
        string temporaryPath = path + ".tmp";
        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(persisted, JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static RoomSessionResponse CreateSession(
        RoomEntry room,
        PvpRoomPlayerPersistence player,
        string token) =>
        new(
            room.RoomCode,
            player.PlayerId,
            player.CompanyId,
            token,
            player.IsHost,
            CreateState(room));

    private static RoomStateResponse CreateState(RoomEntry room) =>
        new(
            room.RoomCode,
            room.MatchId,
            room.Status.ToString(),
            room.MaxPlayers,
            room.CreatedAtUtc,
            room.LastActivityUtc,
            room.Players.Select(player => new RoomPlayerResponse(
                player.Slot,
                player.PlayerId,
                player.DisplayName,
                player.IsHost,
                player.Connected)).ToArray());

    private static PvpRoomPlayerPersistence CreatePlayer(
        int slot,
        string displayName,
        string token,
        bool isHost) =>
        new(
            slot,
            $"player_{Guid.NewGuid():N}",
            $"company_{slot + 1}",
            displayName,
            HashToken(token),
            isHost,
            true);

    private static bool TryNormalizeDisplayName(string? raw, out string value)
    {
        value = raw?.Trim() ?? string.Empty;
        return value.Length is >= 1 and <= 24 &&
               !value.Any(char.IsControl) &&
               value.All(character => !char.IsWhiteSpace(character) || character == ' ');
    }

    private static bool TryNormalizeRoomCode(string? raw, out string value)
    {
        value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
        return value.Length == InviteCodeLength &&
               value.All(character => InviteAlphabet.Contains(character));
    }

    private static bool TryReadBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;
        string authorization = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        token = authorization[prefix.Length..].Trim();
        return token.Length is >= 32 and <= 512;
    }

    private static string GenerateSessionToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
