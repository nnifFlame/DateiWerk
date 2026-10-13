using System.Text.Json;

namespace DateiWerk;
static class MediaTests
{
    public static void Run(string root)
    {
        Directory.CreateDirectory(root); var results = new List<string>(); int count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; results.Add("PASS: " + name); }
        try
        {
            string ffmpeg = MediaConverter.Ffmpeg("");
            string audio = Path.Combine(root, "sine.wav"), video = Path.Combine(root, "source.mp4"), silent = Path.Combine(root, "silent.mp4");
            MediaConverter.Execute(ffmpeg, ["-nostdin", "-y", "-loglevel", "error", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "0.4", audio], default, 30);
            MediaConverter.Execute(ffmpeg, ["-nostdin", "-y", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=blue:s=160x120:r=5", "-f", "lavfi", "-i", "sine=frequency=440", "-t", "0.4", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", video], default, 30);
            MediaConverter.Execute(ffmpeg, ["-nostdin", "-y", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=blue:s=160x120:r=5", "-t", "0.4", "-c:v", "libx264", "-pix_fmt", "yuv420p", silent], default, 30);
            Check(Formats.Analyze(audio, "", default).Targets.Length == 4, "Audioformate ohne manuelle Konverterwahl verfügbar");
            Check(Formats.Analyze(video, "", default).Targets.Length == 7, "Video mit Audiospur bietet sieben Ausgabeformate");
            Check(Formats.Analyze(silent, "", default).Targets.All(t => t.Extension is "mp4" or "mkv" or "webm"), "Video ohne Ton bietet keinen leeren Audioexport");
            foreach (string target in new[] { "mp3", "wav", "flac", "ogg", "mp4", "mkv", "webm" })
            {
                string output = Path.Combine(root, "export." + target); if (File.Exists(output)) File.Delete(output);
                Engine.Convert(target is "mp4" or "mkv" or "webm" ? video : audio, output, target, "", default);
                Check(new FileInfo(output).Length > 0, "Medienexport → " + target);
                var analyzed = Formats.Analyze(output, "", default); Check(analyzed.Kind == (target is "mp4" or "mkv" or "webm" ? "Video" : "Audio"), "Export wieder lesbar: " + target);
            }
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            string cancelled = Path.Combine(root, "cancelled.mp3"); try { Engine.Convert(audio, cancelled, "mp3", "", cancellation.Token); throw new Exception("Abbruch ignoriert"); } catch (OperationCanceledException) { Check(!File.Exists(cancelled), "Medienabbruch ohne Ausgabe"); }
            results.Add($"PASS: {count} Medienkonverterprüfungen.");
        }
        catch (Exception e) { results.Add("FAIL: " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(root, "Medien-Testergebnis.txt"), results);
    }
}
