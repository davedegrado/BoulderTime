using System.Security.Cryptography;
using System.Text;

namespace BoulderTime.Domain.Users;

/// <summary>
/// The fingerprint of the address of a suspended account that was then deleted. Without it, deleting a suspended
/// account and signing up again with the same address (or the same Google account) would be a way round the
/// suspension. Only a SHA-256 hash is kept, never the address: it answers "is this the same address?" and nothing
/// else. It lasts <see cref="RetentionPeriod"/>, then goes (ADR-038).
/// </summary>
public class SuspendedEmail
{
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(2 * 365);

    /// <summary>Lowercase hex SHA-256 of the normalised address, with a fixed prefix so it matches no other hash of it.</summary>
    public string EmailHash { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    private SuspendedEmail() { } // EF Core

    public static SuspendedEmail Record(string email, DateTimeOffset now) =>
        new() { EmailHash = Hash(email), RecordedAt = now };

    /// <summary>Recorded again (a second suspended account with the same address): the two years start over.</summary>
    public void Renew(DateTimeOffset now) => RecordedAt = now;

    public bool IsExpired(DateTimeOffset now) => RecordedAt <= now - RetentionPeriod;

    public static string Hash(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("bouldertime/suspended-email/" + User.NormalizeEmail(email))))
            .ToLowerInvariant();
}
