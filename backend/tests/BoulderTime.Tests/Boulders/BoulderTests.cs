using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Boulders;

[Collection(DatabaseCollection.Name)]
public sealed class BoulderTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<(Gym Gym, Sector Sector, TestUser Staff, Application.Grading.GradeSystemDto Font, Application.Grading.GradeSystemDto Color)> SetupAsync()
    {
        var gym = await _f.GymAsync();
        var sector = await _f.SectorAsync(gym);
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        var font = await owner.GradeSystemAsync(gym, "FONTAINEBLEAU");
        var color = await owner.GradeSystemAsync(gym, "COLOR");
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);
        return (gym, sector, staff, font, color);
    }

    [Fact]
    public async Task Staff_uploads_a_photo_and_creates_a_boulder_with_grades_and_hold_colour_visible_to_everyone()
    {
        var (gym, sector, staff, font, color) = await SetupAsync();
        var photo = await staff.UploadPhotoAsync(_f, gym);

        var res = await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", new
        {
            sectorId = sector.Id,
            photoPath = photo,
            holdColor = "BLUE",
            setterUserId = staff.Id,
            grades = new[]
            {
                new { gradeSystemId = color.Id, gradeValueId = color.Values.Single(v => v.Label == "Yellow").Id },
                new { gradeSystemId = font.Id, gradeValueId = font.Values.Single(v => v.Label == "6A").Id },
            },
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await res.ReadAsync<BoulderDetailDto>())!;

        // Grade colour (Yellow) and hold colour (Blue) are separate facts.
        created.HoldColor.Should().Be(HoldColor.Blue);
        created.Grades.Select(g => $"{g.SystemType}:{g.Label}").Should().Equal("Fontainebleau:6A", "Color:Yellow");
        created.Setter!.UserId.Should().Be(staff.Id);

        var anon = _f.CreateClient();
        var list = await (await anon.GetAsync($"/api/gyms/{gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>();
        list!.Items.Should().ContainSingle(b => b.Id == created.Id && b.SectorName == "Cave");
        (await anon.GetAsync(list.Items[0].PhotoUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Photo_is_required_and_must_actually_be_uploaded_to_this_gyms_folder()
    {
        var (gym, sector, staff, font, _) = await SetupAsync();
        var other = await _f.GymAsync();
        await _f.StaffAsync(other, staff, GymRole.Staff);
        var otherGymPhoto = await staff.UploadPhotoAsync(_f, other);

        var neverUploaded = $"gyms/{gym.Id}/boulders/{Guid.NewGuid():N}.jpg";
        foreach (var path in new[] { "", neverUploaded, otherGymPhoto, $"gyms/{gym.Id}/boulders/../../{other.Id}/x.jpg" })
        {
            var res = await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", BoulderTestData.BoulderBody(sector, path, font, "6A"));
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"photo path '{path}' must be rejected");
        }
    }

    [Fact]
    public async Task Upload_tickets_are_signed_expire_with_tampering_and_enforce_type_and_size()
    {
        var (gym, _, staff, _, _) = await SetupAsync();
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulder-photos", new { contentType = "image/gif", sizeBytes = 10 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 50_000_000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ticket = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 12 })).ReadAsync<UploadTicket>())!;
        var tampered = ticket.UploadUrl[..^3] + (ticket.UploadUrl.EndsWith("AAA") ? "BBB" : "AAA");

        var content = new ByteArrayContent(BoulderTestData.FakeJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(tampered, content)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var wrongType = new ByteArrayContent(BoulderTestData.FakeJpeg);
        wrongType.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, wrongType)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grades_must_come_from_this_gyms_active_systems_one_per_system()
    {
        var (gym, sector, staff, font, _) = await SetupAsync();
        var other = await _f.GymAsync();
        var otherOwner = await _f.UserAsync();
        await _f.StaffAsync(other, otherOwner, GymRole.Owner);
        var foreignSystem = await otherOwner.GradeSystemAsync(other, "V_SCALE");
        var photo = await staff.UploadPhotoAsync(_f, gym);

        async Task<HttpStatusCode> Create(object grades) => (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            new { sectorId = sector.Id, photoPath = photo, holdColor = "RED", grades })).StatusCode;

        (await Create(Array.Empty<object>())).Should().Be(HttpStatusCode.BadRequest);
        (await Create(new[] { new { gradeSystemId = foreignSystem.Id, gradeValueId = foreignSystem.Values[3].Id } })).Should().Be(HttpStatusCode.BadRequest);
        (await Create(new[] { new { gradeSystemId = font.Id, gradeValueId = font.Values[5].Id }, new { gradeSystemId = font.Id, gradeValueId = font.Values[6].Id } })).Should().Be(HttpStatusCode.BadRequest);
        (await Create(new[] { new { gradeSystemId = font.Id, gradeValueId = foreignSystem.Values[3].Id } })).Should().Be(HttpStatusCode.BadRequest);
        (await Create(new[] { new { gradeSystemId = font.Id, gradeValueId = font.Values[5].Id } })).Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Sector_setter_and_hold_colour_are_validated()
    {
        var (gym, _, staff, font, _) = await SetupAsync();
        var hidden = await _f.SectorAsync(gym, "Old room", active: false);
        var visible = await _f.SectorAsync(gym, "Slab");
        var outsider = await _f.UserAsync();
        var photo = await staff.UploadPhotoAsync(_f, gym);
        var grades = new[] { new { gradeSystemId = font.Id, gradeValueId = font.Values[5].Id } };

        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", new { sectorId = hidden.Id, photoPath = photo, holdColor = "RED", grades })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", new { sectorId = visible.Id, photoPath = photo, holdColor = "RED", grades, setterUserId = outsider.Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", new { sectorId = visible.Id, photoPath = photo, holdColor = "TURQUOISE", grades })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Climbers_cannot_create_boulders_or_request_upload_tickets()
    {
        var (gym, sector, _, font, _) = await SetupAsync();
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 10 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await climber.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", BoulderTestData.BoulderBody(sector, $"gyms/{gym.Id}/boulders/x.jpg", font, "6A"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await climber.Client.GetAsync($"/api/gyms/{gym.Id}/boulders?status=REMOVED")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bulk_removal_retraces_a_sector_keeps_history_and_retracing_creates_a_new_boulder()
    {
        var (gym, sector, staff, font, _) = await SetupAsync();
        var slab = await _f.SectorAsync(gym, "Slab");
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var photo = await staff.UploadPhotoAsync(_f, gym);
            var body = BoulderTestData.BoulderBody(i < 2 ? sector : slab, photo, font, "6A");
            ids.Add((await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders", body)).ReadAsync<BoulderDetailDto>())!.Id);
        }

        var result = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders/remove", new { boulderIds = ids })).ReadAsync<RemoveBouldersResult>())!;
        result.Removed.Should().Be(3);
        result.Sectors.Select(s => (s.SectorName, s.Removed)).Should().Equal(("Cave", 2), ("Slab", 1));

        // Removing again is a no-op, not an error.
        var again = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders/remove", new { boulderIds = ids })).ReadAsync<RemoveBouldersResult>())!;
        again.Removed.Should().Be(0);

        // Removed boulders are gone from the active list but still readable (climbing history).
        var anon = _f.CreateClient();
        (await (await anon.GetAsync($"/api/gyms/{gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!.Total.Should().Be(0);
        var old = (await (await anon.GetAsync($"/api/boulders/{ids[0]}")).ReadAsync<BoulderDetailDto>())!;
        old.Status.Should().Be(BoulderStatus.Removed);
        old.RemovedAt.Should().NotBeNull();
        old.Grades.Should().ContainSingle(g => g.Label == "6A");

        // Retrace: a brand-new boulder with its own id; the old one is untouched.
        var retraced = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            BoulderTestData.BoulderBody(sector, await staff.UploadPhotoAsync(_f, gym), font, "6B"))).ReadAsync<BoulderDetailDto>())!;
        retraced.Id.Should().NotBe(ids[0]);
        (await _f.Db(db => db.Boulders.CountAsync(b => b.GymId == gym.Id))).Should().Be(4);

        var removedList = (await (await staff.Client.GetAsync($"/api/gyms/{gym.Id}/boulders?status=REMOVED")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        removedList.Total.Should().Be(3);
    }

    [Fact]
    public async Task Bulk_removal_is_atomic_when_any_boulder_belongs_to_another_gym()
    {
        var (gym, sector, staff, font, _) = await SetupAsync();
        var mine = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            BoulderTestData.BoulderBody(sector, await staff.UploadPhotoAsync(_f, gym), font, "6A"))).ReadAsync<BoulderDetailDto>())!;

        var other = await _f.GymAsync();
        var otherSector = await _f.SectorAsync(other);
        var otherOwner = await _f.UserAsync();
        await _f.StaffAsync(other, otherOwner, GymRole.Owner);
        var otherFont = await otherOwner.GradeSystemAsync(other, "FONTAINEBLEAU");
        var theirs = (await (await otherOwner.Client.PostAsJsonAsync($"/api/gyms/{other.Id}/boulders",
            BoulderTestData.BoulderBody(otherSector, await otherOwner.UploadPhotoAsync(_f, other), otherFont, "6A"))).ReadAsync<BoulderDetailDto>())!;

        var res = await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders/remove", new { boulderIds = new[] { mine.Id, theirs.Id } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.Db(db => db.Boulders.CountAsync(b => b.Status == BoulderStatus.Removed))).Should().Be(0);
    }

    [Fact]
    public async Task Restore_brings_a_boulder_back_and_editing_replaces_grades()
    {
        var (gym, sector, staff, font, color) = await SetupAsync();
        var created = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            BoulderTestData.BoulderBody(sector, await staff.UploadPhotoAsync(_f, gym), font, "6A"))).ReadAsync<BoulderDetailDto>())!;

        await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders/remove", new { boulderIds = new[] { created.Id } });
        var restored = (await (await staff.Client.PostAsync($"/api/boulders/{created.Id}/restore", null)).ReadAsync<BoulderDetailDto>())!;
        restored.Status.Should().Be(BoulderStatus.Active);
        restored.RemovedAt.Should().BeNull();

        var edited = (await (await staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", new
        {
            sectorId = sector.Id,
            photoPath = created.PhotoPath, // unchanged photo needs no re-upload
            holdColor = "PINK",
            grades = new[]
            {
                new { gradeSystemId = font.Id, gradeValueId = font.Values.Single(v => v.Label == "6A+").Id },
                new { gradeSystemId = color.Id, gradeValueId = color.Values.Single(v => v.Label == "Green").Id },
            },
        })).ReadAsync<BoulderDetailDto>())!;

        edited.HoldColor.Should().Be(HoldColor.Pink);
        edited.Grades.Select(g => g.Label).Should().Equal("6A+", "Green");
        (await _f.Db(db => db.BoulderGrades.CountAsync(g => g.BoulderId == created.Id))).Should().Be(2);
    }

    [Fact]
    public async Task Retiring_a_grade_value_keeps_it_on_existing_boulders_but_blocks_new_use()
    {
        var (gym, sector, staff, font, _) = await SetupAsync();
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        var created = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            BoulderTestData.BoulderBody(sector, await staff.UploadPhotoAsync(_f, gym), font, "6A"))).ReadAsync<BoulderDetailDto>())!;

        var keep = font.Values.Where(v => v.Label != "6A").Select(v => new { id = (Guid?)v.Id, label = v.Label }).ToArray();
        (await owner.Client.PutAsJsonAsync($"/api/grade-systems/{font.Id}/values", new { values = keep })).EnsureSuccessStatusCode();

        (await (await _f.CreateClient().GetAsync($"/api/boulders/{created.Id}")).ReadAsync<BoulderDetailDto>())!
            .Grades.Should().ContainSingle(g => g.Label == "6A");
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/boulders",
            BoulderTestData.BoulderBody(sector, await staff.UploadPhotoAsync(_f, gym), font, "6A"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
