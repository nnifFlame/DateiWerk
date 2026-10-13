using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PdfSharp.Drawing;
using SkiaSharp;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DateiWerk;
static class PdfConverter
{
    const int MaxPages = 2000;
    public static FileAnalysis Analyze(string input, CancellationToken ct)
    {
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(input);
        if (pdf.NumberOfPages is < 1 or > MaxPages) throw new IOException("PDF-Konvertierung unterstützt 1 bis 2.000 Seiten.");
        bool text = false;
        foreach (var page in pdf.GetPages()) { ct.ThrowIfCancellationRequested(); if (page.Letters.Any(l => !string.IsNullOrWhiteSpace(l.Value))) { text = true; break; } }
        var formats = new List<TargetFormat>();
        if (text) { formats.Add(new("docx", "Word – extrahierter Text")); formats.AddRange(Formats.Targets("txt", "html")); }
        if (pdf.NumberOfPages == 1) formats.AddRange(Formats.Targets("png", "jpg", "webp"));
        else foreach (string ext in new[] { "png", "jpg", "webp" }) formats.Add(new(ext + ".zip", "Alle Seiten als " + ext.ToUpperInvariant() + " im ZIP"));
        return new("PDF", $"PDF • {pdf.NumberOfPages} {(pdf.NumberOfPages == 1 ? "Seite" : "Seiten")} • {(new FileInfo(input).Length / 1024d):F1} KiB",
            text ? "PDF-Konverter integriert. Word/TXT/HTML übernehmen den vorhandenen Text, nicht das Original-Layout. Bilder enthalten die vollständigen Seiten (150 DPI)."
                 : "PDF ohne auslesbaren Text: wahrscheinlich ein Scan. Alle Seiten können als Bilder exportiert werden. OCR-Texterkennung ist noch nicht enthalten.", formats.ToArray());
    }
    public static void Convert(string input, string output, string target, CancellationToken ct)
    {
        Engine.Atomic(output, tmp =>
        {
            if (target is "txt" or "html" or "docx")
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(input); var pages = new List<string>();
                foreach (var page in pdf.GetPages()) { ct.ThrowIfCancellationRequested(); pages.Add(ContentOrderTextExtractor.GetText(page)); }
                if (pages.All(string.IsNullOrWhiteSpace)) throw new IOException("Die PDF enthält keinen auslesbaren Text. Wähle ein Bildformat; OCR ist noch nicht enthalten.");
                if (target == "docx") WriteDocx(tmp, pages);
                else File.WriteAllText(tmp, target == "html" ? Html(string.Join("\n\n", pages)) : string.Join("\n\f\n", pages), new UTF8Encoding(false));
            }
            else
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(input);
                bool archive = target.EndsWith(".zip"); string ext = archive ? target[..^4] : target;
                if (ext is not ("png" or "jpg" or "webp")) throw new IOException("Ungültiges PDF-Zielformat.");
                if (!archive && pdf.NumberOfPages != 1) throw new IOException("Mehrseitige PDFs benötigen den Export aller Seiten als ZIP.");
                using var source = File.OpenRead(input);
                using var zip = archive ? ZipFile.Open(tmp, ZipArchiveMode.Create) : null;
                for (int i = 0; i < pdf.NumberOfPages; i++)
                {
                    ct.ThrowIfCancellationRequested(); var page = pdf.GetPage(i + 1);
                    if (page.Width * page.Height * (150d / 72) * (150d / 72) > 100_000_000) throw new IOException("PDF-Seite überschreitet das Bildlimit von 100 Megapixeln.");
                    source.Position = 0; using var image = PDFtoImage.Conversion.ToImage(source, page: i, leaveOpen: true, options: new(Dpi: 150));
                    using var encoded = image.Encode(ext switch { "jpg" => SKEncodedImageFormat.Jpeg, "webp" => SKEncodedImageFormat.Webp, _ => SKEncodedImageFormat.Png }, ext == "png" ? 100 : 92);
                    if (zip != null) { using var entry = zip.CreateEntry($"Seite-{i + 1:D4}.{ext}", CompressionLevel.NoCompression).Open(); encoded.SaveTo(entry); }
                    else { using var file = File.Create(tmp); encoded.SaveTo(file); }
                }
            }
            ct.ThrowIfCancellationRequested();
        });
    }
    public static string Html(string text) => "<!doctype html><html lang=\"de\"><meta charset=\"utf-8\"><title>Export</title><body><pre>" + System.Net.WebUtility.HtmlEncode(text) + "</pre></body></html>";
    public static void WriteDocx(string output, IEnumerable<string> pages)
    {
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var body = new XElement(w + "body"); bool first = true;
        foreach (string text in pages)
        {
            if (!first) body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "br", new XAttribute(w + "type", "page"))))); first = false;
            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                body.Add(new XElement(w + "p", new XElement(w + "r", new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), CleanXml(line)))));
        }
        body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", "11906"), new XAttribute(w + "h", "16838")), new XElement(w + "pgMar", new XAttribute(w + "top", "1134"), new XAttribute(w + "bottom", "1134"), new XAttribute(w + "left", "1134"), new XAttribute(w + "right", "1134"))));
        using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
        Write(zip, "[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Write(zip, "_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        Write(zip, "word/document.xml", new XDocument(new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), body)).ToString());
    }
    static string CleanXml(string text)
    {
        var result = new StringBuilder(); foreach (var rune in text.EnumerateRunes()) { int v = rune.Value; if (v is 9 or 10 or 13 || v is >= 32 and <= 0xd7ff || v is >= 0xe000 and <= 0xfffd || v is >= 0x10000 and <= 0x10ffff) result.Append(rune); } return result.ToString();
    }
    static void Write(ZipArchive zip, string name, string content) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(content); }
    public static void ImageToPdf(string input, string output)
    {
        using var original = ImageConverter.Load(input); using var png = new MemoryStream(); original.Save(png, System.Drawing.Imaging.ImageFormat.Png); png.Position = 0;
        using var pdf = new PdfSharp.Pdf.PdfDocument(); var page = pdf.AddPage();
        using var image = XImage.FromStream(png); double scale = Math.Min((page.Width.Point - 72) / image.PixelWidth, (page.Height.Point - 72) / image.PixelHeight);
        using (var graphics = XGraphics.FromPdfPage(page)) { double width = image.PixelWidth * scale, height = image.PixelHeight * scale; graphics.DrawImage(image, (page.Width.Point - width) / 2, (page.Height.Point - height) / 2, width, height); }
        pdf.Save(output);
    }
    public static void TextToPdf(string text, string output, CancellationToken ct)
    {
        using var pdf = new PdfSharp.Pdf.PdfDocument(); var font = new XFont("Arial", 11, XFontStyleEx.Regular);
        PdfSharp.Pdf.PdfPage? page = null; XGraphics? graphics = null; double y = 0;
        void NewPage() { graphics?.Dispose(); page = pdf.AddPage(); graphics = XGraphics.FromPdfPage(page); y = 48; }
        NewPage();
        try
        {
            foreach (string paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                ct.ThrowIfCancellationRequested(); var remaining = paragraph.Replace("\t", "    ");
                do
                {
                    if (y > page!.Height.Point - 48) NewPage(); int count = remaining.Length;
                    // Binary search avoids quadratic measuring for long lines.
                    int low = 0, high = count;
                    while (low < high) { int mid = (low + high + 1) / 2; if (graphics!.MeasureString(remaining[..mid], font).Width <= page!.Width.Point - 96) low = mid; else high = mid - 1; }
                    count = Math.Max(1, low); if (count < remaining.Length) { int space = remaining.LastIndexOf(' ', count - 1, count); if (space > 0) count = space; }
                    count = Math.Min(count, remaining.Length); graphics!.DrawString(remaining[..count], font, XBrushes.Black, new XPoint(48, y)); y += 16; remaining = remaining[count..].TrimStart();
                } while (remaining.Length > 0);
            }
        }
        finally { graphics?.Dispose(); }
        ct.ThrowIfCancellationRequested(); pdf.Save(output);
    }
}
