using Fifa_serv.Services;

namespace Fifa_serv.Background;

public sealed class MoscowMidnightRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MoscowMidnightRefreshService> _logger;
    private static readonly TimeZoneInfo Moscow = ResolveMoscowTimeZone();

    public MoscowMidnightRefreshService(IServiceScopeFactory scopeFactory, ILogger<MoscowMidnightRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextMoscowMidnight();
            _logger.LogInformation("Next data refresh in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                //await scope.ServiceProvider.GetRequiredService<DataRefreshService>().RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Daily data refresh failed");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextMoscowMidnight()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var nowMoscow = TimeZoneInfo.ConvertTime(nowUtc, Moscow);
        var nextLocal = nowMoscow.Date.AddDays(1);
        var nextUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocal, Moscow);
        return nextUtc - nowUtc.UtcDateTime;
    }

    private static TimeZoneInfo ResolveMoscowTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time"); }
    }
}
