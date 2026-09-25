using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoulderTime.Infrastructure.Push;

public sealed class WebPushOptions
{
    /// <summary>VAPID keys identify BoulderTime to the browsers' push services. Generated once; the public one is shared with devices.</summary>
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
    /// <summary>Contact address the push service can use if something is wrong with our sending.</summary>
    public string Subject { get; set; } = "mailto:support@bouldertime.com";
}

/// <summary>
/// Sends a Web Push message: VAPID authentication (RFC 8292) plus payload encryption (RFC 8291, aes128gcm).
/// Written on .NET's own cryptography rather than an external library, so there is nothing extra to trust or update.
/// The push service only ever sees ciphertext: the notification text is readable by the device alone.
/// </summary>
public sealed class WebPushSender(HttpClient http, IOptions<WebPushOptions> options, ILogger<WebPushSender> logger) : IPushSender
{
    private readonly WebPushOptions _options = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.PublicKey) && !string.IsNullOrWhiteSpace(_options.PrivateKey);

    /// <summary>Delivers one message. Returns false when the device is gone and the subscription should be removed.</summary>
    public async Task<PushResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default)
    {
        if (!IsConfigured) return PushResult.Skipped;

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        var body = Encrypt(payload, FromBase64Url(target.P256dh), FromBase64Url(target.Auth));

        using var request = new HttpRequestMessage(HttpMethod.Post, target.Endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.TryAddWithoutValidation("TTL", "86400");          // keep for a day if the device is offline
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Authorization", VapidHeader(new Uri(target.Endpoint)));

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return PushResult.Delivered;
            // The device unsubscribed or the endpoint expired: stop sending to it.
            if (response.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound) return PushResult.Gone;
            logger.LogWarning("Push rejected with {Status}", (int)response.StatusCode);
            return PushResult.Failed;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Push delivery failed");
            return PushResult.Failed;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A short-lived signed token proving the message comes from BoulderTime (RFC 8292).</summary>
    private string VapidHeader(Uri endpoint)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "ES256" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud = $"{endpoint.Scheme}://{endpoint.Host}",
            exp = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds(),
            sub = _options.Subject,
        }));

        using var key = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = FromBase64Url(_options.PrivateKey),
            Q = PublicPoint(FromBase64Url(_options.PublicKey)),
        });
        var signature = key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256);
        return $"vapid t={header}.{claims}.{Base64Url(signature)}, k={_options.PublicKey}";
    }

    /// <summary>
    /// Encrypts the payload for one device (RFC 8291). Public so the test project can verify it against a real decrypt.
    /// Only that device can read it: the key is derived from its own
    /// public key, its auth secret and a fresh key pair we throw away afterwards.
    /// </summary>
    public static byte[] Encrypt(byte[] payload, byte[] devicePublicKey, byte[] authSecret)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var ephemeralPublic = UncompressedPoint(ephemeral.PublicKey.ExportParameters());

        using var devicePeer = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = PublicPoint(devicePublicKey),
        });
        var shared = ephemeral.DeriveRawSecretAgreement(devicePeer.PublicKey);

        // Mix in both public keys so the key material is bound to this exact pair of devices.
        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), devicePublicKey, ephemeralPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);
        var contentKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // The record ends with a 0x02 delimiter before encryption, as the standard requires.
        var plaintext = Concat(payload, [0x02]);
        var ciphertext = new byte[plaintext.Length + 16];
        using (var aes = new AesGcm(contentKey, 16))
            aes.Encrypt(nonce, plaintext, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length));

        // Header: salt | record size | key length | our public key, then the encrypted record.
        var recordSize = new byte[] { 0x00, 0x00, 0x10, 0x00 }; // 4096
        return Concat(salt, recordSize, [(byte)ephemeralPublic.Length], ephemeralPublic, ciphertext);
    }

    private static ECPoint PublicPoint(byte[] uncompressed) => uncompressed.Length == 65 && uncompressed[0] == 0x04
        ? new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..65] }
        : throw new ArgumentException("Expected an uncompressed P-256 public key.", nameof(uncompressed));

    private static byte[] UncompressedPoint(ECParameters parameters) => Concat([0x04], parameters.Q.X!, parameters.Q.Y!);

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(p => p.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }
        return result;
    }

    public static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
