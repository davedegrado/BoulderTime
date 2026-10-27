using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Shoes;
using BoulderTime.Domain.Shoes;
using BoulderTime.Infrastructure.Seeding;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Shoes;

/// <summary>
/// Climbing shoes (ADR-070): a shared catalogue people read and never change, and the pairs each person chooses from
/// it — each once, at most one main pair, shown with the profile's content.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ShoeTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static readonly ShoeCatalogSeeder.Entry[] Sample =
    [
        new("La Sportiva", "Solution", ShoeStatus.Current, null, "https://www.lasportiva.com/solution", null),
        new("La Sportiva", "Solution Comp", ShoeStatus.Current, null, null, null),
        new("La Sportiva", "Skwama", ShoeStatus.Current, "https://www.lasportiva.com/skwama.jpg", null, null),
        new("La Sportiva", "Mythos", ShoeStatus.Discontinued, null, null, null),
        new("SCARPA", "Drago", ShoeStatus.Current, null, null, null),
        new("SCARPA", "Instinct VS", ShoeStatus.Current, null, null, null),
        new("OCÚN", "Ozone", ShoeStatus.Current, null, null, null),
        new("SIMOND", "Vertika", ShoeStatus.Current, "http://not-secure.example/vertika.jpg", null, null),
    ];

    private async Task<ShoeCatalogSeeder.Result> Seed(IReadOnlyList<ShoeCatalogSeeder.Entry>? entries = null, bool update = false)
    {
        await using var scope = _f.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ShoeCatalogSeeder>().SeedAsync(entries ?? Sample, update);
    }

    private static async Task<ShoeCatalogPageDto> Search(TestUser u, string query) =>
        (await (await u.Client.GetAsync($"/api/climbing-shoes?{query}")).ReadAsync<ShoeCatalogPageDto>())!;

    private static async Task<Guid> Id(TestUser u, string name) =>
        (await Search(u, $"q={Uri.EscapeDataString(name)}")).Items.Single(s => s.Name == name).Id;

    private static async Task<UserShoesDto> Shoes(TestUser reader, Guid of) =>
        (await (await reader.Client.GetAsync($"/api/users/{of}/shoes")).ReadAsync<UserShoesDto>())!;

    private static async Task<UserShoesDto> Do(TestUser u, HttpMethod method, string path)
    {
        var response = await u.Client.SendAsync(new HttpRequestMessage(method, path));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsync<UserShoesDto>())!;
    }

    [Fact]
    public async Task The_catalogue_is_searched_by_words_of_brand_and_model_whatever_the_case_accents_or_spaces()
    {
        await Seed();
        var me = await _f.UserAsync();

        (await Search(me, "q=" + Uri.EscapeDataString("  SOLUTION  "))).Items.Select(s => s.Name).Should().Equal("Solution", "Solution Comp");
        (await Search(me, "q=" + Uri.EscapeDataString("comp solution"))).Items.Select(s => s.Name).Should().Equal("Solution Comp");
        (await Search(me, "q=ocun")).Items.Single().Brand.Should().Be("OCÚN");
        (await Search(me, "q=scarpa")).Items.Select(s => s.Name).Should().Equal("Drago", "Instinct VS");
        (await Search(me, "q=drago%202")).Items.Should().BeEmpty();
        (await Search(me, "brand=scarpa")).Items.Should().OnlyContain(s => s.Brand == "SCARPA");
        (await Search(me, "brand=Nobody")).Total.Should().Be(0);
        // La Sportiva first; models no longer made after those still made.
        var all = await Search(me, "");
        all.Total.Should().Be(Sample.Length);
        all.Items.First().Brand.Should().Be("La Sportiva");
        all.Items.Last().Name.Should().Be("Mythos");
        all.Items.Single(s => s.Name == "Mythos").Status.Should().Be(ShoeStatus.Discontinued);
        // A page, never the whole catalogue.
        var page = await Search(me, "limit=3");
        page.Items.Should().HaveCount(3);
        page.Total.Should().Be(Sample.Length);
        // Pictures are https links or nothing.
        all.Items.Single(s => s.Name == "Skwama").ImageUrl.Should().Be("https://www.lasportiva.com/skwama.jpg");
        all.Items.Single(s => s.Name == "Vertika").ImageUrl.Should().BeNull();
        all.Items.Single(s => s.Name == "Drago").ImageUrl.Should().BeNull();

        (await _f.CreateClient().GetAsync("/api/climbing-shoes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task People_add_and_remove_pairs_each_once_and_removing_never_touches_the_catalogue()
    {
        await Seed();
        var me = await _f.UserAsync();
        var drago = await Id(me, "Drago");
        var skwama = await Id(me, "Skwama");

        (await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{drago}")).Shoes.Should().ContainSingle();
        (await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{drago}")).Shoes.Should().ContainSingle("the same model is never added twice");
        var mine = await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{skwama}");
        mine.Shoes.Select(s => s.Name).Should().Equal("Drago", "Skwama");
        mine.Shoes.Should().OnlyContain(s => !s.IsPrimary, "the first pair added is not made the main one");

        (await Do(me, HttpMethod.Delete, $"/api/users/me/shoes/{drago}")).Shoes.Select(s => s.Name).Should().Equal("Skwama");
        (await Search(me, "q=drago")).Items.Should().ContainSingle("the model stays in the catalogue");
        (await Do(me, HttpMethod.Delete, $"/api/users/me/shoes/{drago}")).Shoes.Should().ContainSingle("removing what isn't there changes nothing");

        (await me.Client.PutAsync($"/api/users/me/shoes/{Guid.NewGuid()}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await _f.UserAsync()).Client.GetAsync($"/api/users/{me.Id}/shoes")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task One_main_pair_at_most_moved_by_choice_and_never_given_to_another_pair_by_itself()
    {
        await Seed();
        var me = await _f.UserAsync();
        var drago = await Id(me, "Drago");
        var skwama = await Id(me, "Skwama");
        var ozone = await Id(me, "Ozone");
        foreach (var id in new[] { drago, skwama, ozone }) await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{id}");

        var shoes = await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{skwama}/primary");
        shoes.Shoes.First().Name.Should().Be("Skwama", "the main pair comes first");
        shoes.Shoes.Count(s => s.IsPrimary).Should().Be(1);

        shoes = await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{ozone}/primary");
        shoes.Shoes.Single(s => s.IsPrimary).Name.Should().Be("Ozone", "a new main pair takes the place of the previous one");

        shoes = await Do(me, HttpMethod.Delete, $"/api/users/me/shoes/{ozone}");
        shoes.Shoes.Should().OnlyContain(s => !s.IsPrimary, "taking the main pair away leaves none");

        await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{drago}/primary");
        (await Do(me, HttpMethod.Delete, $"/api/users/me/shoes/{drago}/primary")).Shoes.Should().OnlyContain(s => !s.IsPrimary);
        // Only a pair one has can be the main one.
        (await me.Client.PutAsync($"/api/users/me/shoes/{ozone}/primary", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Kept: read again as after a new sign-in.
        (await Shoes(me, me.Id)).Shoes.Select(s => s.Name).Should().BeEquivalentTo("Drago", "Skwama");
    }

    [Fact]
    public async Task The_database_itself_refuses_two_main_pairs_and_the_same_model_twice()
    {
        await Seed();
        var me = await _f.UserAsync();
        var a = await Id(me, "Drago");
        var b = await Id(me, "Skwama");
        var act = () => _f.WithDbAsync(async db =>
        {
            var x = UserClimbingShoe.Add(me.Id, a, DateTimeOffset.UtcNow); x.SetPrimary(true);
            var y = UserClimbingShoe.Add(me.Id, b, DateTimeOffset.UtcNow); y.SetPrimary(true);
            db.UserClimbingShoes.AddRange(x, y);
            return await db.SaveChangesAsync();
        });
        await act.Should().ThrowAsync<UniqueConstraintViolationException>();
    }

    [Fact]
    public async Task Shoes_go_with_the_profile_content_and_only_their_owner_changes_them()
    {
        await Seed();
        var owner = await _f.UserAsync();
        var other = await _f.UserAsync();
        var drago = await Id(owner, "Drago");
        await Do(owner, HttpMethod.Put, $"/api/users/me/shoes/{drago}");

        (await Shoes(other, owner.Id)).Shoes.Should().ContainSingle("a public profile shows them");
        (await owner.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();
        (await Shoes(other, owner.Id)).Shoes.Should().BeEmpty("a private profile keeps them to its owner and friends");
        (await Shoes(owner, owner.Id)).Shoes.Should().ContainSingle();

        // There is no way to write someone else's: every change is the caller's own.
        (await other.Client.PutAsync($"/api/users/{owner.Id}/shoes/{drago}", null)).StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await other.Client.DeleteAsync($"/api/users/me/shoes/{drago}")).EnsureSuccessStatusCode();
        (await Shoes(owner, owner.Id)).Shoes.Should().ContainSingle("removing from one's own never touches anyone else's");
    }

    [Fact]
    public async Task The_seed_can_run_again_adds_each_model_once_never_deletes_and_updates_only_when_asked()
    {
        var first = await Seed();
        first.Added.Should().Be(Sample.Length);
        var me = await _f.UserAsync();
        var drago = await Id(me, "Drago");
        await Do(me, HttpMethod.Put, $"/api/users/me/shoes/{drago}");

        // Again, with the same models written differently and one more.
        var again = await Seed([
            new("la sportiva", "  SOLUTION ", ShoeStatus.Current, null, null, null),
            new("Ocun", "Ozone", ShoeStatus.Current, null, null, null),
            new("SCARPA", "Drago", ShoeStatus.Discontinued, "https://www.scarpa.com/drago.jpg", null, null),
            new("SCARPA", "Chimera", ShoeStatus.Current, null, null, null),
            new("SCARPA", "Chimera", ShoeStatus.Current, null, null, null),
        ]);
        again.Added.Should().Be(1, "only Chimera is new, and only once");
        var count = await _f.WithDbAsync(db => db.ClimbingShoes.CountAsync());
        count.Should().Be(Sample.Length + 1, "nothing is deleted");
        (await Search(me, "q=drago")).Items.Single().ImageUrl.Should().BeNull("without asking, models already there are left as they are");

        var update = await Seed([new("SCARPA", "Drago", ShoeStatus.Discontinued, "https://www.scarpa.com/drago.jpg", null, null)], update: true);
        update.Updated.Should().Be(1);
        var dragoNow = (await Search(me, "q=drago")).Items.Single();
        dragoNow.ImageUrl.Should().Be("https://www.scarpa.com/drago.jpg");
        dragoNow.Status.Should().Be(ShoeStatus.Discontinued);
        (await Shoes(me, me.Id)).Shoes.Single().Name.Should().Be("Drago", "a model no longer made stays chosen");
    }

    [Fact]
    public void The_catalogue_file_has_each_model_once_with_https_links_only()
    {
        var entries = ShoeCatalogSeeder.FromFile();
        entries.GroupBy(e => ClimbingShoe.KeyOf(e.Brand, e.Name)).Should().OnlyContain(g => g.Count() == 1);
        entries.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Brand) && !string.IsNullOrWhiteSpace(e.Name));
        foreach (var url in entries.SelectMany(e => new[] { e.ImageUrl, e.ProductUrl, e.SourceUrl }).Where(u => u != null))
            url.Should().StartWith("https://");
    }
}
