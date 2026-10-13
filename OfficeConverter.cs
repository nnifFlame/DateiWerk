using System.Runtime.InteropServices;

namespace DateiWerk;
static class OfficeConverter
{
    public static bool Available(string family) => Type.GetTypeFromProgID(family + ".Application", false) != null;
    // Office automation requires a dedicated STA thread. A COM call cannot be safely interrupted.
    public static Task RunSta(Action action)
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Thread(() => { try { action(); result.SetResult(); } catch (Exception e) { result.SetException(e); } }) { IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA); worker.Start(); return result.Task;
    }
    public static void Convert(string input, string output, string target, CancellationToken ct)
    {
        string family = Formats.OfficeFamily(Formats.Extension(input));
        if (!Formats.OfficeTargets(family).Any(t => t.Extension == target)) throw new IOException("Dieses Office-Zielformat wird nicht unterstützt.");
        var type = Type.GetTypeFromProgID(family + ".Application", false) ?? throw new IOException($"Microsoft {family} (Desktop-Version) ist nicht installiert.");
        Engine.Atomic(output, tmp =>
        {
            // Real extension is required by Office; the extra file is moved into Atomic's staging path.
            var officePath = tmp + "." + target;
            try {
            object? application = null, collection = null, document = null, options = null, sheets = null, firstSheet = null;
            bool quitAllowed = false; bool? oldUpdateLinks = null;
            try
            {
                ct.ThrowIfCancellationRequested(); application = Activator.CreateInstance(type) ?? throw new IOException("Office konnte nicht gestartet werden.");
                dynamic app = application;
                collection = family switch { "Word" => (object)app.Documents, "Excel" => (object)app.Workbooks, _ => (object)app.Presentations };
                dynamic documents = collection;
                if (documents.Count != 0) throw new IOException($"Die verwendete {family}-Instanz enthält bereits geöffnete Dokumente. Bitte diese schließen, bevor DateiWerk konvertiert.");
                quitAllowed = true;
                app.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
                if (family == "Word")
                {
                    app.Visible = false; app.DisplayAlerts = 0; options = app.Options; dynamic wordOptions = options; oldUpdateLinks = wordOptions.UpdateLinksAtOpen; wordOptions.UpdateLinksAtOpen = false;
                    dynamic docs = collection;
                    document = docs.Open(FileName: Path.GetFullPath(input), ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false,
                        PasswordDocument: Guid.NewGuid().ToString(), PasswordTemplate: Guid.NewGuid().ToString(), Visible: false, NoEncodingDialog: true);
                    dynamic doc = document; ct.ThrowIfCancellationRequested();
                    if (target == "pdf") doc.ExportAsFixedFormat(officePath, 17, OpenAfterExport: false);
                    else doc.SaveAs2(FileName: officePath, FileFormat: target switch { "docx" => 16, "doc" => 0, "rtf" => 6, "txt" => 7, "odt" => 23, _ => throw new IOException("Unbekanntes Zielformat") }, AddToRecentFiles: false, Encoding: 65001);
                }
                else if (family == "Excel")
                {
                    app.Visible = false; app.DisplayAlerts = false; app.AskToUpdateLinks = false; app.EnableEvents = false;
                    dynamic books = collection;
                    document = books.Open(Filename: Path.GetFullPath(input), UpdateLinks: 0, ReadOnly: true, Password: "DW-no-pass", WriteResPassword: "DW-no-pass", IgnoreReadOnlyRecommended: true, AddToMru: false);
                    dynamic book = document; ct.ThrowIfCancellationRequested();
                    if (target == "pdf") book.ExportAsFixedFormat(0, officePath, OpenAfterPublish: false);
                    else
                    {
                        if (target == "csv") { sheets = book.Worksheets; dynamic worksheets = sheets; firstSheet = worksheets.Item[1]; dynamic first = firstSheet; first.Activate(); }
                        book.SaveAs(Filename: officePath, FileFormat: target switch { "xlsx" => 51, "xls" => 56, "csv" => 62, "ods" => 60, _ => throw new IOException("Unbekanntes Zielformat") }, AddToMru: false, Local: true);
                    }
                }
                else
                {
                    app.DisplayAlerts = 1; // ppAlertsNone
                    dynamic presentations = collection;
                    document = presentations.Open(Path.GetFullPath(input), -1, 0, 0); // read-only, original title, no window
                    dynamic presentation = document; ct.ThrowIfCancellationRequested();
                    presentation.SaveAs(officePath, target switch { "pptx" => 24, "ppt" => 1, "pdf" => 32, "odp" => 35, _ => throw new IOException("Unbekanntes Zielformat") });
                }
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(officePath)) throw new IOException("Office hat keine Ausgabedatei erzeugt.");
            }
            catch (COMException e) { throw new IOException($"Microsoft {family} konnte die Datei nicht konvertieren. Prüfe Office-Aktivierung, Dateischutz und Dateiintegrität. {e.Message}", e); }
            finally
            {
                Release(firstSheet); Release(sheets);
                if (document != null) { try { dynamic doc = document; if (family == "PowerPoint") doc.Close(); else doc.Close(false); } catch { } Release(document); }
                if (options != null && oldUpdateLinks.HasValue) { try { dynamic wordOptions = options; wordOptions.UpdateLinksAtOpen = oldUpdateLinks.Value; } catch { } }
                Release(collection); Release(options);
                if (application != null) { if (quitAllowed) { try { dynamic app = application; if (family == "Word") app.Quit(0); else app.Quit(); } catch { } } Release(application); }
                // Move only if export and cleanup completed normally. Catch path is cleaned below by caller.
            }
            ct.ThrowIfCancellationRequested(); File.Move(officePath, tmp, false);
            } finally { if (File.Exists(officePath)) File.Delete(officePath); }
        });
    }
    static void Release(object? value) { try { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); } catch { } }
}
