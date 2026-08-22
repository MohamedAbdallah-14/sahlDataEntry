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
        "كود المنتج (اختياري)", "كود المنتج", "الكود", "الباركود", "باركود", "رمز المنتج",
        "رقم الصنف" // Sahel exports (buy/transfer)
    ];

    private static readonly string[] NameHeaders =
    [
        "product name", "name", "product",
        "اسم المنتج (إجباري)", "اسم المنتج", "الاسم", "المنتج",
        "اسم الصنف" // Sahel exports
    ];

    private static readonly string[] QuantityHeaders =
    [
        "quantity", "qty",
        "الكمية (إجباري)", "الكمية", "كمية" // Sahel priceoffer uses bare كمية
    ];

    private static readonly string[] PriceHeaders =
    [
        "custom price", "price", "unit price",
        "السعر المخصص (اختياري)", "السعر المخصص", "السعر", "السعر للوحدة"
    ];

    /// <summary>Max rows scanned to locate the real header (Sahel exports carry title/meta rows first).</summary>
    private const int HeaderSearchDepth = 30;

    public static ImportParseResult FromGridRows(IReadOnlyList<IReadOnlyList<string>> gridRows)
    {
        if (gridRows.Count == 0 || gridRows.All(IsEffectivelyEmpty))
        {
            return ImportParseResult.Fail("الملف فارغ أو لا يحتوي صفوفاً.");
        }

        // Real-world exports (e.g. Sahel priceoffer/buy/transfer) put titles, dates and
        // invoice metadata above the table. Scan for the first row that looks like a header.
        var headerRowIndex = FindHeaderRow(gridRows);
        if (headerRowIndex < 0)
        {
            return ImportParseResult.Fail(
                "لم يتم التعرف على صف عناوين الأعمدة. استخدم القالب المُصدَّر من البرنامج أو أسماء مثل: اسم الصنف، الكمية، السعر.");
        }

        var header = gridRows[headerRowIndex].Select(NormalizeHeader).ToList();

        var codeIdx = FindHeader(header, CodeHeaders);
        var nameIdx = FindHeader(header, NameHeaders);
        var qtyIdx = FindHeader(header, QuantityHeaders);
        var priceIdx = FindHeader(header, PriceHeaders);

        if (nameIdx < 0 || qtyIdx < 0)
        {
            return ImportParseResult.Fail(
                "لم يتم التعرف على عناوين الأعمدة. استخدم القالب المُصدَّر من البرنامج أو أسماء مثل: اسم الصنف، الكمية، السعر.");
        }

        var rows = new List<ParsedRow>();

        for (var r = headerRowIndex + 1; r < gridRows.Count; r++)
        {
            var cells = gridRows[r];
            var lineNumber = r + 1; // one-based spreadsheet/line number for user-facing errors

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

    /// <summary>
    /// Finds the first row containing at least two recognized header names,
    /// including a product-name or quantity column. Returns its index or -1.
    /// </summary>
    internal static int FindHeaderRow(IReadOnlyList<IReadOnlyList<string>> gridRows)
    {
        var depth = Math.Min(gridRows.Count, HeaderSearchDepth);

        for (var i = 0; i < depth; i++)
        {
            if (IsEffectivelyEmpty(gridRows[i]))
            {
                continue;
            }

            var normalized = gridRows[i].Select(NormalizeHeader).ToList();

            var hasName = normalized.Any(c => NameHeaders.Contains(c));
            var hasQty = normalized.Any(c => QuantityHeaders.Contains(c));
            var totalMatches = normalized.Count(c =>
                CodeHeaders.Contains(c) || NameHeaders.Contains(c) ||
                QuantityHeaders.Contains(c) || PriceHeaders.Contains(c));

            if ((hasName || hasQty) && totalMatches >= 2)
            {
                return i;
            }
        }

        return -1;
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
