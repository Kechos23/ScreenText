using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScreenText;
class TempAndMenuSmoke {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
    static void CheckLayout(Control parent){foreach(Control c in parent.Controls){if(!c.Visible)continue;if(c is ThemeCombo||c is CheckBox||c.Parent is TableLayoutPanel){Check(c.Top>=0&&c.Bottom<=parent.ClientSize.Height,"Settings control clipped: "+c.Text);foreach(Control sibling in parent.Controls)if(sibling!=c&&sibling.Visible)Check(!sibling.Bounds.IntersectsWith(c.Bounds),"Settings controls overlap: "+c.GetType().Name+" "+c.Text+" with "+sibling.GetType().Name+" "+sibling.Text);}CheckLayout(c);}}

    static T Field<T>(MainForm f,string n){return (T)typeof(MainForm).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string n,params object[] args){return typeof(MainForm).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static void Wait(Task task){var timer=Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);if(timer.Elapsed.TotalSeconds>30)throw new Exception("Timed out");}task.GetAwaiter().GetResult();}
    [STAThread]static int Main(string[] args){
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var clipboard=Clipboard.GetDataObject();
        try{
            var saved=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"saved-by-user.png");string cache=null,edited=null;
            using(var form=new MainForm(true)){
                form.Show();Application.DoEvents();
                var settings=Field<Form>(form,"settingsWindow");var theme=Field<ThemeCombo>(form,"themeChoice");var autoCopy=Field<CheckBox>(form,"autoCopyImage");Check(settings.Contains(theme)&&settings.Contains(autoCopy),"Preferences were not moved to Settings");Check(!form.Contains(theme)&&!form.Contains(autoCopy),"Preferences remain on the main window");
                for(int i=0;i<6;i++){int selected=i%2;using(var close=new System.Windows.Forms.Timer{Interval=80}){close.Tick+=(sender,eventArgs)=>{close.Stop();theme.SelectedIndex=selected;autoCopy.Checked=false;settings.Close();};close.Start();Call(form,"ShowSettings");}Check(!settings.IsDisposed&&!settings.Visible,"Settings did not close safely");Check(form.BackColor==settings.BackColor,"Theme not applied to both windows");Check(theme.SelectedIndex==selected,"Theme choice lost");}Check(!Directory.Exists(Field<string>(form,"temporaryImageDirectory")),"Settings created image files");
                settings.Show();Application.DoEvents();
                var pages=Field<Dictionary<string,FlowLayoutPanel>>(form,"settingsPages");var links=Field<Dictionary<string,Button>>(form,"settingsLinks");
                foreach(var name in new[]{"General","Appearance","Extraction & Translation","Shortcuts","Images","Editing"}){links[name].PerformClick();Application.DoEvents();int visible=0;foreach(var pair in pages){if(pair.Value.Visible)visible++;Check(pair.Value.Visible==(pair.Key==name),"Wrong settings page visible");}Check(visible==1,"Settings pages overlap");Check(!pages[name].VerticalScroll.Visible&&!pages[name].HorizontalScroll.Visible,"Default settings page requires scrolling: "+name);CheckLayout(settings);}
                settings.ClientSize=new Size(760,520);Application.DoEvents();foreach(var name in pages.Keys){Call(form,"SelectSettingsCategory",name);Application.DoEvents();CheckLayout(settings);Check(!pages[name].HorizontalScroll.Visible,"Horizontal scroll at minimum size");}
                settings.Scale(new SizeF(1.25f,1.25f));Application.DoEvents();foreach(var name in pages.Keys){Call(form,"SelectSettingsCategory",name);Application.DoEvents();CheckLayout(settings);}settings.Scale(new SizeF(1.2f,1.2f));Application.DoEvents();foreach(var name in pages.Keys){Call(form,"SelectSettingsCategory",name);Application.DoEvents();CheckLayout(settings);}settings.Hide();
                Check(!(bool)Call(form,"ApplyShortcut",0u,0x41u),"Unmodified letter accepted as shortcut");Check((bool)Call(form,"ApplyShortcut",6u,0x59u),"Valid shortcut rejected");Check(Field<Label>(form,"shortcutHint").Text=="Ctrl + Shift + Y","Shortcut hint did not update");
                Field<ThemeCombo>(form,"defaultExtractChoice").SelectedIndex=1;Check(Field<ThemeCombo>(form,"extractMode").SelectedIndex==1,"Default method did not apply");Field<ThemeCombo>(form,"defaultExtractChoice").SelectedIndex=0;

                var temporaryInput=Path.Combine(Path.GetTempPath(),"ScreenText-test-input-"+Guid.NewGuid().ToString("N")+".png");File.Copy(args[0],temporaryInput);
                Wait((Task)Call(form,"ReadImage",temporaryInput,true,true));Check(!File.Exists(temporaryInput),"OCR input was not cleaned up");Check(!Directory.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"captures")),"Capture directory was created");Check(!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-image.txt")),"A persistent last-image pointer was created");Check(Field<string>(form,"lastImagePath")==null,"New capture was marked as saved");Check(Field<TextBox>(form,"source").Text.Contains("μετάφραση"),"OCR failed: "+Field<TextBox>(form,"source").Text);Check(Field<PictureBox>(form,"thumbnail").Image!=null,"Thumbnail missing");Check(Field<Label>(form,"imageInfo").Text.Contains("temporary"),"Temporary label missing");Check((bool)Call(form,"CopyImage",false)&&Clipboard.ContainsImage(),"Image copy failed");
                cache=Field<string>(form,"temporaryImageDirectory");Check(!Directory.Exists(cache),"Editor settings created image files");edited=(string)Call(form,"PrepareEditableImage");Check(edited.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)&&File.Exists(edited),"Editor copy is not temporary");
                Call(form,"SaveImageTo",saved);Check(File.Exists(saved)&&Field<bool>(form,"currentImageSaved"),"Explicit save failed");Wait((Task)Call(form,"ReadImage",edited,false,true));Check(!Field<bool>(form,"currentImageSaved"),"Reloaded edits wrongly marked saved");
                Call(form,"ClearWorkspace");Check(File.Exists(saved),"Clear deleted an explicit save");form.Close();
            }
            Check(!File.Exists(edited)&&!Directory.Exists(cache),"Temporary editor files survived normal close");Check(File.Exists(saved),"Application exit removed an explicit save");
            using(var fresh=new MainForm(true)){Check(Field<byte[]>(fresh,"lastImage")==null,"A new session restored an old screenshot");fresh.Close();}
            var automaticFolder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"automatic saves Ελληνικά");string autoSaved=null;
            using(var automatic=new MainForm(true)){
                Check(Field<ThemeCombo>(automatic,"storageMode").SelectedIndex==0,"Temporary storage is not default");Check(!Field<Button>(automatic,"browseCaptureFolder").Enabled,"Save folder enabled in temporary mode");
                Call(automatic,"SetCaptureFolder",automaticFolder);Field<ThemeCombo>(automatic,"storageMode").SelectedIndex=1;Check(!Directory.Exists(automaticFolder),"Enabling auto-save created image storage before capture");
                var input=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"capture-input.png");File.Copy(args[0],input);Wait((Task)Call(automatic,"ReadImage",input,true,true));autoSaved=Field<string>(automatic,"lastImagePath");Check(autoSaved!=null&&File.Exists(autoSaved)&&Field<bool>(automatic,"currentImageSaved"),"New screenshot was not automatically saved");Check(Convert.ToBase64String(File.ReadAllBytes(autoSaved))==Convert.ToBase64String(Field<byte[]>(automatic,"lastImage")),"Auto-saved PNG differs from full working image");Check(Field<Button>(automatic,"folderButton").Enabled,"Saved folder action unavailable");
                Wait((Task)Call(automatic,"RepeatExtract"));Check(Directory.GetFiles(automaticFolder).Length==1,"Re-extraction made a duplicate save");Call(automatic,"PrepareEditableImage");Wait((Task)Call(automatic,"ReadImage",(string)Call(automatic,"PrepareEditableImage"),false,true));Check(Directory.GetFiles(automaticFolder).Length==1,"Reload edits made a duplicate save");
                Wait((Task)Call(automatic,"ReadImage",args[0],false,true));Check(Directory.GetFiles(automaticFolder).Length==1&&Field<string>(automatic,"lastImagePath")==null,"Opening a file created an automatic copy");
                Call(automatic,"AutoSaveCurrentImage");Check(Directory.GetFiles(automaticFolder).Length==2,"Automatic names collided");Field<ThemeCombo>(automatic,"storageMode").SelectedIndex=0;Call(automatic,"SetCurrentImage",args[0],true);Call(automatic,"AutoSaveCurrentImage");Check(Directory.GetFiles(automaticFolder).Length==2&&Field<string>(automatic,"lastImagePath")==null,"Temporary mode still saved images");
                var blocked=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"not-a-folder.txt");File.WriteAllText(blocked,"fixture");Call(automatic,"SetCaptureFolder",blocked);Field<ThemeCombo>(automatic,"storageMode").SelectedIndex=1;File.Copy(args[0],input);Wait((Task)Call(automatic,"ReadImage",input,true,true));Check(Field<byte[]>(automatic,"lastImage")!=null&&!Field<bool>(automatic,"currentImageSaved")&&Field<string>(automatic,"lastImagePath")==null,"Failed save lost the temporary image");Check(Field<Label>(automatic,"status").Text.Contains("Auto-save failed")&&Field<Label>(automatic,"storageFeedback").Text.Contains("Auto-save failed"),"Failed save was not reported");Check(Field<TextBox>(automatic,"source").Text.Contains("μετάφραση"),"Failed save stopped OCR");
                Call(automatic,"ClearWorkspace");automatic.Close();
            }
            Check(File.Exists(autoSaved)&&Directory.GetFiles(automaticFolder).Length==2,"Clear/close removed automatic saves");
            using(var prefs=new MainForm()){
                Call(prefs,"SetCaptureFolder",automaticFolder);Field<ThemeCombo>(prefs,"storageMode").SelectedIndex=1;
                Field<ThemeCombo>(prefs,"defaultExtractChoice").SelectedIndex=1;
                Check((bool)Call(prefs,"ApplyShortcut",6u,0x59u),"Could not register screenshot shortcut");
                Check(RegisterHotKey(prefs.Handle,88,7u,0x4Bu),"Could not reserve conflict test shortcut");try{Check(!(bool)Call(prefs,"ApplyShortcut",7u,0x4Bu),"Shortcut conflict was accepted");Check(Field<uint>(prefs,"shortcutKey")==0x59u,"Conflict replaced the working shortcut");Check(!RegisterHotKey(prefs.Handle,89,6u,0x59u),"Old shortcut registration was lost");}finally{UnregisterHotKey(prefs.Handle,88);UnregisterHotKey(prefs.Handle,89);}
                Field<ThemeCombo>(prefs,"themeChoice").SelectedIndex=1;Field<CheckBox>(prefs,"autoCopyImage").Checked=true;
                Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"theme.txt"))=="light","Theme preference not saved");Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"auto-copy-image.txt"))=="true","Clipboard preference not saved");Call(prefs,"ExitApplication");
            }
            using(var prefs=new MainForm()){Check(Field<ThemeCombo>(prefs,"themeChoice").SelectedIndex==1&&Field<CheckBox>(prefs,"autoCopyImage").Checked,"Settings did not persist on restart");Check(Field<ThemeCombo>(prefs,"extractMode").SelectedIndex==1,"Default extraction did not persist");Check(Field<ThemeCombo>(prefs,"storageMode").SelectedIndex==1&&Field<TextBox>(prefs,"captureFolder").Text==automaticFolder,"Storage mode/folder did not persist");Check(Field<uint>(prefs,"shortcutModifiers")==6u&&Field<uint>(prefs,"shortcutKey")==0x59u,"Shortcut did not persist");Call(prefs,"ExitApplication");}
            File.WriteAllText(args[1],"PASS: temporary storage remains default; optional automatic PNG saves with unique names; storage folder/mode persist; no duplicates for repeat/import; save failure preserves image and OCR with visible warning; automatic saves survive Clear/close; exclusive Settings categories; ordinary pages without default scrolling; AI Connections permits vertical scrolling; configurable shortcut and conflict rollback; default extraction persistence; Settings layout at normal, minimum, 125% and 150% simulated scale; Settings reopen and close six times; live dark/light theme; preferences persist on restart; preferences removed from main window; memory-only captures; no captures folder or pointer; OCR input cleanup; temporary label; clipboard copy; editor copies under Windows Temp; explicit Save persistence; edited image import; temporary cleanup on close; saved files retained; new session starts empty.",Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(args[1],ex.ToString(),Encoding.UTF8);return 1;}
        finally{if(clipboard!=null)Clipboard.SetDataObject(clipboard,true);else Clipboard.Clear();}
    }
}
