using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DateiWerk;
static class Tests
{
    public static void Run()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"DateiWerk-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);int count=0;
        void Check(bool condition,string name){if(!condition)throw new Exception("Test fehlgeschlagen: "+name);count++;}
        void Reject(Action action,string name){try{action();}catch{count++;return;}throw new Exception("Nicht abgewiesen: "+name);}
        try{
            File.WriteAllText(root+"/object.json", "{\"name\":\"Test\"}");
            var objectAnalysis=Formats.Analyze(root+"/object.json","",default);Check(!objectAnalysis.Targets.Any(t=>t.Extension=="csv"),"JSON-Objekt erhält keinen CSV-Export");
            File.WriteAllText(root+"/table.json", "[[\"a\",\"b\"]]");
            Check(Formats.Analyze(root+"/table.json","",default).Targets.Any(t=>t.Extension=="csv"),"JSON-Zeilentabelle erhält CSV-Export");
            File.WriteAllText(root+"/broken.json", "{broken");Reject(()=>Formats.Analyze(root+"/broken.json","",default),"Ungültiges JSON");
            using(var package=ZipFile.Open(root+"/fake.docx",ZipArchiveMode.Create)){package.CreateEntry("wrong.txt");}
            Reject(()=>Formats.Analyze(root+"/fake.docx","",default),"Falscher Office-Container");
            using(var package=ZipFile.Open(root+"/sample.docx",ZipArchiveMode.Create)){package.CreateEntry("[Content_Types].xml");package.CreateEntry("word/document.xml");}
            var officeAnalysis=Formats.Analyze(root+"/sample.docx","",default);Check(officeAnalysis.OfficeFamily=="Word","Word-Container erkannt");Check(officeAnalysis.Targets.Any(t=>t.Extension=="pdf")==OfficeConverter.Available("Word"),"Office-Verfügbarkeit berücksichtigt");
            File.WriteAllText(root+"/media.wav","placeholder");Reject(()=>Formats.Analyze(root+"/media.wav","",default),"Beschädigte Mediendatei wird abgewiesen");
            foreach(var size in new[]{0,1,1024*1024,1024*1024+79}){
                var data=RandomNumberGenerator.GetBytes(size);var input=Path.Combine(root,"input"+size);File.WriteAllBytes(input,data);var enc=input+".dwenc";var dec=input+".dec";
                Engine.Crypt(input,enc,"correct horse battery staple",false,default);Engine.Crypt(enc,dec,"correct horse battery staple",true,default);Check(data.SequenceEqual(File.ReadAllBytes(dec)),"Crypto-Roundtrip "+size);
                Reject(()=>Engine.Crypt(enc,dec+"wrong","wrong",true,default),"Falsches Passwort");Check(!File.Exists(dec+"wrong"),"Keine Ausgabe bei falschem Passwort");
                var tamper=File.ReadAllBytes(enc);tamper[^1]^=1;File.WriteAllBytes(enc+"bad",tamper);Reject(()=>Engine.Crypt(enc+"bad",dec+"bad","correct horse battery staple",true,default),"Manipulation");
                File.WriteAllBytes(enc+"short",tamper[..^1]);Reject(()=>Engine.Crypt(enc+"short",dec+"short","correct horse battery staple",true,default),"Abgeschnittene Datei");
            }
            var dir=Path.Combine(root,"folder");Directory.CreateDirectory(Path.Combine(dir,"empty"));Directory.CreateDirectory(Path.Combine(dir,"nested"));File.WriteAllText(Path.Combine(dir,"nested","ü.txt"),"Grüße!");
            Engine.Zip(dir,root+"/archive.zip",0,default);Engine.Unzip(root+"/archive.zip",root+"/unpacked",default);Check(File.ReadAllText(root+"/unpacked/nested/ü.txt")=="Grüße!","ZIP-Inhalt");Check(Directory.Exists(root+"/unpacked/empty"),"Leerer Ordner");
            Reject(()=>Engine.Zip(dir,dir+"/inside.zip",0,default),"ZIP innerhalb Quelle");
            string folderEncrypted=root+"/folder.zip.dwenc", folderZip=root+"/restored.zip";
            Engine.Encrypt(dir,folderEncrypted,"correct horse battery staple",0,default);Engine.Crypt(folderEncrypted,folderZip,"correct horse battery staple",true,default);
            Engine.Unzip(folderZip,root+"/restored-folder",default);Check(File.ReadAllText(root+"/restored-folder/nested/ü.txt")=="Grüße!","Ordner-AES-Roundtrip erhält Dateiinhalt");Check(Directory.Exists(root+"/restored-folder/empty"),"Ordner-AES erhält leere Unterordner");
            Reject(()=>Engine.Encrypt(dir,dir+"/inside.zip.dwenc","correct horse battery staple",0,default),"Ordner-Verschlüsselung verhindert Ausgabe innerhalb Quelle");
            using(var folderCancel=new CancellationTokenSource()){folderCancel.Cancel();Reject(()=>Engine.Encrypt(dir,root+"/folder-cancel.dwenc","correct horse battery staple",0,folderCancel.Token),"Ordner-Verschlüsselung abbrechbar");Check(!File.Exists(root+"/folder-cancel.dwenc"),"Ordner-Abbruch ohne Ausgabe");}
            Check(LaunchOptions.Parse(["--open",Path.Combine(dir,"nested","ü.txt")]).Input==Path.GetFullPath(Path.Combine(dir,"nested","ü.txt")),"Explorer-Dateipfad wird übernommen");
            Check(LaunchOptions.Parse(["--encrypt",dir]).Operation==3,"Explorer-Ordner-Verschlüsselung auswählbar");
            Check(ExplorerMenu.Command(@"C:\Ordner mit Leerzeichen\DateiWerk.exe")=="\"C:\\Ordner mit Leerzeichen\\DateiWerk.exe\" --open \"%1\"","Explorer-Befehl schützt Pfade mit Leerzeichen");
            Check(Updater.IsTrustedAsset("https://github.com/nnifFlame/DateiWerk/releases/download/v0.5.0/DateiWerk-Setup.exe","v0.5.0"),"Updater akzeptiert eigene HTTPS-Releases");
            Check(!Updater.IsTrustedAsset("https://github.com.evil.example/nnifFlame/DateiWerk/releases/download/v0.5.0/DateiWerk-Setup.exe","v0.5.0"),"Updater weist fremde Hosts ab");
            Check(!Updater.IsTrustedAsset("http://github.com/nnifFlame/DateiWerk/releases/download/v0.5.0/DateiWerk-Setup.exe","v0.5.0"),"Updater erzwingt HTTPS");
            using(var zip=ZipFile.Open(root+"/evil.zip",ZipArchiveMode.Create)){zip.CreateEntry("../escape.txt");}
            Reject(()=>Engine.Unzip(root+"/evil.zip",root+"/evil",default),"ZIP-Pfadmanipulation");Check(!Directory.Exists(root+"/evil")&&!File.Exists(root+"/escape.txt"),"ZIP-Bereinigung");
            var csv="a,b\r\n\"mehr\nzeilig\",\"a\"\"b\"\r\n";File.WriteAllText(root+"/data.csv",csv);Engine.Convert(root+"/data.csv",root+"/data.json","json","",default);Engine.Convert(root+"/data.json",root+"/back.csv","csv","",default);
            Check(JsonSerializer.Serialize(Engine.ParseTable(csv,','))==JsonSerializer.Serialize(Engine.ParseTable(File.ReadAllText(root+"/back.csv"),',')),"CSV-Roundtrip");
            Engine.Convert(root+"/data.csv",root+"/table.xlsx","xlsx","",default);
            using(var zip=ZipFile.OpenRead(root+"/table.xlsx")){using var s=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var xml=System.Xml.Linq.XDocument.Load(s);System.Xml.Linq.XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";var values=xml.Descendants(ns+"t").Select(t=>t.Value).ToArray();Check(values.Contains("mehr\nzeilig")&&values.Contains("a\"b"),"CSV → XLSX erhält Sonderzeichen und Zeilenumbrüche");}
            using(var image=new System.Drawing.Bitmap(32,32)){image.Save(root+"/image.png",System.Drawing.Imaging.ImageFormat.Png);}
            Engine.Convert(root+"/image.png",root+"/image.jpg","jpg","",default);using(var image=System.Drawing.Image.FromFile(root+"/image.jpg")){Check(image.Width==32&&image.Height==32,"Bildkonvertierung");}
            Reject(()=>Engine.Convert(root+"/image.png",root+"/image.jpg","jpg","",default),"Überschreibschutz");
            using var cancel=new CancellationTokenSource();cancel.Cancel();Reject(()=>Engine.Zip(dir,root+"/cancel.zip",0,cancel.Token),"Abbruch");Check(!File.Exists(root+"/cancel.zip"),"Abbruch-Bereinigung");
            var report=$"PASS: {count} Prüfungen; Formatanalyse, Office-Container / Verfügbarkeit, Crypto, Manipulation, ZIP, Pfadschutz, CSV, Bilder, Abbruch. Keine Office-Export- oder UI-Integrationstests.\n";File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Testergebnis.txt"),report);
        }catch(Exception e){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"Testergebnis.txt"),"FAIL: "+e);Environment.ExitCode=1;}
        finally{Directory.Delete(root,true);}
    }
}
