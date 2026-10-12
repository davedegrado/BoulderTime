using BoulderTime.Application.Help;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Help;

/// <summary>Who reads which answers is decided by the API, not by a hidden tab in the app.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FaqTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<FaqGuideDto> Guide(HttpClient client, string lang = "it") =>
        (await (await client.GetAsync($"/api/help/faq?lang={lang}")).ReadAsync<FaqGuideDto>())!;

    [Fact]
    public async Task Climbers_and_visitors_never_receive_the_staff_answers()
    {
        var anonymous = await Guide(_f.CreateClient());
        anonymous.Climbers.Should().NotBeEmpty();
        anonymous.Staff.Should().BeEmpty();

        var climber = await _f.UserAsync();
        (await Guide(climber.Client)).Staff.Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_of_any_gym_and_platform_admins_receive_both_guides()
    {
        var gym = await _f.GymAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);
        var admin = await _f.UserAsync(platformAdmin: true);

        var forStaff = await Guide(staff.Client);
        forStaff.Staff.Should().NotBeEmpty();
        forStaff.Climbers.Should().NotBeEmpty();
        (await Guide(admin.Client)).Staff.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Both_languages_ask_the_same_questions()
    {
        var gym = await _f.GymAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Owner);
        var it = await Guide(staff.Client, "it");
        var en = await Guide(staff.Client, "en");
        en.Climbers.Select(s => s.Items.Count).Should().Equal(it.Climbers.Select(s => s.Items.Count));
        en.Staff.Select(s => s.Items.Count).Should().Equal(it.Staff.Select(s => s.Items.Count));
        en.Climbers[0].Title.Should().NotBe(it.Climbers[0].Title);
    }

    [Theory]
    [InlineData(FaqAudience.Both, false, true)]
    [InlineData(FaqAudience.Both, true, true)]
    [InlineData(FaqAudience.Climbers, false, true)]
    [InlineData(FaqAudience.Climbers, true, false)]
    [InlineData(FaqAudience.Staff, false, false)]
    [InlineData(FaqAudience.Staff, true, true)]
    public void Each_answer_reaches_its_audience(FaqAudience audience, bool isStaff, bool reads) =>
        HelpService.CanRead(audience, isStaff).Should().Be(reads);
}
