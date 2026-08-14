using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out Uri? baseUri))
    throw new ArgumentException("사용법: PvpRealtimeStreamSmoke <http-base-url>");

using var http = new HttpClient { BaseAddress = baseUri };
RoomSession hostA = await CreateRoomAsync("스트림 방장 A");
RoomSession guestA = await JoinRoomAsync(hostA.RoomCode, "스트림 참가자 A");
RoomSession hostB = await CreateRoomAsync("스트림 방장 B");
RoomSession guestB = await JoinRoomAsync(hostB.RoomCode, "스트림 참가자 B");
await StartRoomAsync(hostA);
await StartRoomAsync(hostB);

using ClientWebSocket hostSocketA = await ConnectStreamAsync(hostA);
using ClientWebSocket guestSocketA = await ConnectStreamAsync(guestA);
using ClientWebSocket hostSocketB = await ConnectStreamAsync(hostB);

StreamMessage initialHostA = await ReceiveAsync(hostSocketA);
StreamMessage initialGuestA = await ReceiveAsync(guestSocketA);
StreamMessage initialHostB = await ReceiveAsync(hostSocketB);
Assert(initialHostA.Type == "state", "host A initial state message");
Assert(initialHostA.StreamId == initialGuestA.StreamId,
    "same room uses one stream epoch");
Assert(initialHostA.StreamId != initialHostB.StreamId,
    "different rooms use isolated stream epochs");
Assert(GetString(initialHostA.State, "playerId") == hostA.PlayerId,
    "stream state is personalized for host");
Assert(GetString(initialGuestA.State, "playerId") == guestA.PlayerId,
    "stream state is personalized for guest");

(string unitId, int targetX, int targetY) =
    FindAdjacentMovement(initialHostA.State, hostA.CompanyId);
JsonElement hostPlayer = FindPlayer(initialHostA.State, hostA.PlayerId);
await PostAcceptedAsync(
    $"/api/v1/rooms/{hostA.RoomCode}/commands",
    new
    {
        requestId = "stream-move-request",
        protocolVersion = 1,
        matchId = GetString(initialHostA.State, "matchId"),
        expectedRevision = GetInt(initialHostA.State, "revision"),
        commandId = "stream-move-command",
        turn = GetInt(initialHostA.State, "turn"),
        sequence = GetInt(hostPlayer, "expectedSequence"),
        kind = "MoveUnit",
        regionId = "map",
        resourceId = (string?)null,
        targetCompanyId = (string?)null,
        targetId = unitId,
        quantity = 0,
        limitPrice = 0,
        targetX,
        targetY,
        action = ""
    },
    hostA.AccessToken);

StreamMessage hostCommandUpdate = await ReceiveAsync(hostSocketA);
StreamMessage guestCommandUpdate = await ReceiveAsync(guestSocketA);
Assert(hostCommandUpdate.Version > initialHostA.Version,
    "host receives command push");
Assert(guestCommandUpdate.Version > initialGuestA.Version,
    "guest receives opponent command push");
Assert(hostCommandUpdate.State.GetProperty("ownPendingCommands")
        .GetArrayLength() == 1,
    "host sees own pending command");
Assert(guestCommandUpdate.State.GetProperty("ownPendingCommands")
        .GetArrayLength() == 0,
    "guest cannot see host pending command payload");
Assert(!await HasMessageWithinAsync(hostSocketB, TimeSpan.FromSeconds(2)),
    "room B receives no room A state change");

await MarkReadyAsync(hostA, hostCommandUpdate.State, "stream-ready-host");
StreamMessage hostReadyUpdate = await ReceiveAsync(hostSocketA);
StreamMessage guestSawHostReady = await ReceiveAsync(guestSocketA);
Assert(GetBool(FindPlayer(guestSawHostReady.State, hostA.PlayerId), "ready"),
    "guest sees host ready without manual refresh");

await MarkReadyAsync(guestA, guestSawHostReady.State, "stream-ready-guest");
StreamMessage hostResolved = await ReceiveAsync(hostSocketA);
StreamMessage guestResolved = await ReceiveAsync(guestSocketA);
Assert(GetInt(hostResolved.State, "turn") == 2,
    "host receives resolved next turn");
Assert(GetInt(guestResolved.State, "turn") == 2,
    "guest receives resolved next turn");
Assert(hostResolved.Version > hostReadyUpdate.Version,
    "stream version increases after resolution");

