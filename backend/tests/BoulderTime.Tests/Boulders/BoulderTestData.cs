using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

public static class BoulderTestData
{
    /// <summary>A real JPEG header is not needed: storage checks content type and size, not image decoding.</summary>
    public static readonly byte[] FakeJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];

    public static Task<Sector> SectorAsync(this ApiFactory f, Gym gym, string name = "Cave", bool active = true) =>
        f.Db(async db =>
        {
            var s = Sector.Create(gym.Id, name, null, 0);
            if (!active) s.SetActive(false);
            db.Sectors.Add(s);
            await db.SaveChangesAsync();
            return s;
        });

    public static async Task<GradeSystemDto> GradeSystemAsync(this TestUser admin, Gym gym, string type = "FONTAINEBLEAU", string? name = null)
    {
        var res = await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/grade-systems", new { type, name });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<GradeSystemDto>())!;
    }

    /// <summary>Requests a ticket and uploads bytes to it exactly as the browser does. Returns the object path.</summary>
    public static async Task<string> UploadPhotoAsync(this TestUser staff, ApiFactory f, Gym gym, byte[]? bytes = null, string contentType = "image/jpeg")
    {
        bytes ??= FakeJpeg;
        var ticketRes = await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulder-photos", new { contentType, sizeBytes = bytes.Length });
        ticketRes.EnsureSuccessStatusCode();
        var ticket = (await ticketRes.ReadAsync<UploadTicket>())!;

        var upload = new ByteArrayContent(bytes);
        upload.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        (await f.CreateClient().PutAsync(ticket.UploadUrl, upload)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    public static object BoulderBody(Sector sector, string photoPath, GradeSystemDto system, string label, string holdColor = "BLUE") => new
    {
        sectorId = sector.Id,
        photoPath,
        holdColor,
        grades = new[] { new { gradeSystemId = system.Id, gradeValueId = system.Values.Single(v => v.Label == label).Id } },
    };
}
