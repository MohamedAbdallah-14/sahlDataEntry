namespace SahelBundleKeyboard.Infrastructure.Persistence;

/// <summary>Resolved portable data locations. Everything lives in a Data folder beside the EXE.</summary>
public sealed class DataPaths
{
    public string Root { get; }
    public string DataFile { get; }
    public string BackupsFolder { get; }
    public string LogsFolder { get; }

    public DataPaths(string root)
    {
        Root = root;
        DataFile = Path.Combine(root, "data.json");
        BackupsFolder = Path.Combine(root, "backups");
        LogsFolder = Path.Combine(root, "logs");
    }

    /// <summary>Creates the folder tree on first run. Safe to call repeatedly.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupsFolder);
        Directory.CreateDirectory(LogsFolder);
    }

    public static DataPaths ResolveForCurrentProcess()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return new DataPaths(Path.Combine(baseDirectory, "Data"));
    }
}
