using System.Linq;
using Xunit;

namespace NFCeHub.Core.Tests;

public class RangesTests
{
    [Fact]
    public void MergeJoinsTouchingAndOverlappingRangesOfTheSameSeries()
    {
        var merged = Ranges.Merge(new[]
        {
            new NoteRange(1, 10, 20),
            new NoteRange(1, 21, 25),
            new NoteRange(1, 15, 30),
            new NoteRange(2, 10, 20),
            new NoteRange(1, 40, 40),
        });

        Assert.Equal(new[] { new NoteRange(1, 10, 30), new NoteRange(1, 40, 40), new NoteRange(2, 10, 20) }, merged);
    }

    [Fact]
    public void CountNotesDoesNotCountOverlapsTwice()
    {
        Assert.Equal(21, Ranges.CountNotes(new[] { new NoteRange(1, 10, 20), new NoteRange(1, 15, 30) }));
    }

    [Fact]
    public void LabelShowsSingleNoteOrRange()
    {
        Assert.Equal("Série 1 · nº 5", new NoteRange(1, 5, 5).Label());
        Assert.Equal("Série 2 · nº 5 a 9", new NoteRange(2, 5, 9).Label());
    }

    [Fact]
    public void NumbersListsEveryNumberInTheRange()
    {
        Assert.Equal(new long[] { 7, 8, 9 }, new NoteRange(1, 7, 9).Numbers().ToArray());
    }

    [Fact]
    public void SummaryGroupsBySeriesWhenThereIsMoreThanOne()
    {
        var summary = Ranges.Summary(new[] { new NoteRange(1, 1, 3), new NoteRange(2, 8, 8) });

        Assert.StartsWith("Série 1: 3 NFC-e (nº 1 a 3)\nSérie 2: 1 NFC-e (nº 8)\n", summary);
    }
}
