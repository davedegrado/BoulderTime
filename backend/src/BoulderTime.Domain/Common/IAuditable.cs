namespace BoulderTime.Domain.Common;

/// <summary>Entities whose timestamps are maintained automatically on save.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}
