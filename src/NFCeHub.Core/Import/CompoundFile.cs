using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NFCeHub.Core;

public sealed class CompoundFile
{
    private const int EndOfChain = -2;
    private const int FreeSector = -1;
    private static readonly byte[] Signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    private readonly byte[] data;
    private readonly int sectorSize;
    private readonly int miniSectorSize;
    private readonly long miniCutoff;
    private readonly List<int> fat = new();
    private readonly List<int> miniFat = new();
    private readonly List<(string Name, int Type, int Start, long Size)> entries = new();
    private readonly byte[] miniStream;

    public CompoundFile(byte[] data)
    {
        if (!IsCompound(data) || data.Length < 512)
        {
            throw new InvalidDataException("O arquivo não é um documento do Word válido.");
        }
        this.data = data;
        sectorSize = 1 << BitConverter.ToUInt16(data, 0x1E);
        miniSectorSize = 1 << BitConverter.ToUInt16(data, 0x20);
        if (sectorSize < 128 || sectorSize > 65536 || miniSectorSize < 8 || miniSectorSize > sectorSize)
        {
            throw new InvalidDataException("O arquivo não é um documento do Word válido.");
        }
        miniCutoff = BitConverter.ToUInt32(data, 0x38);

        var fatSectors = new List<int>();
        for (var i = 0; i < 109; i++)
        {
            var sector = BitConverter.ToInt32(data, 0x4C + i * 4);
            if (sector >= 0)
            {
                fatSectors.Add(sector);
            }
        }
        var difat = BitConverter.ToInt32(data, 0x44);
        var perSector = sectorSize / 4;
        var guard = 0;
        while (difat >= 0 && guard++ < 100_000)
        {
            var offset = Offset(difat);
            for (var i = 0; i < perSector - 1; i++)
            {
                var sector = BitConverter.ToInt32(data, offset + i * 4);
                if (sector >= 0)
                {
                    fatSectors.Add(sector);
                }
            }
            difat = BitConverter.ToInt32(data, offset + (perSector - 1) * 4);
        }
        foreach (var sector in fatSectors)
        {
            var offset = Offset(sector);
            for (var i = 0; i < perSector; i++)
            {
                fat.Add(BitConverter.ToInt32(data, offset + i * 4));
            }
        }

        var directory = ReadChain(BitConverter.ToInt32(data, 0x30), long.MaxValue);
        for (var offset = 0; offset + 128 <= directory.Length; offset += 128)
        {
            var nameLength = Math.Min(64, (int)BitConverter.ToUInt16(directory, offset + 0x40));
            var name = nameLength >= 2 ? Encoding.Unicode.GetString(directory, offset, nameLength - 2) : "";
            var type = directory[offset + 0x42];
            var start = BitConverter.ToInt32(directory, offset + 0x74);
            var size = (long)BitConverter.ToUInt32(directory, offset + 0x78);
            entries.Add((name, type, start, size));
        }
        var root = entries.FirstOrDefault(e => e.Type == 5);
        miniStream = root.Type == 5 ? ReadChain(root.Start, root.Size) : Array.Empty<byte>();

        var miniFatBytes = ReadChain(BitConverter.ToInt32(data, 0x3C), long.MaxValue);
        for (var i = 0; i + 4 <= miniFatBytes.Length; i += 4)
        {
            miniFat.Add(BitConverter.ToInt32(miniFatBytes, i));
        }
    }

    public static bool IsCompound(byte[] data) =>
        data.Length >= Signature.Length && Signature.Select((b, i) => data[i] == b).All(x => x);

    public byte[] Stream(string name)
    {
        var entry = entries.FirstOrDefault(e => e.Type == 2 && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
        if (entry.Name == null)
        {
            return null;
        }
        return entry.Size < miniCutoff ? ReadMiniChain(entry.Start, entry.Size) : ReadChain(entry.Start, entry.Size);
    }

    private int Offset(int sector)
    {
        var offset = (long)(sector + 1) * sectorSize;
        if (sector < 0 || offset + sectorSize > data.Length)
        {
            throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
        }
        return (int)offset;
    }

    private byte[] ReadChain(int start, long size)
    {
        using var output = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector >= 0 && output.Length < size)
        {
            if (sector >= fat.Count || guard++ > fat.Count)
            {
                throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
            }
            output.Write(data, Offset(sector), sectorSize);
            sector = fat[sector];
        }
        if (sector != EndOfChain && sector != FreeSector && output.Length < size)
        {
            throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
        }
        return Truncate(output, size);
    }

    private byte[] ReadMiniChain(int start, long size)
    {
        using var output = new MemoryStream();
        var sector = start;
        var guard = 0;
        while (sector >= 0 && output.Length < size)
        {
            var offset = (long)sector * miniSectorSize;
            if (sector >= miniFat.Count || guard++ > miniFat.Count || offset + miniSectorSize > miniStream.Length)
            {
                throw new InvalidDataException("O documento do Word está incompleto ou corrompido.");
            }
            output.Write(miniStream, (int)offset, miniSectorSize);
            sector = miniFat[sector];
        }
        return Truncate(output, size);
    }

    private static byte[] Truncate(MemoryStream output, long size)
    {
        var bytes = output.ToArray();
        return bytes.Length > size ? bytes.Take((int)size).ToArray() : bytes;
    }
}
