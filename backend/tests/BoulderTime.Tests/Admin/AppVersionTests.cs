using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Apps;
using BoulderTime.Domain.Apps;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Admin;

/// <summary>
/// The store apps' minimum and recommended versions: public to read (the app asks before anyone signs in), changed only
/// by platform administrators, starting at the versions on the stores so nobody is asked to update (ADR-059).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AppVersionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<AppVersionDto> ReadAsync() => (await (await _f.CreateClient().GetAsync("/api/app-version")).ReadAsync<AppVersionDto>())!;

    [Fact]
    public async Task Anyone_can_read_it_without_signing_in_and_it_starts_at_the_versions_on_the_stores()
    {
        var res = await _f.CreateClient().GetAsync("/api/app-version");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.CacheControl!.Public.Should().BeTrue();
        res.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));

        var v = (await res.ReadAsync<AppVersionDto>())!;
        v.Ios.Should().Be(new AppPlatformVersionDto("1.0.0", null, null));
        v.Android.Should().Be(new AppPlatformVersionDto("1.0.0", null, "https://play.google.com/store/apps/details?id=com.bouldertime.app"));
    }

    [Fact]
    public async Task A_platform_admin_raises_one_platform_and_the_change_is_read_at_once()
    {
        await ReadAsync(); // fills the cache: the change must not wait for it to expire
        var admin = await _f.UserAsync(platformAdmin: true);
        var res = await admin.Client.PutAsJsonAsync("/api/admin/app-versions/ios",
            new { minimum = "1.1.0", recommended = "1.10.0", storeUrl = "https://apps.apple.com/app/id1234567890" });
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var v = await ReadAsync();
        v.Ios.Should().Be(new AppPlatformVersionDto("1.1.0", "1.10.0", "https://apps.apple.com/app/id1234567890"));
        v.Android.Minimum.Should().Be("1.0.0"); // the other app is left as it was
    }

    [Fact]
    public async Task Nobody_else_can_change_it()
    {
        var body = new { minimum = "9.0.0" };
        (await (await _f.UserAsync()).Client.PutAsJsonAsync("/api/admin/app-versions/android", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.CreateClient().PutAsJsonAsync("/api/admin/app-versions/android", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ReadAsync()).Android.Minimum.Should().Be("1.0.0");
    }

    [Theory]
    [InlineData("1.2.x", null, null, "minimum")]
    [InlineData("", null, null, "minimum")]
    [InlineData("1.2.0", "1.1.9", null, "recommended")]
    [InlineData("1.2.0", "1.2.0.1", null, "recommended")]
    [InlineData("1.2.0", null, "https://example.com/bouldertime", "storeUrl")]
    [InlineData("1.2.0", null, "http://apps.apple.com/app/id1", "storeUrl")]
    public async Task Versions_and_store_pages_that_make_no_sense_are_refused(string minimum, string? recommended, string? storeUrl, string field)
    {
        var admin = await _f.UserAsync(platformAdmin: true);
        var res = await admin.Client.PutAsJsonAsync("/api/admin/app-versions/android", new { minimum, recommended, storeUrl });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain(field);
        (await ReadAsync()).Android.Minimum.Should().Be("1.0.0");
    }

    [Theory]
    [InlineData("1.10.0", "1.9.0", 1)]
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("2", "1.99.99", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    public void Versions_compare_as_numbers(string a, string b, int expected) =>
        AppVersionPolicy.Compare(a, b).Should().Be(expected);
}
