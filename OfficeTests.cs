using System.IO.Compression;
using System.Runtime.InteropServices;

namespace DateiWerk;
static class OfficeTests
{
    public static void Run()
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Office-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var results=new List<string>();
        try
        {
            foreach(string family in new[]{"Word","Excel","PowerPoint"})
            {
                if(!OfficeConverter.Available(family)){results.Add("SKIP: "+family+" nicht registriert");continue;}
                var references=new List<object>();object? application=null, document=null;bool quit=false;
                string extension=family switch{"Word"=>"docx","Excel"=>"xlsx",_=>"pptx"};var input=Path.Combine(root,"sample."+extension);
                T Track<T>(T value) where T:class{references.Add(value);return value;}
                try
                {
                    application=Activator.CreateInstance(Type.GetTypeFromProgID(family+".Application")!)!;dynamic app=application;
                    object documents=family switch{"Word"=>(object)app.Documents,"Excel"=>(object)app.Workbooks,_=>(object)app.Presentations};Track(documents);dynamic docs=documents;
                    if(docs.Count!=0)throw new IOException("Vorhandene Dokumente in "+family+"; Test greift nicht darauf zu.");quit=true;app.AutomationSecurity=3;
                    if(family=="Word")
                    {
                        app.Visible=false;app.DisplayAlerts=0;document=docs.Add();dynamic doc=document;dynamic range=Track((object)doc.Content);range.Text="DateiWerk Integrationstest – Grüße!";doc.SaveAs2(input,16);
                    }
                    else if(family=="Excel")
                    {
                        app.Visible=false;app.DisplayAlerts=false;document=docs.Add();dynamic book=document;dynamic sheets=Track((object)book.Worksheets);dynamic sheet=Track((object)sheets.Item[1]);dynamic cells=Track((object)sheet.Cells);dynamic first=Track((object)cells.Item[1,1]);first.Value2="DateiWerk";dynamic second=Track((object)cells.Item[2,1]);second.Value2=42;book.SaveAs(input,51);
                    }
                    else
                    {
                        app.DisplayAlerts=1;document=docs.Add(0);dynamic presentation=document;dynamic slides=Track((object)presentation.Slides);dynamic slide=Track((object)slides.Add(1,1));dynamic shapes=Track((object)slide.Shapes);dynamic title=Track((object)shapes.Title);dynamic frame=Track((object)title.TextFrame);dynamic range=Track((object)frame.TextRange);range.Text="DateiWerk Integrationstest";presentation.SaveAs(input,24);
                    }
                }
                finally
                {
                    foreach(var value in references.AsEnumerable().Reverse())try{Marshal.FinalReleaseComObject(value);}catch{}
                    if(document!=null){try{dynamic doc=document;if(family=="PowerPoint")doc.Close();else doc.Close(false);}catch{}try{Marshal.FinalReleaseComObject(document);}catch{}}
                    if(application!=null){if(quit)try{dynamic app=application;if(family=="Word")app.Quit(0);else app.Quit();}catch{}try{Marshal.FinalReleaseComObject(application);}catch{}}
                }
                foreach(var target in Formats.OfficeTargets(family))
                {
                    var output=Path.Combine(root,family+"-export."+target.Extension);Engine.Convert(input,output,target.Extension,"",default);
                    if(!File.Exists(output)||new FileInfo(output).Length==0)throw new Exception("Leerer Office-Export");
                    if(target.Extension=="pdf"){using var file=File.OpenRead(output);var bytes=new byte[5];file.ReadExactly(bytes);if(System.Text.Encoding.ASCII.GetString(bytes)!="%PDF-")throw new Exception("Ungültiges PDF");}
                    if(target.Extension is "docx" or "xlsx" or "pptx"){using var package=ZipFile.OpenRead(output);if(package.GetEntry("[Content_Types].xml")==null)throw new Exception("Ungültiger OOXML-Export");}
                    if(target.Extension=="csv"&&!File.ReadAllText(output).Contains("DateiWerk"))throw new Exception("CSV-Inhalt fehlt");
                    results.Add("PASS: "+family+" → "+target.Extension);
                }
            }
        }
        catch(Exception e){results.Add("FAIL: "+e);Environment.ExitCode=1;}
        finally{File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"Office-Testergebnis.txt"),results);Directory.Delete(root,true);}
    }
}
