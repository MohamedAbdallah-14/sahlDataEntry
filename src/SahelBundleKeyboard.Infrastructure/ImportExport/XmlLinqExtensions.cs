using System.Xml.Linq;

namespace SahelBundleKeyboard.Infrastructure.ImportExport;

internal static class XmlLinqExtensions
{
    public static string ToStringWithDeclaration(this XElement element)
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + Environment.NewLine + element;
    }
}
