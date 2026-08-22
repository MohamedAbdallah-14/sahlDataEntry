using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Infrastructure.Persistence;

namespace SahelBundleKeyboard.App.Services;

/// <summary>
/// Owns the current AppDataDocument and keeps data.json in sync.
/// Saves are atomic (handled by JsonDataStore) and triggered after every mutation.
/// </summary>
public sealed class AppDataService
{
    private const string LogSource = "AppData";

    private readonly JsonDataStore _store;
    private readonly ILog _log;

    public AppDataService(JsonDataStore store, ILog log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public DataPaths Paths { get; private set; } = null!;

    public AppDataDocument Document { get; private set; } = null!;

    /// <summary>Non-null when the previous file was corrupt; the UI shows this warning once.</summary>
    public string? StartupWarning { get; private set; }

    public void LoadOrCreate()
    {
        Paths = DataPaths.ResolveForCurrentProcess();
        var result = _store.Load();

        Document = result.Document;
        StartupWarning = result.Warning;

        if (StartupWarning is not null)
        {
            _log.Warn(LogSource, "Loaded with warning: data file was corrupt or unsupported.");
        }
    }

    /// <summary>Replaces all data (used by backup import). Caller is responsible for confirmation.</summary>
    public void ReplaceDocument(AppDataDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Save();
    }

    public void Save()
    {
        try
        {
            _store.Save(Document);
        }
        catch (PersistenceException ex)
        {
            _log.Error(LogSource, $"Save failed: {ex.Detail}");
            UiText.ShowError(ex.UserMessage);
        }
    }
}
