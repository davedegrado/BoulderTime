using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Supabase;

/// <summary>
/// Removes the Supabase sign-in behind an account, using the service-role key. Without this the person could still
/// sign in and be handed a fresh, empty profile with the same address.
/// </summary>
public sealed class SupabaseAuthAdmin(HttpClient http, IConfiguration configuration, ILogger<SupabaseAuthAdmin> logger) : IAuthAdmin
{
    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var url = configuration["Supabase:Url"]?.TrimEnd('/');
        var key = configuration["Supabase:ServiceRoleKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            // Local development runs its own auth container; there is nothing to call.
            logger.LogInformation("No Supabase service key configured: the sign-in was left in place");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{url}/auth/v1/admin/users/{userId}");
        request.Headers.TryAddWithoutValidation("apikey", key);
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {key}");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            logger.LogWarning("Supabase refused to delete the sign-in: {Status}", (int)response.StatusCode);
    }
}
