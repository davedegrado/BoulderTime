using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Users;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Users;

/// <summary>Accepting the terms: recorded with the version, asked again when that version changes.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LegalAcceptanceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<CurrentUserDto> MeAsync(TestUser user) =>
        (await (await user.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;

    [Fact]
    public async Task A_new_account_is_asked_to_accept_and_the_version_is_recorded()
    {
        var climber = await _f.UserAsync();

        var before = await MeAsync(climber);
        before.LegalAcceptanceNeeded.Should().BeTrue();
        before.AcceptedLegalVersion.Should().BeNull();

        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion }))
            .EnsureSuccessStatusCode();

        var after = await MeAsync(climber);
        after.LegalAcceptanceNeeded.Should().BeFalse();
        after.AcceptedLegalVersion.Should().Be(LegalDocuments.CurrentVersion);
        (await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).Select(u => u.AcceptedLegalAt).FirstAsync())).Should().NotBeNull();
    }

    [Fact]
    public async Task Accepting_some_other_version_is_refused()
    {
        var climber = await _f.UserAsync();

        // An app showing old text must not be able to record agreement to the current documents.
        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = "2020-01-01" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await MeAsync(climber)).LegalAcceptanceNeeded.Should().BeTrue();
    }

    [Fact]
    public async Task When_the_documents_change_everyone_is_asked_again()
    {
        var climber = await _f.UserAsync();
        await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion });

        // Simulates a new version being published: what was accepted no longer matches what is current.
        await _f.Db(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == climber.Id);
            user.AcceptLegal("2026-01-01", DateTimeOffset.UtcNow);
            return await db.SaveChangesAsync();
        });

        var me = await MeAsync(climber);
        me.LegalAcceptanceNeeded.Should().BeTrue();
        me.AcceptedLegalVersion.Should().Be("2026-01-01"); // the old agreement is kept as the record of what they saw
    }

    [Fact]
    public async Task Nobody_can_accept_on_behalf_of_someone_else()
    {
        (await _f.CreateClient().PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
