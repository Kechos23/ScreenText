using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScreenText;
class EditorSmoke {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(MainForm f,string n){return (T)typeof(MainForm).GetField(n,Private).GetValue(f);}
    static object Call(MainForm f,string n,params object[] args){return typeof(MainForm).GetMethod(n,Private).Invoke(f,args);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static string[] Receive(string record){var watch=Stopwatch.StartNew();while(!File.Exists(record)&&watch.ElapsedMilliseconds<4000){Application.DoEvents();Thread.Sleep(10);}Check(File.Exists(record),"Edit did not launch selected executable");return File.ReadAllLines(record);}
    [STAThread] static int Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            var stage=Path.GetDirectoryName(args[1]);var paths=new Dictionary<string,string>();foreach(var id in EditorTools.Ids){var folder=Path.Combine(stage,"Editor fixtures Ελληνικά",id+" portable");Directory.CreateDirectory(folder);string path=Path.Combine(folder,id=="custom"?"MyEditor.exe":EditorTools.ExecutableName(id));File.Copy(args[2],path);paths[id]=path;}
            Check(EditorTools.FirstExisting(new[]{Path.Combine(stage,"missing.exe"),paths["greenshot"]})==paths["greenshot"],"Candidate search failed for arbitrary Unicode path");Check(EditorTools.Find("custom",Path.Combine(stage,"missing.exe"))==null,"Missing custom executable silently replaced");
            using(var form=new MainForm(true)){
                form.Show();Application.DoEvents();Call(form,"SetCurrentImage",args[0],true);
                foreach(var id in EditorTools.Ids){
                    Field<ThemeCombo>(form,"editorChoice").SelectedIndex=Array.IndexOf(EditorTools.Ids,id);Call(form,"SetEditorExecutable",paths[id]);if(id=="custom")Field<TextBox>(form,"editorArguments").Text="--open \"{image}\"";
                    var record=Path.Combine(Path.GetDirectoryName(paths[id]),"received.txt");Field<Button>(form,"editImageButton").PerformClick();var received=Receive(record);int count=id=="sharex"||id=="custom"?2:1;Check(received.Length==count,"Wrong argument count for "+id);if(id=="sharex")Check(received[0]=="-ImageEditor","ShareX not launched in editor mode");if(id=="custom")Check(received[0]=="--open","Custom arguments ignored");var image=received[received.Length-1];Check(File.Exists(image)&&image.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase),"Editor did not receive temporary full-resolution PNG");using(var decoded=Image.FromFile(image))using(var fixture=Image.FromFile(args[0]))Check(decoded.Size==fixture.Size,"Editor image was resized");
                }
                var safe=EditorTools.StartInfo("custom",paths["sharex"],args[0],"--upload {image}");Check(safe.Arguments.StartsWith("-ImageEditor "),"Manual ShareX bypassed safe editor profile");
                bool rejected=false;try{EditorTools.StartInfo("custom",paths["custom"],args[0],"--bad");}catch(InvalidOperationException){rejected=true;}Check(rejected,"Custom template without image placeholder accepted");
                Field<ThemeCombo>(form,"editorChoice").SelectedIndex=3;Call(form,"SetEditorExecutable",paths["sharex"]);Check(Field<ThemeCombo>(form,"editorChoice").SelectedIndex==0,"Browsed ShareX not recognized");
                var prefs=Field<EditorPreferences>(form,"editorPreferences");prefs.Selected="sharex";prefs.Paths["sharex"]=Path.Combine(stage,"old PC","ShareX.exe");var resolved=(string)Call(form,"ResolveEditor");Check(resolved==null||EditorTools.KnownId(resolved)=="sharex","Stale path switched to another editor");
                Field<ThemeCombo>(form,"editorChoice").SelectedIndex=3;prefs.Paths["custom"]=Path.Combine(stage,"missing custom.exe");Call(form,"RefreshEditorStatus");Call(form,"ClearWorkspace");Call(form,"SetCurrentImage",args[0],true);var before=Directory.GetFiles(Field<string>(form,"temporaryImageDirectory")).Length;bool opened=false;
                using(var timer=new System.Windows.Forms.Timer{Interval=100}){timer.Tick+=(s,e)=>{timer.Stop();opened=Field<Form>(form,"settingsWindow").Visible&&Field<Dictionary<string,Button>>(form,"settingsLinks")["Editing"].Tag as string=="selected";Field<Form>(form,"settingsWindow").Hide();};timer.Start();Call(form,"EditCurrentImage");}
                Check(opened,"Missing editor did not open Editing settings");Check(Directory.GetFiles(Field<string>(form,"temporaryImageDirectory")).Length==before,"Missing editor created unused temporary image");form.Close();
            }
            using(var form=new MainForm()) {Field<ThemeCombo>(form,"editorChoice").SelectedIndex=3;Call(form,"SetEditorExecutable",paths["custom"]);Field<TextBox>(form,"editorArguments").Text="--edit {image}";Call(form,"ExitApplication");}
            using(var form=new MainForm()) {var prefs=Field<EditorPreferences>(form,"editorPreferences");Check(prefs.Selected=="custom"&&prefs.Paths["custom"]==paths["custom"]&&prefs.Arguments=="--edit {image}","Editor configuration did not persist");Call(form,"ExitApplication");}
            File.WriteAllText(args[1],"PASS: direct Edit launches executable probes for ShareX/Paint/Greenshot/Custom; full-resolution temporary PNG argument; spaces/Unicode paths; ShareX editor-only flag even for custom selection; custom argument quoting and validation; profile auto-recognition; no cross-editor fallback after stale path; missing editor opens Editing settings without creating an image; selection/path/arguments persist. Probes validate integration arguments, not real editor UI. Greenshot is not installed on this PC.",Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(args[1],ex.ToString(),Encoding.UTF8);return 1;}
    }
}
