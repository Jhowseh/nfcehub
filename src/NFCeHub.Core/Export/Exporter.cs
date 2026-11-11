using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace NFCeHub.Core;

public static class Exporter
{
    public const string UnauthorizedFolder = "Sem autorizacao";

    private sealed class StoredXml
    {
        public StoredXml(XmlInfo info, byte[] content, XmlRecord record)
        {
            Info = info;
            Content = content;
            Record = record;
        }

        public XmlInfo Info { get; }

        public byte[] Content { get; }

        public XmlRecord Record { get; }
    }

    public static ExportSummary Export(ReadOnlySession db, IEnumerable<NoteRange> requested, string output,
        ExportOptions options = null, Action<int, long, string> progress = null,
        CancellationToken cancel = default)
    {
        options ??= new ExportOptions();
        var ranges = Ranges.Merge(requested);
        var total = Ranges.CountNotes(ranges);
        progress?.Invoke(0, total, "Procurando as notas no banco…");
        var found = SysPdv.FindRecords(db, ranges);
        using var xmlQuery = SysPdv.PrepareXmlQuery(db);

        var results = new List<NoteResult>();
        var done = 0;
        foreach (var range in ranges)
        {
            foreach (var number in range.Numbers())
            {
                if (cancel.IsCancellationRequested)
                {
                    return new ExportSummary(results, output, true);
                }
                var result = new NoteResult(range.Series, number);
                try
                {
                    var records = found.TryGetValue((range.Series, number), out var list) ? list : new List<XmlRecord>();
                    ExportNote(xmlQuery, records, result, output, options);
                }
                catch (Exception e)
                {
                    result.Status = NoteStatus.Error;
                    result.Detail = string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message;
                }
                results.Add(result);
                done++;
                progress?.Invoke(done, total, $"Série {range.Series} · nº {number}: {result.Status.Describe()}");
            }
        }
        return new ExportSummary(results, output, false);
    }

    private static void ExportNote(PreparedQuery xmlQuery, List<XmlRecord> records, NoteResult result, string output,
        ExportOptions options)
    {
        if (records.Count == 0)
        {
            return;
        }
        var stored = Load(xmlQuery, records);
        var authorized = stored.Where(x => x.Info.Kind == XmlKind.AuthorizedNote).ToList();
        var events = stored.Where(x => x.Info.Kind == XmlKind.Event).ToList();
        var unauthorized = stored.Where(x => x.Info.Kind == XmlKind.UnauthorizedNote).ToList();

        void Save(StoredXml xml, string subfolder = null)
        {
            var folder = output;
            if (options.ByMonth)
            {
                folder = Path.Combine(folder, XmlFiles.MonthFolder(xml.Info.AccessKey));
            }
            if (subfolder != null)
            {
                folder = Path.Combine(folder, subfolder);
            }
            var path = Path.Combine(folder, XmlFiles.FileName(xml.Info));
            XmlFiles.WriteFile(path, xml.Content);
            result.Files.Add(path);
        }

        if (authorized.Count > 0)
        {
            authorized.ForEach(x => Save(x));
            if (options.IncludeCancellation)
            {
                events.ForEach(x => Save(x));
            }
            var cancelled = events.Any(x => x.Info.IsCancellation) ||
                            authorized.Any(x => x.Record.Status == SysPdv.CancelledStatus);
            result.Status = cancelled ? NoteStatus.Cancelled : NoteStatus.Authorized;
            return;
        }

        result.Status = NoteStatus.Unauthorized;
        var last = records[records.Count - 1];
        result.Detail = !string.IsNullOrEmpty(last.Message)
            ? last.Message
            : $"situação {(string.IsNullOrEmpty(last.Status) ? "?" : last.Status)} no SysPDV";
        if (options.IncludeUnauthorized)
        {
            unauthorized.ForEach(x => Save(x, UnauthorizedFolder));
        }
    }

    private static List<StoredXml> Load(PreparedQuery xmlQuery, List<XmlRecord> records)
    {
        var stored = new List<StoredXml>();
        foreach (var record in records)
        {
            var xml = XmlFiles.Decompress(xmlQuery.Scalar(record.Id));
            if (xml != null && xml.Length > 0)
            {
                stored.Add(new StoredXml(XmlFiles.Inspect(xml), xml, record));
            }
        }
        return stored;
    }
}
