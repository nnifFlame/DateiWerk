using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DateiWerk;
static class TableConverter
{
    public static void ToXlsx(string input, string output, CancellationToken ct)
    {
        var text = File.ReadAllText(input, new UTF8Encoding(false, true));
        var rows = Formats.Extension(input) == "json" ? JsonSerializer.Deserialize<List<List<string>>>(text) ?? throw new IOException("Keine JSON-Zeilentabelle.") : Engine.ParseTable(text, Formats.Extension(input) == "csv" ? ',' : '\t');
        if (rows.Count > 1048576 || rows.Any(r => r.Count > 16384)) throw new IOException("Tabelle überschreitet das Excel-Zeilen- oder Spaltenlimit.");
        Engine.Atomic(output, tmp =>
        {
            using var zip = ZipFile.Open(tmp, ZipArchiveMode.Create);
            void Write(string name, string data) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(data); }
            Write("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Write("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Write("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Daten\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Write("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            using var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); using var xml = System.Xml.XmlWriter.Create(stream, new() { Encoding = new UTF8Encoding(false) });
            const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; xml.WriteStartDocument(); xml.WriteStartElement("worksheet", ns); xml.WriteStartElement("sheetData", ns);
            for (int r = 0; r < rows.Count; r++)
            {
                ct.ThrowIfCancellationRequested(); xml.WriteStartElement("row", ns); xml.WriteAttributeString("r", (r + 1).ToString());
                for (int c = 0; c < rows[r].Count; c++)
                {
                    string value = rows[r][c]; if (value.Length > 32767) throw new IOException("Eine Zelle überschreitet das Excel-Limit von 32.767 Zeichen.");
                    xml.WriteStartElement("c", ns); xml.WriteAttributeString("r", Column(c) + (r + 1)); xml.WriteAttributeString("t", "inlineStr"); xml.WriteStartElement("is", ns); xml.WriteStartElement("t", ns); xml.WriteAttributeString("xml", "space", XNamespace.Xml.NamespaceName, "preserve"); xml.WriteString(value); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndDocument(); ct.ThrowIfCancellationRequested();
        });
    }
    static string Column(int index) { string text = ""; index++; while (index > 0) { index--; text = (char)('A' + index % 26) + text; index /= 26; } return text; }
}
