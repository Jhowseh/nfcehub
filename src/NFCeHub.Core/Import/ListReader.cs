using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NFCeHub.Core;

public enum ListColumn
{
    Start,
    End,
    Series,
    Number,
    Model,
}

public static class ListReader
{
    private const long MaxRange = 100_000;
    private const string Before = @"(?<!\d)(?<!\d[.,])";
    private const string After = @"(?!\d)(?![.,]\d)";

    private static readonly Regex AccessKey = new(@"(?<!\d)(\d{4}(?:[ .\-]?\d{4}){10})(?!\d)");
    private static readonly Regex[] Noise =
    {
        new(@"\d{2}\.?\d{3}\.?\d{3}/\d{4}-?\d{2}"),
        new(@"\d{3}\.\d{3}\.\d{3}-\d{2}"),
        new(@"\d{1,2}/\d{1,2}/\d{2,4}"),
        new(@"\d{4}-\d{2}-\d{2}(?:t[\d:.\-+]+)?"),
        new(@"\d{1,2}:\d{2}(?::\d{2})?"),
        new(@"(?:r\$\s*)?\d{1,3}(?:\.\d{3})*,\d{2}(?!\d)"),
        new(@"\d+(?:[.,]\d+)?\s*%"),
        new(@"pag(?:ina)?\.?:?\s*\d+\s*(?:de|/)\s*\d+"),
        new(@"(?<![\d/])\d{1,3}\s*/\s*\d{1,3}(?![\d/])"),
        new(@"\d{20,}"),
        new(@"\(\s*\d+\s*\)"),
    };
    private static readonly Regex Thousands = new(@"(?<![\d.,])(\d{1,3}(?:\.\d{3})+)(?![\d.,])");
    private static readonly Regex SeriesRegex = new(@"\bserie(?:\s+d[aeo]s?\s+[a-z\-]+)?\s*(?:n[o.]?\s*)?[:=\-]?\s*0*(\d{1,3})\b");
    private static readonly Regex ModelRegex = new(@"\bmod(?:elo)?\.?(?:\s+d[aeo]s?\s+[a-z\-]+)?\s*[:=\-]?\s*(55|65)\b");
    private static readonly Regex StartEnd = new(@"inicial\D{0,20}?(\d{1,9})\D{0,40}?final\D{0,20}?(\d{1,9})");
    private static readonly Regex RangeRegex = new(Before + @"(\d{1,9})\s*(?:a|ate|-|–|—|\.\.|/)\s*(\d{1,9})" + After);
    private static readonly Regex Integer = new(Before + @"(\d{1,9})" + After);
    private static readonly Regex Skip = new(@"\b(total|totais|quantidade|qtde?|soma|cnpj|cpf|inscricao|telefone|fone|cep)\b");
    private static readonly Regex Nfe = new(@"\bnf-?e\b|\bmod(?:elo)?\.?\s*55\b");
    private static readonly Regex Nfce = new(@"\bnfc-?e\b|\bmod(?:elo)?\.?\s*65\b|\bcupom\b");
    private static readonly Regex AnyDigit = new(@"\d");

    private static readonly (ListColumn Column, Regex Pattern)[] Headers =
    {
        (ListColumn.Start, new Regex(@"\b(?:inicial|inicio|ini)\b\.?")),
        (ListColumn.End, new Regex(@"\b(?:final|fim)\b\.?")),
        (ListColumn.Series, new Regex(@"\b(?:series?|ser)\b\.?")),
        (ListColumn.Number, new Regex(@"\b(?:numero|num|nnf|documento|doc)\b\.?")),
        (ListColumn.Model, new Regex(@"\b(?:modelo|mod)\b\.?")),
    };

    private enum RowKind
    {
        Ignored,
        Nfe,
        Nfce,
    }

    public static Reading ReadFile(string path, int defaultSeries = 1)
    {
        var data = File.ReadAllBytes(path);
        var reading = Extract(DocumentText.Read(data), defaultSeries);
        if (reading.Ranges.Count == 0 && DocumentText.IsPdf(data))
        {
            reading.Warnings.Add("Se o PDF for uma imagem escaneada, não há texto para ler.");
        }
        return reading;
    }

