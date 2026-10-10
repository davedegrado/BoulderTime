namespace BoulderTime.Domain.Boulders;

/// <summary>
/// How hard a boulder works the body, apart from its grade: a green can be a gentle slab or a punishing roof. Set by
/// staff, optional (boulders set before it existed simply have none), never derived from or tied to any grade.
/// </summary>
public enum BoulderIntensity
{
    Low = 1,
    Medium = 2,
    High = 3,
}
