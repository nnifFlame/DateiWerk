using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace DateiWerk;
static class MediaConverter
{
    static readonly object Gate = new(); static string? tools;
    public static string Tools()
    {
        lock (Gate)
        {
            if (tools != null) return tools;
            using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("DateiWerk.Media.zip") ?? throw new IOException("Das integrierte Medienpaket fehlt in diesem Build.");
            // Cache is restricted to exact filenames from our embedded package, never user archive paths.
            var root = Path.Combine(Path.GetTempPath(), "DateiWerk-Medien-v03"); Directory.CreateDirectory(root);
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                if (entry.Name is not ("ffmpeg.exe" or "ffprobe.exe")) continue;
                var path = Path.Combine(root, entry.Name); using var source = entry.Open();
                using var memory = new MemoryStream(); source.CopyTo(memory); byte[] bytes = memory.ToArray();
                var expected = SHA256.HashData(bytes); bool valid = false;
                if (File.Exists(path)) { using var file = File.OpenRead(path); valid = SHA256.HashData(file).AsSpan().SequenceEqual(expected); }
                if (!valid) { var temp = path + "." + Guid.NewGuid().ToString("N") + ".part"; try { File.WriteAllBytes(temp, bytes); File.Move(temp, path, true); } finally { if (File.Exists(temp)) File.Delete(temp); } }
            }
            if (!File.Exists(Path.Combine(root, "ffmpeg.exe")) || !File.Exists(Path.Combine(root, "ffprobe.exe"))) throw new IOException("Medienpaket ist unvollständig.");
            tools = root; return root;
        }
    }
    public static string Ffmpeg(string custom) => File.Exists(custom) ? custom : Path.Combine(Tools(), "ffmpeg.exe");
    internal static string Execute(string exe, IEnumerable<string> args, CancellationToken ct, int timeoutSeconds = 0)
    {
        var psi = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi) ?? throw new IOException("Medienkonverter konnte nicht gestartet werden.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); var timer = Stopwatch.StartNew();
        while (!process.WaitForExit(100))
        {
            if (ct.IsCancellationRequested || (timeoutSeconds > 0 && timer.Elapsed.TotalSeconds > timeoutSeconds))
            {
                process.Kill(true); process.WaitForExit(); ct.ThrowIfCancellationRequested(); throw new IOException("Die Medienanalyse hat das Zeitlimit überschritten.");
            }
        }
        var error = stderr.GetAwaiter().GetResult(); string result = stdout.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new IOException("Medienkonverter: " + error[..Math.Min(error.Length, 2000)]);
        ct.ThrowIfCancellationRequested(); return result;
    }
    public static FileAnalysis Analyze(string path, string custom, CancellationToken ct)
    {
        var probe = Path.Combine(Tools(), "ffprobe.exe");
        var result = Execute(probe, ["-v", "error", "-show_entries", "stream=codec_type,codec_name", "-of", "json", path], ct, 20);
        using var json = JsonDocument.Parse(result); var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        bool audio = streams.Any(s => s.TryGetProperty("codec_type", out var t) && t.GetString() == "audio"), video = streams.Any(s => s.TryGetProperty("codec_type", out var t) && t.GetString() == "video");
        if (!audio && !video) throw new IOException("Die Datei enthält keine unterstützten Audio- oder Videoströme.");
        var options = new List<TargetFormat>(); if (video) options.AddRange(Formats.Targets("mp4", "mkv", "webm")); if (audio) options.AddRange(Formats.Targets("mp3", "wav", "flac", "ogg"));
        var codecs = string.Join(", ", streams.Where(s => s.TryGetProperty("codec_name", out _)).Select(s => s.GetProperty("codec_name").GetString()));
        return new(video ? "Video" : "Audio", $"{(video ? "Video" : "Audio")} • {codecs} • {new FileInfo(path).Length / 1048576d:F1} MiB", "Audio-/Videokonverter integriert. Tatsächliche Medienströme wurden geprüft. Es werden die erste Video- und Audiospur exportiert; Untertitel und Zusatzspuren werden nicht übernommen.", options.ToArray(), Media: true);
    }
    public static void Convert(string input, string output, string target, string custom, CancellationToken ct)
    {
        Engine.Atomic(output, tmp =>
        {
            var args = new List<string> { "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-i", input };
            if (target is "mp3" or "wav" or "flac" or "ogg") args.AddRange(["-map", "0:a:0", "-vn", "-c:a", target switch { "mp3" => "libmp3lame", "wav" => "pcm_s16le", "flac" => "flac", _ => "libvorbis" }]);
            else { args.AddRange(["-map", "0:v:0", "-map", "0:a:0?", "-c:v", target == "webm" ? "libvpx-vp9" : "libx264", "-c:a", target == "webm" ? "libopus" : "aac"]); if (target != "webm") args.AddRange(["-preset", "veryfast", "-pix_fmt", "yuv420p"]); }
            args.AddRange(["-f", target == "mkv" ? "matroska" : target, tmp]); Execute(Ffmpeg(custom), args, ct);
        });
    }
}