hostSocketA.Abort();
using ClientWebSocket reconnectedHostA = await ConnectStreamAsync(hostA);
StreamMessage reconnectState = await ReceiveAsync(reconnectedHostA);
Assert(GetInt(reconnectState.State, "turn") == 2,
    "reconnected websocket receives full latest snapshot");
Assert(reconnectState.StreamId == initialHostA.StreamId,
    "reconnect remains on the same runtime epoch");

using ClientWebSocket timeoutSocketB = await ConnectStreamAsync(hostB);
StreamMessage timeoutInitial = await ReceiveAsync(timeoutSocketB);
StreamMessage timeoutResolved = await ReceiveAsync(
    timeoutSocketB,
    TimeSpan.FromSeconds(20));
if (timeoutResolved.Version == timeoutInitial.Version)
{
    timeoutResolved = await ReceiveAsync(
        timeoutSocketB,
        TimeSpan.FromSeconds(5));
}
Assert(timeoutResolved.Version > timeoutInitial.Version,
    "turn timeout emits a changed stream version");
Assert(GetInt(timeoutResolved.State, "turn") == 2,
    "turn timeout automatically resolves room B without polling");

bool unauthorizedRejected = false;
using (var unauthorized = new ClientWebSocket())
{
    try
    {
        await unauthorized.ConnectAsync(
            CreateStreamUri(hostA.RoomCode),
            CancellationToken.None);
    }
    catch (WebSocketException)
    {
        unauthorizedRejected = true;
    }
}
Assert(unauthorizedRejected, "websocket rejects missing bearer token");

guestSocketA.Abort();
hostSocketB.Abort();
reconnectedHostA.Abort();
timeoutSocketB.Abort();
Console.WriteLine(
    $"PASS PvpRealtimeStreamSmoke roomA={hostA.RoomCode} " +
    $"roomB={hostB.RoomCode} versions={initialHostA.Version}->" +
    $"{hostResolved.Version} turn=2 isolated=true reconnect=true " +
    "timeoutPush=true");

async Task<RoomSession> CreateRoomAsync(string displayName)
{
    using HttpResponseMessage response = await http.PostAsJsonAsync(
        "/api/v1/rooms",
        new { displayName, maxPlayers = 2 });
    return await ReadSessionAsync(response, HttpStatusCode.Created);
}

async Task<RoomSession> JoinRoomAsync(string roomCode, string displayName)
{
    using HttpResponseMessage response = await http.PostAsJsonAsync(
        $"/api/v1/rooms/{roomCode}/join",
        new { displayName });
    return await ReadSessionAsync(response, HttpStatusCode.Created);
}

async Task<RoomSession> ReadSessionAsync(
    HttpResponseMessage response,
    HttpStatusCode expected)
{
    string json = await response.Content.ReadAsStringAsync();
    Assert(response.StatusCode == expected,
        $"session HTTP {(int)response.StatusCode}: {json}");
    using JsonDocument document = JsonDocument.Parse(json);
    JsonElement root = document.RootElement;
    return new RoomSession(
        GetString(root, "roomCode"),
        GetString(root, "playerId"),
        GetString(root, "companyId"),
        GetString(root, "accessToken"));
}

async Task StartRoomAsync(RoomSession host)
{
    using var request = AuthorizedRequest(
        HttpMethod.Post,
        $"/api/v1/rooms/{host.RoomCode}/start",
        host.AccessToken);
    using HttpResponseMessage response = await http.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    Assert(response.IsSuccessStatusCode,
        $"start room HTTP {(int)response.StatusCode}: {body}");
}

async Task<ClientWebSocket> ConnectStreamAsync(RoomSession session)
{
    var socket = new ClientWebSocket();
    socket.Options.SetRequestHeader(
        "Authorization",
        "Bearer " + session.AccessToken);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await socket.ConnectAsync(CreateStreamUri(session.RoomCode), timeout.Token);
    return socket;
}

Uri CreateStreamUri(string roomCode)
{
    var builder = new UriBuilder(baseUri)
    {
        Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
    };
    string prefix = builder.Path.TrimEnd('/');
    builder.Path = prefix + $"/api/v1/rooms/{roomCode}/stream";
    return builder.Uri;
}

