using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DateiWerk;
static class Program
{
    [STAThread] static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--register-explorer" or "--unregister-explorer")
        {
            try { if (args[0] == "--register-explorer") ExplorerMenu.Register(Environment.ProcessPath!); else ExplorerMenu.Unregister(); }
            catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Explorer-Fehler.txt"), e.ToString()); Environment.ExitCode = 1; }
            return;
        }
        if (args.Contains("--self-test")) { Tests.Run(); return; }
        if (args.Contains("--layout-test")) { ApplicationConfiguration.Initialize(); LayoutTests.Run(); return; }
        if (args.Contains("--theme-test")) { ApplicationConfiguration.Initialize(); ThemeTests.Run(); return; }
        if (args.Contains("--check-update-public")) { try { var update = Updater.CheckPublicDownloadAsync().GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Update-Public-Testergebnis.txt"), update == null ? "PASS: Öffentliche Update-Beschreibung erfolgreich geprüft; keine neuere Version." : "PASS: Update erkannt: " + update.Version); } catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Update-Public-Testergebnis.txt"), "FAIL: " + e); Environment.ExitCode = 1; } return; }
        if (args.Length == 1 && args[0] == "--check-update") { try { var update = Updater.CheckAsync().GetAwaiter().GetResult(); File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Update-Testergebnis.txt"), update == null ? "PASS: GitHub-Update-Prüfung erfolgreich; keine neuere stabile Version." : "PASS: Update verfügbar: " + update.Version); } catch (Exception e) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Update-Testergebnis.txt"),"FAIL: "+e); Environment.ExitCode=1; } return; }
        if (args.Contains("--office-test")) { OfficeTests.Run(); return; }
        if (args.Length == 2 && args[0] == "--pdf-test") { PdfTests.Run(args[1]); return; }
        if (args.Length == 2 && args[0] == "--media-test") { MediaTests.Run(args[1]); return; }
        if (args.Length == 2 && args[0] == "--document-test") { DocumentTests.Run(args[1]); return; }
        if (args.Contains("--ui-test"))
        {
            ApplicationConfiguration.Initialize(); using var form = new MainWindow(); form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000,-20000); form.Show();
            bool external = args.Length >= 2 && args[0] == "--ui-test"; var sample = external ? args[1] : Path.Combine(AppContext.BaseDirectory,"Beispieltabelle.csv"); if (!external) File.WriteAllText(sample,"Name,Anzahl\r\nBilder,12\r\nDokumente,8\r\n"); if(args.Contains("--encrypt"))form.SelectPreviewOperation(3); form.SelectPreviewFile(sample);
            var timer = Stopwatch.StartNew(); while (!form.AnalysisReady && timer.Elapsed.TotalSeconds < 5) { Application.DoEvents(); Thread.Sleep(10); }
            if (!form.AnalysisReady) throw new Exception("UI-Analyse wurde nicht abgeschlossen.");
            form.PerformLayout(); using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(Path.Combine(AppContext.BaseDirectory,"Oberflaeche.png"), ImageFormat.Png); form.Close(); if (!external) File.Delete(sample); return;
        }
        ApplicationConfiguration.Initialize(); Application.Run(new MainWindow(LaunchOptions.Parse(args)));
    }
}

