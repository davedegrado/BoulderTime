using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Channels;
using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Email;

/// <summary>
/// Sends the moderation emails through Resend's HTTP API, outside the request (ADR-050). Configured with
/// <c>Email__ResendApiKey</c> (a secret), <c>Moderation__AlertEmail</c> (where they go), optionally <c>Email__From</c>
/// and <c>Moderation__AppUrl</c>. Without the key or the address it sends nothing and says so once in the log.
/// </summary>
public sealed class ResendModerationAlerts(HttpClient http, IConfiguration configuration, ILogger<ResendModerationAlerts> logger)
    : BackgroundService, IModerationAlerts
{
    private readonly string? _apiKey = Clean(configuration["Email:ResendApiKey"]);
    private readonly string? _to = Clean(configuration["Moderation:AlertEmail"]);
    private readonly string _from = Clean(configuration["Email:From"]) ?? "BoulderTime <noreply@bouldertime.com>";
    private readonly string _appUrl = (Clean(configuration["Moderation:AppUrl"]) ?? "https://bouldertime.com").TrimEnd('/');
    private bool _toldNotConfigured;

    private readonly Channel<ModerationAlert> _queue = Channel.CreateBounded<ModerationAlert>(new BoundedChannelOptions(200)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    public bool IsConfigured => _apiKey is not null && _to is not null;

    public void Send(ModerationAlert alert)
    {
        if (!IsConfigured)
        {
            if (!_toldNotConfigured) logger.LogWarning("Moderation emails are off: set Email__ResendApiKey and Moderation__AlertEmail");
            _toldNotConfigured = true;
            return;
        }
        _queue.Writer.TryWrite(alert);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var alert in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await DeliverAsync(alert, stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Could not send a moderation email");
            }
        }
    }

    private async Task DeliverAsync(ModerationAlert alert, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = _from,
                to = new[] { _to },
                subject = $"[BoulderTime] {alert.Subject}",
                text = $"{alert.Body}\n{_appUrl}{alert.Path}\n",
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Resend refused a moderation email: {Status} {Body}", (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
