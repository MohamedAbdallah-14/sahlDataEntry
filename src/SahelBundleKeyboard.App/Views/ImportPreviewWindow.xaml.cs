using System.Collections.ObjectModel;
using System.Windows;
using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.App.Views;

/// <summary>Import preview with per-row validation before anything is committed.</summary>
public sealed partial class ImportPreviewWindow : Window
{
    public ImportPreviewWindow(ImportParseResult parseResult, string targetBundleName)
    {
        InitializeComponent();

        RowsGrid.ItemsSource = new ObservableCollection<RowPresentation>(
            parseResult.Rows.Select(RowPresentation.From));

        var valid = parseResult.ValidCount;
        var invalid = parseResult.Rows.Count - valid;

        SummaryText.Text =
            $"الحزمة الهدف: {targetBundleName}\n" +
            $"صفوف صالحة: {valid} — صفوف غير صالحة (سيتم تخطيها): {invalid}" +
            (invalid > 0 ? " — الأخطاء بالأحمر في عمود الحالة." : ".");

        ConfirmButton.IsEnabled = valid > 0;
        if (valid == 0)
        {
            SummaryText.Text += "\nلا توجد صفوف صالحة للاستيراد.";
        }
    }

    public IReadOnlyList<BundleItem>? ResultItems { get; private set; }

    public bool ReplaceExisting => ReplaceRadio.IsChecked == true;

    public int SkippedCount { get; private set; }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var allRows = RowsGrid.ItemsSource.OfType<RowPresentation>().ToList();
        var validRows = allRows.Where(r => r.IsValid).ToList();
        ResultItems = RowMapper.ToItems(validRows.Select(r => r.Source).ToList());
        SkippedCount = allRows.Count - validRows.Count;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>Read-only presentation row for the preview grid.</summary>
    public sealed class RowPresentation
    {
        public static RowPresentation From(ParsedRow row) => new()
        {
            Source = row,
            SourceLineNumber = row.SourceLineNumber,
            ProductCode = row.ProductCode,
            ProductName = row.ProductName,
            QuantityRaw = row.QuantityRaw,
            PriceRaw = row.PriceRaw.Length > 0 ? row.PriceRaw : "—",
            StatusText = row.IsValid ? "✓ جاهز للاستيراد" : "✗ " + string.Join(" | ", row.Errors),
            IsValid = row.IsValid
        };

        public ParsedRow Source { get; init; } = null!;

        public int SourceLineNumber { get; init; }

        public string ProductCode { get; init; } = string.Empty;

        public string ProductName { get; init; } = string.Empty;

        public string QuantityRaw { get; init; } = string.Empty;

        public string PriceRaw { get; init; } = string.Empty;

        public string StatusText { get; init; } = string.Empty;

        public bool IsValid { get; init; }
    }
}
