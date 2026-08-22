using SahelBundleKeyboard.Core.Models;
using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.Infrastructure.Tests.ImportExport;

public class RowMapperTests
{
    private const string TemplateHeader = "كود المنتج (اختياري),اسم المنتج (إجباري),الكمية (إجباري),السعر المخصص (اختياري)";

    [Fact]
    public void ValidRows_MapToItems_PreservingOrderAndDuplicates()
    {
        var csv = TemplateHeader + "\n" +
                  "62901,زيت عافية,1.5,\n" +
                  ",أرز أبو كاس,2.25,17.5\n" +
                  "62901,زيت عافية,1.5,";

        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.False(result.FileLevelError);
        Assert.Equal(3, result.ValidCount);

        var items = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList());
        Assert.Equal(3, items.Count);
        Assert.Equal("زيت عافية", items[0].ProductName);
        Assert.Equal("أرز أبو كاس", items[1].ProductName);
        Assert.Equal("زيت عافية", items[2].ProductName); // duplicate preserved
        Assert.Equal([0, 1, 2], items.Select(i => i.Order));

        Assert.Equal(1.5m, items[0].BaseQuantity);
        Assert.Null(items[0].CustomPrice);          // empty price stays null
        Assert.Equal(17.5m, items[1].CustomPrice);  // custom price parsed
    }

    [Fact]
    public void EnglishHeaders_AreAccepted()
    {
        var csv = "Product Code,Product Name,Quantity,Custom Price\nCODE1,Tea,3,10\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.Equal(0, result.FileLevelError ? 1 : 0);
        Assert.Equal(1, result.ValidCount);
        var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList())[0];
        Assert.Equal("CODE1", item.ProductCode);
        Assert.Equal(3m, item.BaseQuantity);
        Assert.Equal(10m, item.CustomPrice);
    }

    [Fact]
    public void UnrecognizedHeaders_FailWithTemplateGuidance()
    {
        var csv = "colA,colB,colC\nx,y,z\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.True(result.FileLevelError);
        Assert.Contains("القالب", result.FileError);
    }

    [Fact]
    public void InvalidRows_ReportArabicErrorsWithSourceLineNumbers()
    {
        var csv = TemplateHeader + "\n" +
                  "c0,,2,\n" +               // missing name -> error
                  "c1,كمية خاطئة,abc,\n" +  // bad quantity -> error
                  "c2,سالب,-1,\n" +         // qty <= 0 -> error
                  "c3,سعر سالب,1,-4\n" +    // negative price -> error
                  "c4,فاصلة عشرية,\"1,5\",\n"; // comma decimal -> error

        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.Equal(5, result.Rows.Count);

        var row1 = result.Rows[0];
        Assert.False(row1.IsValid);
        Assert.Contains(row1.Errors, e => e.Contains("اسم المنتج مطلوب"));
        Assert.Equal(2, row1.SourceLineNumber);

        Assert.Contains(result.Rows[1].Errors, e => e.Contains("الكمية مطلوبة"));
        Assert.Contains(result.Rows[2].Errors, e => e.Contains("أكبر من صفر"));
        Assert.Contains(result.Rows[3].Errors, e => e.Contains("لا يمكن أن يكون سالباً"));
        Assert.Contains(result.Rows[4].Errors, e => e.Contains("رقماً عشرياً"));

        Assert.Equal(0, result.ValidCount);
    }

    [Fact]
    public void EmptyAndWhitespaceLines_AreSkipped()
    {
        var csv = TemplateHeader + "\n" +
                  "c,صنف,1,\n" +
                  ",,,\n" +
                  "   ,   ,   ,\n" +
                  "c2,صنف2,2,\n";

        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(2, result.ValidCount);
    }

    [Theory]
    [InlineData("اسم الصنف,كمية,السعر", "دولاب بقاعدة,1,1500", 1)]          // priceoffer layout
    [InlineData("رقم الصنف,اسم الصنف,الوحدة,الكمية", "75,كارت فاز,,1", 1)]   // transfer layout: no price col
    [InlineData("رقم الصنف,اسم الصنف,الوحدة,الكمية,السعر,الإجمالى", "327,مسامير تثبيت ابيض,,16848,0.1697,2859.1", 0.1697)] // buy layout
    public void SahelExportHeaders_AreRecognized(string header, string dataRow, double expectedPrice)
    {
        var csv = header + "\n" + dataRow + "\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.False(result.FileLevelError, result.FileError);
        Assert.Equal(1, result.ValidCount);

        var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList())[0];
        if (expectedPrice == 1 && !dataRow.Contains(",") == false)
        {
            // transfer variant has no price column at all
        }

        Assert.True(item.BaseQuantity > 0);
    }

    [Fact]
    public void TransferLayout_WithoutPriceColumn_ImportsNullPrices()
    {
        var csv = "رقم الصنف,اسم الصنف,الوحدة,الكمية\n75,كارت فاز,,1\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.False(result.FileLevelError, result.FileError);
        var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList()).Single();
        Assert.Null(item.CustomPrice);
        Assert.Equal("كارت فاز", item.ProductName);
        Assert.Equal("75", item.ProductCode);
        Assert.Equal(1m, item.BaseQuantity);
    }

    [Fact]
    public void BuyLayout_MapsAllSahelColumns_IgnoringTotalsAndUnits()
    {
        var csv = "الإجمالى,السعر,الكمية,الوحدة,اسم الصنف,رقم الصنف\n" +
                  "2859.1056,0.1697,16848,,مسامير تثبيت ابيض,327\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.False(result.FileLevelError, result.FileError);
        var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList()).Single();
        Assert.Equal("327", item.ProductCode);
        Assert.Equal("مسامير تثبيت ابيض", item.ProductName);
        Assert.Equal(16848m, item.BaseQuantity);
        Assert.Equal(0.1697m, item.CustomPrice);
    }

    [Fact]
    public void TitleAndMetaRowsAboveHeader_AreSkipped_LikeRealSahelExports()
    {
        var csv =
            "اسم المشروع\n" +
            "عرض أسعار\n" +
            "46239,التاريخ,,3,رقم الفاتورة\n" +
            "0.75021,الوقت,,مدير النظام,كاشير\n" +
            "\n" +
            "الإجمالى,السعر,كمية,اسم الصنف\n" +
            "1500,1500,1,دولاب بقاعدة\n" +
            "282.7,0.55,514,ترنش ابيض\n";

        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));

        Assert.False(result.FileLevelError, result.FileError);
        Assert.Equal(2, result.ValidCount);

        var items = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList());
        Assert.Equal("دولاب بقاعدة", items[0].ProductName);
        Assert.Equal(1m, items[0].BaseQuantity);
        Assert.Equal(1500m, items[0].CustomPrice);
    }

    [Fact]
    public void ArabicIndicDigits_InQuantities_AreAccepted()
    {
        var csv = TemplateHeader + "\n,صنف,٢٫٥,\n";
        var result = RowMapper.FromGridRows(CsvParser.Parse(csv));
        Assert.Equal(1, result.ValidCount);
        var item = RowMapper.ToItems(result.Rows.Where(r => r.IsValid).ToList())[0];
        Assert.Equal(2.5m, item.BaseQuantity);
    }

    [Fact]
    public void ToItems_NeverMutatesValues_UsesTrimmedText()
    {
        var rows = new List<ParsedRow>
        {
            new(2, "  CODE9  ", "  صنف مع مسافات ", "2.50", "")
        };

        var items = RowMapper.ToItems(rows);
        Assert.Single(items);
        Assert.Equal("CODE9", items[0].ProductCode);
        Assert.Equal("صنف مع مسافات", items[0].ProductName);
        Assert.Equal(2.50m, items[0].BaseQuantity);
        Assert.Null(items[0].CustomPrice);
    }
}
