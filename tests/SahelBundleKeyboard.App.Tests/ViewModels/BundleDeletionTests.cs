using SahelBundleKeyboard.App.Services;
using SahelBundleKeyboard.App.ViewModels;
using SahelBundleKeyboard.Core.Automation;
using SahelBundleKeyboard.Core.Logging;
using SahelBundleKeyboard.Infrastructure.Persistence;

namespace SahelBundleKeyboard.App.Tests.ViewModels;

/// <summary>
/// Headless regression tests for bundle/item deletion flows (no WPF).
/// Reproduces the NullReferenceException reported on real Windows when deleting
/// a bundle or an item after the CommandManager requery change.
/// </summary>
public class BundleDeletionTests
{
    private static (MainViewModel Main, BundlesViewModel Bundles, AppDataService Data) Build()
    {
        var root = Path.Combine(Path.GetTempPath(), "sbk-del-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var paths = new DataPaths(root);
        var data = new AppDataService(new JsonDataStore(paths, NullLog.Instance), NullLog.Instance);
        data.LoadOrCreate();

        var main = new MainViewModel(
            data,
            _ => Task.FromResult(StartRunResult.Ok()),
            () => { },
            _ => { });

        var bundles = new BundlesViewModel(main, data, confirm: _ => true);
        main.BundlesVm = bundles;
        return (main, bundles, data);
    }

    [Fact]
    public void DeleteBundle_RemovesIt_AndSelectsAnother_WithoutNullReference()
    {
        var (main, bundles, _) = Build();

        bundles.CreateBundle();
        bundles.CreateBundle();
        Assert.Equal(2, main.Bundles.Count);

        var toDelete = main.SelectedBundle!;
        bundles.DeleteBundle();

        Assert.Single(main.Bundles);
        Assert.DoesNotContain(toDelete, main.Bundles);
        Assert.NotNull(main.SelectedBundle);
    }

    [Fact]
    public void DeleteLastBundle_LeavesCleanState_NoException()
    {
        var (main, bundles, data) = Build();

        bundles.CreateBundle();
        bundles.DeleteBundle();

        Assert.Empty(main.Bundles);
        Assert.Null(main.SelectedBundle);
        Assert.Empty(data.Document.Bundles);

        // Creating again after emptying must work.
        bundles.CreateBundle();
        Assert.Single(main.Bundles);
    }

    [Fact]
    public void DeleteSelectedItem_RemovesRow_Reorders_AndKeepsModelInSync()
    {
        var (main, bundles, _) = Build();

        bundles.CreateBundle();
        var bundle = main.SelectedBundle!;
        bundles.AddItemCommand.Execute(null);

        Assert.Equal(2, bundle.Items.Count);

        bundles.SelectedItem = bundle.Items[0];
        bundles.DeleteSelectedItem();

        Assert.Single(bundle.Items);
        Assert.Single(bundle.Model.Items);
        Assert.Equal(0, bundle.Model.Items[0].Order);
    }

    [Fact]
    public void DeleteItem_AfterImportReplacedRows_DoesNotThrow_AndResyncs()
    {
        var (main, bundles, _) = Build();

        bundles.CreateBundle();
        var bundle = main.SelectedBundle!;

        // Simulate import replace: model rows replaced wholesale behind wrappers.
        bundle.Model.Items.Clear();
        bundle.Model.Items.Add(new Core.Models.BundleItem { ProductName = "مستورد", BaseQuantity = 2, Order = 0 });

        bundles.SelectedItem = bundle.Items[0]; // stale wrapper
        bundles.DeleteSelectedItem();           // must resync instead of throwing

        Assert.Single(bundle.Items);
        Assert.Equal("مستورد", bundle.Items[0].ProductName);
    }
}
