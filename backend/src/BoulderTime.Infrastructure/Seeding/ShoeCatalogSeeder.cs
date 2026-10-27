using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Shoes;
using BoulderTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Infrastructure.Seeding;

/// <summary>
/// Fills the climbing-shoe catalogue from <c>Seeding/climbing-shoes.json</c> (ADR-070). Idempotent: a model already
/// there (same brand and name, whatever the case, accents or spaces) is never added again, and nothing is ever deleted
/// or taken from anyone. By default it only adds; with <c>updateMetadata</c> it also brings status, picture and product
/// page of the models already there in line with the file — never their brand or name, which are what people chose.
/// </summary>
public sealed class ShoeCatalogSeeder(AppDbContext db, IClock clock)
{
    public sealed record Entry(string Brand, string Name, ShoeStatus Status, string? ImageUrl, string? ProductUrl, string? SourceUrl);
    public sealed record Result(int Added, int Updated, int Unchanged);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static IReadOnlyList<Entry> FromFile()
    {
        using var stream = typeof(ShoeCatalogSeeder).Assembly.GetManifestResourceStream("BoulderTime.Infrastructure.Seeding.climbing-shoes.json")
            ?? throw new InvalidOperationException("The climbing-shoe catalogue is missing from the build.");
        return JsonSerializer.Deserialize<List<Entry>>(stream, Json) ?? [];
    }

    public async Task<Result> SeedAsync(IReadOnlyList<Entry>? entries = null, bool updateMetadata = false, CancellationToken ct = default)
    {
        entries ??= FromFile();
        var now = clock.UtcNow;
        var existing = await db.ClimbingShoes.ToDictionaryAsync(s => s.Key, ct);
        int added = 0, updated = 0, unchanged = 0;
        foreach (var e in entries)
        {
            var key = ClimbingShoe.KeyOf(e.Brand, e.Name);
            if (existing.TryGetValue(key, out var shoe))
            {
                if (updateMetadata && shoe.Update(e.Status, Url(e.ImageUrl), Url(e.ProductUrl), Url(e.SourceUrl), now)) updated++;
                else unchanged++;
                continue;
            }
            shoe = ClimbingShoe.Create(e.Brand, e.Name, e.Status, Url(e.ImageUrl), Url(e.ProductUrl), Url(e.SourceUrl), now);
            db.ClimbingShoes.Add(shoe);
            existing[key] = shoe; // the same model twice in the file is added once
            added++;
        }
        await db.SaveChangesAsync(ct);
        return new Result(added, updated, unchanged);
    }

    /// <summary>Only https links are kept: anything else in the file is left out rather than shown.</summary>
    private static string? Url(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.ToString() : null;
}
