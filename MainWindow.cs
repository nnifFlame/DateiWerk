using System.Diagnostics;

namespace DateiWerk;
sealed class MainWindow : Form
{
    readonly TextBox source = new() { ReadOnly = true }, destination = new(), password = new() { UseSystemPasswordChar = true }, confirm = new() { UseSystemPasswordChar = true }, ffmpeg = new() { ReadOnly = true };
    readonly ComboBox format = new() { DropDownStyle = ComboBoxStyle.DropDownList }, compression = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button start = new() { Text = "Jetzt konvertieren", AutoSize = true }, cancel = new() { Text = "Abbrechen", AutoSize = true, Enabled = false }, openOutput = new() { Text = "Ausgabe anzeigen", AutoSize = true, Visible = false };
    readonly Label status = new() { Text = "Bereit", AutoSize = true }, analysisLabel = new() { AutoSize = true }, note = new() { AutoSize = true }, formatHelp = new() { AutoSize = true }, outputLabel = new() { Text = "Ausgabedatei", AutoSize = true };
    readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 8 };
    readonly TableLayoutPanel options = new() { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 1, Padding = new(16) };
    readonly Panel body = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new(24, 12, 24, 8) };
    readonly List<Button> tasks = [];
    readonly Button chooseFolder = new() { Text = "Ordner wählen", AutoSize = true };
    readonly TableLayoutPanel formatRow, zipRow, passRow, confirmRow, mediaRow;
    FileAnalysis? analysis;
    CancellationTokenSource? analysisCts, cts;
    int operation;
    bool initialized, analyzing, automaticTask = true;
    string suggested = "", lastOutput = "";
    UpdateInfo? pendingUpdate; string? downloadedUpdate; bool updating;
    bool darkMode = Appearance.LoadDarkMode();
    public MainWindow(LaunchOptions? launch = null)
    {
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "DateiWerk 0.6.0 • Dateien einfach bearbeiten"; ClientSize = new(1020, 840); MinimumSize = new(850, 700);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        StartPosition = FormStartPosition.CenterScreen; Font = new("Segoe UI", 10); BackColor = Color.FromArgb(241, 245, 249);
        var header = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Color.FromArgb(21, 40, 68), Padding = new(28, 10, 28, 12) };
        header.Controls.Add(new Label { Text = "Dateien umwandeln · ZIP-Archive · AES-256-Verschlüsselung", Dock = DockStyle.Bottom, Height = 26, ForeColor = Color.FromArgb(199, 216, 236) });
        header.Controls.Add(new Label { Text = "DateiWerk", Location = new(28, 10), Size = new(270, 56), Font = new("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.White });
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 112, Padding = new(28, 10, 28, 12), ColumnCount = 1, BackColor = Color.White };
        var commands = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        StylePrimary(start); commands.Controls.AddRange([start, cancel, openOutput]); footer.Controls.Add(commands, 0, 0); footer.Controls.Add(progress, 0, 1); footer.Controls.Add(status, 0, 2);
        Controls.Add(body); Controls.Add(footer); Controls.Add(header);
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new(0, 0, 0, 12) };
        content.ColumnStyles.Add(new(SizeType.Percent, 100)); body.Controls.Add(content);
        void Add(Control c) { c.Dock = DockStyle.Top; content.Controls.Add(c); }
        var settings = new Button { Text = "⚙  Einstellungen", AccessibleName = "Einstellungen öffnen", Name = "SettingsButton", Size = new(195, 42), Location = new(header.ClientSize.Width - 223, 18), Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.White, ForeColor = Color.FromArgb(21, 40, 68), FlatStyle = FlatStyle.Flat };
        settings.Click += (_, _) => { using var dialog = new SettingsWindow(darkMode, value => { darkMode = value; ApplyAppearance(); }); dialog.ShowDialog(this); };
        header.Controls.Add(settings); settings.BringToFront();
        header.Resize += (_, _) => settings.Location = new(header.ClientSize.Width - settings.Width - 28, 18);
        Shown += (_, _) => settings.Location = new(header.ClientSize.Width - settings.Width - 28, 18);
        var updatesBar = new FlowLayoutPanel { AutoSize = true, Margin = new(0, 0, 0, 8) };
        var automaticUpdates = new CheckBox { Text = "Updates automatisch", AutoSize = true, Checked = Updater.Automatic };
        var checkUpdates = new Button { Text = "Nach Updates suchen", AutoSize = true };
        automaticUpdates.CheckedChanged += (_, _) => { try { Updater.Automatic = automaticUpdates.Checked; } catch (Exception e) { MessageBox.Show(this, e.Message); } };
        checkUpdates.Click += async (_, _) => await CheckUpdates(true); updatesBar.Controls.AddRange([automaticUpdates, checkUpdates]); Add(updatesBar);
        Add(Title("1  Was möchtest du machen?"));
        var taskBar = new TableLayoutPanel { ColumnCount = 5, RowCount = 1, Height = 56, Dock = DockStyle.Top, Margin = new(0, 4, 0, 18) };
        string[] names = ["Konvertieren", "ZIP erstellen", "ZIP entpacken", "Verschlüsseln", "Entschlüsseln"];
        for (int i = 0; i < names.Length; i++) { int index = i; taskBar.ColumnStyles.Add(new(SizeType.Percent, 20)); var b = new Button { Text = names[i], Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, Margin = new(3) }; b.Click += (_, _) => { automaticTask = false; operation = index; UpdateView(); SuggestOutput(); }; tasks.Add(b); taskBar.Controls.Add(b, i, 0); }
        Add(taskBar); Add(Title("2  Datei auswählen"));
        var inputCard = new TableLayoutPanel { ColumnCount = 3, RowCount = 2, Dock = DockStyle.Top, AutoSize = true, BackColor = Color.White, Padding = new(16), Margin = new(0, 6, 0, 8) };
        inputCard.ColumnStyles.Add(new(SizeType.Percent, 100)); inputCard.ColumnStyles.Add(new(SizeType.Absolute, 145)); inputCard.ColumnStyles.Add(new(SizeType.Absolute, 145));
        source.PlaceholderText = "Datei auswählen oder hier hineinziehen"; source.Dock = DockStyle.Fill;
        var file = new Button { Text = "Datei wählen", Dock = DockStyle.Fill, AutoSize = true }; file.Click += (_, _) => { using var d = new OpenFileDialog { Title = "Datei zum Bearbeiten auswählen", Filter = "Alle Dateien|*.*|Office-Dateien|*.doc;*.docx;*.docm;*.xls;*.xlsx;*.xlsm;*.xlsb;*.ppt;*.pptx;*.pptm;*.rtf;*.odt;*.ods;*.odp|Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico" }; if (d.ShowDialog(this) == DialogResult.OK) source.Text = d.FileName; };
        chooseFolder.Dock = DockStyle.Fill; chooseFolder.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) source.Text = d.SelectedPath; };
        inputCard.Controls.Add(source, 0, 0); inputCard.Controls.Add(file, 1, 0); inputCard.Controls.Add(chooseFolder, 2, 0);
        inputCard.Controls.Add(new Label { Text = "Du kannst eine Datei oder einen Ordner ins Fenster ziehen. Es wird nichts hochgeladen.", AutoSize = true, ForeColor = Color.DimGray, Margin = new(3, 10, 3, 0) }, 0, 1); inputCard.SetColumnSpan(inputCard.GetControlFromPosition(0, 1)!, 3); Add(inputCard);
        var infoCard = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, BackColor = Color.FromArgb(226, 236, 249), Padding = new(16), Margin = new(0, 0, 0, 16) };
        analysisLabel.Font = new("Segoe UI", 11, FontStyle.Bold); note.ForeColor = Color.FromArgb(61, 81, 110); note.Margin = new(3, 7, 3, 0);
        infoCard.Controls.Add(analysisLabel); infoCard.Controls.Add(note); Add(infoCard);
        Add(Title("3  Format und Ausgabe festlegen")); options.BackColor = Color.White; options.Margin = new(0, 6, 0, 8); Add(options);
        options.ColumnStyles.Add(new(SizeType.Percent, 100));
        TableLayoutPanel Row(string label, Control value, Button? button = null)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new(0, 42), ColumnCount = 3, RowCount = 1, Margin = new(0, 5, 0, 5) };
            row.RowStyles.Add(new(SizeType.AutoSize));
            row.ColumnStyles.Add(new(SizeType.AutoSize)); row.ColumnStyles.Add(new(SizeType.Percent, 100)); row.ColumnStyles.Add(new(SizeType.AutoSize));
            row.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(3, 3, 16, 3) }, 0, 0); value.Anchor = AnchorStyles.Left | AnchorStyles.Right; row.Controls.Add(value, 1, 0);
            if (button != null) { button.Anchor = AnchorStyles.Left | AnchorStyles.Right; button.AutoSize = true; row.Controls.Add(button, 2, 0); }
            options.Controls.Add(row, 0, options.RowCount++); return row;
        }
        formatRow = Row("Zielformat", format);
        options.Controls.Add(formatHelp, 0, options.RowCount++); formatHelp.ForeColor = Color.DimGray; formatHelp.Margin = new(168, 2, 3, 8);
        compression.Items.AddRange(["Schnell – weniger Wartezeit", "Ausgewogen", "Kleinste Datei – dauert länger", "Ohne Komprimierung"]); compression.SelectedIndex = 0; zipRow = Row("ZIP-Stufe", compression);
        passRow = Row("Passwort", password); confirmRow = Row("Passwort wiederholen", confirm);
        var mediaChoose = new Button { Text = "FFmpeg wählen", AutoSize = true }; mediaChoose.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "FFmpeg|ffmpeg.exe" }; if (d.ShowDialog(this) == DialogResult.OK) ffmpeg.Text = d.FileName; }; mediaRow = Row("Medien-Konverter", ffmpeg, mediaChoose);
        var outputChoose = new Button { Text = "Ziel wählen", AutoSize = true }; outputChoose.Click += (_, _) =>
        {
            if (operation == 2) { using var d = new FolderBrowserDialog { Description = "Übergeordneten Ordner auswählen" }; if (d.ShowDialog(this) == DialogResult.OK) destination.Text = Path.Combine(d.SelectedPath, "Entpackt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); }
            else { using var d = new SaveFileDialog { FileName = Path.GetFileName(destination.Text), InitialDirectory = Path.GetDirectoryName(destination.Text), OverwritePrompt = true }; if (d.ShowDialog(this) == DialogResult.OK) destination.Text = d.FileName; }
        };
        var outRow = Row("Ausgabedatei", destination, outputChoose); var oldLabel = outRow.GetControlFromPosition(0, 0)!; outRow.Controls.Remove(oldLabel); oldLabel.Dispose(); outRow.Controls.Add(outputLabel, 0, 0); outputLabel.Anchor = AnchorStyles.Left; outputLabel.Margin = new(3, 3, 16, 3);
        var policy = new Label { Text = "Originale bleiben erhalten. Vorhandene Ausgaben werden nicht überschrieben.", AutoSize = true, ForeColor = Color.DimGray, Margin = new(3, 10, 3, 0) }; options.Controls.Add(policy, 0, options.RowCount++);
        source.TextChanged += async (_, _) => await Analyze(); ffmpeg.TextChanged += async (_, _) => await Analyze();
        format.SelectedIndexChanged += (_, _) => { if (initialized) { UpdateView(); SuggestOutput(); } }; destination.TextChanged += (_, _) => UpdateReady();
        start.Click += async (_, _) => await Run(); cancel.Click += (_, _) => { cts?.Cancel(); status.Text = "Abbruch angefordert. Laufende Office-Aufrufe werden zuerst beendet …"; };
        openOutput.Click += (_, _) => { if (File.Exists(lastOutput) || Directory.Exists(lastOutput)) { var psi = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; psi.ArgumentList.Add("/select,"); psi.ArgumentList.Add(lastOutput); Process.Start(psi); } };
        AllowDrop = true; DragEnter += (_, e) => e.Effect = cts == null && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (cts == null && e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) source.Text = paths[0]; };
        FormClosing += (_, e) => { if (cts != null) { cts.Cancel(); e.Cancel = true; status.Text = "Vorgang wird beendet. Danach kannst du das Fenster schließen."; } else analysisCts?.Cancel(); };
        body.Resize += (_, _) => { int width = Math.Max(400, body.ClientSize.Width - 100); analysisLabel.MaximumSize = new(width, 0); note.MaximumSize = new(width, 0); formatHelp.MaximumSize = new(Math.Max(250, width - 165), 0); status.MaximumSize = new(Math.Max(400, ClientSize.Width - 70), 0); };
        initialized = true; analysisLabel.Text = "Noch keine Datei ausgewählt"; note.Text = "Nach der Auswahl werden Dateityp und passende Ausgabeformate ermittelt."; UpdateView();
        if (launch?.Input != null) Shown += (_, _) => { if (launch.Operation.HasValue) { operation = launch.Operation.Value; automaticTask = false; } source.Text = launch.Input; };
        if (launch != null && Updater.Automatic) Shown += async (_, _) => await CheckUpdates(false);
    }
    static Label Title(string text) => new() { Text = text, AutoSize = true, Font = new("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(34, 51, 73), Margin = new(0, 0, 0, 2) };
    internal void SelectPreviewFile(string path) => source.Text = path;
    internal void SelectPreviewOperation(int value) { automaticTask = false; operation = value; UpdateView(); }
    internal bool AnalysisReady => !analyzing && analysis != null;
    internal void PreviewTheme(bool dark) { darkMode = dark; ApplyAppearance(); }
    void ApplyAppearance()
    {
        Appearance.Apply(this, darkMode);
        for (int i = 0; i < tasks.Count; i++)
        {
            tasks[i].BackColor = i == operation ? Color.FromArgb(30, 93, 176) : darkMode ? Appearance.Surface : Color.White;
            tasks[i].ForeColor = i == operation || darkMode ? Color.White : Color.FromArgb(34, 51, 73);
        }
    }
    static void StylePrimary(Button b) { b.BackColor = Color.FromArgb(30, 93, 176); b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; b.Padding = new(14, 5, 14, 5); }
    async Task Analyze()
    {
        analysisCts?.Cancel(); var current = new CancellationTokenSource(); analysisCts = current;
        string path = source.Text, ff = ffmpeg.Text; analysis = null; analyzing = true; analysisLabel.Text = "Datei wird analysiert …"; note.Text = "Dateityp, Inhalt und verfügbare Konverter werden geprüft."; format.Items.Clear(); UpdateReady();
        try
        {
            var result = await Task.Run(() => Formats.Analyze(path, ff, current.Token));
            if (current.IsCancellationRequested || IsDisposed) return;
            analysis = result; analysisLabel.Text = result.Description; note.Text = result.Notice;
            if (automaticTask) operation = result.Kind switch { "Ordner" => 1, "Verschlüsselt" => 4, "ZIP" => 2, _ => 0 };
            format.Items.AddRange(result.Targets); if (format.Items.Count > 0) format.SelectedIndex = 0;
            openOutput.Visible = false;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { if (!current.IsCancellationRequested && !IsDisposed) { analysis = new("Fehler", "Analyse nicht möglich", e.Message + " ZIP und Verschlüsselung sind weiterhin möglich.", []); analysisLabel.Text = analysis.Description; note.Text = analysis.Notice; } }
        finally { if (ReferenceEquals(analysisCts, current) && !IsDisposed) { analyzing = false; UpdateView(); SuggestOutput(); } current.Dispose(); if (ReferenceEquals(analysisCts, current)) analysisCts = null; }
    }
    void UpdateView()
    {
        if (!initialized) return;
        for (int i = 0; i < tasks.Count; i++) { tasks[i].BackColor = i == operation ? Color.FromArgb(30, 93, 176) : Color.White; tasks[i].ForeColor = i == operation ? Color.White : Color.FromArgb(34, 51, 73); }
        chooseFolder.Enabled = operation is 1 or 3; formatRow.Visible = operation == 0; formatHelp.Visible = operation is 0 or 3; zipRow.Visible = operation == 1 || operation == 3 && Directory.Exists(source.Text);
        passRow.Visible = operation >= 3; confirmRow.Visible = operation == 3; mediaRow.Visible = false;
        outputLabel.Text = operation == 2 ? "Neuer Zielordner" : "Ausgabedatei";
        start.Text = operation switch { 1 => "ZIP erstellen", 2 => "ZIP entpacken", 3 => "Mit AES-256 verschlüsseln", 4 => "Datei entschlüsseln", _ => "Jetzt konvertieren" };
        formatHelp.Text = format.SelectedItem is TargetFormat selected ? selected.Extension switch
        {
            "csv" when analysis?.OfficeFamily == "Excel" => "Nur das erste Arbeitsblatt. Formeln werden als Werte exportiert; CSV-Trennzeichen entsprechen den Windows-Regionseinstellungen.",
            "docx" when analysis?.Kind == "PDF" => "Bearbeitbarer Text aus allen PDF-Seiten. Layout, Bilder und Tabellenformatierung werden nicht übernommen; keine OCR.",
            "png.zip" or "jpg.zip" or "webp.zip" => "Alle PDF-Seiten werden einzeln als Bilder gespeichert und gemeinsam in ein ZIP gepackt.",
            "pdf" => "PDF ist für das Lesen und Teilen geeignet. Bilder werden auf A4 eingebettet; Texte erhalten ein einfaches Seitenlayout.",
            "doc" or "xls" or "ppt" or "odt" or "ods" or "odp" => "Formatwechsel kann Layout und Funktionen verändern. Makros werden bei der Konvertierung nicht ausgeführt.",
            "jpg" => "JPEG verliert Transparenz und kann Bildqualität verlieren.",
            _ => "Das Zielformat wird nach Dateityp und verfügbaren Konvertern angeboten."
        } : "Für diese Datei ist derzeit kein passendes Ausgabeformat verfügbar.";
        if (operation == 3) formatHelp.Text = Directory.Exists(source.Text) ? "Der ganze Ordner wird als ZIP gepackt und mit AES-256-GCM verschlüsselt. Entschlüsseln erzeugt wieder das ZIP zum Entpacken." : "Datei oder Ordner wählen. Mindestens 12 Passwortzeichen; Originale bleiben erhalten.";
        ApplyAppearance(); UpdateReady();
    }
    void UpdateReady()
    {
        bool file = File.Exists(source.Text), directory = Directory.Exists(source.Text);
        start.Enabled = cts == null && !updating && !analyzing && !string.IsNullOrWhiteSpace(destination.Text) && (operation switch
        {
            0 => file && format.SelectedItem is TargetFormat,
            1 => file || directory,
            2 => file && analysis?.Kind == "ZIP",
            3 => file || directory,
            4 => file && analysis?.Kind == "Verschlüsselt",
            _ => false
        });
    }
    async Task CheckUpdates(bool manual)
    {
        if (updating || cts != null) { if (manual) status.Text = "Update-Prüfung nach Ende des laufenden Vorgangs möglich."; return; }
        try
        {
            if (pendingUpdate == null) pendingUpdate = await Updater.CheckAsync();
            if (IsDisposed) return;
            if (pendingUpdate == null) { if (manual) status.Text = "DateiWerk ist aktuell."; return; }
            if (downloadedUpdate == null)
            {
                status.Text = "Update " + pendingUpdate.Version + " wird heruntergeladen …";
                downloadedUpdate = await Updater.DownloadAsync(pendingUpdate, new Progress<int>(percent => { if (!IsDisposed && cts == null) status.Text = $"Update wird heruntergeladen: {percent}%"; }));
            }
            if (IsDisposed) return;
            if (cts != null || (!manual && source.Text.Length > 0)) { status.Text = "Update bereit. Über „Nach Updates suchen“ nach deinem Vorgang installieren."; return; }
            updating = true; body.Enabled = false; UpdateReady(); status.Text = "Update wird installiert. DateiWerk startet anschließend neu.";
            Updater.StartInstaller(downloadedUpdate, pendingUpdate); Close();
        }
        catch (Exception e) { updating = false; body.Enabled = true; UpdateReady(); if (manual) MessageBox.Show(this, e.Message, "Update nicht möglich"); else if (cts == null && !IsDisposed) status.Text = "Update-Prüfung momentan nicht möglich. DateiWerk bleibt nutzbar."; }
    }
    void SuggestOutput()
    {
        if (source.Text.Length == 0) return;
        if (destination.Text.Length > 0 && destination.Text != suggested) return;
        var input = source.Text.TrimEnd(Path.DirectorySeparatorChar); var parent = Path.GetDirectoryName(input); if (string.IsNullOrEmpty(parent)) return;
        string name = Path.GetFileNameWithoutExtension(input), ext = (format.SelectedItem as TargetFormat)?.Extension ?? "";
        string next = operation switch { 1 => Path.GetFileName(input) + "-archiv.zip", 2 => name + "-entpackt", 3 => Path.GetFileName(input) + (Directory.Exists(input) ? ".zip.dwenc" : ".dwenc"), 4 => name + (Path.GetExtension(name).Length == 0 ? ".entschluesselt" : ""), _ => name + "-konvertiert" + (ext.Length > 0 ? "." + ext : "") };
        var suffix = operation == 2 ? "" : operation == 0 && ext.Length > 0 ? "." + ext : Path.GetExtension(next);
        var stem = suffix.Length == 0 ? next : next[..^suffix.Length];
        var candidate = Path.Combine(parent, next); int count = 2; while (File.Exists(candidate) || Directory.Exists(candidate)) { candidate = Path.Combine(parent, stem + "-" + count++ + suffix); }
        suggested = candidate; destination.Text = candidate;
    }
    async Task Run()
    {
        string input = source.Text, output = destination.Text.Trim(), target = (format.SelectedItem as TargetFormat)?.Extension ?? "", ff = ffmpeg.Text, pass = password.Text;
        int op = operation, level = compression.SelectedIndex;
        if (op == 0 && !output.EndsWith("." + target, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, $"Die Ausgabedatei muss die Endung .{target} besitzen. Bitte den Ausgabepfad anpassen.", "Ausgabe prüfen"); return; }
        if (op == 3 && (pass.Length < 12 || pass != confirm.Text)) { MessageBox.Show(this, "Mindestens 12 Zeichen und identische Passwortbestätigung erforderlich. Eine lange zufällige Passphrase ist empfehlenswert.", "Passwort prüfen"); return; }
        if (op == 4 && pass.Length == 0) { MessageBox.Show(this, "Bitte das Passwort eingeben.", "Passwort fehlt"); return; }
        cts = new(); var token = cts.Token; body.Enabled = false; start.Enabled = false; cancel.Enabled = true; openOutput.Visible = false; progress.Style = ProgressBarStyle.Marquee;
        status.Text = analysis?.OfficeFamily.Length > 0 && op == 0 ? "Microsoft Office konvertiert … Abbrechen greift nach dem laufenden Office-Aufruf." : "Wird verarbeitet …";
        var timer = Stopwatch.StartNew();
        try
        {
            await OfficeConverter.RunSta(() => { switch (op) { case 0: Engine.Convert(input, output, target, ff, token); break; case 1: Engine.Zip(input, output, level, token); break; case 2: Engine.Unzip(input, output, token); break; case 3: Engine.Encrypt(input, output, pass, level, token); break; case 4: Engine.Crypt(input, output, pass, true, token); break; } });
            lastOutput = Path.GetFullPath(output); status.Text = $"Fertig in {timer.Elapsed.TotalSeconds:F1} Sekunden. Ausgabe: {Path.GetFileName(output)}"; openOutput.Visible = true;
        }
        catch (OperationCanceledException) { status.Text = "Abgebrochen. Unvollständige Ausgabe wurde entfernt."; }
        catch (Exception e) { status.Text = "Vorgang fehlgeschlagen. Die Originaldatei bleibt erhalten."; MessageBox.Show(this, e.Message, "DateiWerk", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally { password.Clear(); confirm.Clear(); cts.Dispose(); cts = null; body.Enabled = true; cancel.Enabled = false; progress.Style = ProgressBarStyle.Blocks; SuggestOutput(); UpdateReady(); }
    }
}


