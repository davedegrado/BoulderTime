using System.Security.Cryptography;
using System.Text;
using BoulderTime.Infrastructure.Push;

namespace BoulderTime.Tests.Push;

/// <summary>
/// The encryption is ours rather than a library's, so it is verified the only way that counts: a message is encrypted
/// for a device, then decrypted the way a browser would, using only the device's own keys.
/// </summary>
public sealed class WebPushEncryptionTests
{
    [Fact]
    public void A_device_can_decrypt_what_we_send_it_and_nobody_else_can()
    {
        // The "device": a key pair and an auth secret, exactly what a browser gives us when it subscribes.
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var deviceParameters = device.PublicKey.ExportParameters();
        var devicePublic = new byte[] { 0x04 }.Concat(deviceParameters.Q.X!).Concat(deviceParameters.Q.Y!).ToArray();
        var authSecret = RandomNumberGenerator.GetBytes(16);

        var payload = Encoding.UTF8.GetBytes("""{"title":"Nuovi blocchi","body":"Grotta ritracciata"}""");
        var encrypted = WebPushSender.Encrypt(payload, devicePublic, authSecret);

        Decrypt(encrypted, device, devicePublic, authSecret).Should().Equal(payload);

        // A different device, with different keys, gets nothing out of the same message.
        using var other = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var otherParameters = other.PublicKey.ExportParameters();
        var otherPublic = new byte[] { 0x04 }.Concat(otherParameters.Q.X!).Concat(otherParameters.Q.Y!).ToArray();
        var attempt = () => Decrypt(encrypted, other, otherPublic, authSecret);
        attempt.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Two_messages_with_the_same_text_never_look_alike()
    {
        using var device = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = device.PublicKey.ExportParameters();
        var devicePublic = new byte[] { 0x04 }.Concat(p.Q.X!).Concat(p.Q.Y!).ToArray();
        var auth = RandomNumberGenerator.GetBytes(16);
        var payload = Encoding.UTF8.GetBytes("stesso testo");

        // Fresh salt and key pair per message, so the push service can't tell repeats apart.
        WebPushSender.Encrypt(payload, devicePublic, auth).Should().NotEqual(WebPushSender.Encrypt(payload, devicePublic, auth));
    }

    /// <summary>The browser's side of RFC 8291, written out so the test proves interoperability rather than symmetry.</summary>
    private static byte[] Decrypt(byte[] message, ECDiffieHellman device, byte[] devicePublic, byte[] authSecret)
    {
        var salt = message[..16];
        var keyLength = message[20];
        var senderPublic = message[21..(21 + keyLength)];
        var ciphertext = message[(21 + keyLength)..];

        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..65] },
        });
        var shared = device.DeriveRawSecretAgreement(sender.PublicKey);

        var keyInfo = Encoding.ASCII.GetBytes("WebPush: info\0").Concat(devicePublic).Concat(senderPublic).ToArray();
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var plaintext = new byte[ciphertext.Length - 16];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, ciphertext.AsSpan(0, plaintext.Length), ciphertext.AsSpan(plaintext.Length), plaintext);
        return plaintext[..^1]; // drop the 0x02 delimiter
    }
}
