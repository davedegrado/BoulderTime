using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Push;

/// <summary>
/// Sends a notification to a store app through Firebase Cloud Messaging (HTTP v1), which reaches Android directly and
/// iOS through Apple. Web Push cannot: inside a native app there is no service worker to receive it.
///
/// Written on .NET's own cryptography, like the Web Push sender: a service-account JSON is used to sign a short-lived
/// JWT, Google exchanges it for an access token, and the token sends the message. Nothing extra to trust or update.
/// </summary>
public sealed class FcmPushSender(HttpClient http, IConfiguration configuration, ILogger<FcmPushSender> logger) : IPushSender
{
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly ServiceAccount? _account = ServiceAccount.Read(configuration["Push:ServiceAccountJson"], logger);
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry;

    public bool Handles(PushPlatform platform) => platform == PushPlatform.Native;

    public bool IsConfigured => _account is not null;

    public async Task<PushResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default) =>
        (await SendWithDetailAsync(target, message, ct)).Result;

    public async Task<PushAttempt> SendWithDetailAsync(PushTarget target, PushMessage message, CancellationToken ct = default)
    {
        if (_account is null) return new(PushResult.Skipped, "NOT_CONFIGURED");

        string accessToken;
        try { accessToken = await AccessTokenAsync(ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Could not get a Firebase access token");
            return new(PushResult.Failed, "GOOGLE_AUTH_FAILED");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{_account.ProjectId}/messages:send")
        {
            Content = new StringContent(Payload(target, message), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {accessToken}");

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return new(PushResult.Delivered);

            var body = await response.Content.ReadAsStringAsync(ct);
            var code = ErrorCode(body);
            var detail = $"{(int)response.StatusCode} {code ?? response.StatusCode.ToString()}";

            // The app was uninstalled or the token was replaced: stop sending to it.
            if (response.StatusCode is HttpStatusCode.NotFound || code == "UNREGISTERED") return new(PushResult.Gone, detail);
            if (response.StatusCode == HttpStatusCode.BadRequest && body.Contains("INVALID_ARGUMENT", StringComparison.Ordinal)
                && body.Contains("token", StringComparison.OrdinalIgnoreCase))
                return new(PushResult.Gone, detail);

            // The code says why: THIRD_PARTY_AUTH_ERROR is Firebase unable to reach Apple (no APNs key uploaded),
            // SENDER_ID_MISMATCH a token from another Firebase project.
            logger.LogWarning("Firebase rejected a notification with {Status} {Code}", (int)response.StatusCode, code);
            return new(PushResult.Failed, detail);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Firebase delivery failed");
            return new(PushResult.Failed, "NETWORK_ERROR");
        }
    }

    /// <summary>
    /// Firebase's own error code (UNREGISTERED, THIRD_PARTY_AUTH_ERROR, SENDER_ID_MISMATCH…), found in the error's
    /// details; failing that, its general status (INVALID_ARGUMENT, UNAUTHENTICATED…). Null when the body says neither.
    /// </summary>
    internal static string? ErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error)) return null;
            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                foreach (var d in details.EnumerateArray())
                    if (d.TryGetProperty("errorCode", out var c) && c.GetString() is { Length: > 0 } code) return code;
            return error.TryGetProperty("status", out var status) ? status.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>A Google access token, kept until shortly before it expires rather than fetched per message.</summary>
    private async Task<string> AccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiry) return _accessToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiry) return _accessToken;

            using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"] = SignedAssertion(_account!),
            }), ct);
            response.EnsureSuccessStatusCode();

            var token = JsonSerializer.Deserialize<AccessTokenResponse>(await response.Content.ReadAsStringAsync(ct), JsonOptions)
                ?? throw new InvalidOperationException("Google returned no access token.");
            _accessToken = token.AccessToken;
            // A minute of margin, so a token never expires mid-flight.
            _accessTokenExpiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, token.ExpiresIn) - 60);
            return _accessToken;
        }
        finally { _tokenLock.Release(); }
    }

    /// <summary>
    /// The message as Firebase expects it.
    ///
    /// Both halves are deliberate: `notification` is what the system shows while the app is closed, and `data`
    /// carries the page to open when it is tapped. There is deliberately **no `click_action`**: that names an
    /// activity to start, and an app with no activity declaring it simply does nothing when the notification is
    /// tapped. Left out, Firebase opens the app itself and Capacitor hands the data to the app.
    /// </summary>
    internal static string Payload(PushTarget target, PushMessage message) => JsonSerializer.Serialize(new
    {
        message = new
        {
            token = target.Address,
            notification = new { title = message.Title, body = message.Body },
            data = new { url = message.Url },
            android = new
            {
                collapse_key = message.Tag,
                priority = "high",
                // Same tag replaces rather than stacks, matching what the web notifications do.
                notification = new { tag = message.Tag },
            },
            apns = Apns(message.Tag),
        },
    }, JsonOptions);

    /// <summary>
    /// The iPhone half. APNs spells its keys with hyphens, which no C# property can, hence the dictionaries:
    /// `thread-id` groups notifications of the same kind, and the `apns-collapse-id` header makes a newer one replace
    /// the older, as `tag` does on Android. APNs refuses a collapse id over 64 bytes, so a longer tag only groups.
    /// </summary>
    private static object Apns(string? tag)
    {
        var aps = new Dictionary<string, object> { ["sound"] = "default" };
        if (tag is null) return new { payload = new { aps } };
        aps["thread-id"] = tag;
        return Encoding.UTF8.GetByteCount(tag) <= 64
            ? new { headers = new Dictionary<string, string> { ["apns-collapse-id"] = tag }, payload = new { aps } }
            : new { payload = new { aps } };
    }

    /// <summary>The service account proving who we are: a JWT signed with its private key (RFC 7523).</summary>
    internal static string SignedAssertion(ServiceAccount account, DateTimeOffset? now = null)
    {
        var issued = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = account.ClientEmail,
            scope = Scope,
            aud = TokenEndpoint,
            iat = issued,
            exp = issued + 3600,
        }));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{claims}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AccessTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    /// <summary>The parts of a Firebase service-account JSON that sending needs.</summary>
    internal sealed record ServiceAccount(string ProjectId, string ClientEmail, string PrivateKey)
    {
        /// <summary>Returns null — push simply off — rather than failing to start when nothing is configured.</summary>
        public static ServiceAccount? Read(string? json, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var account = Parse(json);
                logger.LogInformation("Firebase push is configured for project {Project}", account.ProjectId);
                return account;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Push:ServiceAccountJson is not a usable Firebase service account; app notifications are off");
                return null;
            }
        }

        internal static ServiceAccount Parse(string json)
        {
            using var document = JsonDocument.Parse(json.Trim().Trim('\''));
            var root = document.RootElement;
            var projectId = root.GetProperty("project_id").GetString();
            var clientEmail = root.GetProperty("client_email").GetString();
            // Pasted into an environment variable, the key's line breaks often arrive as the two characters \n.
            var privateKey = root.GetProperty("private_key").GetString()?.Replace("\\n", "\n");
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey))
                throw new InvalidOperationException("project_id, client_email and private_key are all required.");
            return new ServiceAccount(projectId, clientEmail, privateKey);
        }
    }
}
