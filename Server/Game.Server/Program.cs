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
builder.Services.AddSingleton(PvpMatchRuntime.FromEnvironment());
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

app.MapGet("/api/v1/match", (
    HttpRequest request,
    PvpMatchRuntime runtime) =>
{
    return runtime.TryAuthenticate(request, out AuthenticatedPlayer player)
        ? Results.Ok(runtime.GetReconnectState(player))
        : Results.Json(ApiError.Unauthorized(), statusCode: 401);
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/commands", (
    HttpRequest request,
    SubmitCommandRequest body,
    PvpMatchRuntime runtime) =>
{
    if (!runtime.TryAuthenticate(request, out AuthenticatedPlayer player))
        return Results.Json(ApiError.Unauthorized(), statusCode: 401);

    CommandResponse result = runtime.Submit(player, body);
    return Results.Json(result, statusCode: result.Accepted ? 200 : 409);
}).RequireRateLimiting("pvp");

app.MapPost("/api/v1/ready", (
    HttpRequest request,
    ReadyRequest body,
    PvpMatchRuntime runtime) =>
{
    if (!runtime.TryAuthenticate(request, out AuthenticatedPlayer player))
        return Results.Json(ApiError.Unauthorized(), statusCode: 401);

    ReadyResponse result = runtime.MarkReady(player, body);
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
