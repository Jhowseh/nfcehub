using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NFCeHub.Core;

public static class RtfText
{
    private static readonly HashSet<string> SkippedGroups = new()
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "object", "objdata", "fldinst", "datafield",
        "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr", "footerf",
        "listtable", "listoverridetable", "listtext", "pntext", "rsidtbl", "generator", "xmlnstbl",
        "themedata", "colorschememapping", "latentstyles", "datastore", "filetbl", "revtbl",
        "pgdsctbl", "template", "nonshppict", "footnote", "annotation", "bkmkstart", "bkmkend",
    };

    public static bool IsRtf(byte[] data)
    {
        var start = 0;
        while (start < data.Length && (data[start] == ' ' || data[start] == '\r' || data[start] == '\n' || data[start] == '\t'))
        {
            start++;
        }
        return data.Length - start >= 5 && Encoding.ASCII.GetString(data, start, 5) == "{\\rtf";
    }

    public static string Read(byte[] data)
    {
        var text = new StringBuilder();
        var pending = new List<byte>();
        var encoding = Encoding.GetEncoding(1252);
        var groups = new Stack<(bool Skip, int Uc)>();
        var skip = false;
        var uc = 1;
        var toSkip = 0;
        var i = 0;

        void Flush()
        {
            if (pending.Count > 0)
            {
                text.Append(encoding.GetString(pending.ToArray()));
                pending.Clear();
            }
        }

        void Emit(string value)
        {
            Flush();
            if (!skip)
            {
                text.Append(value);
            }
        }

        while (i < data.Length)
        {
            var c = (char)data[i];
            if (c == '{')
            {
                Flush();
                groups.Push((skip, uc));
                toSkip = 0;
                i++;
                continue;
            }
            if (c == '}')
            {
                Flush();
                if (groups.Count > 0)
                {
                    (skip, uc) = groups.Pop();
                }
                toSkip = 0;
                i++;
                continue;
            }
            if (c == '\r' || c == '\n')
            {
                i++;
                continue;
            }
            if (c != '\\')
            {
                i++;
                if (toSkip > 0)
                {
                    toSkip--;
                    continue;
                }
                if (!skip)
                {
                    Flush();
                    text.Append(encoding.GetString(data, i - 1, 1));
                }
                continue;
            }

            i++;
            if (i >= data.Length)
            {
                break;
            }
            var symbol = (char)data[i];
            if (!IsLetter(symbol))
            {
                i++;
                if (symbol == '\'')
                {
                    if (i + 1 < data.Length && byte.TryParse(Encoding.ASCII.GetString(data, i, 2), NumberStyles.HexNumber, null, out var value))
                    {
                        if (toSkip > 0)
                        {
                            toSkip--;
                        }
                        else if (!skip)
                        {
                            pending.Add(value);
                        }
                    }
                    i += 2;
                    continue;
                }
                if (toSkip > 0)
                {
                    toSkip--;
                    continue;
                }
                switch (symbol)
                {
                    case '*':
                        skip = true;
                        break;
                    case '\\':
                    case '{':
                    case '}':
                        Emit(symbol.ToString());
                        break;
                    case '~':
                        Emit(" ");
                        break;
                    case '_':
                        Emit("-");
                        break;
                    case '\r':
                    case '\n':
                        Emit("\n");
                        break;
                }
                continue;
            }

            var wordStart = i;
            while (i < data.Length && IsLetter((char)data[i]))
            {
                i++;
            }
            var word = Encoding.ASCII.GetString(data, wordStart, i - wordStart);
            var paramStart = i;
            if (i < data.Length && data[i] == '-')
            {
                i++;
            }
            while (i < data.Length && char.IsDigit((char)data[i]))
            {
                i++;
            }
            int? param = i > paramStart && int.TryParse(Encoding.ASCII.GetString(data, paramStart, i - paramStart), out var p) ? p : null;
            if (i < data.Length && data[i] == ' ')
            {
                i++;
            }

            if (word == "bin")
            {
                i += param ?? 0;
                continue;
            }
            if (toSkip > 0)
            {
                toSkip--;
                continue;
            }
            if (SkippedGroups.Contains(word))
            {
                skip = true;
                continue;
            }
            switch (word)
            {
                case "ansicpg":
                    if (param is > 0)
                    {
                        try
                        {
                            encoding = Encoding.GetEncoding(param.Value);
                        }
                        catch (System.ArgumentException)
                        {
                        }
                        catch (System.NotSupportedException)
                        {
                        }
                    }
                    break;
                case "uc":
                    uc = param ?? 1;
                    break;
                case "u":
                    var code = param ?? 0;
                    Emit(((char)(code < 0 ? code + 65536 : code)).ToString());
                    toSkip = uc;
                    break;
                case "par":
                case "sect":
                case "page":
                case "line":
                case "row":
                case "nestrow":
                case "column":
                    Emit("\n");
                    break;
                case "tab":
                case "cell":
                case "nestcell":
                    Emit("\t");
                    break;
                case "emdash":
                case "endash":
                    Emit("-");
                    break;
                case "emspace":
                case "enspace":
                case "qmspace":
                    Emit(" ");
                    break;
                case "lquote":
                case "rquote":
                    Emit("'");
                    break;
                case "ldblquote":
                case "rdblquote":
                    Emit("\"");
                    break;
            }
        }
        Flush();
        return text.ToString();
    }

    private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
}
