using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace NFCeHub.Core;

public static class DocumentText
{
    public static bool IsPdf(string path) =>
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public static string Read(string path) => IsPdf(path) ? Pdf(path) : PlainText(path);

    public static string Pdf(string path)
    {
        var pages = new List<string>();
        using var document = PdfDocument.Open(path);
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

    public static string PlainText(string path)
    {
        var data = File.ReadAllBytes(path);
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
