using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Climbing;

/// <summary>A gym with a sector, Font + Colour grading, and helpers to create graded boulders.</summary>
public sealed class ClimbingWorld
{
    public required Gym Gym { get; init; }
    public required Sector Sector { get; init; }
    public required TestUser Staff { get; init; }
    public required GradeSystemDto Font { get; init; }
    public required GradeSystemDto Color { get; init; }
    public required ApiFactory Factory { get; init; }

    public static async Task<ClimbingWorld> CreateAsync(ApiFactory f, string gymName = "Crimp Factory")
    {
        var gym = await f.GymAsync(GymStatus.Active, gymName);
        var owner = await f.UserAsync();
        await f.StaffAsync(gym, owner, GymRole.Owner);
        return new ClimbingWorld
        {
            Gym = gym, Sector = await f.SectorAsync(gym), Staff = owner, Factory = f,
            Font = await owner.GradeSystemAsync(gym, "FONTAINEBLEAU"),
            Color = await owner.GradeSystemAsync(gym, "COLOR"),
        };
    }

    public async Task<BoulderDetailDto> BoulderAsync(string font = "6A", string? color = null)
    {
        var grades = new List<object> { new { gradeSystemId = Font.Id, gradeValueId = Font.Values.Single(v => v.Label == font).Id } };
        if (color is not null) grades.Add(new { gradeSystemId = Color.Id, gradeValueId = Color.Values.Single(v => v.Label == color).Id });
        var res = await Staff.Client.PostAsJsonAsync($"/api/gyms/{Gym.Id}/boulders",
            new { sectorId = Sector.Id, photoPath = await Staff.UploadPhotoAsync(Factory, Gym), holdColor = "BLUE", grades });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<BoulderDetailDto>())!;
    }
}
