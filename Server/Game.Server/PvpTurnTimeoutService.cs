namespace Game.Server;

public sealed class PvpTurnTimeoutService : BackgroundService
{
    private readonly PvpRoomRegistry _registry;
    private readonly ILogger<PvpTurnTimeoutService> _logger;

    public PvpTurnTimeoutService(
        PvpRoomRegistry registry,
        ILogger<PvpTurnTimeoutService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                IReadOnlyList<PvpMatchRuntime> runtimes =
                    _registry.GetActiveRuntimes();
                for (int i = 0; i < runtimes.Count; i++)
                    runtimes[i].ProcessTurnTimeout(DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "PvP 턴 제한시간 처리에 실패했습니다.");
            }
        }
    }
}
