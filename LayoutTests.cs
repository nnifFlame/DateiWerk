using System.Drawing.Imaging;
namespace DateiWerk;
static class LayoutTests
{
    public static void Run()
    {
        var results = new List<string>();
        try
        {
            foreach (float factor in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                using var form = new MainWindow();
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-20000, -20000);
                form.Show(); if (factor != 1f) form.Scale(new SizeF(factor, factor));
                foreach (int mode in Enumerable.Range(0, 5))
                {
                    form.SelectPreviewOperation(mode); form.PerformLayout(); Application.DoEvents();
                    Check(form);
                    results.Add($"PASS: Layout {factor * 100:0}% / Modus {mode}.");
                }
                form.SelectPreviewOperation(2); form.PerformLayout(); Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(AppContext.BaseDirectory, $"Layout-{factor * 100:0}.png"), ImageFormat.Png);
                form.Close();
            }
        }
        catch (Exception e) { results.Add("FAIL: " + e); Environment.ExitCode = 1; }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "Layout-Testergebnis.txt"), results);
    }
    static void Check(Control control)
    {
        foreach (Control child in control.Controls)
        {
            if (!child.Visible) continue;
            if (child is Label label && label.Text is "Neuer Zielordner" or "Ausgabedatei" or "Passwort wiederholen")
            {
                var needed = label.GetPreferredSize(Size.Empty);
                if (label.Width < needed.Width || label.Height < needed.Height || label.Right > label.Parent!.ClientSize.Width || label.Bottom > label.Parent.ClientSize.Height)
                    throw new Exception($"Beschriftung abgeschnitten: {label.Text}, {label.Bounds}, benötigt {needed}.");
            }
            Check(child);
        }
    }
}
