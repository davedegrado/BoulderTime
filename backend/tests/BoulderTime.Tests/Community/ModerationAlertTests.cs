using System.Collections.Concurrent;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Community;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Community;

/// <summary>The team is emailed when a comment, a video or a person is reported; problems with a boulder stay with the gym (ADR-050).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ModerationAlertTests : IAsyncLifetime
{
    private sealed class FakeAlerts : IModerationAlerts
    {
        public ConcurrentQueue<ModerationAlert> Sent { get; } = new();
        public void Send(ModerationAlert alert) => Sent.Enqueue(alert);
    }

    private readonly FakeAlerts _alerts = new();
    private readonly ApiFactory _f;

    public ModerationAlertTests(PostgresFixture postgres) =>
        _f = new ApiFactory(postgres) { OverrideServices = s => s.AddSingleton<IModerationAlerts>(_alerts) };

    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Reporting_a_comment_or_a_person_emails_the_team_and_a_boulder_problem_does_not()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var comment = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "Compra scarpe da me" })).ReadAsync<CommentDto>())!;

        (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "SPAM" })).EnsureSuccessStatusCode();
        _alerts.Sent.Should().ContainSingle();
        _alerts.Sent.Single().Subject.Should().Contain("commento");
        _alerts.Sent.Single().Body.Should().Contain("Compra scarpe da me");
        _alerts.Sent.Single().Path.Should().Be("/admin/reports");

        (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "BOULDER", entityId = b.Id, reason = "SPAM" })).EnsureSuccessStatusCode();
        _alerts.Sent.Should().HaveCount(1);

        (await reporter.Client.PostAsJsonAsync($"/api/users/{author.Id}/reports", new { reason = "HARASSMENT", description = "Insulti" })).EnsureSuccessStatusCode();
        _alerts.Sent.Should().HaveCount(2);
        _alerts.Sent.Last().Body.Should().Contain("molestie");
    }
}
