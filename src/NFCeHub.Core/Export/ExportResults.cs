using System;
using System.Collections.Generic;
using System.Linq;

namespace NFCeHub.Core;

public enum NoteStatus
{
    Authorized,
    Cancelled,
    Unauthorized,
    NotFound,
    Error,
}

public static class NoteStatuses
{
    public static readonly IReadOnlyList<NoteStatus> All = (NoteStatus[])Enum.GetValues(typeof(NoteStatus));

    public static string Describe(this NoteStatus status) => status switch
    {
        NoteStatus.Authorized => "autorizada",
        NoteStatus.Cancelled => "cancelada",
        NoteStatus.Unauthorized => "sem autorização",
        NoteStatus.NotFound => "não encontrada no banco",
        _ => "erro",
    };

    public static bool IsProblem(this NoteStatus status) =>
        status == NoteStatus.Unauthorized || status == NoteStatus.NotFound || status == NoteStatus.Error;
}

public sealed class ExportOptions
{
    public bool IncludeCancellation { get; set; } = true;

    public bool IncludeUnauthorized { get; set; }

    public bool ByMonth { get; set; }
}

public sealed class NoteResult
{
    public NoteResult(int series, long number)
    {
        Series = series;
        Number = number;
    }

    public int Series { get; }

    public long Number { get; }

    public NoteStatus Status { get; set; } = NoteStatus.NotFound;

    public List<string> Files { get; } = new();

    public string Detail { get; set; } = "";
}

public sealed class ExportSummary
{
    public ExportSummary(List<NoteResult> results, string output, bool interrupted)
    {
        Results = results;
        Output = output;
        Interrupted = interrupted;
    }

    public List<NoteResult> Results { get; }

    public string Output { get; }

    public bool Interrupted { get; }

    public int FilesWritten => Results.Sum(r => r.Files.Count);

    public int Count(NoteStatus status) => Results.Count(r => r.Status == status);

    public List<NoteResult> Problems() => Results.Where(r => r.Status.IsProblem()).ToList();
}
