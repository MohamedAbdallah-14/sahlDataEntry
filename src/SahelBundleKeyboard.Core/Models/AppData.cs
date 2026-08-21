namespace SahelBundleKeyboard.Core.Models;

/// <summary>Current schema version written by this build.</summary>
public static class AppDataSchema
{
    public const int CurrentVersion = 1;
}

/// <summary>Root persisted document. Stored as UTF-8 JSON beside the executable.</summary>
public sealed class AppData
{
    public int SchemaVersion { get; set; } = AppDataSchema.CurrentVersion;

    public AppSettings Settings { get; set; } = new();

    public List<Bundle> Bundles { get; set; } = new();
}
