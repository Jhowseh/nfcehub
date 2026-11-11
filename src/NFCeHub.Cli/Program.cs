using System;
using System.Collections.Generic;
using System.Text;
using NFCeHub.Core;

namespace NFCeHub.Cli;

public static class Program
{
    private const string Usage =
        "usage: nfcehub-cli --output DIR [--series N] [--start N] [--end N] [--list FILE]\n" +
        "                   [--no-cancellation] [--include-unauthorized] [--by-month] [--config FILE]\n" +
        "                   [--print-text FILE]";

    private static readonly HashSet<string> Flags = new() { "--no-cancellation", "--include-unauthorized", "--by-month" };

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var values = new Dictionary<string, string>();
        var flags = new HashSet<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (Flags.Contains(arg))
            {
                flags.Add(arg);
            }
            else if (arg.StartsWith("--") && i + 1 < args.Length)
            {
                values[arg] = args[++i];
            }
            else
            {
                return Fail();
            }
        }

        if (values.TryGetValue("--print-text", out var textFile))
        {
            Console.WriteLine(DocumentText.Read(textFile));
            return 0;
        }
        if (values.TryGetValue("--config", out var config))
        {
            AppPaths.ConfigFile = config;
        }
        var series = values.TryGetValue("--series", out var s) ? int.Parse(s) : 1;
        var ranges = new List<NoteRange>();
        if (values.TryGetValue("--start", out var start))
        {
            var first = long.Parse(start);
            var last = values.TryGetValue("--end", out var end) ? long.Parse(end) : first;
            ranges.Add(new NoteRange(series, first, last));
        }
        if (values.TryGetValue("--list", out var list))
        {
            var reading = ListReader.ReadFile(list, series);
            Console.WriteLine(reading.Description());
            Console.WriteLine(Ranges.Summary(reading.Ranges));
            reading.Warnings.ForEach(Console.WriteLine);
            ranges.AddRange(reading.Ranges);
        }
        if (!values.TryGetValue("--output", out var output) || ranges.Count == 0)
        {
            return Fail();
        }

        var options = new ExportOptions
        {
            IncludeCancellation = !flags.Contains("--no-cancellation"),
            IncludeUnauthorized = flags.Contains("--include-unauthorized"),
            ByMonth = flags.Contains("--by-month"),
        };
        ExportSummary summary;
        using (var db = Database.Open())
        {
            summary = Exporter.Export(db, ranges, output, options, (done, total, text) => Console.WriteLine($"[{done}/{total}] {text}"));
        }
        Console.WriteLine();
        Console.WriteLine($"{summary.FilesWritten} file(s) written to {summary.Output}");
        foreach (var status in NoteStatuses.All)
        {
            if (summary.Count(status) > 0)
            {
                Console.WriteLine($"  {status.Describe()}: {summary.Count(status)}");
            }
        }
        var report = ExportReport.Write(summary);
        if (report != null)
        {
            Console.WriteLine($"Report: {report}");
        }
        return 0;
    }

    private static int Fail()
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
