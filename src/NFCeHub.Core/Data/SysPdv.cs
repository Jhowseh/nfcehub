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
LEFT JOIN TRANSACAO_XMLNOTA x
  ON x.TRNSEQ = t.TRNSEQ AND x.CXANUM = t.CXANUM AND x.TRNDAT = t.TRNDAT
WHERE t.TRNNFCENUM BETWEEN @p0 AND @p1";

    private const string SqlRecordsByDate = @"
SELECT t.TRNNFCESER, t.TRNNFCENUM, x.ID, x.TRNSTA, x.TRNMENSNFE
FROM TRANSACAO t
LEFT JOIN TRANSACAO_XMLNOTA x
  ON x.TRNSEQ = t.TRNSEQ AND x.CXANUM = t.CXANUM AND x.TRNDAT = t.TRNDAT
WHERE t.TRNDAT BETWEEN @p0 AND @p1 AND t.TRNNFCENUM BETWEEN @p2 AND @p3";

    private const string SqlLowestSale = @"
SELECT TRNNFCESER, MIN(TRNNFCENUM) FROM TRANSACAO
WHERE TRNDAT BETWEEN @p0 AND @p1 AND TRNNFCENUM > '' AND TRNTIP <> '0'
GROUP BY TRNNFCESER";

    private const string SqlFirstDate = "SELECT MIN(TRNDAT) FROM TRANSACAO";

    private const int FirstWindowDays = 7;

    private const int MaxWindowDays = 56;

    private const int MarginDays = 7;

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
        if (wanted.Count == 0)
        {
            return found;
        }
        var firstDate = db.Rows(SqlFirstDate).Select(row => row[0]).FirstOrDefault();
        if (firstDate == null)
        {
            return found;
        }
        var oldest = Convert.ToDateTime(firstDate, CultureInfo.InvariantCulture).Date;
        var lowest = ranges.GroupBy(r => r.Series).ToDictionary(g => g.Key, g => g.Min(r => r.Start));
        var pending = new HashSet<int>(lowest.Keys);
        var low = PaddedNumber(ranges.Min(r => r.Start));
        var high = PaddedNumber(ranges.Max(r => r.End));

        var cursor = DateTime.Today;
        var upper = DateTime.Today.AddYears(1);
        var days = FirstWindowDays;
        while (pending.Count > 0 && cursor >= oldest)
        {
            var start = cursor.AddDays(1 - days);
            Collect(db.Rows(SqlRecordsByDate, start, upper, low, high), wanted, found);
            foreach (var row in db.Rows(SqlLowestSale, start, upper))
            {
                if (TryKey(row, out var key) && lowest.TryGetValue(key.Series, out var min) && key.Number < min)
                {
                    pending.Remove(key.Series);
                }
            }
            cursor = start.AddDays(-1);
            upper = cursor;
            days = Math.Min(days * 2, MaxWindowDays);
        }

        if (cursor >= oldest)
        {
            Collect(db.Rows(SqlRecordsByDate, cursor.AddDays(1 - MarginDays), cursor, low, high), wanted, found);
            var missing = new HashSet<(int, long)>(wanted.Where(k => !found.ContainsKey(k)));
            if (missing.Count > 0)
            {
                var missingLow = PaddedNumber(missing.Min(k => k.Item2));
                var missingHigh = PaddedNumber(missing.Max(k => k.Item2));
                Collect(db.Rows(SqlRecords, missingLow, missingHigh), missing, found);
            }
        }

        foreach (var records in found.Values)
        {
            records.Sort((a, b) => a.Id.CompareTo(b.Id));
        }
        return found;
    }

    private static void Collect(List<object[]> rows, HashSet<(int, long)> wanted,
        Dictionary<(int, long), List<XmlRecord>> found)
    {
        foreach (var row in rows)
        {
            if (row[2] == null || !TryKey(row, out var key) || !wanted.Contains(key))
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

    private static bool TryKey(object[] row, out (int Series, long Number) key)
    {
        var ok = int.TryParse(Text(row[0]), out var series) & long.TryParse(Text(row[1]), out var number);
        key = (series, number);
        return ok;
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
