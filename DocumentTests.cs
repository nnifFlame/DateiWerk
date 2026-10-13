namespace DateiWerk;
static class DocumentTests
{
    public static void Run(string root)
    {
        var result = new List<string>();
        try
        {
            string docx = Path.Combine(root, "export.docx"), wordPdf = Path.Combine(root, "word-check.pdf"); if (File.Exists(wordPdf)) File.Delete(wordPdf);
            Engine.Convert(docx, wordPdf, "pdf", "", default);
            using (var pdf = UglyToad.PdfPig.PdfDocument.Open(wordPdf)) { if (pdf.NumberOfPages != 2 || !pdf.GetPage(1).Text.Contains("DateiWerk")) throw new Exception("Word-Export hat falschen Inhalt oder falsche Seitenzahl."); }
            result.Add("PASS: Integrierter PDF → DOCX-Export lässt sich in Word öffnen und erhält Text und zwei Seiten.");
            string csv = Path.Combine(root, "table-input.csv"), xlsx = Path.Combine(root, "table.xlsx"), excelCsv = Path.Combine(root, "excel-check.csv");
            File.WriteAllText(csv, "Name,Anzahl\r\nDateiWerk,42\r\n"); if (File.Exists(xlsx)) File.Delete(xlsx); if (File.Exists(excelCsv)) File.Delete(excelCsv);
            Engine.Convert(csv, xlsx, "xlsx", "", default); Engine.Convert(xlsx, excelCsv, "csv", "", default);
            if (!File.ReadAllText(excelCsv).Contains("DateiWerk")) throw new Exception("Excel konnte die erzeugte XLSX nicht korrekt lesen.");
            result.Add("PASS: Integrierter CSV → XLSX-Export lässt sich in Excel öffnen und erhält die Daten.");
        }
        catch (Exception e) { result.Add("FAIL: " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(root,"Dokument-Testergebnis.txt"),result);
    }
}
