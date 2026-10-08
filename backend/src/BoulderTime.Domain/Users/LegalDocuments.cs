namespace BoulderTime.Domain.Users;

/// <summary>
/// The terms and privacy notice people agree to. The version is a date, bumped only when the change is substantial
/// enough that everyone should read it again — a typo fix is not. Raising it asks every account to accept anew, so
/// it is deliberately a decision, not a side effect of editing text.
/// </summary>
public static class LegalDocuments
{
    public const string CurrentVersion = "2026-10-08";
}
