using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NFCeHub.Core;

public sealed class XmlRecord
{
    public XmlRecord(long id, string status, string message)
    {
        Id = id;
        Status = status;
        Message = message;
    }

    public long Id { get; }

    public string Status { get; }

    public string Message { get; }
}

public sealed class LastNote
{
    public LastNote(int series, long number, DateTime date)
    {
        Series = series;
        Number = number;
        Date = date;
    }

    public int Series { get; }

    public long Number { get; }

    public DateTime Date { get; }
}

public static class SysPdv
{
    public const string CancelledStatus = "CA";

    private const int NumberWidth = 10;

    private const string SqlRecords = @"
SELECT t.TRNNFCESER, t.TRNNFCENUM, x.ID, x.TRNSTA, x.TRNMENSNFE
FROM TRANSACAO t
JOIN TRANSACAO_XMLNOTA x
  ON x.TRNSEQ = t.TRNSEQ AND x.CXANUM = t.CXANUM AND x.TRNDAT = t.TRNDAT
WHERE t.TRNNFCENUM BETWEEN @p0 AND @p1";

    private const string SqlXml = "SELECT TRNXML FROM TRANSACAO_XMLNOTA WHERE ID = @p0";

    private const string SqlLastNfce = @"
SELECT FIRST 1 TRNNFCESER, TRNNFCENUM, TRNDAT FROM TRANSACAO
WHERE TRNDAT >= CURRENT_DATE - 120 AND TRNNFCENUM IS NOT NULL AND TRNNFCENUM <> ''
ORDER BY TRNDAT DESC, TRNNFCENUM DESC";

    private const string SqlPing = "SELECT 1 FROM RDB$DATABASE";

    public static Dictionary<(int Series, long Number), List<XmlRecord>> FindRecords(ReadOnlySession db,
        IReadOnlyList<NoteRange> ranges)
    {
        var wanted = new HashSet<(int, long)>(ranges.SelectMany(r => r.Numbers().Select(n => (r.Series, n))));
        var found = new Dictionary<(int, long), List<XmlRecord>>();
        foreach (var series in ranges.Select(r => r.Series).Distinct().OrderBy(s => s))
        {
            var ofSeries = ranges.Where(r => r.Series == series).ToList();
            var low = ofSeries.Min(r => r.Start);
            var high = ofSeries.Max(r => r.End);
            foreach (var row in db.Rows(SqlRecords, PaddedNumber(low), PaddedNumber(high)))
            {
                if (!int.TryParse(Text(row[0]), out var rowSeries) || !long.TryParse(Text(row[1]), out var rowNumber))
                {
                    continue;
                }
                var key = (rowSeries, rowNumber);
                if (!wanted.Contains(key))
                {
                    continue;
                }
                if (!found.TryGetValue(key, out var records))
                {
                    found[key] = records = new List<XmlRecord>();
                }
                records.Add(new XmlRecord(Convert.ToInt64(row[2], CultureInfo.InvariantCulture), Text(row[3]), Text(row[4])));
            }
        }
        foreach (var records in found.Values)
        {
            records.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        return found;
    }

    public static PreparedQuery PrepareXmlQuery(ReadOnlySession db) => db.Prepare(SqlXml, 0L);

    public static LastNote LastNfce(ReadOnlySession db)
    {
        var row = db.Rows(SqlLastNfce).FirstOrDefault();
        if (row == null)
        {
            return null;
        }
        var series = int.TryParse(Text(row[0]), out var s) ? s : 0;
        var number = long.Parse(Text(row[1]), CultureInfo.InvariantCulture);
        return new LastNote(series, number, Convert.ToDateTime(row[2], CultureInfo.InvariantCulture));
    }

    public static void Ping(ReadOnlySession db) => db.Rows(SqlPing);

    private static string PaddedNumber(long n) => n.ToString(CultureInfo.InvariantCulture).PadLeft(NumberWidth, '0');

    private static string Text(object value) => (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Trim();
}
