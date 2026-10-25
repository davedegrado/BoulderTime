using System.Threading.Channels;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Badges;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Badges;

/// <summary>
/// Badges, outside the requests (ADR-069): records the day of each climbing activity and awards the badges it
/// reaches, one person at a time; and every hour settles the leaderboard titles of the months and years that closed.
/// Whatever fails here is logged and dropped: the request that queued it has long succeeded.
/// </summary>
public sealed class BadgeWorker(IServiceScopeFactory scopes, ILogger<BadgeWorker> logger) : BackgroundService, IBadgeQueue
{
    private readonly Channel<(Guid UserId, DateTimeOffset At)> _queue =
        Channel.CreateBounded<(Guid, DateTimeOffset)>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(Guid userId, DateTimeOffset at) => _queue.Writer.TryWrite((userId, at));

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(AwardAsync(stoppingToken), SettleAsync(stoppingToken));

    private async Task AwardAsync(CancellationToken ct)
    {
        await foreach (var (userId, at) in _queue.Reader.ReadAllAsync(ct))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var awarder = scope.ServiceProvider.GetRequiredService<BadgeAwarder>();
                await awarder.RecordActivityAsync(userId, at, ct);
                await awarder.EvaluateAsync(userId, ct: ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not award badges after an activity");
            }
        }
    }

    private async Task SettleAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), ct);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var titles = await scope.ServiceProvider.GetRequiredService<CompetitionTitles>().SettleDueAsync(ct);
                if (titles > 0) logger.LogInformation("Handed out {Count} leaderboard title(s)", titles);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Settling leaderboard titles failed; will retry in an hour");
            }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
