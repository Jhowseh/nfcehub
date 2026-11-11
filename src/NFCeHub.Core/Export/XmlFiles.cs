using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NFCeHub.Core;

public enum XmlKind
{
    AuthorizedNote,
    Event,
    UnauthorizedNote,
}

public sealed class XmlInfo
{
    public XmlInfo(XmlKind kind, string accessKey, string eventType = "", int eventSeq = 1)
    {
        Kind = kind;
        AccessKey = accessKey;
        EventType = eventType;
        EventSeq = eventSeq;
    }

    public XmlKind Kind { get; }

    public string AccessKey { get; }

    public string EventType { get; }

    public int EventSeq { get; }

    public bool IsCancellation => Kind == XmlKind.Event && EventType == XmlFiles.CancellationEvent;
}

public static class XmlFiles
{
    public const string CancellationEvent = "110111";

    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
    private static readonly Regex RootRegex = new(@"<(?!\?)(?:\w+:)?(\w+)", RegexOptions.Compiled);
    private static readonly Regex NfeIdRegex = new(@"Id=""NFe(\d{44})""", RegexOptions.Compiled);

    public static byte[] Decompress(object value)
    {
        if (value == null)
        {
            return null;
        }
        var data = value as byte[] ?? Latin1.GetBytes(Convert.ToString(value, CultureInfo.InvariantCulture));
        if (data.Length > 2 && data[0] == 0x78)
        {
            try
            {
                using var input = new MemoryStream(data, 2, data.Length - 2);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch (InvalidDataException)
            {
            }
        }
        var text = Latin1.GetString(data, 0, Math.Min(data.Length, 16)).TrimStart('﻿', 'ï', '»', '¿', ' ', '\r', '\n', '\t');
        if (text.StartsWith("<"))
        {
            return data;
        }
        throw new InvalidDataException("XML compactado inválido no banco.");
    }

    public static XmlInfo Inspect(byte[] xml)
    {
        var text = Latin1.GetString(xml);
        var m = RootRegex.Match(text);
        var root = m.Success ? m.Groups[1].Value : "";
        if (root == "procEventoNFe")
        {
            var seq = Tag(text, "nSeqEvento");
            return new XmlInfo(XmlKind.Event, Tag(text, "chNFe"), Tag(text, "tpEvento"),
                int.TryParse(seq, out var s) ? s : 1);
        }
        var id = NfeIdRegex.Match(text);
        var accessKey = id.Success ? id.Groups[1].Value : Tag(text, "chNFe");
        var hasProtocol = text.Contains("<protNFe") || text.Contains(":protNFe");
        return new XmlInfo(root == "nfeProc" && hasProtocol ? XmlKind.AuthorizedNote : XmlKind.UnauthorizedNote, accessKey);
    }

    public static string FileName(XmlInfo info) => info.Kind == XmlKind.Event
        ? $"{info.EventType}{info.AccessKey}{info.EventSeq:00}-procEventoNFe.xml"
        : $"{info.AccessKey}-nfe.xml";

    public static string MonthFolder(string accessKey)
    {
        if (accessKey.Length == 44 && accessKey.Substring(2, 4).All(char.IsDigit))
        {
            return $"20{accessKey.Substring(2, 2)}-{accessKey.Substring(4, 2)}";
        }
        return "sem-data";
    }

    public static void WriteFile(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, data);
        if (!NativeMethods.MoveFileEx(temp, path, NativeMethods.MoveFileReplaceExisting))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static string Tag(string xml, string name)
    {
        var m = Regex.Match(xml, @"<(?:\w+:)?" + name + @"[^>]*>([^<]*)<");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }
}
