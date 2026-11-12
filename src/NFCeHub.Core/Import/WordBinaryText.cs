using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NFCeHub.Core;

public static class WordBinaryText
{
    private const ushort WordIdent = 0xA5EC;
    private const int ClxIndex = 33;

    public static string Read(byte[] data)
    {
        var file = new CompoundFile(data);
        var word = file.Stream("WordDocument")
                   ?? throw new InvalidDataException("O arquivo não é um documento do Word (.doc).");
        if (word.Length < 0x200 || BitConverter.ToUInt16(word, 0) != WordIdent)
        {
            throw new InvalidDataException("Versão do Word não suportada. Salve o arquivo como .docx ou PDF e tente de novo.");
        }
        var flags = BitConverter.ToUInt16(word, 0x0A);
        if ((flags & 0x0100) != 0)
        {
            throw new InvalidDataException("O documento do Word está protegido por senha.");
        }
        var table = file.Stream((flags & 0x0200) != 0 ? "1Table" : "0Table")
                    ?? throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");

        var position = 32;
        position += 2 + BitConverter.ToUInt16(word, position) * 2;
        var longs = position + 2;
        position += 2 + BitConverter.ToUInt16(word, position) * 4;
        var pairs = BitConverter.ToUInt16(word, position);
        var fcLcb = position + 2;
        if (pairs <= ClxIndex || fcLcb + (ClxIndex + 1) * 8 > word.Length)
        {
            throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
        }
        var textLength = BitConverter.ToInt32(word, longs + 12);
        var clx = (int)BitConverter.ToUInt32(word, fcLcb + ClxIndex * 8);
        var clxLength = (int)BitConverter.ToUInt32(word, fcLcb + ClxIndex * 8 + 4);
        return Clean(Pieces(word, table, clx, clxLength, textLength));
    }

    private static string Pieces(byte[] word, byte[] table, int clx, int clxLength, int textLength)
    {
        var position = clx;
        var end = Math.Min((long)clx + clxLength, table.Length);
        while (position < end)
        {
            if (table[position] == 1)
            {
                position += 3 + BitConverter.ToInt16(table, position + 1);
                continue;
            }
            if (table[position] != 2)
            {
                break;
            }
            var length = BitConverter.ToInt32(table, position + 1);
            var start = position + 5;
            if (length < 4 || start + (long)length > table.Length)
            {
                break;
            }
            var count = (length - 4) / 12;
            var ansi = Encoding.GetEncoding(1252);
            var text = new StringBuilder();
            for (var i = 0; i < count; i++)
            {
                var cpStart = BitConverter.ToInt32(table, start + i * 4);
                var cpEnd = Math.Min(BitConverter.ToInt32(table, start + (i + 1) * 4), textLength);
                if (cpEnd <= cpStart)
                {
                    continue;
                }
                var fc = BitConverter.ToUInt32(table, start + (count + 1) * 4 + i * 8 + 2);
                var chars = cpEnd - cpStart;
                if ((fc & 0x40000000) != 0)
                {
                    var offset = (int)((fc & 0x3FFFFFFF) / 2);
                    if (offset + (long)chars > word.Length)
                    {
                        throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
                    }
                    text.Append(ansi.GetString(word, offset, chars));
                }
                else
                {
                    var offset = (int)(fc & 0x3FFFFFFF);
                    if (offset + (long)chars * 2 > word.Length)
                    {
                        throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
                    }
                    text.Append(Encoding.Unicode.GetString(word, offset, chars * 2));
                }
            }
            return text.ToString();
        }
        throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
    }

    private static string Clean(string raw)
    {
        var text = new StringBuilder(raw.Length);
        var fields = new Stack<bool>();
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            switch (c)
            {
                case '\x13':
                    fields.Push(true);
                    continue;
                case '\x14':
                    if (fields.Count > 0)
                    {
                        fields.Pop();
                        fields.Push(false);
                    }
                    continue;
                case '\x15':
                    if (fields.Count > 0)
                    {
                        fields.Pop();
                    }
                    continue;
            }
            if (fields.Contains(true))
            {
                continue;
            }
            switch (c)
            {
                case '\r':
                case '\x0B':
                case '\x0C':
                case '\x0E':
                    text.Append('\n');
                    break;
                case '\x07':
                    if (i + 1 < raw.Length && raw[i + 1] == '\x07')
                    {
                        text.Append('\n');
                        i++;
                    }
                    else
                    {
                        text.Append('\t');
                    }
                    break;
                case '\t':
                    text.Append('\t');
                    break;
                case '\x1E':
                    text.Append('-');
                    break;
                default:
                    if (c >= ' ')
                    {
                        text.Append(c);
                    }
                    break;
            }
        }
        return text.ToString();
    }
}
