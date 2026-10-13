using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace DateiWerk;
static class ExplorerMenu
{
    static readonly string[] Keys = [@"Software\Classes\*\shell\DateiWerk", @"Software\Classes\Directory\shell\DateiWerk"];
    public static string Command(string executable) => $"\"{executable}\" --open \"%1\"";
    public static void Register(string executable)
    {
        executable = Path.GetFullPath(executable);
        if (!File.Exists(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Bitte die ausführbare DateiWerk-EXE verwenden.");
        foreach (var path in Keys)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue("", "DateiWerk", RegistryValueKind.String);
            key.SetValue("Icon", $"\"{executable}\",0", RegistryValueKind.String);
            key.SetValue("MultiSelectModel", "Single", RegistryValueKind.String);
            using var command = key.CreateSubKey("command"); command.SetValue("", Command(executable), RegistryValueKind.String);
        }
        Notify();
    }
    public static void Unregister()
    {
        foreach (var path in Keys) Registry.CurrentUser.DeleteSubKeyTree(path, false);
        Notify();
    }
    static void Notify() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}
record LaunchOptions(string? Input = null, int? Operation = null)
{
    public static LaunchOptions Parse(string[] args)
    {
        if (args.Length == 1 && (File.Exists(args[0]) || Directory.Exists(args[0]))) return new(Path.GetFullPath(args[0]));
        if (args.Length != 2) return new();
        int? action = args[0] switch { "--encrypt" => 3, "--zip" => 1, "--decrypt" => 4, "--unzip" => 2, _ => null };
        return args[0] is "--open" or "--encrypt" or "--zip" or "--decrypt" or "--unzip" ? new(Path.GetFullPath(args[1]), action) : new();
    }
}
