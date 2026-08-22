using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.Persistence;

namespace SahelBundleKeyboard.Infrastructure.Tests.Persistence;

public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "sbk-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public DataPaths ToDataPaths() => new(System.IO.Path.Combine(Path, "Data"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public class JsonDataStoreTests
{
    private static AppDataDocument SampleDocument()
    {
        var bundle = new Bundle { Name = "عرض رمضان" };
        bundle.Items.Add(new BundleItem
        {
            ProductCode = "6290100000001",
            ProductName = "زيت عافية ١ لتر",
            BaseQuantity = 1.5m,
            CustomPrice = 42.50m,
            Order = 0
        });
        bundle.Items.Add(new BundleItem
        {
            ProductName = "أرز أبو كاس ٥ كيلو",
            BaseQuantity = 2,
            Order = 1
        });

        return new AppDataDocument
        {
            Settings = new AppSettings { DelayMilliseconds = 250, CountdownSeconds = 1, StartShortcut = "Ctrl+Alt+K" },
            Bundles = [bundle]
        };
    }

    [Fact]
    public void SaveThenLoad_RoundTripsArabicAndDecimals()
    {
        using var temp = new TempDir();
        var store = new JsonDataStore(temp.ToDataPaths(), NullLog.Instance);
        var document = SampleDocument();

        store.Save(document);
        var loaded = store.Load();

        Assert.False(loaded.WasCorruptOrUnsupported);
        Assert.Single(loaded.Document.Bundles);
        Assert.Equal("عرض رمضان", loaded.Document.Bundles[0].Name);
        Assert.Equal("زيت عافية ١ لتر", loaded.Document.Bundles[0].Items[0].ProductName);
        Assert.Equal(1.5m, loaded.Document.Bundles[0].Items[0].BaseQuantity);
        Assert.Equal(42.50m, loaded.Document.Bundles[0].Items[0].CustomPrice);
        Assert.Equal(250, loaded.Document.Settings.DelayMilliseconds);
        Assert.Equal("Ctrl+Alt+K", loaded.Document.Settings.StartShortcut);
        Assert.Equal(document.Bundles[0].Id, loaded.Document.Bundles[0].Id); // stable ids survive persistence
    }

    [Fact]
    public void Load_MissingFile_ReturnsFreshDefaults_AndCreatesFolders()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        var store = new JsonDataStore(paths, NullLog.Instance);

        var result = store.Load();

        Assert.False(result.WasCorruptOrUnsupported);
        Assert.Empty(result.Document.Bundles);
        Assert.Equal(AppDataDocument.CurrentSchemaVersion, result.Document.SchemaVersion);
        Assert.True(Directory.Exists(paths.Root));
    }

    [Fact]
    public void Load_CorruptFile_QuarantinesOriginalWithoutOverwritingIt()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        paths.EnsureCreated();
        File.WriteAllText(paths.DataFile, "{ not valid json !!");

        var store = new JsonDataStore(paths, NullLog.Instance);
        var result = store.Load();

        Assert.True(result.WasCorruptOrUnsupported);
        Assert.NotNull(result.Warning);

        // Original bytes preserved under a quarantine name; no plain data.json left behind.
        var quarantined = Directory.GetFiles(paths.Root, "data.json.corrupt-*");
        Assert.Single(quarantined);
        Assert.Contains("not valid json", File.ReadAllText(quarantined[0]));
    }

    [Fact]
    public void Load_FutureSchemaVersion_IsQuarantinedNotOverwritten()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        paths.EnsureCreated();
        File.WriteAllText(paths.DataFile, """{"schemaVersion": 99, "bundles": []}""");

        var store = new JsonDataStore(paths, NullLog.Instance);
        var result = store.Load();

        Assert.True(result.WasCorruptOrUnsupported);
        Assert.NotEmpty(Directory.GetFiles(paths.Root, "data.json.corrupt-*"));
    }

    [Fact]
    public void Save_AfterCorruption_WritesCleanNewFile_KeepingQuarantine()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        paths.EnsureCreated();
        File.WriteAllText(paths.DataFile, "broken");

        var store = new JsonDataStore(paths, NullLog.Instance);
        _ = store.Load();
        store.Save(SampleDocument());

        Assert.True(File.Exists(paths.DataFile));
        var reloaded = store.Load();
        Assert.False(reloaded.WasCorruptOrUnsupported);
        Assert.Single(reloaded.Document.Bundles);
    }

    [Fact]
    public void Save_FailsOnReadOnlyLocation_WithArabicPersistenceException()
    {
        using var temp = new TempDir();
        var paths = temp.ToDataPaths();
        paths.EnsureCreated();

        // Point the data file at a directory path to force an IO failure.
        Directory.CreateDirectory(System.IO.Path.Combine(paths.Root, "as-directory"));
        var brokenPaths = new DataPaths(paths.Root);
        File.Delete(brokenPaths.DataFile + ".tmp");
        Directory.CreateDirectory(brokenPaths.DataFile); // occupy the target path

        var store = new JsonDataStore(brokenPaths, NullLog.Instance);

        var ex = Assert.Throws<PersistenceException>(() => store.Save(SampleDocument()));
        Assert.Contains("فشل حفظ البيانات", ex.UserMessage);
    }
}
