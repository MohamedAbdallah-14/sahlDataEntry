namespace SahelBundleKeyboard.Infrastructure.Persistence;

/// <summary>User-facing persistence failure. Message is Arabic; Detail carries technical info for the log.</summary>
public sealed class PersistenceException : Exception
{
    public string UserMessage { get; }
    public string? Detail { get; }

    public PersistenceException(string userMessage, string? detail = null, Exception? inner = null)
        : base(userMessage, inner)
    {
        UserMessage = userMessage;
        Detail = detail;
    }
}

/// <summary>Outcome of loading the persisted document.</summary>
public sealed record LoadResult(AppDataDocument Document, bool WasCorruptOrUnsupported, string? Warning)
{
    public static LoadResult Fresh() => new(AppDataDocument.CreateDefault(), false, null);
}

/// <summary>Envelope stored on disk: schema version + full application data.</summary>
public sealed class AppDataDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public SahelBundleKeyboard.Core.Models.AppSettings Settings { get; set; } = new();

    public List<SahelBundleKeyboard.Core.Models.Bundle> Bundles { get; set; } = [];

    public static AppDataDocument CreateDefault() => new();
}
