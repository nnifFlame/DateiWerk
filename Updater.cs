using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace DateiWerk;
record UpdateInfo(string Version, string TermsVersion, string Sha256, string SetupUrl);
static class Updater
{
    public const string Repository = "nnifFlame/DateiWerk", TermsVersion = "2026-10-13.1";
    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new(0, 0, 0);
    public static bool Automatic
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\DateiWerk"); return key?.GetValue("AutomaticUpdates") is not int value || value != 0; }
        set { using var key = Registry.CurrentUser.CreateSubKey(@"Software\DateiWerk"); key.SetValue("AutomaticUpdates", value ? 1 : 0, RegistryValueKind.DWord); }
    }
    public static bool CanInstallSilently(UpdateInfo update)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\DateiWerk"); var installed = key?.GetValue("InstallPath") as string;
        return installed != null && Path.GetFullPath(installed).TrimEnd(Path.DirectorySeparatorChar).Equals(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            && string.Equals(key?.GetValue("TermsVersion") as string, update.TermsVersion, StringComparison.Ordinal);
    }
    static HttpClient Client(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout }; client.DefaultRequestHeaders.UserAgent.ParseAdd("DateiWerk-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); return client;
    }
    internal static bool IsTrustedAsset(string url, string tag)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "github.com" && uri.IsDefaultPort
            && uri.AbsolutePath == $"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/DateiWerk-Setup.exe" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    }
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var client = Client(TimeSpan.FromSeconds(15));
        using var response = await client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode(); using var release = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (release.RootElement.GetProperty("draft").GetBoolean() || release.RootElement.GetProperty("prerelease").GetBoolean()) return null;
        string tag = release.RootElement.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version <= CurrentVersion) return null;
        var assets = release.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        var setup = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == "DateiWerk-Setup.exe");
        var manifest = assets.FirstOrDefault(a => a.GetProperty("name").GetString() == "update.json");
        if (setup.ValueKind == JsonValueKind.Undefined || manifest.ValueKind == JsonValueKind.Undefined) throw new IOException("Release enthält keinen vollständigen Installer.");
        string setupUrl = setup.GetProperty("browser_download_url").GetString() ?? "";
        string manifestUrl = manifest.GetProperty("browser_download_url").GetString() ?? "";
        if (!IsTrustedAsset(setupUrl, tag) || manifestUrl != $"https://github.com/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/update.json") throw new IOException("Ungültige Update-Quelle.");
        string manifestText = await client.GetStringAsync(manifestUrl, ct); if (manifestText.Length > 65536) throw new IOException("Update-Beschreibung ist zu groß.");
        using var info = JsonDocument.Parse(manifestText); string publishedVersion = info.RootElement.GetProperty("version").GetString() ?? "";
        string hash = info.RootElement.GetProperty("sha256").GetString() ?? "", terms = info.RootElement.GetProperty("termsVersion").GetString() ?? "";
        if (!Version.TryParse(publishedVersion, out var parsed) || parsed != version || hash.Length != 64 || !hash.All(Uri.IsHexDigit) || terms.Length is < 1 or > 100) throw new IOException("Ungültige Update-Beschreibung.");
        if (setup.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String && !string.Equals(digest.GetString(), "sha256:" + hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update-Prüfsummen passen nicht zusammen.");
        return new(publishedVersion, terms, hash, setupUrl);
    }
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        if (!IsTrustedAsset(update.SetupUrl, "v" + update.Version)) throw new IOException("Ungültige Update-Quelle.");
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DateiWerk", "Updates"); Directory.CreateDirectory(root);
        string target = Path.Combine(root, "DateiWerk-Setup-" + update.Version + ".exe"), temporary = target + "." + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using var client = Client(TimeSpan.FromMinutes(20)); using var response = await client.GetAsync(update.SetupUrl, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
            long expected = response.Content.Headers.ContentLength ?? 0, written = 0;
            if (expected > 1024L * 1024 * 1024) throw new IOException("Update-Datei überschreitet das Größenlimit.");
            await using (var input = await response.Content.ReadAsStreamAsync(ct))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
            {
                var buffer = new byte[1024 * 1024]; int count;
                while ((count = await input.ReadAsync(buffer, ct)) > 0) { written += count; if (written > 1024L * 1024 * 1024) throw new IOException("Update-Datei zu groß."); await output.WriteAsync(buffer.AsMemory(0, count), ct); if (expected > 0) progress?.Report((int)Math.Min(100, written * 100 / expected)); }
            }
            using (var file = File.OpenRead(temporary)) { string actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct)); if (!actual.Equals(update.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update-Datei hat eine ungültige Prüfsumme."); }
            File.Move(temporary, target, true); return target;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void StartInstaller(string setup, UpdateInfo update)
    {
        using (var file = File.OpenRead(setup)) if (!Convert.ToHexString(SHA256.HashData(file)).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase)) throw new IOException("Update-Datei wurde nach dem Download verändert.");
        var psi = new ProcessStartInfo(setup) { UseShellExecute = true };
        if (CanInstallSilently(update)) foreach (var arg in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/UPDATE=1" }) psi.ArgumentList.Add(arg);
        // When terms changed or this is a portable copy, the normal installer requires new interactive acceptance.
        Process.Start(psi);
    }
}
