namespace BoulderTime.Domain.Staff;

/// <summary>A user's role at a gym. One row per (gym, user). Created only by accepting an invitation or by seeding.</summary>
public class GymStaffMember
{
    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid UserId { get; private set; }
    public GymRole Role { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private GymStaffMember() { }

    public static GymStaffMember Create(Guid gymId, Guid userId, GymRole role, Guid? invitedByUserId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        GymId = gymId,
        UserId = userId,
        Role = role,
        InvitedByUserId = invitedByUserId,
        CreatedAt = now,
    };

    public void ChangeRole(GymRole role) => Role = role;
}