static class Engine
{
    const int Chunk=1024*1024, Iterations=600000;
    static readonly byte[] Magic=Encoding.ASCII.GetBytes("DWENC001");
    public static void Atomic(string output, Action<string> write)
    {
        output=Path.GetFullPath(output);if(File.Exists(output)||Directory.Exists(output))throw new IOException("Die Ausgabe existiert bereits. Bitte einen neuen Namen wählen.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);var tmp=output+"."+Guid.NewGuid().ToString("N")+".part";
        try{write(tmp);File.Move(tmp,output,false);}finally{if(File.Exists(tmp))File.Delete(tmp);}
    }
    static void Copy(Stream input,Stream output,CancellationToken ct){var b=new byte[Chunk];int n;while((n=input.Read(b))>0){ct.ThrowIfCancellationRequested();output.Write(b,0,n);}ct.ThrowIfCancellationRequested();}
    public static void Zip(string input,string output,int level,CancellationToken ct)
    {
        var root=Path.GetFullPath(input);var dst=Path.GetFullPath(output);
        if(Directory.Exists(root)&&dst.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Das ZIP-Ziel muss außerhalb des Quellordners liegen.");
        var cl=level switch{0=>CompressionLevel.Fastest,1=>CompressionLevel.Optimal,2=>CompressionLevel.SmallestSize,_=>CompressionLevel.NoCompression};
        Atomic(output,tmp=>{using var zip=ZipFile.Open(tmp,ZipArchiveMode.Create);if(File.Exists(root)){Add(root,Path.GetFileName(root));return;}
            Walk(root);
            void Walk(string dir){ct.ThrowIfCancellationRequested();foreach(var path in Directory.EnumerateFileSystemEntries(dir)){ct.ThrowIfCancellationRequested();if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Verknüpfungen im Quellordner werden nicht unterstützt: "+path);var rel=Path.GetRelativePath(root,path).Replace('\\','/');if(Directory.Exists(path)){zip.CreateEntry(rel+"/");Walk(path);}else Add(path,rel);}}
            void Add(string path,string name){ct.ThrowIfCancellationRequested();var entry=zip.CreateEntry(name,cl);entry.LastWriteTime=File.GetLastWriteTime(path).Year is >=1980 and <=2107?File.GetLastWriteTime(path):new DateTime(1980,1,1);using var a=File.OpenRead(path);using var b=entry.Open();Copy(a,b,ct);}
        });
    }
    public static void Unzip(string input,string output,CancellationToken ct)
    {
        output=Path.GetFullPath(output);if(Directory.Exists(output)||File.Exists(output))throw new IOException("Bitte einen neuen, noch nicht vorhandenen Zielordner wählen.");
        var tmp=output+"."+Guid.NewGuid().ToString("N")+".part";Directory.CreateDirectory(tmp);
        try{using var zip=ZipFile.OpenRead(input);if(zip.Entries.Count>100000)throw new IOException("Archiv enthält zu viele Einträge (Limit 100.000).");long total=0;var prefix=tmp+Path.DirectorySeparatorChar;
            foreach(var e in zip.Entries){ct.ThrowIfCancellationRequested();total=checked(total+e.Length);if(total>100L*1024*1024*1024)throw new IOException("Entpacklimit: 100 GiB.");var name=e.FullName.Replace('/',Path.DirectorySeparatorChar);if(name.Contains(':')||Path.IsPathRooted(name))throw new IOException("Unsicherer Archivpfad.");var path=Path.GetFullPath(Path.Combine(tmp,name));if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Unsicherer Archivpfad.");if(e.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}Directory.CreateDirectory(Path.GetDirectoryName(path)!);using var a=e.Open();using var b=new FileStream(path,FileMode.CreateNew);Copy(a,b,ct);}
            ct.ThrowIfCancellationRequested();Directory.Move(tmp,output);
        }finally{if(Directory.Exists(tmp))Directory.Delete(tmp,true);}
    }
    public static void Crypt(string input,string output,string password,bool decrypt,CancellationToken ct)
    {
        Atomic(output,tmp=>{using var src=File.OpenRead(input);using var dst=new FileStream(tmp,FileMode.CreateNew);byte[] header;
            if(decrypt){header=new byte[44];src.ReadExactly(header);if(!header.AsSpan(0,8).SequenceEqual(Magic)||BitConverter.ToInt32(header,8)!=Iterations)throw new IOException("Kein unterstütztes DateiWerk-Archiv.");}
            else{header=new byte[44];Magic.CopyTo(header,0);BitConverter.GetBytes(Iterations).CopyTo(header,8);RandomNumberGenerator.Fill(header.AsSpan(12,16));RandomNumberGenerator.Fill(header.AsSpan(28,8));BitConverter.GetBytes(src.Length).CopyTo(header,36);dst.Write(header);}
            long length=BitConverter.ToInt64(header,36);if(length<0)throw new IOException("Ungültige Dateigröße.");
            var key=Rfc2898DeriveBytes.Pbkdf2(password,header.AsSpan(12,16),Iterations,HashAlgorithmName.SHA256,32);
            var plain=new byte[Chunk];var cipher=new byte[Chunk];var tag=new byte[16];var nonce=new byte[12];header.AsSpan(28,8).CopyTo(nonce);var aad=new byte[48];header.CopyTo(aad,0);
            try{using var aes=new AesGcm(key,16);long done=0;uint index=0;
                do{ct.ThrowIfCancellationRequested();var count=(int)Math.Min(Chunk,length-done);System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8),index);System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(44),index);
                    if(decrypt){src.ReadExactly(cipher.AsSpan(0,count));src.ReadExactly(tag);aes.Decrypt(nonce,cipher.AsSpan(0,count),tag,plain.AsSpan(0,count),aad);dst.Write(plain,0,count);}
                    else{src.ReadExactly(plain.AsSpan(0,count));aes.Encrypt(nonce,plain.AsSpan(0,count),cipher.AsSpan(0,count),tag,aad);dst.Write(cipher,0,count);dst.Write(tag);}
                    done+=count;if(index==uint.MaxValue)throw new IOException("Datei zu groß.");index++;
                }while(done<length);
                if(src.ReadByte()!=-1)throw new IOException("Datei wurde verändert oder enthält unerwartete Zusatzdaten.");ct.ThrowIfCancellationRequested();
            }catch(AuthenticationTagMismatchException){throw new IOException("Falsches Passwort oder beschädigte / manipulierte Datei.");}
            finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(plain);}
        });
    }
    public static void Encrypt(string input, string output, string password, int compression, CancellationToken ct)
    {
        if (!Directory.Exists(input)) { Crypt(input, output, password, false, ct); return; }
        var root = Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(output);
        if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("Die verschlüsselte Ausgabe muss außerhalb des Quellordners liegen.");
        if ((File.GetAttributes(input) & FileAttributes.ReparsePoint) != 0) throw new IOException("Ordnerverknüpfungen werden nicht unterstützt.");
        if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Die Ausgabe existiert bereits. Bitte einen neuen Namen wählen.");
        var archive = Path.Combine(Path.GetTempPath(), "DateiWerk-Ordner-" + Guid.NewGuid().ToString("N") + ".zip");
        try { ct.ThrowIfCancellationRequested(); Zip(input, archive, compression, ct); Crypt(archive, output, password, false, ct); }
        finally { if (File.Exists(archive)) File.Delete(archive); }
    }
    public static void Convert(string input,string output,string target,string ffmpeg,CancellationToken ct)
    {
        var supported = Formats.Analyze(input, ffmpeg, ct);
        if (!supported.Targets.Any(t => t.Extension == target)) throw new IOException("Das gewählte Zielformat ist für diese Datei nicht verfügbar. " + supported.Notice);
        if (supported.Kind == "PDF") { PdfConverter.Convert(input, output, target, ct); return; }
        if (supported.Kind == "Bild") { ImageConverter.Convert(input, output, target, ct); return; }
        if (supported.Media) { MediaConverter.Convert(input, output, target, ffmpeg, ct); return; }
        if (target == "xlsx" && supported.Kind == "Text") { TableConverter.ToXlsx(input, output, ct); return; }
        if (supported.OfficeFamily.Length > 0) { OfficeConverter.Convert(input, output, target, ct); return; }
        var ext=Path.GetExtension(input).TrimStart('.').ToLowerInvariant();
        string[] images=["png","jpg","jpeg","bmp","gif","tif","tiff","ico"];
        string[] media=["mp3","wav","flac","ogg","mp4","mkv","webm"];
        Atomic(output,tmp=>{ct.ThrowIfCancellationRequested();
            if(images.Contains(ext)&&images.Contains(target)){
                using var image=Image.FromFile(input);if(image.FrameDimensionsList.Any(d=>image.GetFrameCount(new FrameDimension(d))>1))throw new IOException("Animierte Bilder und mehrseitige TIFFs werden noch nicht unterstützt.");
                var f=target switch{"jpg"=>ImageFormat.Jpeg,"bmp"=>ImageFormat.Bmp,"gif"=>ImageFormat.Gif,"tiff"=>ImageFormat.Tiff,_=>ImageFormat.Png};
                if(target=="jpg"){using var flattened=new Bitmap(image.Width,image.Height);using var g=Graphics.FromImage(flattened);g.Clear(Color.White);g.DrawImage(image,0,0);flattened.Save(tmp,f);}else image.Save(tmp,f);
            }else if(media.Contains(target)){
                if(!File.Exists(ffmpeg))throw new IOException("Für Audio / Video bitte eine ffmpeg.exe auswählen (ffmpeg.org). Diese ist nicht enthalten.");
                var mux=target switch{"mkv"=>"matroska","mp4"=>"mp4","mp3"=>"mp3",_=>target};
                var psi=new ProcessStartInfo(ffmpeg){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};foreach(var arg in new[]{"-nostdin","-n","-i",input,"-f",mux,tmp})psi.ArgumentList.Add(arg);
                using var p=Process.Start(psi)??throw new IOException("FFmpeg konnte nicht gestartet werden.");var errors=p.StandardError.ReadToEndAsync();while(!p.WaitForExit(100)){if(ct.IsCancellationRequested){p.Kill(true);p.WaitForExit();ct.ThrowIfCancellationRequested();}}var log=errors.GetAwaiter().GetResult();if(p.ExitCode!=0)throw new IOException("FFmpeg-Konvertierung fehlgeschlagen: "+log[..Math.Min(log.Length,2000)]);
            }else{
                if(new FileInfo(input).Length>50*1024*1024)throw new IOException("Textkonvertierung ist auf 50 MiB begrenzt.");
                var text=File.ReadAllText(input,new UTF8Encoding(false,true));string result;
                if (target == "pdf") { PdfConverter.TextToPdf(text, tmp, ct); return; }
                if (target == "docx") { PdfConverter.WriteDocx(tmp, [text]); ct.ThrowIfCancellationRequested(); return; }
                if(ext is "csv" or "tsv" && target is "json" or "csv" or "tsv"){
                    var rows=ParseTable(text,ext=="csv"?',':'\t');result=target=="json"?JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}):WriteTable(rows,target=="csv"?',':'\t');
                }else if(ext=="json"&&target is "csv" or "tsv"){
                    var rows=JsonSerializer.Deserialize<List<List<string>>>(text)??throw new IOException("Erwartet wird ein JSON-Array aus Arrays mit Zeichenketten.");result=WriteTable(rows,target=="csv"?',':'\t');
                }else if(target=="json"&&ext=="json"){using var doc=JsonDocument.Parse(text);result=JsonSerializer.Serialize(doc,new JsonSerializerOptions{WriteIndented=true});}
                else if(target=="xml"&&ext=="xml")result=XDocument.Parse(text).ToString();
                else if(target=="html"&&ext is "txt" or "csv" or "tsv" or "json" or "xml")result="<!doctype html><html lang=\"de\"><meta charset=\"utf-8\"><title>Export</title><body><pre>"+System.Net.WebUtility.HtmlEncode(text)+"</pre></body></html>";
                else if(target=="txt"&&ext is "txt" or "csv" or "tsv" or "json" or "xml" or "md" or "log")result=text;
                else throw new IOException("Diese Formatkombination wird noch nicht unterstützt. Siehe LIESMICH für die Konvertierungsmatrix.");
                File.WriteAllText(tmp,result,new UTF8Encoding(false));
            }ct.ThrowIfCancellationRequested();
        });
    }
    internal static List<List<string>> ParseTable(string text,char delimiter)
    {
        var rows=new List<List<string>>();var row=new List<string>();var cell=new StringBuilder();bool quoted=false,closed=false;
        for(int i=0;i<text.Length;i++){char c=text[i];if(quoted){if(c=='"'){if(i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else{quoted=false;closed=true;}}else cell.Append(c);}
            else if(c=='"'&&cell.Length==0&&!closed)quoted=true;
            else if(c==delimiter){row.Add(cell.ToString());cell.Clear();closed=false;}
            else if(c is '\r' or '\n'){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(cell.ToString());rows.Add(row);row=new();cell.Clear();closed=false;}
            else{if(closed||c=='"')throw new IOException("Ungültige CSV-Anführungszeichen.");cell.Append(c);}}
        if(quoted)throw new IOException("Nicht geschlossenes CSV-Feld.");if(cell.Length>0||row.Count>0||closed){row.Add(cell.ToString());rows.Add(row);}return rows;
    }
    internal static string WriteTable(List<List<string>> rows,char delimiter)=>string.Join("\r\n",rows.Select(row=>string.Join(delimiter,row.Select(v=>"\""+v.Replace("\"","\"\"")+"\""))));
}
