namespace DateiWerk;
sealed class SettingsWindow : Form
{
    public SettingsWindow(bool dark, Action<bool> themeChanged)
    {
        Text = "DateiWerk · Einstellungen"; Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        AutoScaleDimensions = new(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Font = new("Segoe UI", 10); ClientSize = new(580, 330); MinimumSize = new(520, 360); StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(241, 245, 249);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), ColumnCount = 1, AutoScroll = true };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100)); Controls.Add(layout);
        void Add(Control c) { c.Margin = new(0, 0, 0, 14); c.Anchor = AnchorStyles.Left; layout.RowStyles.Add(new(SizeType.AutoSize)); layout.Controls.Add(c, 0, layout.RowCount++); }
        Add(new Label { Text = "Darstellung", Font = new(Font, FontStyle.Bold), AutoSize = true });
        var toggle = new CheckBox { Text = "Dark Mode", Name = "DarkModeToggle", AutoSize = true, Checked = dark }; Add(toggle);
        Add(new Label { Text = "Explorer-Rechtsklick-Menü", Font = new(Font, FontStyle.Bold), AutoSize = true });
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Top };
        var install = new Button { Text = "Explorer-Menü einrichten", AutoSize = true };
        var remove = new Button { Text = "Explorer-Menü entfernen", AutoSize = true };
        buttons.Controls.AddRange([install, remove]); Add(buttons);
        var info = new Label { Text = "Windows 11: gegebenenfalls unter „Weitere Optionen anzeigen“.", AutoSize = true, MaximumSize = new(520, 0), ForeColor = Color.DimGray }; Add(info);
        var close = new Button { Text = "Schließen", AutoSize = true, DialogResult = DialogResult.OK }; Add(close); AcceptButton = close; CancelButton = close;
        bool changing = false;
        toggle.CheckedChanged += (_, _) =>
        {
            if (changing) return;
            try { Appearance.SaveDarkMode(toggle.Checked); dark = toggle.Checked; themeChanged(dark); Appearance.Apply(this, dark); }
            catch (Exception e) { changing = true; toggle.Checked = dark; changing = false; MessageBox.Show(this, e.Message, "Einstellung nicht gespeichert"); }
        };
        install.Click += (_, _) => { try { ExplorerMenu.Register(Environment.ProcessPath!); info.Text = "DateiWerk ist jetzt im Explorer-Rechtsklick-Menü eingerichtet."; } catch (Exception e) { MessageBox.Show(this, e.Message, "Explorer-Menü"); } };
        remove.Click += (_, _) => { try { ExplorerMenu.Unregister(); info.Text = "DateiWerk-Eintrag aus dem Explorer-Menü entfernt."; } catch (Exception e) { MessageBox.Show(this, e.Message, "Explorer-Menü"); } };
        Shown += (_, _) => Appearance.Apply(this, dark); Appearance.Apply(this, dark);
    }
}
