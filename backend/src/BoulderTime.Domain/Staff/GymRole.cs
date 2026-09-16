namespace BoulderTime.Domain.Staff;

/// <summary>Ordered by authority: a higher value includes every permission of lower values.</summary>
public enum GymRole
{
    Staff = 1,
    Admin = 2,
    Owner = 3,
}

public static class GymRoleRules
{
    public static bool AtLeast(this GymRole role, GymRole minimum) => role >= minimum;

    /// <summary>
    /// Who may grant a role: owners grant anything; admins grant Staff or Admin; staff grant nothing.
    /// </summary>
    public static bool CanGrant(this GymRole actor, GymRole target) =>
        actor == GymRole.Owner || (actor == GymRole.Admin && target != GymRole.Owner);

    /// <summary>Who may change or remove an existing member: never someone above you; owners are managed only by owners.</summary>
    public static bool CanManage(this GymRole actor, GymRole member) =>
        actor == GymRole.Owner || (actor == GymRole.Admin && member != GymRole.Owner);
}
