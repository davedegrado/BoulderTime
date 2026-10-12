using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Community;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

/// <summary>The word filter on comments and captions: what it catches, and the ordinary words it must leave alone.</summary>
public sealed class ContentFilterTests
{
    [Theory]
    [InlineData("sei uno stronzo")]
    [InlineData("Che STRONZATA di blocco")]
    [InlineData("stronzooo")]
    [InlineData("s t r o n z o")]
    [InlineData("s.t.r.o.n.z.o")]
    [InlineData("str0nz0")]
    [InlineData("vaffanculo")]
    [InlineData("figlio di puttana")]
    [InlineData("you're a b1tch")]
    [InlineData("fuck you")]
    [InlineData("Fuck   off!")]
    [InlineData("just kys")]
    [InlineData("kill yourself")]
    [InlineData("frocio")]
    [InlineData("Ritardato? no: retarded")]
    [InlineData("porco dio che crimp")]
    [InlineData("PORCODIO")]
    [InlineData("dio cane, sono caduto all'ultima presa")]
    [InlineData("d1o c4ne")]
    [InlineData("porca madonna")]
    [InlineData("Dioboia che tallone")]
    public void Catches_slurs_insults_and_their_disguises(string text) =>
        ContentFilter.IsObjectionable(text).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("Bel blocco, il passaggio in placca è delicato")]
    [InlineData("Night session, crux on the left")]
    [InlineData("A small niggle in my finger, took it easy")]
    [InlineData("Dopo, un negroni al bar")]
    [InlineData("Parti dallo zoccolo, la crepa è a destra")]
    [InlineData("Follow the dyke to the top")]
    [InlineData("Blocco bastardo ma bellissimo")]
    [InlineData("This crux is shit hard")]
    [InlineData("7a+ con un lancio, 6c per me")]
    [InlineData("Scunthorpe climbers say hi")]
    [InlineData("Più facile di quanto sembri: spingi sui piedi")]
    [InlineData("Grazie a Dio ci sono le prese verdi")]
    [InlineData("La via della Madonna della Ceriola")]
    [InlineData("Il cane della palestra mi guarda mentre cado")]
    public void Leaves_ordinary_climbing_talk_alone(string text) =>
        ContentFilter.IsObjectionable(text).Should().BeFalse();
}

[Collection(DatabaseCollection.Name)]
public sealed class ContentFilterApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task A_comment_or_a_caption_with_a_slur_is_turned_away_with_a_reason_in_the_readers_language()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var climber = await _f.UserAsync(language: "it");

        var refused = await climber.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "sei un coglione" });
        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Il testo contiene parole che qui non sono ammesse.");
        (await climber.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "Bel blocco" })).EnsureSuccessStatusCode();

        var comment = (await (await climber.Client.GetAsync($"/api/boulders/{b.Id}/comments")).ReadAsync<Application.Common.PagedResult<CommentDto>>())!.Items[0];
        (await climber.Client.PatchAsJsonAsync($"/api/comments/{comment.Id}", new { content = "pezzo di merda" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
