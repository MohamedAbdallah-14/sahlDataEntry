using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.Backup;
using SahelBundleKeyboard.Infrastructure.Tests.Persistence;
using SahelBundleKeyboard.Infrastructure.Persistence;

namespace SahelBundleKeyboard.Infrastructure.Tests.Backup;

public class BackupServiceTests
{
    private static AppDataDocument SampleDocument()
    {
        var bundle = new Bundle { Name = "عرض" };
        bundle.Items.Add(new BundleItem { ProductName = "صنف", BaseQuantity = 3, CustomPrice = null, Order = 0 });

        return new AppDataDocument { Bundles = [bundle], Settings = new AppSettings() };
    }

    [Fact]
    public void Export_ThenReadPreview_RoundTripsEverything()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        var store = new JsonDataStore(paths, NullLog.Instance);
        var backup = new BackupService(paths, NullLog.Instance);
        var document = SampleDocument();

        var exportPath = System.IO.Path.Combine(temp.Path, "backup.json");
        backup.Export(document, exportPath);

        var preview = backup.ReadPreview(exportPath);

        Assert.Equal(1, preview.BundleCount);
        Assert.Equal(1, preview.ItemCount);
        Assert.Equal(document.Settings.DelayMilliseconds, preview.DelayMilliseconds);
        Assert.Equal("Ctrl+Alt+G", preview.StartShortcut);
    }

    [Fact]
    public void ReadPreview_RejectsNonJsonGarbage()
    {
        using var temp = new TempDir();
        var garbagePath = System.IO.Path.Combine(temp.Path, "garbage.json");
        File.WriteAllText(garbagePath, "hello not json");

        var backup = new BackupService(temp.ToDataPaths(), NullLog.Instance);

        var ex = Assert.Throws<PersistenceException>(() => backup.ReadPreview(garbagePath));
        Assert.Contains("تالف", ex.UserMessage);
    }

    [Fact]
    public void ReadPreview_RejectsFutureSchemaVersion()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "future.json");
        File.WriteAllText(path, """{"schemaVersion": 42}""");

        var backup = new BackupService(temp.ToDataPaths(), NullLog.Instance);

        Assert.Throws<PersistenceException>(() => backup.ReadPreview(path));
    }

    [Fact]
    public void ReadPreview_RejectsInvalidBundleContent()
    {
        using var temp = new TempDir();
        var path = System.IO.Path.Combine(temp.Path, "invalid.json");
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "bundles": [
                {
                  "id": "00000000-0000-0000-0000-000000000001",
                  "name": "عرض",
                  "items": [
                    { "id": "00000000-0000-0000-0000-000000000002", "productName": "", "baseQuantity": -5, "order": 0 }
                  ]
                }
              ]
            }
            """);

        var backup = new BackupService(temp.ToDataPaths(), NullLog.Instance);

        var ex = Assert.Throws<PersistenceException>(() => backup.ReadPreview(path));
        Assert.Contains("غير صالحة", ex.UserMessage);
    }

    [Fact]
    public void ApplyImport_CreatesTimestampedSafetyCopyOfCurrentData()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        var store = new JsonDataStore(paths, NullLog.Instance);
        var backup = new BackupService(paths, NullLog.Instance);

        // Current data on disk.
        store.Save(SampleDocument());

        // A validated preview from elsewhere (different bundle name).
        var otherDoc = new AppDataDocument
        {
            Bundles = [new Bundle { Name = "مستوردة" }],
            Settings = new AppSettings()
        };
        var preview = new BackupPreview(otherDoc, 1, 0, 120, 2, "Ctrl+Alt+G", "Ctrl+Alt+P", "Ctrl+Alt+S");

        var imported = backup.ApplyImport(preview);
        store.Save(imported);

        Assert.Equal("مستوردة", store.Load().Document.Bundles[0].Name);
        Assert.NotEmpty(Directory.GetFiles(paths.BackupsFolder, "pre-import-*.json"));
    }
}
