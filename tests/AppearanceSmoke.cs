using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ScreenText;
class AppearanceSmoke {
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,Private).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){return typeof(MainForm).GetMethod(name,Private).Invoke(f,args);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Event(Control c,string name,EventArgs args){c.GetType().GetMethod(name,Private).Invoke(c,new object[]{args});}
    static Bitmap Frame(Control c){var image=new Bitmap(c.Width,c.Height);c.DrawToBitmap(image,c.ClientRectangle);return image;}
    static byte[] Pixels(Bitmap image){var bits=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var bytes=new byte[bits.Stride*bits.Height];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);return bytes;}finally{image.UnlockBits(bits);}}
    static void Same(Bitmap a,Bitmap b){var left=Pixels(a);var right=Pixels(b);Check(left.Length==right.Length,"Hover resized a control");for(int i=0;i<left.Length;i++)if(left[i]!=right[i])throw new Exception("Hover left stale pixels after mouse leave");}
    static void Hover(Control c){Event(c,"OnMouseLeave",EventArgs.Empty);using(var normal=Frame(c)){var bounds=c.Bounds;for(int i=0;i<10;i++){Event(c,"OnMouseEnter",EventArgs.Empty);using(var hovered=Frame(c)){}Event(c,"OnMouseLeave",EventArgs.Empty);using(var restored=Frame(c))Same(normal,restored);Check(c.Bounds==bounds,"Hover changed control bounds");}}}
    static double Linear(byte channel){double c=channel/255.0;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);}
    static double Luminance(Color c){return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
    static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    [STAThread] static int Main(string[] args){
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            using(var form=new MainForm(true)){
                form.Show();var settings=Field<Form>(form,"settingsWindow");settings.Show(form);Call(form,"SelectSettingsCategory","Appearance");Application.DoEvents();var theme=Field<ThemeCombo>(form,"themeChoice");var colors=Field<Dictionary<string,Button>>(form,"accentButtons");
                foreach(var color in new[]{"Green","Blue","Violet","Rose","Amber","Slate"})foreach(var mode in new[]{0,1}){
                    theme.SelectedIndex=mode;Call(form,"SetAccent",color);Application.DoEvents();Check(theme.SelectedIndex==mode,"Accent changed dark/light mode");Check(Field<string>(form,"accentName")==color,"Color selection failed");
                    var capture=Field<Button>(form,"capture");Check(Contrast(capture.BackColor,capture.ForeColor)>=4.5,"Insufficient primary button contrast: "+color+" "+mode);Hover(theme);Hover(colors[color]);foreach(var nav in Field<Dictionary<string,Button>>(form,"settingsLinks").Values)Hover(nav);
                    var pages=Field<Dictionary<string,FlowLayoutPanel>>(form,"settingsPages");Check(!pages["Appearance"].VerticalScroll.Visible&&!pages["Appearance"].HorizontalScroll.Visible,"Appearance needs scrolling at normal size");
                }
                Call(form,"SelectSettingsCategory","Appearance");
                for(int i=0;i<20;i++){
                    theme.DroppedDown=true;Application.DoEvents();Check(theme.DroppedDown,"Selector did not open");var menu=(ToolStripDropDown)typeof(ThemeCombo).GetField("options",Private).GetValue(theme);var list=(ThemeCombo.SelectorList)typeof(ThemeCombo).GetField("optionList",Private).GetValue(theme);Check(menu.Width==theme.Width,"Popup width differs from field");Check(list.Width==theme.Width&&list.Values.Length==2,"Selector list sizing/options incorrect");int index=1-theme.SelectedIndex;Event(list,"OnMouseMove",new MouseEventArgs(MouseButtons.None,0,15,6+index*list.RowHeight+list.RowHeight/2,0));Event(list,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,15,6+index*list.RowHeight+list.RowHeight/2,0));Application.DoEvents();Check(!theme.DroppedDown&&theme.SelectedIndex==index,"Menu selection did not close/apply");Check(!menu.IsDisposed,"Popup was disposed while closing");
                }
                Check((bool)typeof(ThemeCombo).GetMethod("IsInputKey",Private).Invoke(theme,new object[]{Keys.Enter}),"Enter would escape to the Settings Done button");
                theme.Focus();Event(theme,"OnKeyDown",new KeyEventArgs(Keys.Home));Check(theme.SelectedIndex==0,"Keyboard Home failed");Event(theme,"OnKeyDown",new KeyEventArgs(Keys.Down));Check(theme.SelectedIndex==1,"Keyboard Down failed");Event(theme,"OnKeyDown",new KeyEventArgs(Keys.F4));Check(theme.DroppedDown,"Keyboard open failed");Event(theme,"OnKeyDown",new KeyEventArgs(Keys.F4));Check(!theme.DroppedDown,"Keyboard toggle close failed");
                theme.DroppedDown=true;var keyboardList=(ThemeCombo.SelectorList)typeof(ThemeCombo).GetField("optionList",Private).GetValue(theme);Event(keyboardList,"OnKeyDown",new KeyEventArgs(Keys.Home));Event(keyboardList,"OnKeyDown",new KeyEventArgs(Keys.Enter));Check(theme.SelectedIndex==0&&!theme.DroppedDown,"Popup keyboard selection failed");theme.DroppedDown=true;Event(keyboardList,"OnKeyDown",new KeyEventArgs(Keys.Escape));Check(!theme.DroppedDown,"Popup Escape failed");
                theme.Enabled=false;theme.DroppedDown=true;Check(!theme.DroppedDown,"Disabled selector opened");theme.Enabled=true;settings.Hide();Call(form,"ExitApplication");
            }
            using(var form=new MainForm()){Call(form,"SetAccent","Violet");Field<ThemeCombo>(form,"themeChoice").SelectedIndex=1;Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"accent-color.txt"))=="Violet","Accent did not save");Call(form,"ExitApplication");}
            using(var form=new MainForm()){Check(Field<string>(form,"accentName")=="Violet"&&Field<ThemeCombo>(form,"themeChoice").SelectedIndex==1,"Independent mode/color persistence failed");Call(form,"ExitApplication");}
            File.WriteAllText(args[0],"PASS: six accent palettes in dark and light; primary text contrast >= 4.5:1; hover enter/leave pixel restoration for selectors, color buttons and navigation; no hover resizing; 20 full-width popup open/pointer select/close cycles; keyboard navigation and opening; disabled selector; independent mode/color persistence. App-event tests, not physical mouse automation.",Encoding.UTF8);return 0;
        }catch(Exception ex){File.WriteAllText(args[0],ex.ToString(),Encoding.UTF8);return 1;}
    }
}
