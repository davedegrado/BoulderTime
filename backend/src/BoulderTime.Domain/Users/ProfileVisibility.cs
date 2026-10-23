namespace BoulderTime.Domain.Users;

/// <summary>
/// Who sees the content of a person's profile (statistics, history, gyms…). Their name and photo are always shown, so
/// a private person can still be recognised, for example from a leaderboard. Independent of the leaderboards: a private
/// profile can still appear in them, and a public one can stay out (ADR-061).
/// </summary>
public enum ProfileVisibility
{
    Public = 0,
    /// <summary>Only the person sees the content. (Friends would see it too, once BoulderTime has friends.)</summary>
    Private = 1,
}