async Task<StreamMessage> ReceiveAsync(
    ClientWebSocket socket,
    TimeSpan? timeoutDuration = null)
{
    using var timeout = new CancellationTokenSource(
        timeoutDuration ?? TimeSpan.FromSeconds(10));
    byte[] buffer = new byte[16 * 1024];
    using var content = new MemoryStream();
    WebSocketReceiveResult result;
    do
    {
        result = await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            timeout.Token);
        Assert(result.MessageType == WebSocketMessageType.Text,
            "websocket sends text messages");
        content.Write(buffer, 0, result.Count);
        Assert(content.Length <= 2 * 1024 * 1024,
            "websocket message size bound");
    } while (!result.EndOfMessage);

    string json = Encoding.UTF8.GetString(content.ToArray());
    StreamMessage? message = JsonSerializer.Deserialize<StreamMessage>(
        json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return message ?? throw new InvalidDataException(
        "stream JSON could not be parsed");
}

async Task<bool> HasMessageWithinAsync(
    ClientWebSocket socket,
    TimeSpan duration)
{
    using var timeout = new CancellationTokenSource(duration);
    try
    {
        byte[] buffer = new byte[1024];
        await socket.ReceiveAsync(
            new ArraySegment<byte>(buffer),
            timeout.Token);
        return true;
    }
    catch (OperationCanceledException)
    {
        return false;
    }
}

async Task MarkReadyAsync(
    RoomSession session,
    JsonElement state,
    string requestId)
{
    JsonElement player = FindPlayer(state, session.PlayerId);
    await PostAcceptedAsync(
        $"/api/v1/rooms/{session.RoomCode}/ready",
        new
        {
            requestId,
            protocolVersion = 1,
            matchId = GetString(state, "matchId"),
            turn = GetInt(state, "turn"),
            expectedRevision = GetInt(state, "revision"),
            lastSequence = GetInt(player, "expectedSequence")
        },
        session.AccessToken);
}

async Task PostAcceptedAsync(string path, object body, string token)
{
    using var request = AuthorizedRequest(HttpMethod.Post, path, token);
    request.Content = JsonContent.Create(body);
    using HttpResponseMessage response = await http.SendAsync(request);
    string json = await response.Content.ReadAsStringAsync();
    Assert(response.IsSuccessStatusCode,
        $"POST {path} HTTP {(int)response.StatusCode}: {json}");
    using JsonDocument document = JsonDocument.Parse(json);
    Assert(GetBool(document.RootElement, "accepted"),
        $"POST {path} accepted");
}

HttpRequestMessage AuthorizedRequest(
    HttpMethod method,
    string path,
    string token)
{
    var request = new HttpRequestMessage(method, path);
    request.Headers.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            token);
    return request;
}

(string UnitId, int X, int Y) FindAdjacentMovement(
    JsonElement state,
    string companyId)
{
    JsonElement map = state.GetProperty("world").GetProperty("map");
    int width = GetInt(map, "width");
    int height = GetInt(map, "height");
    JsonElement unit = map.GetProperty("units").EnumerateArray()
        .First(item => GetString(item, "ownerCompanyId") == companyId);
    int x = GetInt(unit, "x");
    int y = GetInt(unit, "y");
    int[] terrain = map.GetProperty("terrain").EnumerateArray()
        .Select(item => item.GetInt32()).ToArray();
    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
    {
        int targetX = ((x + dx) % width + width) % width;
        int targetY = y + dy;
        if (targetY >= 0 && targetY < height &&
            terrain[targetY * width + targetX] != 0)
        {
            return (GetString(unit, "unitId"), targetX, targetY);
        }
    }
    throw new InvalidOperationException("adjacent land target not found");
}

JsonElement FindPlayer(JsonElement state, string playerId) =>
    state.GetProperty("players").EnumerateArray()
        .First(item => GetString(item, "playerId") == playerId);

static string GetString(JsonElement value, string property) =>
    value.GetProperty(property).GetString() ?? string.Empty;
static int GetInt(JsonElement value, string property) =>
    value.GetProperty(property).GetInt32();
static bool GetBool(JsonElement value, string property) =>
    value.GetProperty(property).GetBoolean();
static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("FAIL: " + message);
}

internal sealed record RoomSession(
    string RoomCode,
    string PlayerId,
    string CompanyId,
    string AccessToken);

internal sealed class StreamMessage
{
    public string Type { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public long Version { get; set; }
    public DateTimeOffset ServerUtc { get; set; }
    public JsonElement State { get; set; }
}
