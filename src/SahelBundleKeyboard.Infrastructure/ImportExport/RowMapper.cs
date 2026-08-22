using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Core.Sequencing;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

public sealed record ParsedRow(int SourceLineNumber, string ProductCode, string ProductName, string QuantityRaw, string PriceRaw)
{
    public List<string> Errors { get; } = [];

    public bool IsValid => Errors.Count == 0;
}

public sealed record ImportParseResult(IReadOnlyList<ParsedRow> Rows, bool FileLevelError, string? FileError)
{
    public int ValidCount => Rows.Count(r => r.IsValid);

    public static ImportParseResult Fail(string error) => new([], true, error);
}

/// <summary>
/// Maps parsed spreadsheet rows (any source) to bundle items with per-row Arabic errors.
/// The first row must be a header row with recognizable column names (Arabic or English).
/// </summary>
public static class RowMapper
{
    private static readonly string[] CodeHeaders =
    [
        "product code", "code", "barcode", "sku",
        "كود المنتج (اختياري)", "كود المنتج", "الكود", "الباركود", "باركود", "رمز المنتج"
    ];

    private static readonly string[] NameHeaders =
    [
        "product name", "name", "product",
        "اسم المنتج (إجباري)", "اسم المنتج", "الاسم", "المنتج"
    ];

    private static readonly string[] QuantityHeaders =
    [
        "quantity", "qty",
        "الكمية (إجباري)", "الكمية"
    ];

    private static readonly string[] PriceHeaders =
    [
        "custom price", "price", "unit price",
        "السعر المخصص (اختياري)", "السعر المخصص", "السعر", "السعر للوحدة"
    ];

    public static ImportParseResult FromGridRows(IReadOnlyList<IReadOnlyList<string>> gridRows, int startLineNumber = 2)
    {
        if (gridRows.Count == 0 || IsEffectivelyEmpty(gridRows[0]))
        {
            return ImportParseResult.Fail("الملف فارغ أو لا يحتوي صفوفاً.");
        }

        var header = gridRows[0].Select(NormalizeHeader).ToList();

        var codeIdx = FindHeader(header, CodeHeaders);
        var nameIdx = FindHeader(header, NameHeaders);
        var qtyIdx = FindHeader(header, QuantityHeaders);
        var priceIdx = FindHeader(header, PriceHeaders);

        if (nameIdx < 0 || qtyIdx < 0)
        {
            return ImportParseResult.Fail(
                "لم يتم التعرف على عناوين الأعمدة. استخدم القالب المُصدَّر من البرنامج أو استخدم أسماء الأعمدة القياسية.");
        }

        var rows = new List<ParsedRow>();

        for (var r = 1; r < gridRows.Count; r++)
        {
            var cells = gridRows[r];
            var lineNumber = startLineNumber + r - 1;

            // Skip fully empty lines silently.
            if (IsEffectivelyEmpty(cells))
            {
                continue;
            }

            var row = new ParsedRow(
                lineNumber,
                CellAt(cells, codeIdx),
                CellAt(cells, nameIdx),
                CellAt(cells, qtyIdx),
                priceIdx >= 0 ? CellAt(cells, priceIdx) : string.Empty);

            MapRowInto(row);
            rows.Add(row);
        }

        return new ImportParseResult(rows, false, null);
    }

    /// <summary>Validates one row and reports errors; never mutates the stored values.</summary>
    private static void MapRowInto(ParsedRow row)
    {
        if (row.ProductName.Trim().Length == 0)
        {
            row.Errors.Add("اسم المنتج مطلوب.");
        }
        else if (row.ProductName.Trim().Length > BundleLimits.MaxProductNameLength)
        {
            row.Errors.Add($"اسم المنتج طويل جداً (الحد الأقصى {BundleLimits.MaxProductNameLength} حرفاً).");
        }

        if (row.ProductCode.Trim().Length > BundleLimits.MaxProductCodeLength)
        {
            row.Errors.Add($"كود المنتج طويل جداً (الحد الأقصى {BundleLimits.MaxProductCodeLength} حرفاً).");
        }

        if (!QuantityFormatter.TryParse(row.QuantityRaw, out var quantity))
        {
            row.Errors.Add("الكمية مطلوبة ويجب أن تكون رقماً عشرياً صحيحاً مثل 1.5 (استخدم النقطة الفاصلة).");
        }
        else if (quantity <= 0m)
        {
            row.Errors.Add("الكمية يجب أن تكون أكبر من صفر.");
        }

        var priceText = row.PriceRaw.Trim();
        if (priceText.Length > 0)
        {
            if (!QuantityFormatter.TryParse(priceText, out var price))
            {
                row.Errors.Add("السعر المخصص يجب أن يكون رقماً صحيحاً أو اتركه فارغاً (مثال: 12.5).");
            }
            else if (price < 0m)
            {
                row.Errors.Add("السعر المخصص لا يمكن أن يكون سالباً.");
            }
        }
    }

    /// <summary>Converts validated rows into bundle items in source order with sequential orders.</summary>
    public static IReadOnlyList<BundleItem> ToItems(IReadOnlyList<ParsedRow> validRows, int startingOrder = 0)
    {
        var items = new List<BundleItem>(validRows.Count);

        foreach (var row in validRows)
        {
            _ = QuantityFormatter.TryParse(row.QuantityRaw, out var quantity);
            decimal? price = null;
            if (row.PriceRaw.Trim().Length > 0 && QuantityFormatter.TryParse(row.PriceRaw, out var parsedPrice))
            {
                price = parsedPrice;
            }

            items.Add(new BundleItem
            {
                ProductCode = row.ProductCode.Trim(),
                ProductName = row.ProductName.Trim(),
                BaseQuantity = quantity,
                CustomPrice = price,
                Order = startingOrder + items.Count
            });
        }

        return items;
    }

    private static int FindHeader(List<string> headerCells, string[] candidates)
    {
        for (var i = 0; i < headerCells.Count; i++)
        {
            if (candidates.Contains(headerCells[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string cell) =>
        QuantityFormatter.NormalizeDigits(cell.Trim()).ToLowerInvariant().Replace("  ", " ");

    private static string CellAt(IReadOnlyList<string> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index].Trim() : string.Empty;

    private static bool IsEffectivelyEmpty(IReadOnlyList<string> cells) =>
        cells.All(c => c.Trim().Length == 0);
}
