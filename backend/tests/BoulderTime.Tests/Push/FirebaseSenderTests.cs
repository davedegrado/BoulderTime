using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoulderTime.Infrastructure.Push;

namespace BoulderTime.Tests.Push;

/// <summary>
/// The Firebase sender proves who we are with a signed token rather than a library, so the signing is verified the
/// only way that counts: Google's side of it, checked against the service account's public key.
/// </summary>
public sealed class FirebaseSenderTests
{
    private static (string Json, RSA Key) ServiceAccountJson(bool escapedNewlines = false)
    {
        var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var json = JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = "bouldertime-test",
            client_email = "push@bouldertime-test.iam.gserviceaccount.com",
            private_key = escapedNewlines ? pem.Replace("\n", "\\n") : pem,
        });
        return (json, rsa);
    }

    [Fact]
    public void Google_can_verify_the_token_we_sign_and_it_says_what_we_are_asking_for()
    {
        var (json, key) = ServiceAccountJson();
        var account = FcmPushSender.ServiceAccount.Parse(json);
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var assertion = FcmPushSender.SignedAssertion(account, now);

        var parts = assertion.Split('.');
        parts.Should().HaveCount(3);
        // Google verifies the signature with the public half of the service account's key.
        var signed = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        key.VerifyData(signed, FromBase64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();

        using var claims = JsonDocument.Parse(FromBase64Url(parts[1]));
        claims.RootElement.GetProperty("iss").GetString().Should().Be("push@bouldertime-test.iam.gserviceaccount.com");
        claims.RootElement.GetProperty("aud").GetString().Should().Be("https://oauth2.googleapis.com/token");
        claims.RootElement.GetProperty("scope").GetString().Should().Be("https://www.googleapis.com/auth/firebase.messaging");
        claims.RootElement.GetProperty("iat").GetInt64().Should().Be(now.ToUnixTimeSeconds());
        // An hour is Google's maximum; a longer one is refused outright.
        (claims.RootElement.GetProperty("exp").GetInt64() - now.ToUnixTimeSeconds()).Should().Be(3600);
    }

    [Fact]
    public void A_key_pasted_into_an_environment_variable_still_works()
    {
        // Through Railway's variables the key's line breaks usually arrive as the two characters \n.
        var (json, key) = ServiceAccountJson(escapedNewlines: true);

        var account = FcmPushSender.ServiceAccount.Parse(json);
        var parts = FcmPushSender.SignedAssertion(account).Split('.');

        key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), FromBase64Url(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Should().BeTrue();
        account.ProjectId.Should().Be("bouldertime-test");
    }

    [Fact]
    public void An_unusable_service_account_turns_app_notifications_off_instead_of_stopping_the_API()
    {
        var logger = new BoulderTime.Tests.Infrastructure.CapturingLogger();

        FcmPushSender.ServiceAccount.Read(null, logger).Should().BeNull();
        FcmPushSender.ServiceAccount.Read("   ", logger).Should().BeNull();
        FcmPushSender.ServiceAccount.Read("not json at all", logger).Should().BeNull();
        FcmPushSender.ServiceAccount.Read("""{"project_id":"x"}""", logger).Should().BeNull();
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
