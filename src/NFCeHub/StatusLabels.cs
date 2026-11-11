using System.Collections.Generic;
using System.Globalization;
using NFCeHub.Core;

namespace NFCeHub;

public static class StatusLabels
{
    private static readonly Dictionary<NoteStatus, (string One, string Many, string Color)> Labels = new()
    {
        [NoteStatus.Authorized] = ("autorizada", "autorizadas", "Green"),
        [NoteStatus.Cancelled] = ("cancelada", "canceladas", "Link"),
        [NoteStatus.Unauthorized] = ("sem autorização", "sem autorização", "Yellow"),
        [NoteStatus.NotFound] = ("não encontrada", "não encontradas", "Red"),
        [NoteStatus.Error] = ("com erro", "com erro", "Red"),
    };

    public static string Label(NoteStatus status, long count) => count == 1 ? Labels[status].One : Labels[status].Many;

    public static string Color(NoteStatus status) => Labels[status].Color;
}

public static class Format
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public static string Number(long n) => n.ToString("N0", PtBr);

    public static string Plural(long n, string one, string many) =>
        n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
}
