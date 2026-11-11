using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace NFCeHub.Core;

public static class ExportReport
{
    public static string Write(ExportSummary summary)
    {
        var problems = summary.Problems();
        if (problems.Count == 0)
        {
            return null;
        }
        var now = DateTime.Now;
        var lines = new List<string>
        {
            "NFC-e sem XML autorizado exportado",
            "Gerado em " + now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
            "",
            "Série;Número;Situação;Detalhe",
        };
        lines.AddRange(problems.Select(r => $"{r.Series};{r.Number};{r.Status.Describe()};{r.Detail}"));
        Directory.CreateDirectory(summary.Output);
        var path = Path.Combine(summary.Output,
            "Relatorio-NFCe-nao-exportadas-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(true));
        return path;
    }
}
