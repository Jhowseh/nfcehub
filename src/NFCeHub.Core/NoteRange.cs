using System;
using System.Collections.Generic;
using System.Linq;

namespace NFCeHub.Core;

public readonly struct NoteRange : IEquatable<NoteRange>, IComparable<NoteRange>
{
    public NoteRange(int series, long start, long end)
    {
        Series = series;
        Start = start;
        End = end;
    }

    public int Series { get; }

    public long Start { get; }

    public long End { get; }

    public long Count => End - Start + 1;

    public IEnumerable<long> Numbers()
    {
        for (var n = Start; n <= End; n++)
        {
            yield return n;
        }
    }

    public string Label() => Start == End
        ? $"Série {Series} · nº {Start}"
        : $"Série {Series} · nº {Start} a {End}";

    public int CompareTo(NoteRange other)
    {
        var bySeries = Series.CompareTo(other.Series);
        if (bySeries != 0)
        {
            return bySeries;
        }
        var byStart = Start.CompareTo(other.Start);
        return byStart != 0 ? byStart : End.CompareTo(other.End);
    }

    public bool Equals(NoteRange other) => Series == other.Series && Start == other.Start && End == other.End;

    public override bool Equals(object obj) => obj is NoteRange other && Equals(other);

    public override int GetHashCode() => (Series * 397) ^ Start.GetHashCode() ^ (End.GetHashCode() * 31);

    public override string ToString() => Label();

    public static bool operator ==(NoteRange a, NoteRange b) => a.Equals(b);

    public static bool operator !=(NoteRange a, NoteRange b) => !a.Equals(b);
}

public static class Ranges
{
    public static List<NoteRange> Merge(IEnumerable<NoteRange> ranges)
    {
        var merged = new List<NoteRange>();
        foreach (var r in ranges.OrderBy(x => x))
        {
            if (merged.Count > 0)
            {
                var last = merged[merged.Count - 1];
                if (last.Series == r.Series && r.Start <= last.End + 1)
                {
                    merged[merged.Count - 1] = new NoteRange(r.Series, last.Start, Math.Max(last.End, r.End));
                    continue;
                }
            }
            merged.Add(r);
        }
        return merged;
    }

    public static List<NoteRange> FromNumbers(IEnumerable<(int Series, long Number)> pairs) =>
        Merge(pairs.Select(p => new NoteRange(p.Series, p.Number, p.Number)));

    public static long CountNotes(IEnumerable<NoteRange> ranges) => Merge(ranges).Sum(r => r.Count);

    public static string Summary(IEnumerable<NoteRange> input, int limit = 8)
    {
        var ranges = Merge(input);
        var allSeries = ranges.Select(r => r.Series).Distinct().OrderBy(s => s).ToList();
        var lines = new List<string>();
        if (allSeries.Count > 1)
        {
            foreach (var s in allSeries)
            {
                var ofSeries = ranges.Where(r => r.Series == s).ToList();
                var start = ofSeries[0].Start;
                var end = ofSeries[ofSeries.Count - 1].End;
                lines.Add($"Série {s}: {CountNotes(ofSeries)} NFC-e (nº {start}{(end != start ? $" a {end}" : "")})");
            }
            lines.Add("");
        }
        lines.AddRange(ranges.Take(limit).Select(r => r.Label()));
        if (ranges.Count > limit)
        {
            lines.Add($"… e mais {ranges.Count - limit} faixa(s)");
        }
        return string.Join("\n", lines);
    }
}
