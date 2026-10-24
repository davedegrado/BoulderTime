using BoulderTime.Domain.Notifications;
using BoulderTime.Infrastructure.Push;

namespace BoulderTime.Tests.Push;

/// <summary>A notification meant for newer apps reaches browsers and store apps of that version or later, never 1.0.</summary>
public sealed class PushVersionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData(null, false)]
    [InlineData("1.0.0", false)]
    [InlineData("1.1", true)]
    [InlineData("1.1.0", true)]
    [InlineData("1.10.0", true)]
    public void A_store_app_gets_it_from_the_minimum_version_on(string? appVersion, bool reached) =>
        PushDispatcher.ReachesVersion(PushSubscription.CreateNative(Guid.NewGuid(), "t", Now, appVersion), "1.1.0").Should().Be(reached);

    [Fact]
    public void A_browser_always_gets_it_and_without_a_minimum_everyone_does()
    {
        PushDispatcher.ReachesVersion(PushSubscription.CreateWeb(Guid.NewGuid(), "https://push.example/a", "k", "a", Now), "1.1.0").Should().BeTrue();
        PushDispatcher.ReachesVersion(PushSubscription.CreateNative(Guid.NewGuid(), "t", Now), null).Should().BeTrue();
    }
}
