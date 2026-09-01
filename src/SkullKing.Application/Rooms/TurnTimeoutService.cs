using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SkullKing.Application.Rooms;

/// <summary>每秒巡检一次，把超时或掉线过久的回合交给托管推进。</summary>
public sealed class TurnTimeoutService(RoomService rooms, ILogger<TurnTimeoutService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await rooms.TickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "回合巡检出错");
            }
        }
    }
}