    public static Reading Extract(string text, int defaultSeries = 1)
    {
        var (pairs, nfeCount) = FromAccessKeys(text);
        if (pairs.Count > 0)
        {
            return new Reading(Ranges.FromNumbers(pairs), ReadingSource.AccessKeys, nfeCount);
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(Clean).ToList();
        Reading reading;
        if (lines.Any(l => HeaderColumns(l) != null))
        {
            var (ranges, warnings, tableNfe) = FromTable(lines, defaultSeries);
            reading = new Reading(ranges, ranges.Count > 0 ? ReadingSource.Table : ReadingSource.None, nfeCount + tableNfe, warnings);
        }
        else
        {
            var (ranges, warnings, textNfe) = FromFreeText(lines, defaultSeries);
            reading = new Reading(ranges, ranges.Count > 0 ? ReadingSource.Text : ReadingSource.None, nfeCount + textNfe, warnings);
        }
        if (reading.NfeCount > 0 && reading.Ranges.Count == 0)
        {
            reading.Warnings.Add("O arquivo só tem NF-e (modelo 55), não NFC-e.");
        }
        return reading;
    }

    public static string StripAccents(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().ToLowerInvariant();
    }

    public static bool ValidAccessKey(string key)
    {
        if (key.Length != 44 || !key.All(char.IsDigit))
        {
            return false;
        }
        int total = 0, weight = 2;
        for (var i = 42; i >= 0; i--)
        {
            total += (key[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }
        var remainder = total % 11;
        var checkDigit = remainder < 2 ? 0 : 11 - remainder;
        return checkDigit == key[43] - '0';
    }

    public static List<ListColumn> HeaderColumns(string line)
    {
        if (AnyDigit.IsMatch(line))
        {
            return null;
        }
        var found = new List<(int Position, ListColumn Column)>();
        foreach (var (column, pattern) in Headers)
        {
            var m = pattern.Match(line);
            if (m.Success)
            {
                found.Add((m.Index, column));
            }
        }
        var columns = found.OrderBy(f => f.Position).Select(f => f.Column).ToList();
        if (columns.Contains(ListColumn.Start) && columns.Contains(ListColumn.End))
        {
            columns.Remove(ListColumn.Number);
        }
        else if (columns.Contains(ListColumn.Number))
        {
            columns.RemoveAll(c => c == ListColumn.Start || c == ListColumn.End);
        }
        else
        {
            return null;
        }
        return columns.Count >= 2 || (columns.Count == 1 && columns[0] == ListColumn.Number) ? columns : null;
    }

    private static (List<(int, long)> Pairs, int NfeCount) FromAccessKeys(string text)
    {
        var pairs = new List<(int, long)>();
        var nfeCount = 0;
        foreach (Match m in AccessKey.Matches(text))
        {
            var key = Regex.Replace(m.Groups[1].Value, @"\D", "");
            if (!ValidAccessKey(key))
            {
                continue;
            }
            if (key.Substring(20, 2) == "65")
            {
                pairs.Add((int.Parse(key.Substring(22, 3)), long.Parse(key.Substring(25, 9))));
            }
            else
            {
                nfeCount++;
            }
        }
        return (pairs, nfeCount);
    }

    private static string Clean(string raw)
    {
        var line = StripAccents(raw);
        foreach (var r in Noise)
        {
            line = r.Replace(line, " ");
        }
        return Thousands.Replace(line, m => m.Groups[1].Value.Replace(".", ""));
    }

    private static List<long> Integers(string line) =>
        Integer.Matches(line).Cast<Match>().Select(m => long.Parse(m.Groups[1].Value)).ToList();

    private static RowKind TableRow(List<long> numbers, List<ListColumn> columns, int series, out NoteRange range)
    {
        range = default;
        if (numbers.Count < columns.Count)
        {
            return RowKind.Ignored;
        }
        var values = new Dictionary<ListColumn, long>();
        for (var i = 0; i < columns.Count; i++)
        {
            values[columns[i]] = numbers[i];
        }
        if (values.TryGetValue(ListColumn.Model, out var model))
        {
            if (model == 55)
            {
                return RowKind.Nfe;
            }
            if (model != 65)
            {
                return RowKind.Ignored;
            }
        }
        var rowSeries = values.TryGetValue(ListColumn.Series, out var s) ? s : series;
        if (rowSeries > 999)
        {
            return RowKind.Ignored;
        }
        if (values.TryGetValue(ListColumn.Number, out var number))
        {
            range = new NoteRange((int)rowSeries, number, number);
            return RowKind.Nfce;
        }
        var start = values[ListColumn.Start];
        var end = values[ListColumn.End];
        if (end < start || end - start >= MaxRange)
        {
            return RowKind.Ignored;
        }
        range = new NoteRange((int)rowSeries, start, end);
        return RowKind.Nfce;
    }

    private static (List<NoteRange>, List<string>, int) FromTable(List<string> lines, int defaultSeries)
    {
        var ranges = new List<NoteRange>();
        var warnings = new List<string>();
        var nfeCount = 0;
        List<ListColumn> columns = null;
        var series = defaultSeries;
        var model = 65;
        foreach (var original in lines)
        {
            var line = original;
            if (line.Trim().Length == 0)
            {
                continue;
            }
            var newColumns = HeaderColumns(line);
            if (newColumns != null)
            {
                columns = newColumns;
                continue;
            }
            if (Skip.IsMatch(line) || ReadModel(line, ref model))
            {
                continue;
            }
            if (columns == null || !columns.Contains(ListColumn.Series))
            {
                line = ReadSeries(line, ref series);
            }
            if (columns == null)
            {
                continue;
            }
            if ((Nfe.IsMatch(line) && !Nfce.IsMatch(line)) || (model == 55 && !columns.Contains(ListColumn.Model)))
            {
                if (Integer.IsMatch(line))
                {
                    nfeCount++;
                }
                continue;
            }
            if (columns.Count == 1 && columns[0] == ListColumn.Number && ReadRanges(line, series, ranges, warnings))
            {
                continue;
            }
            switch (TableRow(Integers(line), columns, series, out var range))
            {
                case RowKind.Nfe:
                    nfeCount++;
                    break;
                case RowKind.Nfce:
                    ranges.Add(range);
                    break;
            }
        }
        return (Ranges.Merge(ranges), warnings, nfeCount);
    }

    private static (List<NoteRange>, List<string>, int) FromFreeText(List<string> lines, int defaultSeries)
    {
        var ranges = new List<NoteRange>();
        var warnings = new List<string>();
        var nfeCount = 0;
        var series = defaultSeries;
        var model = 65;
        foreach (var original in lines)
        {
            var line = original;
            if (line.Trim().Length == 0 || !AnyDigit.IsMatch(line) || Skip.IsMatch(line) || ReadModel(line, ref model))
            {
                continue;
            }
            if ((Nfe.IsMatch(line) && !Nfce.IsMatch(line)) || model == 55)
            {
                nfeCount++;
                continue;
            }
            line = ReadSeries(line, ref series);
            if (ReadRanges(line, series, ranges, warnings))
            {
                continue;
            }
            ranges.AddRange(Integers(line).Select(n => new NoteRange(series, n, n)));
        }
        return (Ranges.Merge(ranges), warnings, nfeCount);
    }

    private static bool ReadModel(string line, ref int model)
    {
        var m = ModelRegex.Match(line);
        if (!m.Success || Integer.IsMatch(Remove(line, m)))
        {
            return false;
        }
        model = int.Parse(m.Groups[1].Value);
        return true;
    }

    private static string ReadSeries(string line, ref int series)
    {
        var m = SeriesRegex.Match(line);
        if (!m.Success)
        {
            return line;
        }
        series = int.Parse(m.Groups[1].Value);
        return Remove(line, m);
    }

    private static string Remove(string line, Match m) =>
        line.Substring(0, m.Index) + " " + line.Substring(m.Index + m.Length);

    private static bool ReadRanges(string line, int series, List<NoteRange> ranges, List<string> warnings)
    {
        foreach (var regex in new[] { StartEnd, RangeRegex })
        {
            var matched = false;
            foreach (Match m in regex.Matches(line))
            {
                var start = long.Parse(m.Groups[1].Value);
                var end = long.Parse(m.Groups[2].Value);
                if (end < start)
                {
                    continue;
                }
                matched = true;
                if (end - start >= MaxRange)
                {
                    warnings.Add($"Faixa {start} a {end} ignorada (grande demais).");
                    continue;
                }
                ranges.Add(new NoteRange(series, start, end));
            }
            if (matched)
            {
                return true;
            }
        }
        return false;
    }
}
