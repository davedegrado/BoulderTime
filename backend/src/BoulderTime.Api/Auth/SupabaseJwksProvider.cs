using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BoulderTime.Api.Auth;

/// <summary>
/// Fetches and caches the project's public signing keys. Refreshes on a schedule and — rate-limited —
/// when a token references an unknown key id, so key rotation works without restarts.
/// </summary>
public sealed class SupabaseJwksProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<SupabaseAuthOptions> options,
    ILogger<SupabaseJwksProvider> logger)
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MinRefetchOnUnknownKid = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<SecurityKey> _keys = [];
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<SecurityKey>> GetKeysAsync(string? kid, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var stale = now - _fetchedAt > RefreshInterval;
        var unknownKid = kid is not null && _keys.All(k => k.KeyId != kid) && now - _fetchedAt > MinRefetchOnUnknownKid;

        if (stale || unknownKid)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (DateTimeOffset.UtcNow - _fetchedAt > MinRefetchOnUnknownKid)
                    await RefreshAsync(ct);
            }
            finally
            {
                _lock.Release();
            }
        }
        return _keys;
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(SupabaseJwksProvider));
            var json = await client.GetStringAsync(options.Value.JwksUrl, ct);
            _keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
            logger.LogInformation("Loaded {Count} Supabase signing key(s)", _keys.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException)
        {
            // Keep serving previously cached keys; tokens signed by unknown keys will fail validation (401).
            logger.LogWarning(ex, "Could not refresh Supabase JWKS from {Url}", options.Value.JwksUrl);
        }
        finally
        {
            _fetchedAt = DateTimeOffset.UtcNow;
        }
    }
}
