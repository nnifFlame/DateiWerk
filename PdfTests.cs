using System.IO.Compression;
using System.Xml.Linq;
using SkiaSharp;

namespace DateiWerk;
static class PdfTests
{
    public static void Run(string directory)
    {
        int count = 0; var results = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; results.Add("PASS: " + name); }
        try
        {
            string input = Path.Combine(directory, "reference.pdf"), scan = Path.Combine(directory, "scan.pdf");
            var analysis = Formats.Analyze(input, "", default);
            Check(analysis.Kind == "PDF" && analysis.Targets.Any(t => t.Extension == "docx"), "PDF enthält Word-Export");
            Check(analysis.Targets.Any(t => t.Extension == "png.zip") && !analysis.Targets.Any(t => t.Extension == "png"), "Mehrseitige PDF verliert keine Seiten");
            foreach (string target in new[] { "txt", "html", "docx", "png.zip", "jpg.zip", "webp.zip" })
            {
                string output = Path.Combine(directory, "export." + target); if (File.Exists(output)) File.Delete(output); Engine.Convert(input, output, target, "", default);
                Check(File.Exists(output) && new FileInfo(output).Length > 0, "PDF → " + target);
                if (target.EndsWith(".zip"))
                {
                    using var zip = ZipFile.OpenRead(output); Check(zip.Entries.Count == 2, "Alle Seiten in " + target);
                    using var stream = zip.Entries[0].Open(); using var image = SKBitmap.Decode(stream); Check(image != null && image.Width > 1000 && image.Height > 1500, "Bildauflösung " + target);
                    if (target == "png.zip") { using var s = zip.Entries[0].Open(); using var dest = File.Create(Path.Combine(directory, "preview.png")); s.CopyTo(dest); }
                }
                else if (target == "docx")
                {
                    using var zip = ZipFile.OpenRead(output); using var s = zip.GetEntry("word/document.xml")!.Open(); var xml = XDocument.Load(s); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                    Check(xml.Descendants(w + "t").Any(t => t.Value.Contains("DateiWerk")), "DOCX enthält editierbaren Text");
                    Check(xml.Descendants(w + "br").Count(e => (string?)e.Attribute(w + "type") == "page") == 1, "DOCX erhält PDF-Seitentrennung");
                }
                else Check(File.ReadAllText(output).Contains("DateiWerk"), "Textinhalt " + target);
            }
            var scanAnalysis = Formats.Analyze(scan, "", default); Check(scanAnalysis.Targets.Any(t => t.Extension == "png") && !scanAnalysis.Targets.Any(t => t.Extension == "txt"), "Scan bietet Bildexport statt leerem Text");
            foreach (string target in new[] { "png", "jpg", "webp" })
            {
                string output = Path.Combine(directory, "scan-export." + target); if (File.Exists(output)) File.Delete(output); Engine.Convert(scan, output, target, "", default); Check(File.Exists(output), "Einseitige PDF → " + target);
            }
            string imageInput = Path.Combine(directory, "scan-export.webp"), imagePdf = Path.Combine(directory, "image-export.pdf"); if (File.Exists(imagePdf)) File.Delete(imagePdf); Engine.Convert(imageInput, imagePdf, "pdf", "", default);
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(imagePdf)) Check(pdf.NumberOfPages == 1, "WebP → PDF integriert");
            string textInput = Path.Combine(directory, "long.txt"); File.WriteAllText(textInput, string.Join("\n", Enumerable.Repeat("DateiWerk – Grüße, ÄÖÜ: automatische Zeilenumbrüche für längere Texte.", 140)));
            string textPdf = Path.Combine(directory, "text-export.pdf"); if (File.Exists(textPdf)) File.Delete(textPdf); Engine.Convert(textInput, textPdf, "pdf", "", default);
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(textPdf)) { Check(pdf.NumberOfPages > 1, "Text → PDF verteilt lange Texte auf Seiten"); Check(pdf.GetPage(1).Text.Contains("Grüße"), "PDF erhält Umlaute"); }
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); string cancelOutput = Path.Combine(directory, "cancel.png.zip");
            try { Engine.Convert(input, cancelOutput, "png.zip", "", cancelled.Token); throw new Exception("Abbruch nicht berücksichtigt"); } catch (OperationCanceledException) { Check(!File.Exists(cancelOutput), "PDF-Abbruch ohne Ausgabe"); }
            results.Add($"PASS: {count} PDF-/WebP-Konverterprüfungen.");
        }
        catch (Exception e) { results.Add("FAIL: " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(directory, "PDF-Testergebnis.txt"), results);
    }
}
