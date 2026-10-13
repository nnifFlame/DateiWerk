using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DateiWerk;

record TargetFormat(string Extension, string Name)
{
    public override string ToString() => $"{Name} (.{Extension})";
}
record FileAnalysis(string Kind, string Description, string Notice, TargetFormat[] Targets, string OfficeFamily = "", bool Media = false);

static class Formats
{
    public static string Extension(string path) => Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
    public static readonly string[] Images = ["png", "jpg", "jpeg", "webp", "bmp", "gif", "tif", "tiff", "ico"];
    public static readonly string[] Audio = ["mp3", "wav", "flac", "ogg", "m4a", "aac", "wma", "aiff", "opus"];
    public static readonly string[] Video = ["mp4", "mkv", "webm", "avi", "mov", "wmv", "m4v", "mpeg", "mpg"];
    public static string OfficeFamily(string ext) => ext switch
    {
        "doc" or "docx" or "docm" or "rtf" or "odt" => "Word",
        "xls" or "xlsx" or "xlsm" or "xlsb" or "ods" => "Excel",
        "ppt" or "pptx" or "pptm" or "pps" or "ppsx" or "odp" => "PowerPoint",
        _ => ""
    };
    public static TargetFormat[] Targets(params string[] extensions) => extensions.Select(x => new TargetFormat(x, x switch
    {
        "docx" => "Word-Dokument", "doc" => "Word 97–2003", "xlsx" => "Excel-Arbeitsmappe", "xls" => "Excel 97–2003",
        "pptx" => "PowerPoint-Präsentation", "ppt" => "PowerPoint 97–2003", "pdf" => "PDF-Dokument",
        "rtf" => "Rich Text", "txt" => "Text", "odt" => "OpenDocument Text", "ods" => "OpenDocument Tabelle", "odp" => "OpenDocument Präsentation",
        "csv" => "CSV-Tabelle", "tsv" => "Tabulator-Tabelle", "json" => "JSON-Daten", "xml" => "XML-Daten", "html" => "HTML-Seite",
        "jpg" => "JPEG-Bild", "png" => "PNG-Bild", "tiff" => "TIFF-Bild", "bmp" => "Bitmap", "gif" => "GIF-Bild",
        _ => x.ToUpperInvariant()
    })).ToArray();
    public static TargetFormat[] OfficeTargets(string family) => family switch
    {
        "Word" => Targets("pdf", "docx", "doc", "rtf", "txt", "odt"),
        "Excel" => Targets("pdf", "xlsx", "xls", "csv", "ods"),
        "PowerPoint" => Targets("pdf", "pptx", "ppt", "odp"),
        _ => []
    };
    public static FileAnalysis Analyze(string path, string ffmpeg, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Directory.Exists(path)) return new("Ordner", "Ordner ausgewählt", "Du kannst den Ordner als ZIP packen oder einschließlich aller Dateien mit AES-256 verschlüsseln.", []);
        if (!File.Exists(path)) return new("", "Wähle eine Datei oder ziehe sie ins Fenster.", "Alle Vorgänge finden lokal statt.", []);
        var ext = Extension(path); var info = new FileInfo(path);
        string size = info.Length < 1048576 ? $"{info.Length / 1024d:F1} KiB" : $"{info.Length / 1048576d:F1} MiB";
        using var stream = File.OpenRead(path); var header = new byte[16]; int n = stream.Read(header); stream.Close();
        if (n >= 8 && Encoding.ASCII.GetString(header, 0, 8) == "DWENC001")
            return new("Verschlüsselt", $"DateiWerk AES-256-Datei • {size}", "Wähle Entschlüsseln und gib das Passwort ein.", []);
        if (ext == "dwenc") throw new IOException("Die Datei hat keinen gültigen DateiWerk-Dateikopf.");
        if (ext == "pdf") return PdfConverter.Analyze(path, ct);
        var family = OfficeFamily(ext);
        if (family != "")
        {
            if (ext is "docx" or "docm" or "xlsx" or "xlsm" or "pptx" or "pptm" or "ppsx" or "xlsb")
            {
                using var zip = ZipFile.OpenRead(path);
                string part = family switch { "Word" => "word/document.xml", "Excel" => ext == "xlsb" ? "xl/workbook.bin" : "xl/workbook.xml", _ => "ppt/presentation.xml" };
                if (zip.GetEntry("[Content_Types].xml") == null || zip.GetEntry(part) == null) throw new IOException("Dateiinhalt und Office-Dateiendung passen nicht zusammen.");
            }
            else if (ext is "doc" or "xls" or "ppt" or "pps")
            {
                byte[] ole = [0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1];
                if (n < 8 || !header.AsSpan(0, 8).SequenceEqual(ole)) throw new IOException("Kein gültiger Office-Binärcontainer.");
            }
            else if (ext == "rtf" && (n < 5 || Encoding.ASCII.GetString(header, 0, 5) != "{\\rtf")) throw new IOException("Kein gültiger RTF-Dateikopf.");
            else if (ext is "odt" or "ods" or "odp")
            {
                using var zip = ZipFile.OpenRead(path); var mime = zip.GetEntry("mimetype") ?? throw new IOException("Kein OpenDocument-Container.");
                if (mime.Length > 256) throw new IOException("Ungültiger OpenDocument-Typ.");
                using var reader = new StreamReader(mime.Open()); var expected = family switch { "Word" => "text", "Excel" => "spreadsheet", _ => "presentation" };
                if (reader.ReadToEnd() != "application/vnd.oasis.opendocument." + expected) throw new IOException("OpenDocument-Inhalt und Dateiendung passen nicht zusammen.");
            }
            bool available = OfficeConverter.Available(family);
            return new(family, $"Microsoft {family} • .{ext} • {size}", available
                ? $"Microsoft {family} ist registriert und wird für den Export verwendet. Makros werden deaktiviert. " + (family == "Excel" ? "CSV exportiert nur das erste Arbeitsblatt und verliert Formeln / Formatierung." : "Ältere Formate und OpenDocument können Layoutdetails verlieren.")
                : $"Für diese Konvertierung wird die installierte Desktop-Version von Microsoft {family} benötigt. Sie wurde hier nicht gefunden.", available ? OfficeTargets(family) : [], family);
        }
        if (Images.Contains(ext)) return ImageConverter.Analyze(path);
        if (ext == "zip")
        {
            using var zip = ZipFile.OpenRead(path);
            return new("ZIP", $"ZIP-Archiv • {zip.Entries.Count} Einträge • {size}", "Wähle ZIP entpacken. Passwortgeschützte ZIPs werden noch nicht unterstützt.", []);
        }
        if (Audio.Contains(ext) || Video.Contains(ext))
        {
            return MediaConverter.Analyze(path, ffmpeg, ct);
        }
        if (ext is "txt" or "csv" or "tsv" or "json" or "xml" or "md" or "log")
        {
            if (info.Length > 50 * 1024 * 1024) return new("Text", $"Textdatei • {size}", "Textkonvertierung ist auf 50 MiB begrenzt.", []);
            var text = File.ReadAllText(path, new UTF8Encoding(false, true)); ct.ThrowIfCancellationRequested();
            if (text.Contains('\0')) throw new IOException("Die Datei enthält Binärdaten statt unterstütztem Text.");
            string[] options = ["txt"]; string notice = "UTF-8-Text erkannt. HTML stellt den Text als vorformatierten Inhalt dar.";
            if (ext is "csv" or "tsv")
            {
                var rows = Engine.ParseTable(text, ext == "csv" ? ',' : '\t');
                options = ["xlsx", "json", "csv", "tsv", "txt", "html"]; notice = $"{rows.Count} Zeilen erkannt. CSV: Komma, TSV: Tabulator. JSON und XLSX übernehmen Zellen als Zeichenketten.";
            }
            else if (ext == "json")
            {
                using var json = JsonDocument.Parse(text); bool table = json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.EnumerateArray().All(row => row.ValueKind == JsonValueKind.Array && row.EnumerateArray().All(cell => cell.ValueKind == JsonValueKind.String));
                options = table ? ["xlsx", "json", "csv", "tsv", "txt", "html"] : ["json", "txt", "html"];
                notice = table ? "JSON-Zeilentabelle erkannt: CSV und TSV sind verfügbar." : "Gültiges JSON erkannt. CSV/TSV benötigen ein Array aus Zeilenarrays mit Zeichenketten.";
            }
            else if (ext == "xml") { XDocument.Parse(text); options = ["xml", "txt", "html"]; }
            else if (ext == "txt") options = ["txt", "html"];
            options = options.Concat(new[] { "pdf", "docx" }).Distinct().ToArray();
            return new("Text", $".{ext.ToUpperInvariant()} • UTF-8-Text • {size}", notice + " PDF und Word exportieren den Text als Dokument.", Targets(options));
        }
        return new("Unbekannt", $".{ext} • {size}", "Für dieses Format gibt es noch keinen Konverter. Du kannst die Datei zippen oder verschlüsseln.", []);
    }
}
