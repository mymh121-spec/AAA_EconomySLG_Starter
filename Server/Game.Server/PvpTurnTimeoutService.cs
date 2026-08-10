namespace Game.Server;

public sealed class PvpTurnTimeoutService : BackgroundService
{
    private readonly PvpMatchRuntime _runtime;
    private readonly ILogger<PvpTurnTimeoutService> _logger;

    public PvpTurnTimeoutService(
        PvpMatchRuntime runtime,
        ILogger<PvpTurnTimeoutService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                _runtime.ProcessTurnTimeout(DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "PvP 턴 제한시간 처리에 실패했습니다.");
            }
        }
    }
}
