using System.IO;
using System.Text;
using System.Text.Json;
using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Core.Validation;
using SahelBundleKeyboard.Infrastructure.Persistence;

namespace SahelBundleKeyboard.Infrastructure.Backup;

/// <summary>
/// Full backup export/import. A backup contains every bundle, item, item order and
/// setting in one versioned JSON file. Import validates completely before replacing
/// anything and writes a timestamped safety copy of the current data first.
/// </summary>
public sealed class BackupService
{
    private const string LogSource = "Backup";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly DataPaths _paths;
    private readonly ILog _log;

    public BackupService(DataPaths paths, ILog log)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Serializes the current document to the chosen path after a parse round-trip.</summary>
    public void Export(AppDataDocument document, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            var json = JsonSerializer.Serialize(document, SerializerOptions);

            // Round-trip check: never claim success for a backup we cannot read back.
            _ = JsonSerializer.Deserialize<AppDataDocument>(json, SerializerOptions)
                ?? throw new PersistenceException("تعذر التحقق من ملف النسخة الاحتياطية بعد إنشائه.");

            File.WriteAllText(destinationPath, json, new UTF8Encoding(false));
            _log.Info(LogSource, $"Backup exported to {destinationPath} (bundles={document.Bundles.Count}).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new PersistenceException("فشل تصدير النسخة الاحتياطية. تأكد من صحة مسار الحفظ.", ex.Message, ex);
        }
    }

    /// <summary>Parses and fully validates a backup file without touching current data.</summary>
    public BackupPreview ReadPreview(string sourcePath)
    {
        string json;
        try
        {
            if (!File.Exists(sourcePath))
            {
                throw new PersistenceException("ملف النسخة الاحتياطية غير موجود.");
            }

            json = File.ReadAllText(sourcePath, Encoding.UTF8);
        }
        catch (IOException ex)
        {
            throw new PersistenceException("تعذر قراءة ملف النسخة الاحتياطية.", ex.Message, ex);
        }

        AppDataDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<AppDataDocument>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new PersistenceException("ملف النسخة الاحتياطية تالف أو ليس ملف نسخة صحيحاً.", ex.Message, ex);
        }

        if (document is null || document.SchemaVersion > AppDataDocument.CurrentSchemaVersion)
        {
            throw new PersistenceException(
                document is null
                    ? "ملف النسخة الاحتياطية فارغ."
                    : $"إصدار ملف النسخة ({document.SchemaVersion}) غير مدعوم في هذا البرنامج.");
        }

        var validation = new ValidationResult();
        foreach (var bundle in document.Bundles)
        {
            validation.Merge(BundleValidator.ValidateBundle(bundle));
        }

        validation.Merge(SettingsValidator.Validate(document.Settings));

        if (!validation.IsValid)
        {
            throw new PersistenceException(
                "النسخة الاحتياطية تحتوي بيانات غير صالحة:\n" + validation.ToAggregatedMessage());
        }

        return new BackupPreview(
            document,
            document.Bundles.Count,
            document.Bundles.Sum(b => b.Items.Count),
            document.Settings.DelayMilliseconds,
            document.Settings.CountdownSeconds,
            document.Settings.StartShortcut,
            document.Settings.PauseResumeShortcut,
            document.Settings.StopShortcut);
    }

    /// <summary>
    /// Applies a previously validated preview: safety-copies the current data file into
    /// Data/backups, then returns the imported document for the caller to adopt and save.
    /// </summary>
    public AppDataDocument ApplyImport(BackupPreview preview)
    {
        Directory.CreateDirectory(_paths.BackupsFolder);

        if (File.Exists(_paths.DataFile))
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safetyCopy = Path.Combine(_paths.BackupsFolder, $"pre-import-{stamp}.json");
            try
            {
                File.Copy(_paths.DataFile, safetyCopy, overwrite: false);
                _log.Info(LogSource, $"Safety copy created before import: {safetyCopy}.");
            }
            catch (IOException ex)
            {
                throw new PersistenceException(
                    "تعذر إنشاء نسخة أمان للبيانات الحالية قبل الاستبدال. لم يتم الاستيراد.",
                    ex.Message, ex);
            }
        }

        _log.Info(LogSource, $"Backup import applied: bundles={preview.BundleCount}, items={preview.ItemCount}.");
        return preview.Document;
    }
}

public sealed record BackupPreview(
    AppDataDocument Document,
    int BundleCount,
    int ItemCount,
    int DelayMilliseconds,
    int CountdownSeconds,
    string StartShortcut,
    string PauseResumeShortcut,
    string StopShortcut);
