using System.Drawing.Imaging;
using SkiaSharp;

namespace DateiWerk;
static class ImageConverter
{
    public static Image Load(string input)
    {
        if (Formats.Extension(input) != "webp") return Image.FromFile(input);
        using var bitmap = SKBitmap.Decode(input) ?? throw new IOException("WebP konnte nicht gelesen werden.");
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100); using var stream = encoded.AsStream(); using var image = Image.FromStream(stream); return new Bitmap(image);
    }
    public static FileAnalysis Analyze(string input)
    {
        using var image = Load(input);
        if ((long)image.Width * image.Height > 100_000_000) throw new IOException("Bilder über 100 Megapixel werden nicht unterstützt.");
        bool multiple = image.FrameDimensionsList.Any(d => image.GetFrameCount(new FrameDimension(d)) > 1);
        if (Formats.Extension(input) == "webp") { using var stream = File.OpenRead(input); using var codec = SKCodec.Create(stream); multiple = codec != null && codec.FrameCount > 1; }
        return new("Bild", $"Bild • {image.Width} × {image.Height} Pixel • {new FileInfo(input).Length / 1024d:F1} KiB", multiple ? "Animationen und mehrseitige Bilder sind noch nicht konvertierbar." : "Bildkonverter integriert. JPEG verliert Transparenz und Qualität; GIF reduziert Farben. PDF bettet das Bild auf einer A4-Seite ein.", multiple ? [] : Formats.Targets("png", "jpg", "webp", "bmp", "gif", "tiff", "pdf"));
    }
    public static void Convert(string input, string output, string target, CancellationToken ct)
    {
        Engine.Atomic(output, tmp =>
        {
            ct.ThrowIfCancellationRequested(); if (target == "pdf") PdfConverter.ImageToPdf(input, tmp);
            else
            {
                using var image = Load(input);
                if (target == "webp") { using var stream = new MemoryStream(); image.Save(stream, ImageFormat.Png); stream.Position = 0; using var bitmap = SKBitmap.Decode(stream); using var encoded = bitmap.Encode(SKEncodedImageFormat.Webp, 92); using var file = File.Create(tmp); encoded.SaveTo(file); }
                else { var format = target switch { "jpg" => ImageFormat.Jpeg, "bmp" => ImageFormat.Bmp, "gif" => ImageFormat.Gif, "tiff" => ImageFormat.Tiff, _ => ImageFormat.Png }; if (target == "jpg") { using var flattened = new Bitmap(image.Width, image.Height); using var g = Graphics.FromImage(flattened); g.Clear(Color.White); g.DrawImage(image, 0, 0); flattened.Save(tmp, format); } else image.Save(tmp, format); }
            }
            ct.ThrowIfCancellationRequested();
        });
    }
}
