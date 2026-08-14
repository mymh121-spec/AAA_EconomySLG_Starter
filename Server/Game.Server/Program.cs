using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Game.Server;
using Microsoft.AspNetCore.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("PVP_URLS") ??
    "http://127.0.0.1:5100");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("pvp", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                TokensPerPeriod = 15,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSingleton(PvpRoomRegistry.FromEnvironment());
builder.Services.AddHostedService<PvpTurnTimeoutService>();

WebApplication app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new HealthResponse(
    "정상",
    "economy-slg-pvp",
    PvpMatchRuntime.ServerVersion,
    DateTimeOffset.UtcNow)));

app.MapPost("/api/v1/rooms", (
    CreateRoomRequest body,
    PvpRoomRegistry registry) =>
{
    RoomOperationResult<RoomSessionResponse> result = registry.CreateRoom(body);
    return result.Success
        ? Results.Json(result.Value, statusCode: result.StatusCode)
        : Results.Json(result.Error, statusCode: result.StatusCode);
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/rooms/{roomCode}/join", (
    string roomCode,
    JoinRoomRequest body,
    PvpRoomRegistry registry) =>
{
    RoomOperationResult<RoomSessionResponse> result = registry.JoinRoom(roomCode, body);
    return result.Success
        ? Results.Json(result.Value, statusCode: result.StatusCode)
        : Results.Json(result.Error, statusCode: result.StatusCode);
}).RequireRateLimiting("pvp");

app.MapGet("/api/v1/rooms/{roomCode}", (
    string roomCode,
    HttpRequest request,
    PvpRoomRegistry registry) =>
{
    RoomOperationResult<RoomStateResponse> result = registry.GetRoom(roomCode, request);
    return result.Success
        ? Results.Json(result.Value, statusCode: result.StatusCode)
        : Results.Json(result.Error, statusCode: result.StatusCode);
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/rooms/{roomCode}/start", (
    string roomCode,
    HttpRequest request,
    PvpRoomRegistry registry) =>
{
    RoomOperationResult<RoomStateResponse> result = registry.StartRoom(roomCode, request);
    return result.Success
        ? Results.Json(result.Value, statusCode: result.StatusCode)
        : Results.Json(result.Error, statusCode: result.StatusCode);
}).RequireRateLimiting("pvp");

app.MapGet("/api/v1/rooms/{roomCode}/match", (
    string roomCode,
    HttpRequest request,
    PvpRoomRegistry registry) =>
{
    if (!registry.TryGetMatch(
            roomCode, request, out AuthenticatedPlayer player,
            out PvpMatchRuntime runtime, out ApiError error, out int statusCode))
    {
        return Results.Json(error, statusCode: statusCode);
    }

    return Results.Ok(runtime.GetReconnectState(player));
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/rooms/{roomCode}/commands", (
    string roomCode,
    HttpRequest request,
    SubmitCommandRequest body,
    PvpRoomRegistry registry) =>
{
    if (!registry.TryGetMatch(
            roomCode, request, out AuthenticatedPlayer player,
            out PvpMatchRuntime runtime, out ApiError error, out int statusCode))
    {
        return Results.Json(error, statusCode: statusCode);
    }

    CommandResponse result = runtime.Submit(player, body);
    registry.RecordMatchActivity(roomCode, runtime.IsFinished);
    return Results.Json(result, statusCode: result.Accepted ? 200 : 409);
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/rooms/{roomCode}/ready", (
    string roomCode,
    HttpRequest request,
    ReadyRequest body,
    PvpRoomRegistry registry) =>
{
    if (!registry.TryGetMatch(
            roomCode, request, out AuthenticatedPlayer player,
            out PvpMatchRuntime runtime, out ApiError error, out int statusCode))
    {
        return Results.Json(error, statusCode: statusCode);
    }

    ReadyResponse result = runtime.MarkReady(player, body);
    registry.RecordMatchActivity(roomCode, runtime.IsFinished);
    return Results.Json(result, statusCode: result.Accepted ? 200 : 409);
}).RequireRateLimiting("pvp");

app.MapGet("/", () => Results.Ok(new
{
    service = "경제 SLG PvP 권위 서버",
    status = "정상",
    health = "/health",
    protocolVersion = 1
}));

app.Run();
