using Xunit;

namespace NFCeHub.Core.Tests;

public class ListReaderTests
{
    [Fact]
    public void ReadsAccessKeysAndIgnoresModel55()
    {
        var text = string.Join("\n",
            AccessKeys.Make("65", 3, 128690),
            AccessKeys.Make("65", 3, 128691),
            AccessKeys.Make("55", 1, 999));

        var reading = ListReader.Extract(text);

        Assert.Equal(ReadingSource.AccessKeys, reading.Source);
        Assert.Equal(new[] { new NoteRange(3, 128690, 128691) }, reading.Ranges);
        Assert.Equal(1, reading.NfeCount);
    }

    [Fact]
    public void IgnoresAccessKeysWithWrongCheckDigit()
    {
        var key = AccessKeys.Make("65", 1, 10);
        var broken = key.Substring(0, 43) + ((key[43] - '0' + 1) % 10);

        Assert.True(ListReader.ValidAccessKey(key));
        Assert.False(ListReader.ValidAccessKey(broken));
    }

    [Fact]
    public void ReadsTableWithStartEndAndSeriesColumns()
    {
        var text = "Empresa Exemplo LTDA\nCNPJ 11.222.333/0001-81\nInicial a Final  Série  Espécie\n" +
                   "100 a 120  2  NFC-e\n130 a 130  2  NFC-e\nTotal 22\n";

        var reading = ListReader.Extract(text);

        Assert.Equal(ReadingSource.Table, reading.Source);
        Assert.Equal(new[] { new NoteRange(2, 100, 120), new NoteRange(2, 130, 130) }, reading.Ranges);
    }

    [Fact]
    public void ReadsFreeTextWithSeriesRangesAndLooseNumbers()
    {
        var text = "Relação de cupons\nSérie 3\n128690 a 128700\n128705, 128707\nTotal: 13 notas\n" +
                   "NF-e 55 nº 1234\nPágina 1 de 1\n";

        var reading = ListReader.Extract(text, defaultSeries: 1);

        Assert.Equal(ReadingSource.Text, reading.Source);
        Assert.Equal(new[]
        {
            new NoteRange(3, 128690, 128700),
            new NoteRange(3, 128705, 128705),
            new NoteRange(3, 128707, 128707),
        }, reading.Ranges);
        Assert.Equal(1, reading.NfeCount);
    }

    [Fact]
    public void UsesDefaultSeriesWhenTheFileHasNone()
    {
        var reading = ListReader.Extract("200, 205", defaultSeries: 4);

        Assert.Equal(new[] { new NoteRange(4, 200, 200), new NoteRange(4, 205, 205) }, reading.Ranges);
    }

    [Fact]
    public void DropsHugeRangesWithAWarning()
    {
        var reading = ListReader.Extract("1 a 500000");

        Assert.Empty(reading.Ranges);
        Assert.Contains(reading.Warnings, w => w.Contains("grande demais"));
    }

    [Fact]
    public void WarnsWhenOnlyNfeLinesAreFound()
    {
        var reading = ListReader.Extract("NF-e 1234\nNF-e 1235");

        Assert.Empty(reading.Ranges);
        Assert.Equal(ReadingSource.None, reading.Source);
        Assert.Contains("O arquivo só tem NF-e (modelo 55), não NFC-e.", reading.Warnings);
    }

    [Fact]
    public void ThousandSeparatorsAreJoined()
    {
        var reading = ListReader.Extract("264.371");

        Assert.Equal(new[] { new NoteRange(1, 264371, 264371) }, reading.Ranges);
    }

    [Fact]
    public void HeaderPrefersStartAndEndOverNumber()
    {
        Assert.Equal(new[] { ListColumn.Start, ListColumn.End, ListColumn.Series },
            ListReader.HeaderColumns("numero inicial  numero final  serie"));
        Assert.Equal(new[] { ListColumn.Series, ListColumn.Number }, ListReader.HeaderColumns("serie numero"));
        Assert.Null(ListReader.HeaderColumns("serie 1"));
    }
}
