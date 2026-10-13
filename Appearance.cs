using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace DateiWerk;
static class Appearance
{
    public static readonly Color Background = Color.FromArgb(17, 24, 39), Surface = Color.FromArgb(31, 41, 55), Text = Color.FromArgb(241, 245, 249);
    record Original(Color Back, Color Fore) { public bool Dark { get; set; } }
    static readonly ConditionalWeakTable<Control, Original> originals = new();
    public static bool LoadDarkMode() { using var key = Registry.CurrentUser.OpenSubKey(@"Software\DateiWerk"); return key?.GetValue("DarkMode") is int value && value != 0; }
    public static void SaveDarkMode(bool dark) { using var key = Registry.CurrentUser.CreateSubKey(@"Software\DateiWerk"); key.SetValue("DarkMode", dark ? 1 : 0, RegistryValueKind.DWord); }
    public static void Apply(Control control, bool dark)
    {
        Capture(control); ApplyCore(control, dark);
    }
    static void Capture(Control control)
    {
        originals.GetValue(control, c =>
        {
            if (c is Button b) b.Paint += (_, e) =>
            {
                if (b.Enabled || !originals.GetValue(b, x => new(x.BackColor, x.ForeColor)).Dark) return;
                using var background = new SolidBrush(b.BackColor); e.Graphics.FillRectangle(background, b.ClientRectangle);
                using var border = new Pen(Color.FromArgb(83, 102, 126)); e.Graphics.DrawRectangle(border, 0, 0, b.Width - 1, b.Height - 1);
                TextRenderer.DrawText(e.Graphics, b.Text, b.Font, b.ClientRectangle, Color.FromArgb(164, 181, 203), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            };
            return new(c.BackColor, c.ForeColor);
        });
        foreach (Control child in control.Controls) Capture(child);
    }
    static void ApplyCore(Control control, bool dark)
    {
        var original = originals.GetValue(control, c => new(c.BackColor, c.ForeColor));
        original.Dark = dark;
        control.BackColor = dark ? original.Back == Color.FromArgb(21, 40, 68) ? Color.FromArgb(15, 23, 42)
            : original.Back == Color.FromArgb(226, 236, 249) ? Color.FromArgb(30, 49, 73)
            : original.Back == Color.FromArgb(30, 93, 176) ? original.Back
            : control is TextBox or ComboBox ? Color.FromArgb(39, 51, 69)
            : original.Back == Color.White ? Surface : Background : original.Back;
        control.ForeColor = dark ? original.Fore == Color.DimGray || original.Fore == Color.FromArgb(61, 81, 110) ? Color.FromArgb(190, 205, 223) : Text : original.Fore;
        if (control is Button button) { button.UseVisualStyleBackColor = false; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = dark ? Color.FromArgb(83, 102, 126) : Color.FromArgb(145, 157, 172); }
        if (control is ComboBox combo) combo.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
        if (control is Form form && form.IsHandleCreated) { int enabled = dark ? 1 : 0; try { DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)); } catch (DllNotFoundException) { } }
        foreach (Control child in control.Controls) ApplyCore(child, dark);
        control.Invalidate();
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
