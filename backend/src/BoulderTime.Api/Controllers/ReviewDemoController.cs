using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Gyms;
using BoulderTime.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

public sealed record ReviewDemoRequest(string? StaffEmail);

/// <summary>
/// Creates the hidden demo gyms the stores' reviewers use (ADR-051). The one piece of seeding reachable over HTTP:
/// platform admins only, limited to draft gyms nobody else can see, and safe to press twice.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class ReviewDemoController(GymAccess access, ICurrentUser currentUser, DemoSeeder seeder) : ControllerBase
{
    [HttpPost("api/admin/review-demo")]
    public async Task<ReviewDemoResult> Create([FromBody] ReviewDemoRequest request, CancellationToken ct)
    {
        await access.RequirePlatformAdminAsync(ct);
        return await seeder.SeedReviewGymsAsync(currentUser.RequireUserId(), request.StaffEmail, ct);
    }
}
