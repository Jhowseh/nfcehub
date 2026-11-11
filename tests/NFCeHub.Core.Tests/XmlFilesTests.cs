using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace NFCeHub.Core.Tests;

public class XmlFilesTests
{
    private static readonly string Key = AccessKeys.Make("65", 1, 109375);

    private static byte[] Zlib(string text)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            var data = Encoding.UTF8.GetBytes(text);
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    [Fact]
    public void DecompressesZlibContent()
    {
        var xml = "<nfeProc><NFe/></nfeProc>";

        Assert.Equal(Encoding.UTF8.GetBytes(xml), XmlFiles.Decompress(Zlib(xml)));
    }

    [Fact]
    public void KeepsUncompressedXmlAsIs()
    {
        var data = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><NFe/>");

        Assert.Equal(data, XmlFiles.Decompress(data));
    }

    [Fact]
    public void RejectsContentThatIsNeitherZlibNorXml()
    {
        Assert.Throws<InvalidDataException>(() => XmlFiles.Decompress(new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void AuthorizedNoteNeedsProtocol()
    {
        var authorized = $"<nfeProc><NFe><infNFe Id=\"NFe{Key}\"/></NFe><protNFe/></nfeProc>";
        var withoutProtocol = $"<NFe><infNFe Id=\"NFe{Key}\"/></NFe>";

        var info = XmlFiles.Inspect(Encoding.UTF8.GetBytes(authorized));
        Assert.Equal(XmlKind.AuthorizedNote, info.Kind);
        Assert.Equal(Key, info.AccessKey);
        Assert.Equal($"{Key}-nfe.xml", XmlFiles.FileName(info));

        Assert.Equal(XmlKind.UnauthorizedNote, XmlFiles.Inspect(Encoding.UTF8.GetBytes(withoutProtocol)).Kind);
    }

    [Fact]
    public void CancellationEventGetsEventFileName()
    {
        var xml = $"<procEventoNFe><evento><infEvento><chNFe>{Key}</chNFe><tpEvento>110111</tpEvento>" +
                  "<nSeqEvento>1</nSeqEvento></infEvento></evento></procEventoNFe>";

        var info = XmlFiles.Inspect(Encoding.UTF8.GetBytes(xml));

        Assert.Equal(XmlKind.Event, info.Kind);
        Assert.True(info.IsCancellation);
        Assert.Equal($"110111{Key}01-procEventoNFe.xml", XmlFiles.FileName(info));
    }

    [Fact]
    public void MonthFolderComesFromTheAccessKey()
    {
        Assert.Equal("2026-10", XmlFiles.MonthFolder(Key));
        Assert.Equal("sem-data", XmlFiles.MonthFolder("123"));
    }

    [Fact]
    public void SeriesFolderUsesTheSeriesNumber()
    {
        Assert.Equal("Série 1", XmlFiles.SeriesFolder(1));
        Assert.Equal("Série 11", XmlFiles.SeriesFolder(11));
    }

    [Fact]
    public void WriteFileReplacesExistingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "a.xml");
        XmlFiles.WriteFile(path, new byte[] { 1 });
        XmlFiles.WriteFile(path, new byte[] { 2 });

        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(path));
        Directory.Delete(Path.GetDirectoryName(path), true);
    }
}
