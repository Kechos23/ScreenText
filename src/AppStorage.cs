using System;using System.IO;
namespace ScreenText {
 public static class AppStorage {
  static readonly Lazy<string> Settings=new Lazy<string>(()=>ChooseSettingsDirectory(Backend.Base,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenText","Settings")));
  static readonly string[] Files={"theme.txt","accent-color.txt","capture-storage.txt","capture-folder.txt","auto-copy-image.txt","close-to-tray.txt","default-extract.txt","screenshot-shortcut.txt","editor-settings.json","image-editor.txt","languages.json","ai-settings.json","ai-active-provider.txt","copilot-settings.json","openai-connection.txt","codex-cli-settings.json"};
  public static string SettingsDirectory{get{return Settings.Value;}}
  public static string ChooseSettingsDirectory(string portable,string perUser,Func<string,bool> canWrite=null){
   var writable=canWrite??CanWrite;if(writable(portable))return portable;if(!writable(perUser))return portable;
   foreach(string name in Files)try{string from=Path.Combine(portable,name),to=Path.Combine(perUser,name);if(File.Exists(from)&&!File.Exists(to))File.Copy(from,to,false);}catch{}
   return perUser;
  }
  static bool CanWrite(string folder){string probe=null;try{Directory.CreateDirectory(folder);probe=Path.Combine(folder,".screentext-writecheck-"+Guid.NewGuid().ToString("N"));using(new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None)){}File.Delete(probe);return true;}catch{return false;}finally{if(probe!=null)try{File.Delete(probe);}catch{}}}
 }
}