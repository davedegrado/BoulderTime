using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Shoes;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Shoes;

/// <param name="ImageUrl">The maker's picture, linked from its site; null when there is none.</param>
/// <param name="ProductUrl">The maker's page for the model, to credit and link the picture.</param>
public sealed record ClimbingShoeDto(Guid Id, string Brand, string Name, ShoeStatus Status, string? ImageUrl, string? ProductUrl);
public sealed record UserShoeDto(Guid Id, string Brand, string Name, ShoeStatus Status, string? ImageUrl, string? ProductUrl, bool IsPrimary);
public sealed record UserShoesDto(IReadOnlyList<UserShoeDto> Shoes);
public sealed record ShoeCatalogPageDto(IReadOnlyList<ClimbingShoeDto> Items, int Total);

/// <summary>
/// Climbing shoes (ADR-070): the shared catalogue, read by anyone signed in and never changed by them, and the pairs
/// each person chose from it. A person's shoes go with their profile's content, so a private profile shows them only to
/// its owner and friends; only the owner changes them. The database keeps a model once per person and one main pair.
/// </summary>
public sealed class ShoeService(IAppDbContext db, ICurrentUser currentUser, ProfileAccess access, IClock clock)
{
    public const int DefaultLimit = 30, MaxLimit = 50;
    /// <summary>The brands in the order the catalogue shows them.</summary>
    public static readonly string[] BrandOrder = ["La Sportiva", "SCARPA", "OCÚN", "SIMOND"];

    /// <summary>
    /// A page of the catalogue: every word of the search found in the brand or the name, whatever its case, accents or
    /// extra spaces; optionally one brand. Models still made first, then by brand and name.
    /// </summary>
    public async Task<ShoeCatalogPageDto> SearchAsync(string? query, string? brand, int? limit, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var q = db.ClimbingShoes.AsNoTracking().AsQueryable();
        foreach (var word in ClimbingShoe.Fold(query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6))
        {
            var pattern = Input.LikeContains(word);
            q = q.Where(s => EF.Functions.Like(s.SearchText, pattern, @"\"));
        }
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var folded = ClimbingShoe.Fold(brand);
            var known = await db.ClimbingShoes.AsNoTracking().Select(s => s.Brand).Distinct().ToListAsync(ct);
            var match = known.FirstOrDefault(b => ClimbingShoe.Fold(b) == folded);
            if (match is null) return new ShoeCatalogPageDto([], 0);
            q = q.Where(s => s.Brand == match);
        }
        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderBy(s => s.Status == ShoeStatus.Discontinued)
            .ThenBy(s => s.Brand == BrandOrder[0] ? 0 : s.Brand == BrandOrder[1] ? 1 : s.Brand == BrandOrder[2] ? 2 : s.Brand == BrandOrder[3] ? 3 : 4)
            .ThenBy(s => s.Brand).ThenBy(s => s.SearchText)
            .Take(take).ToListAsync(ct);
        return new ShoeCatalogPageDto(rows.Select(ToDto).ToList(), total);
    }

    /// <summary>A person's shoes, the main pair first; nothing for a profile whose content the viewer may not see.</summary>
    public async Task<UserShoesDto> ForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        if (!await access.CanSeeContentAsync(user, ct)) return new UserShoesDto([]);
        return await ReadAsync(userId, ct);
    }

    /// <summary>Adds a model to the caller's shoes; adding one they already have changes nothing.</summary>
    public async Task<UserShoesDto> AddAsync(Guid shoeId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        if (!await db.ClimbingShoes.AnyAsync(s => s.Id == shoeId, ct)) throw new NotFoundException("Climbing shoe", shoeId);
        if (!await db.UserClimbingShoes.AnyAsync(x => x.UserId == userId && x.ShoeId == shoeId, ct))
        {
            if (await db.UserClimbingShoes.CountAsync(x => x.UserId == userId, ct) >= UserClimbingShoe.MaxPerPerson)
                throw new ValidationException("shoeId", $"You can keep up to {UserClimbingShoe.MaxPerPerson} pairs.");
            db.UserClimbingShoes.Add(UserClimbingShoe.Add(userId, shoeId, clock.UtcNow));
            try { await db.SaveChangesAsync(ct); }
            catch (UniqueConstraintViolationException) { /* added at the same moment by another request: it is there */ }
        }
        return await ReadAsync(userId, ct);
    }

    /// <summary>Takes a model away from the caller's shoes, never from the catalogue. A main pair taken away leaves none.</summary>
    public async Task<UserShoesDto> RemoveAsync(Guid shoeId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var mine = await db.UserClimbingShoes.FirstOrDefaultAsync(x => x.UserId == userId && x.ShoeId == shoeId, ct);
        if (mine is not null)
        {
            db.UserClimbingShoes.Remove(mine);
            await db.SaveChangesAsync(ct);
        }
        return await ReadAsync(userId, ct);
    }

    /// <summary>Makes one of the caller's pairs the main one (the previous one no longer is), or no longer the main one.</summary>
    public async Task<UserShoesDto> SetPrimaryAsync(Guid shoeId, bool on, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var mine = await db.UserClimbingShoes.Where(x => x.UserId == userId).ToListAsync(ct);
        var target = mine.FirstOrDefault(x => x.ShoeId == shoeId) ?? throw new NotFoundException("Climbing shoe", shoeId);
        if (target.IsPrimary != on)
        {
            // Two steps in one transaction, so the database's "one main pair" never sees two at once.
            await db.InTransactionAsync(async c =>
            {
                foreach (var other in mine.Where(x => x.IsPrimary)) other.SetPrimary(false);
                await db.SaveChangesAsync(c);
                if (on)
                {
                    target.SetPrimary(true);
                    await db.SaveChangesAsync(c);
                }
            }, ct);
        }
        return await ReadAsync(userId, ct);
    }

    private async Task<UserShoesDto> ReadAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.UserClimbingShoes.AsNoTracking().Where(x => x.UserId == userId)
            .Join(db.ClimbingShoes.AsNoTracking(), x => x.ShoeId, s => s.Id, (x, s) => new { x.IsPrimary, x.CreatedAt, Shoe = s })
            .ToListAsync(ct);
        return new UserShoesDto(rows.OrderByDescending(r => r.IsPrimary).ThenBy(r => r.CreatedAt)
            .Select(r => new UserShoeDto(r.Shoe.Id, r.Shoe.Brand, r.Shoe.Name, r.Shoe.Status, r.Shoe.ImageUrl, r.Shoe.ProductUrl, r.IsPrimary)).ToList());
    }

    private static ClimbingShoeDto ToDto(ClimbingShoe s) => new(s.Id, s.Brand, s.Name, s.Status, s.ImageUrl, s.ProductUrl);
}
