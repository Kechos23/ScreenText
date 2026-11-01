using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using ScreenText;
class StartupSmoke {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Private).GetValue(form);}
    static object Call(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,Private).Invoke(form,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [STAThread]static int Main(string[] args){
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string testKey=@"Software\ScreenText\Tests\Startup-"+Guid.NewGuid().ToString("N");var real=new StartupRegistration();string baseline=real.RegisteredCommand;
        try{
            var oldStore=new StartupRegistration(@"C:\Old Folder\ScreenText.exe",testKey);var movedStore=new StartupRegistration(@"C:\New Folder Ελληνικά\ScreenText.exe",testKey);
            Check(!oldStore.Enabled&&oldStore.RegisteredCommand==null,"Startup was not off by default");using(var key=Registry.CurrentUser.OpenSubKey(testKey))Check(key==null,"Reading startup created a registry entry");
            oldStore.SetEnabled(true);Check(oldStore.Enabled&&oldStore.RegisteredCommand=="\"C:\\Old Folder\\ScreenText.exe\" --startup","Startup command is not quoted correctly");Check(!movedStore.Enabled,"Moved location falsely shown enabled");
            using(var form=new MainForm(false,false,movedStore)){
                var checkbox=Field<CheckBox>(form,"startupAtLogin");Check(!checkbox.Checked&&Field<Label>(form,"startupFeedback").Text.Contains("Another location"),"Stale startup path not identified");checkbox.Checked=true;Check(movedStore.Enabled&&!oldStore.Enabled,"Re-enabling did not replace the old path");
                using(var key=Registry.CurrentUser.OpenSubKey(testKey,true))key.SetValue("KeepMe","unrelated test value");checkbox.Checked=false;Check(movedStore.RegisteredCommand==null,"Disabling left startup registered");using(var key=Registry.CurrentUser.OpenSubKey(testKey))Check((string)key.GetValue("KeepMe")=="unrelated test value","Disabling changed an unrelated value");Call(form,"ExitApplication");
            }
            Exception failure=null;bool shown=false;
            using(var form=new MainForm(false,true,movedStore))using(var timer=new System.Windows.Forms.Timer{Interval=150}){
                form.Shown+=(s,e)=>shown=true;
                timer.Tick+=(s,e)=>{timer.Stop();try{
                    Check(!form.Visible&&!shown&&!form.ShowInTaskbar,"Startup briefly opened a visible window");Check(Field<NotifyIcon>(form,"tray").Visible,"Startup tray icon missing");Check(Field<bool>(form,"hotkeyAttempted")&&Field<bool>(form,"hotkeyRegistered"),"Hidden startup did not register screenshot shortcut");
                    Call(form,"Restore");Application.DoEvents();Check(form.Visible&&form.ShowInTaskbar&&shown,"Tray restore did not show the window");
                    Check(Field<CheckBox>(form,"closeToTray").Checked,"Close to tray is not on by default");Check(Field<Label>(form,"startupFeedback").Text=="","Redundant startup text remains");
                    var fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"close-fixture.png");using(var bitmap=new Bitmap(16,16))bitmap.Save(fixture,System.Drawing.Imaging.ImageFormat.Png);Call(form,"SetCurrentImage",fixture,true);var edit=(string)Call(form,"PrepareEditableImage");Field<TextBox>(form,"source").Text="Keep this text";
                    Call(form,"SetBusy",true,"Test operation");var cancellation=Field<CancellationTokenSource>(form,"operation");form.Close();Application.DoEvents();Check(!form.Visible&&!form.IsDisposed&&!form.ShowInTaskbar&&!Field<bool>(form,"closing"),"X terminated the application");Check(Field<NotifyIcon>(form,"tray").Visible&&Field<bool>(form,"hotkeyRegistered"),"X removed tray or shortcut");Check(!cancellation.IsCancellationRequested&&Field<TextBox>(form,"source").Text=="Keep this text"&&Field<byte[]>(form,"lastImage")!=null&&File.Exists(edit),"X discarded workspace or canceled work");
                    Call(form,"Restore");Application.DoEvents();Call(form,"SetBusy",false,"Ready");var tray=Field<NotifyIcon>(form,"tray");tray.ContextMenuStrip.Items[tray.ContextMenuStrip.Items.Count-1].PerformClick();Application.DoEvents();Check(form.IsDisposed&&!File.Exists(edit),"Tray Exit did not close and clean up");

                }catch(Exception ex){failure=ex;}finally{Call(form,"ExitApplication");}};timer.Start();Application.Run(form);
            }
            if(failure!=null)throw failure;
            using(var form=new MainForm(false,false,movedStore)){Field<CheckBox>(form,"closeToTray").Checked=false;form.Show();Application.DoEvents();form.Close();Check(form.IsDisposed,"X did not quit when Close to tray was disabled");}
            using(var form=new MainForm(false,false,movedStore)){Check(!Field<CheckBox>(form,"closeToTray").Checked,"Close preference did not persist");Field<CheckBox>(form,"closeToTray").Checked=true;Call(form,"ExitApplication");}
            using(var form=new MainForm(false,false,movedStore)){Check(Field<CheckBox>(form,"closeToTray").Checked,"Enabled close preference did not persist");Call(form,"ExitApplication");}
            Check(real.RegisteredCommand==baseline,"Test changed the real Windows startup entry");
            File.WriteAllText(args[0],"PASS: X hides to tray by default without losing workspace, canceling work or releasing shortcut; tray Exit really quits and cleans editor cache; X quits when disabled; close preference persists; redundant startup comment removed; startup is off by default; read does not create an entry; quoted per-user command includes --startup; moved path detected and replaced on enable; disable preserves unrelated values; startup enters tray without Shown/window/taskbar flash; shortcut registered while hidden; tray restore works. Registry writes were isolated under Software/ScreenText/Tests; real Run entry unchanged. Windows sign-in itself was not exercised.",Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(args[0],ex.ToString(),Encoding.UTF8);return 1;}
        finally{try{Registry.CurrentUser.DeleteSubKeyTree(testKey,false);}catch{}}
    }
}
