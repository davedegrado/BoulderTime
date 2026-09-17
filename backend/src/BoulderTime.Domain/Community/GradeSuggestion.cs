using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Community;

/// <summary>
/// A climber's opinion of a boulder's grade in one grading system. At most one per (boulder, user, system).
/// Community suggestions never change the official grade.
/// </summary>
public class GradeSuggestion : IAuditable
{
    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GradeSystemId { get; private set; }
    public Guid GradeValueId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private GradeSuggestion() { }

    public static GradeSuggestion Create(Guid boulderId, Guid userId, Guid systemId, Guid valueId) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UserId = userId, GradeSystemId = systemId, GradeValueId = valueId };

    public void Change(Guid valueId) => GradeValueId = valueId;
}
