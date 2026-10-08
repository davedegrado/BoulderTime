using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Notifications;
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
    public void The_message_opens_the_app_and_says_which_page_to_go_to()
    {
        var target = new PushTarget(PushPlatform.Native, "fcm-token", "", "");
        var message = new PushMessage("Nuovi blocchi", "Grotta ritracciata", "/gyms/rock-n-fire", "gym-1");

        using var payload = JsonDocument.Parse(FcmPushSender.Payload(target, message));
        var sent = payload.RootElement.GetProperty("message");

        sent.GetProperty("token").GetString().Should().Be("fcm-token");
        // The title and body are what the system shows while the app is closed.
        sent.GetProperty("notification").GetProperty("title").GetString().Should().Be("Nuovi blocchi");
        sent.GetProperty("notification").GetProperty("body").GetString().Should().Be("Grotta ritracciata");
        // The page to open travels in the data, which the app reads when the notification is tapped.
        sent.GetProperty("data").GetProperty("url").GetString().Should().Be("/gyms/rock-n-fire");

        // No click_action: it names an activity to start, and an app without one does nothing when tapped. Firebase
        // opens the app itself when it is absent.
        var android = sent.GetProperty("android");
        android.GetProperty("notification").TryGetProperty("click_action", out _).Should().BeFalse();
        // The same tag replaces an earlier notification instead of stacking another one.
        android.GetProperty("notification").GetProperty("tag").GetString().Should().Be("gym-1");
        android.GetProperty("collapse_key").GetString().Should().Be("gym-1");

        // On an iPhone too: APNs reads hyphenated keys, so these have to be spelled exactly so.
        var apns = sent.GetProperty("apns");
        apns.GetProperty("headers").GetProperty("apns-collapse-id").GetString().Should().Be("gym-1");
        var aps = apns.GetProperty("payload").GetProperty("aps");
        aps.GetProperty("thread-id").GetString().Should().Be("gym-1");
        aps.GetProperty("sound").GetString().Should().Be("default");
    }

    [Fact]
    public void A_message_without_a_tag_stacks_on_an_iPhone_too()
    {
        var target = new PushTarget(PushPlatform.Native, "fcm-token", "", "");
        using var payload = JsonDocument.Parse(FcmPushSender.Payload(target, new PushMessage("Novità", "Gara sabato", "/gyms/x")));
        var apns = payload.RootElement.GetProperty("message").GetProperty("apns");

        apns.TryGetProperty("headers", out _).Should().BeFalse();
        apns.GetProperty("payload").GetProperty("aps").TryGetProperty("thread-id", out _).Should().BeFalse();
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
