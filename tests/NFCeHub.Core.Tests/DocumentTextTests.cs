using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace NFCeHub.Core.Tests;

public class DocumentTextTests
{
    [Fact]
    public void ReadsRtfWhateverTheExtension()
    {
        var rtf = @"{\rtf1\ansi\ansicpg1252{\fonttbl{\f0 Times New Roman;}}{\*\generator Word;}" +
                  @"{\header Cabe\'e7alho 999\par}\pard S\'e9rie da Nota:\tab 00002\par " +
                  @"N\u250?mero(s) n\'e3o lan\'e7ado(s):\par 000100 / 000105\line 000107\par" +
                  @"\trowd\intbl 200\cell 210\cell\row}";

        var text = DocumentText.Read(Encoding.ASCII.GetBytes(rtf));

        Assert.Equal("Série da Nota:\t00002\nNúmero(s) não lançado(s):\n000100 / 000105\n000107\n200\t210\t\n", text);
    }

    [Fact]
    public void ReadsDocxParagraphsTabsAndTables()
    {
        var xml = "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
                  "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"><w:body>" +
                  "<w:p><w:pPr><w:tabs><w:tab w:val=\"left\" w:pos=\"720\"/></w:tabs></w:pPr>" +
                  "<w:r><w:t>Série</w:t><w:tab/><w:t xml:space=\"preserve\">3 </w:t></w:r></w:p>" +
                  "<w:p><w:r><w:t>100 a 110</w:t><w:br/><w:t>120</w:t></w:r></w:p>" +
                  "<w:p><w:r><mc:AlternateContent><mc:Choice><w:t>130</w:t></mc:Choice>" +
                  "<mc:Fallback><w:t>130</w:t></mc:Fallback></mc:AlternateContent></w:r></w:p>" +
                  "<w:p><w:r><w:instrText>PAGE 9</w:instrText></w:r></w:p>" +
                  "<w:tbl><w:tr><w:tc><w:p><w:r><w:t>200</w:t></w:r></w:p></w:tc>" +
                  "<w:tc><w:p><w:r><w:t>210</w:t></w:r></w:p></w:tc></w:tr></w:tbl>" +
                  "</w:body></w:document>";

        var text = DocumentText.Read(Docx(xml));

        Assert.Equal("Série\t3 \n100 a 110\n120\n130\n\n200 \t210 \t\n", text);
    }

    [Fact]
    public void ReadsHtmlSavedAsDoc()
    {
        var html = "<html><head><style>p { margin: 0 }</style></head><body>\r\n" +
                   "<p>S&eacute;rie\r\nda Nota: 00004</p><p>500&nbsp;a&nbsp;502<br>510</p>" +
                   "<table><tr><td>600</td><td>601</td></tr></table></body></html>";

        var text = DocumentText.Read(Encoding.UTF8.GetBytes(html));

        Assert.Equal(new[] { new NoteRange(4, 500, 502), new NoteRange(4, 510, 510), new NoteRange(4, 600, 601) },
            ListReader.Extract(text).Ranges);
    }

    [Fact]
    public void ReadsWord97DocumentWithTable()
    {
        var data = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples", "table.doc"));

        var reading = ListReader.Extract(DocumentText.Read(data));

        Assert.Equal(ReadingSource.Table, reading.Source);
        Assert.Equal(new[] { new NoteRange(2, 100, 120), new NoteRange(2, 200, 210), new NoteRange(7, 300, 305) },
            reading.Ranges);
    }

    [Fact]
    public void RejectsZipThatIsNotAWordDocument()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            zip.CreateEntry("planilha.csv");
        }

        Assert.Throws<InvalidDataException>(() => DocumentText.Read(buffer.ToArray()));
    }

    private static byte[] Docx(string documentXml)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), new UTF8Encoding(false));
            writer.Write(documentXml);
        }
        return buffer.ToArray();
    }
}
