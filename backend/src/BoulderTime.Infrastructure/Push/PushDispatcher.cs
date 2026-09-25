using System.Threading.Channels;
using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Push;

/// <summary>
/// Takes queued notifications and delivers them to each of the person's devices, outside the web request.
/// Devices that have gone away are removed, so the list doesn't fill up with dead phones.
/// </summary>
public sealed class PushDispatcher(IServiceScopeFactory scopes, IPushSender sender, ILogger<PushDispatcher> logger)
    : BackgroundService, IPushQueue
{
    private readonly Channel<(Guid UserId, PushMessage Message)> _queue =
        Channel.CreateBounded<(Guid, PushMessage)>(new BoundedChannelOptions(1000)
        {
            // A flood of notifications must never block the request that caused it.
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    public void Enqueue(Guid userId, PushMessage message)
    {
        if (!sender.IsConfigured) return;
        _queue.Writer.TryWrite((userId, message));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (userId, message) in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await DeliverAsync(userId, message, stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not deliver a push notification");
            }
        }
    }

    private async Task DeliverAsync(Guid userId, PushMessage message, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.AppDbContext>();

        var devices = await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);
        if (devices.Count == 0) return;

        foreach (var device in devices)
        {
            var result = await sender.SendAsync(new PushTarget(device.Endpoint, device.P256dh, device.Auth), message, ct);
            switch (result)
            {
                case PushResult.Delivered:
                    device.Succeeded(DateTimeOffset.UtcNow);
                    break;
                case PushResult.Gone:
                    db.PushSubscriptions.Remove(device); // the person uninstalled the app or revoked permission
                    break;
                case PushResult.Failed:
                    device.Failed();
                    if (device.Failures >= 5) db.PushSubscriptions.Remove(device);
                    break;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
