using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Users;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Users;

/// <summary>
/// Accepting the terms: recorded with the version, asked again when that version changes, and together with the
/// declaration of being at least 14 (ADR-041) — on the sign-up form, or on the screen that asks afterwards.
/// </summary>
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

        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion, confirmsMinimumAge = true }))
            .EnsureSuccessStatusCode();

        var after = await MeAsync(climber);
        after.MinimumAgeConfirmed.Should().BeTrue();
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
        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion, confirmsMinimumAge = true }))
            .EnsureSuccessStatusCode();

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

        // The age was declared once and isn't asked again: accepting the new version is enough.
        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion }))
            .EnsureSuccessStatusCode();
        (await MeAsync(climber)).LegalAcceptanceNeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Nobody_can_accept_on_behalf_of_someone_else()
    {
        (await _f.CreateClient().PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Accepting_without_declaring_the_minimum_age_is_refused()
    {
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await climber.Client.PostAsJsonAsync("/api/users/me/legal-acceptance", new { version = LegalDocuments.CurrentVersion, confirmsMinimumAge = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var me = await MeAsync(climber);
        me.LegalAcceptanceNeeded.Should().BeTrue();
        me.MinimumAgeConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Ticking_both_boxes_on_the_sign_up_form_is_the_acceptance()
    {
        var id = Guid.NewGuid();
        var client = _f.ClientFor(id, "nuova@example.com", new
        {
            display_name = "Nuova",
            legal_version = LegalDocuments.CurrentVersion,
            minimum_age_confirmed = true,
        });

        var me = (await (await client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;

        // No second screen asking the same thing: the form already did.
        me.LegalAcceptanceNeeded.Should().BeFalse();
        me.MinimumAgeConfirmed.Should().BeTrue();
        me.AcceptedLegalVersion.Should().Be(LegalDocuments.CurrentVersion);
        (await _f.Db(db => db.Users.Where(u => u.Id == id).Select(u => u.MinimumAgeConfirmedAt).FirstAsync())).Should().NotBeNull();
    }

    [Fact]
    public async Task A_form_answer_for_older_terms_still_asks_for_the_current_ones()
    {
        var id = Guid.NewGuid();
        // An app installed before the terms changed sends the version it showed.
        var client = _f.ClientFor(id, "vecchia@example.com", new { legal_version = "2020-01-01", minimum_age_confirmed = true });

        var me = (await (await client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;

        me.LegalAcceptanceNeeded.Should().BeTrue();
        me.AcceptedLegalVersion.Should().BeNull();
        me.MinimumAgeConfirmed.Should().BeTrue(); // the age was declared all the same, and isn't asked again
    }
}
