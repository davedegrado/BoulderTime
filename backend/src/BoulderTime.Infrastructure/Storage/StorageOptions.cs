namespace BoulderTime.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>"Supabase" (production) or "Local" (development and tests).</summary>
    public string Provider { get; set; } = "Supabase";

    public LocalStorageOptions Local { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    /// <summary>Folder that holds one sub-folder per bucket.</summary>
    public string RootPath { get; set; } = ".local-storage";

    /// <summary>HMAC key for upload tickets. Random per process when empty (tickets then expire on restart).</summary>
    public string? SigningKey { get; set; }
}
