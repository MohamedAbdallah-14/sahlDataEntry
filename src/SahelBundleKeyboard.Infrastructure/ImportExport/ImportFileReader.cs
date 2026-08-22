using System.IO;
using System.Text;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

/// <summary>Parses .csv and .xlsx files into validated import rows.</summary>
public sealed class ImportFileReader
{
    public ImportParseResult Read(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ImportParseResult.Fail("الملف غير موجود.");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        try
        {
            return extension switch
            {
                ".csv" => ReadCsv(filePath),
                ".xlsx" => RowMapper.FromGridRows(XlsxReader.ReadRows(filePath)),
                _ => ImportParseResult.Fail("صيغة الملف غير مدعومة. استخدم ملفات .xlsx أو .csv فقط.")
            };
        }
        catch (InvalidDataException ex)
        {
            return ImportParseResult.Fail("تعذر فتح ملف Excel: " + ex.Message);
        }
        catch (IOException ex)
        {
            return ImportParseResult.Fail("تعذر قراءة الملف: تأكد أنه غير مفتوح في برنامج آخر. (" + ex.GetType().Name + ")");
        }
    }

    private static ImportParseResult ReadCsv(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);

        string content;
        try
        {
            // Detect and strip the UTF-8 BOM; strict decoding catches legacy encodings.
            content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return ImportParseResult.Fail(
                "ترميز الملف غير مدعوم. أعد حفظ الملف بصيغة CSV من نوع UTF-8 ثم حاول مرة أخرى.");
        }

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        return RowMapper.FromGridRows(CsvParser.Parse(content));
    }
}
