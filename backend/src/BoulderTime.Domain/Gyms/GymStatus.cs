namespace BoulderTime.Domain.Gyms;

public enum GymStatus
{
    /// <summary>Created by a platform admin; visible only to its staff and platform admins.</summary>
    Draft = 0,
    /// <summary>Publicly discoverable.</summary>
    Active = 1,
    /// <summary>Hidden from the public; history is kept.</summary>
    Archived = 2,
}
