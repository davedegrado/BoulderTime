namespace BoulderTime.Domain.Boulders;

/// <summary>Which of a boulder's pictures.</summary>
public enum PhotoFile
{
    /// <summary>The photo on the boulder's page.</summary>
    Photo,
    /// <summary>The small picture on the cards and in the lists.</summary>
    Thumbnail,
}

/// <summary>
/// A boulder's picture replaced by a lighter copy of itself. The old file is kept until a platform administrator
/// confirms the gym's new pictures, so that until then every replacement can be undone.
/// </summary>
public sealed class PhotoRecompression
{
    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid BoulderId { get; private set; }
    public PhotoFile File { get; private set; }
    public string OldPath { get; private set; } = string.Empty;
    public string NewPath { get; private set; } = string.Empty;
    public long OldBytes { get; private set; }
    public long NewBytes { get; private set; }
    public Guid? ByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    /// <summary>Undone: the boulder went back to the old file and the new one was deleted.</summary>
    public DateTimeOffset? RolledBackAt { get; private set; }
    /// <summary>Confirmed: the old file was deleted, and there is no going back.</summary>
    public DateTimeOffset? FinalizedAt { get; private set; }

    /// <summary>Neither undone nor confirmed yet: both files are still there.</summary>
    public bool IsOpen => RolledBackAt is null && FinalizedAt is null;

    private PhotoRecompression() { }

    public static PhotoRecompression Create(Guid gymId, Guid boulderId, PhotoFile file, string oldPath, string newPath, long oldBytes, long newBytes,
        Guid? byUserId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, BoulderId = boulderId, File = file, OldPath = oldPath, NewPath = newPath,
        OldBytes = oldBytes, NewBytes = newBytes, ByUserId = byUserId, CreatedAt = now,
    };

    public void RolledBack(DateTimeOffset now) => RolledBackAt = now;

    public void Finalized(DateTimeOffset now) => FinalizedAt = now;
}
