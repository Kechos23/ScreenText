using System;using System.Collections.Concurrent;using System.Collections.Generic;using System.IO;using System.Linq;
namespace ScreenText {
 public static class TextPolicy {public const int EditorMaximum=Int32.MaxValue;public const int TranslationMaximum=50000;}
 public sealed class TransientSession:IDisposable {
  const string Marker="ScreenText transient session v1";static readonly ConcurrentDictionary<string,TransientSession> Active=new ConcurrentDictionary<string,TransientSession>(StringComparer.OrdinalIgnoreCase);
  static readonly HashSet<string> OwnedNames=new HashSet<string>(new[]{".owner",".lease","input.png","capture.png","schema.json","result.json"},StringComparer.OrdinalIgnoreCase);
  public static string DefaultRoot{get{return Path.Combine(Path.GetTempPath(),"ScreenText-transient");}}
  public readonly string DirectoryPath;readonly string root;FileStream lease;bool disposed;readonly object gate=new object();
  TransientSession(string location){root=Path.GetFullPath(location).TrimEnd(Path.DirectorySeparatorChar);Directory.CreateDirectory(root);if(Linked(root))throw new IOException("Temporary session root must not be a link.");DirectoryPath=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);try{lease=new FileStream(Path.Combine(DirectoryPath,".lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);File.WriteAllText(Path.Combine(DirectoryPath,".owner"),Marker);Active[DirectoryPath]=this;}catch{if(lease!=null)lease.Dispose();try{File.Delete(Path.Combine(DirectoryPath,".lease"));Directory.Delete(DirectoryPath,false);}catch{}throw;}}
  public static TransientSession Create(string root=null){return new TransientSession(root??DefaultRoot);}
  static bool Linked(string path){return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;}
  static bool DeleteOwned(string root,string path){
   try{root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);path=Path.GetFullPath(path);if(Path.GetDirectoryName(path)!=root||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),"^[a-f0-9]{32}$")||Linked(root)||Linked(path))return false;
    var files=Directory.GetFiles(path);if(Directory.GetDirectories(path).Length!=0||files.Any(f=>Linked(f)||!OwnedNames.Contains(Path.GetFileName(f))))return false;var marker=Path.Combine(path,".owner");var lockPath=Path.Combine(path,".lease");if(!File.Exists(marker)||!File.Exists(lockPath)||File.ReadAllText(marker)!=Marker)return false;
    using(var check=new FileStream(lockPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){foreach(string file in files)if(file!=marker&&file!=lockPath)File.Delete(file);}
    File.Delete(marker);File.Delete(lockPath);Directory.Delete(path,false);return true;
   }catch{return false;}
  }
  public static int CleanupAbandoned(string root=null){int deleted=0;root=root??DefaultRoot;try{if(!Directory.Exists(root)||Linked(root))return 0;foreach(string directory in Directory.GetDirectories(root))if(DeleteOwned(root,directory))deleted++;}catch{}return deleted;}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;TransientSession ignored;Active.TryRemove(DirectoryPath,out ignored);if(lease!=null){lease.Dispose();lease=null;}DeleteOwned(root,DirectoryPath);}}
  public static void CleanupActive(){foreach(var session in Active.Values)session.Dispose();}
 }
 public static class TransientImages {
  static readonly ConcurrentDictionary<string,TransientSession> Sessions=new ConcurrentDictionary<string,TransientSession>(StringComparer.OrdinalIgnoreCase);
  public static string Create(){var session=TransientSession.Create();string path=Path.Combine(session.DirectoryPath,"input.png");Sessions[path]=session;return path;}
  public static void Delete(string path){if(String.IsNullOrEmpty(path))return;TransientSession session;if(Sessions.TryRemove(path,out session))session.Dispose();else try{File.Delete(path);}catch{}}
 }
}