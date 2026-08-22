using SahelBundleKeyboard.Infrastructure.ImportExport;

namespace SahelBundleKeyboard.Infrastructure.Tests.ImportExport;

public class CsvParserTests
{
    [Fact]
    public void ParsesSimpleRows()
    {
        var rows = CsvParser.Parse("a,b,c\n1,2,3");
        Assert.Equal(2, rows.Count);
        Assert.Equal(["a", "b", "c"], rows[0]);
        Assert.Equal(["1", "2", "3"], rows[1]);
    }

    [Fact]
    public void ParsesQuotedFieldsWithCommasAndQuotes()
    {
        var rows = CsvParser.Parse("\"hello, world\",\"say \"\"hi\"\"\",x");
        var row = Assert.Single(rows);
        Assert.Equal("hello, world", row[0]);
        Assert.Equal("say \"hi\"", row[1]);
        Assert.Equal("x", row[2]);
    }

    [Fact]
    public void ParsesEmbeddedNewlinesInsideQuotes()
    {
        var rows = CsvParser.Parse("\"line1\nline2\",b\r\nc,d");
        Assert.Equal(2, rows.Count);
        Assert.Equal("line1\nline2", rows[0][0]);
    }

    [Fact]
    public void HandlesCrlfAndLoneLfAndTrailingRowWithoutNewline()
    {
        Assert.Equal(3, CsvParser.Parse("1\n2\n3").Count);
        Assert.Equal(3, CsvParser.Parse("1\r\n2\r3").Count);
        Assert.Equal(["b"], CsvParser.Parse("a\nb") is { Count: 2 } r ? (IReadOnlyList<string>)r[1] : []);
    }
}
