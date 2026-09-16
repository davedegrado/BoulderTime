namespace BoulderTime.Domain.Staff;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
    Expired = 4,
}

/// <summary>
/// An invitation to join a gym's staff, addressed to an email. The invitee may not have a BoulderTime account yet;
/// the invitation is matched to them by their verified sign-in email.
/// </summary>
public class StaffInvitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public GymRole Role { get; private set; }
    public Guid InvitedByUserId { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }

    private StaffInvitation() { }

    public static StaffInvitation Create(Guid gymId, string email, GymRole role, Guid invitedBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        GymId = gymId,
        Email = email.Trim().ToLowerInvariant(),
        Role = role,
        InvitedByUserId = invitedBy,
        Status = InvitationStatus.Pending,
        CreatedAt = now,
        ExpiresAt = now + Lifetime,
    };

    public bool IsOpen(DateTimeOffset now) => Status == InvitationStatus.Pending && ExpiresAt > now;

    public void Accept(Guid userId, DateTimeOffset now) => Close(InvitationStatus.Accepted, now, userId);
    public void Decline(DateTimeOffset now) => Close(InvitationStatus.Declined, now);
    public void Revoke(DateTimeOffset now) => Close(InvitationStatus.Revoked, now);
    public void MarkExpired(DateTimeOffset now) => Close(InvitationStatus.Expired, now);

    private void Close(InvitationStatus status, DateTimeOffset now, Guid? acceptedBy = null)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Invitation is already {Status}.");
        Status = status;
        RespondedAt = now;
        AcceptedByUserId = acceptedBy;
    }
}
