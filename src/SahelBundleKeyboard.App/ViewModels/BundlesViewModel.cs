using System.IO;
using System.Windows;
using Microsoft.Win32;
using SahelBundleKeyboard.App.Infrastructure;
using SahelBundleKeyboard.App.Services;
using SahelBundleKeyboard.App.Views;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.App.ViewModels;

/// <summary>Bundle/item CRUD, reordering and import-export flows.</summary>
public sealed class BundlesViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly AppDataService _data;

    public BundlesViewModel(MainViewModel main, AppDataService data)
    {
        _main = main;
        _data = data;

        NewBundleCommand = new RelayCommand(CreateBundle);
        DuplicateBundleCommand = new RelayCommand(DuplicateBundle, () => _main.SelectedBundle is not null);
        DeleteBundleCommand = new RelayCommand(DeleteBundle, () => _main.SelectedBundle is not null);
        AddItemCommand = new RelayCommand(AddItem, () => _main.SelectedBundle is not null);
        DeleteItemCommand = new RelayCommand(DeleteSelectedItem, () => SelectedItem is not null);
        MoveItemUpCommand = new RelayCommand(() => MoveSelectedItem(-1), () => CanMove(-1));
        MoveItemDownCommand = new RelayCommand(() => MoveSelectedItem(+1), () => CanMove(+1));
        ImportCommand = new RelayCommand(Import);
        ExportCsvTemplateCommand = new RelayCommand(ExportCsvTemplate);
        ExportXlsxTemplateCommand = new RelayCommand(ExportXlsxTemplate);
    }

    public RelayCommand NewBundleCommand { get; }
    public RelayCommand DuplicateBundleCommand { get; }
    public RelayCommand DeleteBundleCommand { get; }
    public RelayCommand AddItemCommand { get; }
    public RelayCommand DeleteItemCommand { get; }
    public RelayCommand MoveItemUpCommand { get; }
    public RelayCommand MoveItemDownCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand ExportCsvTemplateCommand { get; }
    public RelayCommand ExportXlsxTemplateCommand { get; }

    public EditableBundleItem? SelectedItem { get; set; }

    public void CreateBundle()
    {
        var name = GenerateBundleName();
        var bundle = new Bundle { Name = name };
        bundle.Items.Add(new BundleItem { ProductName = string.Empty, BaseQuantity = 1, Order = 0 });
        _main.AddBundle(bundle);
    }

    private string GenerateBundleName()
    {
        var baseName = "حزمة جديدة";
        var existing = new HashSet<string>(_main.Bundles.Select(b => b.Name.Trim()));
        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    public void DuplicateBundle()
    {
        if (_main.SelectedBundle is not { } source)
        {
            return;
        }

        foreach (var item in source.Items)
        {
            _ = item.TryCommit(out _);
        }

        var clone = source.Model.DeepClone();
        clone.Name = $"{source.Name} - نسخة";
        _main.AddBundle(clone);
    }

    public void DeleteBundle()
    {
        if (_main.SelectedBundle is not { } bundle)
        {
            return;
        }

        var confirmed = UiText.Confirm($"هل تريد حذف الحزمة \"{bundle.Name}\" نهائياً مع كل أصنافها؟");
        if (!confirmed)
        {
            return;
        }

        _main.RemoveSelectedBundle();
    }

    public void AddItem()
    {
        if (_main.SelectedBundle is not { } bundle)
        {
            return;
        }

        var item = new BundleItem
        {
            ProductName = string.Empty,
            BaseQuantity = 1,
            Order = bundle.Items.Count
        };

        bundle.Model.Items.Add(item);
        var editable = EditableBundleItem.FromModel(item);
        bundle.Items.Add(editable);
        bundle.ReapplyOrder();
        bundle.RefreshItemsSummary();
        _data.Save();
        _main.RefreshSummaries();
    }

    public void DeleteSelectedItem()
    {
        if (_main.SelectedBundle is not { } bundle || SelectedItem is null)
        {
            return;
        }

        bundle.Items.Remove(SelectedItem);
        _ = bundle.Model.Items.Remove(SelectedItem.Model);
        bundle.ReapplyOrder();
        bundle.RefreshItemsSummary();
        SelectedItem = null;
        _data.Save();
        _main.RefreshSummaries();
    }

    private bool CanMove(int direction)
    {
        if (_main.SelectedBundle?.Items is not { } items || SelectedItem is null)
        {
            return false;
        }

        var index = items.IndexOf(SelectedItem);
        return direction < 0 ? index > 0 : index >= 0 && index < items.Count - 1;
    }

    private void MoveSelectedItem(int direction)
    {
        if (_main.SelectedBundle?.Items is not { } items || SelectedItem is null)
        {
            return;
        }

        var index = items.IndexOf(SelectedItem);
        var target = index + direction;
        if (target < 0 || target >= items.Count)
        {
            return;
        }

        items.Move(index, target);
        items[index].Order = index;
        items[target].Order = target;

        // Keep model order aligned with the visual order.
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Order = i;
            _main.SelectedBundle.Model.Items[i] = items[i].Model;
            _main.SelectedBundle.Model.Items[i].Order = i;
        }

        _data.Save();
    }

    public void Import()
    {
        if (_main.SelectedBundle is not { } bundle)
        {
            UiText.ShowInfo("اختر حزمة أولاً لاستيراد الأصناف إليها.");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "استيراد أصناف من Excel أو CSV",
            Filter = "ملفات Excel و CSV (*.xlsx;*.csv)|*.xlsx;*.csv|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var parseResult = new ImportFileReader().Read(dialog.FileName);
        if (parseResult.FileLevelError)
        {
            UiText.ShowError(parseResult.FileError!);
            return;
        }

        var preview = new ImportPreviewWindow(parseResult, bundle.Name) { Owner = Application.Current.MainWindow };
        if (preview.ShowDialog() != true || preview.ResultItems is null)
        {
            return;
        }

        if (preview.ReplaceExisting)
        {
            bundle.Items.Clear();
            bundle.Model.Items.Clear();
        }

        foreach (var item in preview.ResultItems)
        {
            item.Order = bundle.Items.Count;
            bundle.Model.Items.Add(item);
            bundle.Items.Add(EditableBundleItem.FromModel(item));
        }

        bundle.ReapplyOrder();
        bundle.RefreshItemsSummary();
        _data.Save();
        _main.RefreshSummaries();

        UiText.ShowInfo(
            $"تم استيراد {preview.ResultItems.Count} صنفاً إلى حزمة \"{bundle.Name}\"." +
            (preview.SkippedCount > 0 ? $"\nتم تخطي {preview.SkippedCount} صف غير صالح." : string.Empty));
    }

    public void ExportCsvTemplate()
    {
        var dialog = new SaveFileDialog
        {
            Title = "تصدير قالب CSV",
            FileName = "قالب-الحزم.csv",
            Filter = "CSV (*.csv)|*.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            CsvTemplateWriter.Write(dialog.FileName);
            UiText.ShowInfo("تم إنشاء القالب. افتحه في Excel وأضف الأصناف ثم استورده من تبويب \"الحزم\".");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiText.ShowError("تعذر حفظ ملف القالب. تأكد من صحة المسار.");
        }
    }

    public void ExportXlsxTemplate()
    {
        var dialog = new SaveFileDialog
        {
            Title = "تصدير قالب Excel",
            FileName = "قالب-الحزم.xlsx",
            Filter = "Excel (*.xlsx)|*.xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, global::SahelBundleKeyboard.Infrastructure.ImportExport.XlsxTemplateWriter.ToBytes());
            UiText.ShowInfo("تم إنشاء قالب Excel. أضف الأصناف ثم استورده من تبويب \"الحزم\".");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UiText.ShowError("تعذر حفظ ملف القالب. تأكد من صحة المسار.");
        }
    }
}
