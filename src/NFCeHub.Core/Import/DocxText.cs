using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace NFCeHub.Core;

public static class DocxText
{
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string CompatibilityNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    public static bool IsZip(byte[] data) =>
        data.Length >= 4 && data[0] == 'P' && data[1] == 'K' && data[2] == 3 && data[3] == 4;

    public static string Read(byte[] data)
    {
        using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').ToLowerInvariant() == "word/document.xml")
                    ?? throw new InvalidDataException("O arquivo compactado não é um documento do Word (.docx).");
        using var stream = entry.Open();
        return FromXml(stream);
    }

    public static string FromXml(Stream stream)
    {
        var text = new StringBuilder();
        var path = new Stack<string>();
        var skipDepth = 0;
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                var name = Name(reader);
                var empty = reader.IsEmptyElement;
                if (skipDepth > 0 || name == "mc:Fallback" || name == "w:delText" || name == "w:instrText")
                {
                    if (!empty)
                    {
                        skipDepth++;
                    }
                    continue;
                }
                var parent = path.Count > 0 ? path.Peek() : "";
                switch (name)
                {
                    case "w:tab" when parent == "w:r":
                        text.Append('\t');
                        break;
                    case "w:br":
                    case "w:cr":
                        text.Append('\n');
                        break;
                    case "w:noBreakHyphen":
                        text.Append('-');
                        break;
                }
                if (empty)
                {
                    Close(name, path, text);
                }
                else
                {
                    path.Push(name);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (skipDepth > 0)
                {
                    skipDepth--;
                    continue;
                }
                Close(path.Count > 0 ? path.Pop() : "", path, text);
            }
            else if ((reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.SignificantWhitespace)
                     && skipDepth == 0 && path.Count > 0 && path.Peek() == "w:t")
            {
                text.Append(reader.Value);
            }
        }
        return text.ToString();
    }

    private static string Name(XmlReader reader) => reader.NamespaceURI switch
    {
        WordNamespace => "w:" + reader.LocalName,
        CompatibilityNamespace => "mc:" + reader.LocalName,
        _ => reader.LocalName,
    };

    private static void Close(string name, Stack<string> path, StringBuilder text)
    {
        switch (name)
        {
            case "w:p":
                text.Append(path.Contains("w:tc") ? ' ' : '\n');
                break;
            case "w:tr":
                text.Append('\n');
                break;
            case "w:tc":
                text.Append('\t');
                break;
        }
    }
}
