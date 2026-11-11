using System.Collections.Generic;
using System.Linq;

namespace NFCeHub.Core;

public enum ReadingSource
{
    None,
    AccessKeys,
    Table,
    Text,
}

public sealed class Reading
{
    public Reading(List<NoteRange> ranges, ReadingSource source, int nfeCount, List<string> warnings = null)
    {
        Ranges = ranges;
        Source = source;
        NfeCount = nfeCount;
        Warnings = warnings ?? new List<string>();
    }

    public List<NoteRange> Ranges { get; }

    public ReadingSource Source { get; }

    public int NfeCount { get; }

    public List<string> Warnings { get; }

    public long Total => Core.Ranges.CountNotes(Ranges);

    public List<int> Series => Ranges.Select(r => r.Series).Distinct().OrderBy(s => s).ToList();

    public string Description()
    {
        if (Ranges.Count == 0)
        {
            return "Nenhum número de NFC-e encontrado no arquivo.";
        }
        var parts = new List<string> { $"{Total} NFC-e em {Ranges.Count} faixa(s)" };
        if (Series.Count > 1)
        {
            parts.Add("séries " + string.Join(", ", Series));
        }
        if (Source == ReadingSource.AccessKeys)
        {
            parts.Add("lidas das chaves de acesso");
        }
        if (NfeCount > 0)
        {
            parts.Add($"{NfeCount} linha(s) de NF-e modelo 55 ignorada(s)");
        }
        return string.Join("; ", parts) + ".";
    }
}
