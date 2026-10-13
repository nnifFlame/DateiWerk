using System.Drawing.Imaging;
using Microsoft.Win32;
namespace DateiWerk;
static class ThemeTests
{
    public static void Run()
    {
        var report = new List<string>();
        using var previousKey = Registry.CurrentUser.OpenSubKey(@"Software\DateiWerk");
        var previousDark = previousKey?.GetValue("DarkMode");
        var previousKind = previousDark == null ? RegistryValueKind.DWord : previousKey!.GetValueKind("DarkMode");
        try
        {
            using var form = new MainWindow(); form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-20000, -20000); form.Show();
            form.PreviewTheme(false);
            var original = All(form).ToDictionary(c => c, c => (c.BackColor, c.ForeColor));
            if (!All(form).Any(c => c.Name == "SettingsButton") || All(form).Any(c => c.Text.StartsWith("Explorer-Menü"))) throw new Exception("Einstellungen nicht gebündelt.");
            report.Add("PASS: Zahnrad im Hauptfenster; Explorer-Aktionen nur in Einstellungen.");
            form.PreviewTheme(true);
            foreach (int mode in Enumerable.Range(0, 5))
            {
                form.SelectPreviewOperation(mode); Application.DoEvents();
                if (form.BackColor != Appearance.Background) throw new Exception("Dark Mode nach Betriebswechsel verloren.");
                foreach (var c in All(form).Where(c => c.Visible && c.Enabled && c is Label or TextBox or Button or CheckBox))
                    if (Contrast(c.ForeColor, c.BackColor) < 4.5) throw new Exception($"Kontrast zu niedrig: {c.Text}");
            }
            report.Add("PASS: Dark Mode in fünf Betriebsarten; Textkontrast mindestens 4,5:1.");
            Capture(form, "Dark-Mode.png"); form.SelectPreviewOperation(0); form.PreviewTheme(false);
            foreach (var (control, colors) in original) if ((control.BackColor, control.ForeColor) != colors) throw new Exception("Helles Design nicht wiederhergestellt: " + control.Text);
            report.Add("PASS: Wechsel zurück stellt die hellen Farben wieder her."); Capture(form, "Light-Mode.png");
            using var settings = new SettingsWindow(true, _ => { }); settings.ShowInTaskbar = false; settings.StartPosition = FormStartPosition.Manual; settings.Location = new(-20000, -20000); settings.Show(); Application.DoEvents();
            if (!All(settings).Any(c => c.Name == "DarkModeToggle") || All(settings).Count(c => c.Text.StartsWith("Explorer-Menü")) != 2) throw new Exception("Einstellungsaktionen fehlen.");
            var toggle = (CheckBox)All(settings).Single(c => c.Name == "DarkModeToggle");
            toggle.Checked = false; if (Appearance.LoadDarkMode()) throw new Exception("Helles Design nicht gespeichert.");
            toggle.Checked = true; if (!Appearance.LoadDarkMode()) throw new Exception("Dark Mode nicht gespeichert.");
            report.Add("PASS: Designwechsel über Einstellungen gespeichert und erneut geladen.");
            Capture(settings, "Dark-Einstellungen.png"); report.Add("PASS: Einstellungsfenster mit Dark Mode und beiden Explorer-Aktionen."); settings.Close(); form.Close();
        }
        catch (Exception e) { report.Add("FAIL: " + e); Environment.ExitCode = 1; }
        finally { using var restoreKey = Registry.CurrentUser.CreateSubKey(@"Software\DateiWerk"); if (previousDark == null) restoreKey.DeleteValue("DarkMode", false); else restoreKey.SetValue("DarkMode", previousDark, previousKind); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "Theme-Testergebnis.txt"), report);
    }
    static IEnumerable<Control> All(Control c) { yield return c; foreach (Control child in c.Controls) foreach (var nested in All(child)) yield return nested; }
    static void Capture(Form form, string name) { form.PerformLayout(); Application.DoEvents(); using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(AppContext.BaseDirectory, name), ImageFormat.Png); }
    static double Contrast(Color a, Color b) { double x = Light(a), y = Light(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
    static double Light(Color c) { static double Linear(byte n) { double v = n/255.0; return v <= .04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); } return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B); }
}
