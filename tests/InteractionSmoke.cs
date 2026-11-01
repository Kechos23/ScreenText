using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScreenText;
class InteractionSmoke {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
    static void CheckHotkey(MainForm form){bool stolen=RegisterHotKey(form.Handle,901,0x4000|Field<uint>(form,"shortcutModifiers"),Field<uint>(form,"shortcutKey"));if(stolen)UnregisterHotKey(form.Handle,901);Check(!stolen,"Screenshot shortcut was released during window handle recreation");}

    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(MainForm f,string n){return (T)typeof(MainForm).GetField(n,Private).GetValue(f);}
    static object Call(MainForm f,string n,params object[] args){return typeof(MainForm).GetMethod(n,Private).Invoke(f,args);}
    static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
    static void Key(Control c,string method,Keys key){typeof(Control).GetMethod(method,Private).Invoke(c,new object[]{new KeyEventArgs(key)});}
    static void LaunchDuplicate(string arguments){using(var p=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ScreenText.exe"),arguments){UseShellExecute=false,CreateNoWindow=true})){Check(p.WaitForExit(5000),"Duplicate launch did not exit");Check(p.ExitCode==0,"Duplicate launch failed");}}
    [STAThread]static int Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            bool owner;using(var mutex=new Mutex(true,@"Local\ScreenTextTranslate-v1",out owner))using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,Program.ActivationEventName)){
                Check(owner,"Another real ScreenText is running during the test");signal.Reset();Exception failure=null;
                using(var form=new MainForm(false,true))using(var timer=new System.Windows.Forms.Timer{Interval=180}){
                    var listener=Program.ListenForActivation(form,signal);Field<TextBox>(form,"source").Text="Existing workspace";var handle=form.Handle;
                    timer.Tick+=(s,e)=>{timer.Stop();try{
                        Check(!form.Visible,"Startup not hidden");LaunchDuplicate("--startup");Application.DoEvents();Check(!form.Visible,"Automatic duplicate stole focus");LaunchDuplicate("");var watch=Stopwatch.StartNew();while(!form.Visible&&watch.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(10);}Check(form.Visible&&!form.IsDisposed&&Field<TextBox>(form,"source").Text=="Existing workspace","Duplicate did not restore the same workspace");CheckHotkey(form);
                        Call(form,"SelectSettingsCategory","Shortcuts");var settings=Field<Form>(form,"settingsWindow");settings.Show(form);Application.DoEvents();var input=Field<ShortcutCapture>(form,"shortcutInput");
                        Key(input,"OnKeyDown",Keys.Control|Keys.ControlKey);Check(input.Text=="Ctrl + …","Ctrl is not displayed live");Key(input,"OnKeyDown",Keys.Control|Keys.Alt|Keys.Menu);Check(input.Text=="Ctrl + Alt + …","Second modifier is not displayed live");
                        using(var preview=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(preview,new Rectangle(Point.Empty,settings.Size));preview.Save(Path.Combine(Path.GetDirectoryName(args[0]),"Shortcut-live.png"));}
                        Key(input,"OnKeyDown",Keys.Control|Keys.Alt|Keys.Shift|Keys.U);Check(input.Text=="Ctrl + Alt + Shift + U","Complete shortcut not displayed");Key(input,"OnKeyUp",Keys.U);Check(input.Text=="Ctrl + Alt + Shift + U","Releasing keys lost completed shortcut");
                        Check((bool)Call(form,"ApplyShortcut",Field<uint>(form,"pendingModifiers"),Field<uint>(form,"pendingKey")),"New shortcut did not apply");Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"screenshot-shortcut.txt"))=="7|85","Shortcut not saved to disk");
                        Key(input,"OnKeyDown",Keys.Control|Keys.ControlKey);Key(input,"OnKeyUp",Keys.ControlKey);Check(input.Text=="Press a shortcut…","Incomplete shortcut did not follow key release");Key(input,"OnKeyDown",Keys.Escape);Check(input.Text=="Ctrl + Alt + Shift + U","Escape did not restore saved shortcut");
                        Call(form,"SelectSettingsCategory","General");var checkbox=Field<CheckBox>(form,"closeToTray");checkbox.Checked=false;using(var off=new Bitmap(checkbox.Width,checkbox.Height)){checkbox.DrawToBitmap(off,checkbox.ClientRectangle);checkbox.Checked=true;using(var on=new Bitmap(checkbox.Width,checkbox.Height)){checkbox.DrawToBitmap(on,checkbox.ClientRectangle);Check(off.GetPixel(10,checkbox.Height/2)!=on.GetPixel(10,checkbox.Height/2),"Checked state not visibly distinct");}}
                        settings.Hide();form.Close();Check(!form.Visible&&!form.IsDisposed,"X did not hide");LaunchDuplicate("");watch.Restart();while(!form.Visible&&watch.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(10);}Check(form.Visible&&!form.IsDisposed&&Field<TextBox>(form,"source").Text=="Existing workspace","Repeat activation did not restore existing window");CheckHotkey(form);
                    }catch(Exception ex){failure=ex;}finally{Call(form,"ExitApplication");}};
                    try{timer.Start();Application.Run(form);}finally{listener.Unregister(null);}
                }
                if(failure!=null)throw failure;
                using(var restarted=new MainForm()){Check(Field<uint>(restarted,"shortcutModifiers")==7u&&Field<uint>(restarted,"shortcutKey")==85u,"Restart reset the custom shortcut");Call(restarted,"ExitApplication");}
            }
            File.WriteAllText(args[0],"PASS: real second executable launch signals the existing instance and exits; automatic duplicate stays quiet; same window/workspace restored repeatedly; actual shortcut remains registered after hide/restore; modifiers and full shortcut displayed during capture; key-release and Escape behavior; custom shortcut stored and reloaded on restart; checked/unchecked pixels visibly distinct. App-event tests, not physical mouse automation.",Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(args[0],ex.ToString(),Encoding.UTF8);return 1;}
    }
}
