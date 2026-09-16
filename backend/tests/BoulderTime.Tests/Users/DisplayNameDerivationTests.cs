using BoulderTime.Application.Users;

namespace BoulderTime.Tests.Users;

public sealed class DisplayNameDerivationTests
{
    [Theory]
    [InlineData("Marco", "m@example.com", "Marco")]
    [InlineData(null, "anna.bianchi@example.com", "anna bianchi")]
    [InlineData("  ", "x@example.com", "Climber")]
    [InlineData(null, "j_doe@example.com", "j doe")]
    public void Derives_a_sensible_name(string? metadataName, string email, string expected) =>
        UserService.DeriveDisplayName(new VerifiedIdentity(Guid.NewGuid(), email, metadataName, null)).Should().Be(expected);

    [Fact]
    public void Truncates_long_provider_names() =>
        UserService.DeriveDisplayName(new VerifiedIdentity(Guid.NewGuid(), "a@b.co", new string('x', 80), null)).Length.Should().Be(40);
}
