using BoulderTime.Application.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Users;

/// <summary>
/// Erases accounts once their week is up. Runs a few minutes after start and then daily: deletion is a promise with
/// a deadline, and a promise nobody checks is not kept.
/// </summary>
public sealed class AccountErasureWorker(IServiceScopeFactory scopes, ILogger<AccountErasureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var erased = await scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseDueAsync(stoppingToken);
                if (erased > 0) logger.LogInformation("Erased {Count} account(s) past their grace period", erased);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Account erasure run failed; will retry tomorrow");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
