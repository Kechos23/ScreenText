using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ScreenText {
    public static class EditorTools {
        public static readonly string[] Ids={"sharex","paint","greenshot","custom"};
        public static string ExecutableName(string id){return id=="sharex"?"ShareX.exe":id=="greenshot"?"Greenshot.exe":id=="paint"?"mspaint.exe":"";}
        public static string KnownId(string path){var name=Path.GetFileName(path??"");if(name.Equals("ShareX.exe",StringComparison.OrdinalIgnoreCase))return "sharex";if(name.Equals("Greenshot.exe",StringComparison.OrdinalIgnoreCase))return "greenshot";if(name.Equals("mspaint.exe",StringComparison.OrdinalIgnoreCase)||name.Equals("PaintApp.exe",StringComparison.OrdinalIgnoreCase))return "paint";return "custom";}
        public static string ExistingExecutable(string path){try{if(String.IsNullOrWhiteSpace(path))return null;var full=Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));return Path.IsPathRooted(full)&&Path.GetExtension(full).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(full)?Path.GetFullPath(full):null;}catch{return null;}}
        public static string FirstExisting(IEnumerable<string> paths){foreach(var path in paths){var found=ExistingExecutable(path);if(found!=null)return found;}return null;}
        public static string Find(string id,string remembered=null){
            var saved=ExistingExecutable(remembered);if(saved!=null&&(id=="custom"||KnownId(saved)==id))return saved;if(id=="custom"||!Ids.Contains(id))return null;
            string executable=ExecutableName(id),name=Path.GetFileNameWithoutExtension(executable);
            if(id=="paint"){var paint=FirstExisting(new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","mspaint.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mspaint.exe")});if(paint!=null)return paint;}
            foreach(var process in Process.GetProcessesByName(name))using(process){try{var path=ExistingExecutable(process.MainModule.FileName);if(path!=null)return path;}catch{}}
            string folder=id=="sharex"?"ShareX":id=="greenshot"?"Greenshot":"Paint";
            var common=new List<string>();foreach(var root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Path.Combine(Backend.Base,"tools"),Backend.Base})if(!String.IsNullOrEmpty(root))common.Add(Path.Combine(root,folder,executable));
            var normal=FirstExisting(common);if(normal!=null)return normal;
            foreach(var hive in new[]{Microsoft.Win32.RegistryHive.CurrentUser,Microsoft.Win32.RegistryHive.LocalMachine})foreach(var view in new[]{Microsoft.Win32.RegistryView.Registry64,Microsoft.Win32.RegistryView.Registry32})try{
                using(var root=Microsoft.Win32.RegistryKey.OpenBaseKey(hive,view)){
                    using(var app=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\"+executable)){if(app!=null){var found=ExistingExecutable(app.GetValue("") as string);if(found!=null&&KnownId(found)==id)return found;}}
                    using(var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null)foreach(var keyName in uninstall.GetSubKeyNames())try{using(var entry=uninstall.OpenSubKey(keyName)){
                        if(entry==null)continue;var display=entry.GetValue("DisplayName") as string;if(display==null||!display.StartsWith(folder,StringComparison.OrdinalIgnoreCase))continue;
                        var location=entry.GetValue("InstallLocation") as string;if(!String.IsNullOrWhiteSpace(location)){var found=ExistingExecutable(Path.Combine(Environment.ExpandEnvironmentVariables(location.Trim().Trim('"')),executable));if(found!=null)return found;}
                        var icon=entry.GetValue("DisplayIcon") as string;if(icon!=null){int end=icon.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(end>=0){var found=ExistingExecutable(icon.Substring(0,end+4));if(found!=null&&KnownId(found)==id)return found;}}
                    }}catch{}
                }
            }catch{}
            foreach(var root in (Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator))try{if(String.IsNullOrWhiteSpace(root))continue;var found=ExistingExecutable(Path.Combine(root.Trim().Trim('"'),executable));if(found!=null)return found;}catch{}
            return null;
        }
        public static ProcessStartInfo StartInfo(string id,string executable,string image,string arguments=""){
            var path=ExistingExecutable(executable);if(path==null)throw new FileNotFoundException("The selected editor was not found. Choose it in Settings > Editing.");
            var known=KnownId(path);if(id!="custom"&&known!=id)throw new InvalidOperationException("This executable does not match the selected editor.");
            string args;if(known=="sharex")args="-ImageEditor "+Backend.Quote(image);
            else if(id=="custom"&&!String.IsNullOrWhiteSpace(arguments)){if(arguments.IndexOfAny(new[]{'\r','\n','\0'})>=0||!arguments.Contains("{image}"))throw new InvalidOperationException("Custom arguments must contain {image}, or be left empty.");args=arguments.Replace("\"{image}\"",Backend.Quote(image)).Replace("{image}",Backend.Quote(image));}
            else args=Backend.Quote(image);
            return new ProcessStartInfo(path,args){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)};
        }
    }
    public class EditorPreferences {
        public string Selected="";public string Arguments="";public Dictionary<string,string> Paths=new Dictionary<string,string>();
        public static EditorPreferences Load(){
            try{var loaded=new JavaScriptSerializer().Deserialize<EditorPreferences>(File.ReadAllText(Path.Combine(AppStorage.SettingsDirectory,"editor-settings.json")));if(loaded!=null&&EditorTools.Ids.Contains(loaded.Selected)){if(loaded.Paths==null)loaded.Paths=new Dictionary<string,string>();return loaded;}}catch{}
            var settings=new EditorPreferences();try{var legacy=File.ReadAllText(Path.Combine(AppStorage.SettingsDirectory,"image-editor.txt")).Trim();if(!String.IsNullOrWhiteSpace(legacy)){settings.Selected=EditorTools.KnownId(legacy);settings.Paths[settings.Selected]=legacy;return settings;}}catch{}
            settings.Selected=EditorTools.Find("sharex")!=null?"sharex":EditorTools.Find("paint")!=null?"paint":EditorTools.Find("greenshot")!=null?"greenshot":"sharex";return settings;
        }
        public void Save(){var target=Path.Combine(AppStorage.SettingsDirectory,"editor-settings.json");var temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
    }


}