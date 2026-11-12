using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NFCeHub.Core;

public static class DocumentText
{
    private static readonly Regex HtmlStart = new(@"^\s*(?:<\?xml[^>]*>\s*)?(?:<!--.*?-->\s*)*<(?:!doctype\s+html|html|head|body|meta)\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlHidden = new(@"<(script|style|head|xml)\b.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex HtmlLineBreak = new(@"<br\b[^>]*>|</(?:p|div|tr|li|h\d|table|blockquote|pre)\s*>", RegexOptions.IgnoreCase);
    private static readonly Regex HtmlCell = new(@"</t[dh]\s*>", RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTag = new(@"<[^>]*>");
    private static readonly Regex Whitespace = new(@"\s+");

    public static bool IsPdf(byte[] data)
    {
        var head = Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 1024));
        return head.Contains("%PDF-");
    }

    public static string Read(string path) => Read(File.ReadAllBytes(path));

    public static string Read(byte[] data)
    {
        if (IsPdf(data))
        {
            return Pdf(data);
        }
        if (RtfText.IsRtf(data))
        {
            return RtfText.Read(data);
        }
        if (DocxText.IsZip(data))
        {
            return DocxText.Read(data);
        }
        if (CompoundFile.IsCompound(data))
        {
            return WordBinaryText.Read(data);
        }
        var text = PlainText(data);
        return HtmlStart.IsMatch(text) ? Html(text) : text;
    }

    public static string Html(string html)
    {
        var text = HtmlHidden.Replace(html, " ");
        text = Whitespace.Replace(text, " ");
        text = HtmlLineBreak.Replace(text, "\n");
        text = HtmlCell.Replace(text, "\t");
        text = HtmlTag.Replace(text, "");
        return WebUtility.HtmlDecode(text).Replace(' ', ' ');
    }

    public static string Pdf(byte[] data)
    {
        var pages = new List<string>();
        using var document = PdfDocument.Open(data);
        foreach (var page in document.GetPages())
        {
            var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text)).ToList();
            var lines = new List<(double Baseline, double Height, List<Word> Words)>();
            foreach (var word in words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left))
            {
                var bottom = word.BoundingBox.Bottom;
                var height = Math.Max(1, word.BoundingBox.Height);
                var index = lines.FindIndex(l => Math.Abs(l.Baseline - bottom) <= Math.Max(2, Math.Min(l.Height, height) * 0.5));
                if (index >= 0)
                {
                    lines[index].Words.Add(word);
                }
                else
                {
                    lines.Add((bottom, height, new List<Word> { word }));
                }
            }
            pages.Add(string.Join("\n", lines
                .OrderByDescending(l => l.Baseline)
                .Select(l => string.Join(" ", l.Words.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)))));
        }
        return string.Join("\n", pages);
    }

    public static string PlainText(byte[] data)
    {
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        }
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
        }
        var offset = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
        try
        {
            return new UTF8Encoding(false, true).GetString(data, offset, data.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(data);
        }
    }
}
