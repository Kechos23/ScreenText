using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
namespace ScreenText {
 public sealed class CopilotModel {public string id,name;public bool vision;public override string ToString(){return name??id;}}
 public sealed class CopilotSnapshot {public bool authenticated,accessChecked,catalogLoaded;public string host,login,accessIssue;public List<CopilotModel> models=new List<CopilotModel>();}
 public sealed class CopilotCommand {public string action,host,login,model,prompt,image;}
 public sealed class CopilotReply {public CopilotSnapshot account;public string text;}
 public interface ICopilotRuntime {Task<CopilotReply> Run(CopilotCommand command,CancellationToken token,Action<string> progress);}
 public sealed class CopilotRuntime:ICopilotRuntime {
  readonly string runtimeDirectory;public CopilotRuntime(string directory=null){runtimeDirectory=directory??Path.Combine(Backend.Base,"copilot-runtime");}
  public bool Available{get{return File.Exists(Path.Combine(runtimeDirectory,"node.exe"))&&File.Exists(Path.Combine(runtimeDirectory,"worker.mjs"))&&File.Exists(Path.Combine(runtimeDirectory,"copilot.exe"));}}
  static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously);}
  public static string ErrorMessage(string code){switch(code){case "account_changed":return "The Copilot CLI account changed. Check connection before sending another request.";case "login_required":return "Sign in to GitHub Copilot CLI first.";case "model_unavailable":return "The selected Copilot model is not in this account's catalog. Refresh models and choose another.";case "quota":return "Copilot usage is currently limited. Check your plan or try later. Your text was kept.";case "access":return "Copilot access is unavailable. Check your plan and company CLI policy.";case "storage":return "Copilot could not use the Windows credential store. Review credential storage in the official CLI.";case "timeout":return "Copilot did not finish in time. Your previous text was kept.";default:return "Copilot could not complete this action. Your previous text was kept. Check connection or try again.";}}
  public async Task<CopilotReply> Run(CopilotCommand command,CancellationToken token,Action<string> progress){
   if(!Available)throw new AiConnectionException("The bundled Copilot runtime is missing. Use the complete ScreenText package.","copilot_runtime_missing");
   var json=new JavaScriptSerializer{MaxJsonLength=33554432};var start=new ProcessStartInfo(Path.Combine(runtimeDirectory,"node.exe"),Backend.Quote(Path.Combine(runtimeDirectory,"worker.mjs"))){WorkingDirectory=runtimeDirectory};
   foreach(string key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())if(System.Text.RegularExpressions.Regex.IsMatch(key,@"^(GH_|GITHUB_|COPILOT_|OPENAI_|ANTHROPIC_|AZURE_|GEMINI_|GOOGLE_|AWS_|BEDROCK_|NODE_OPTIONS$|OTEL_)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))start.EnvironmentVariables.Remove(key);
   using(var process=RequestProcess.Start(start))using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
    deadline.CancelAfter(TimeSpan.FromMinutes(command.action=="login"?10:command.action=="generate"?3:1));using(deadline.Token.Register(process.Stop)){
     var drain=process.Error.ReadToEndAsync();CopilotReply reply=null;process.Input.AutoFlush=true;
     try{await process.Input.WriteLineAsync(json.Serialize(command));process.Input.Close();
      while(true){deadline.Token.ThrowIfCancellationRequested();string line=await process.Output.ReadLineAsync();if(line==null)break;if(line.Length>4194304)throw new AiConnectionException("Invalid Copilot response.");var item=json.Deserialize<Dictionary<string,object>>(line);string type=item.ContainsKey("type")?item["type"] as string:null;
       if(type=="progress"&&progress!=null)progress(item["text"] as string??"");
       else if(type=="result")reply=json.Deserialize<CopilotReply>(json.Serialize(item["value"]));
       else if(type=="error"){string code=item.ContainsKey("code")?item["code"] as string:null;throw new AiConnectionException(ErrorMessage(code),"copilot_"+(code??"runtime"));}
      }
      while(!process.HasExited){deadline.Token.ThrowIfCancellationRequested();await Task.Delay(40,deadline.Token);}deadline.Token.ThrowIfCancellationRequested();if(process.ExitCode!=0||reply==null)throw new AiConnectionException(ErrorMessage("runtime"),"copilot_runtime");return reply;
     }catch{token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new AiConnectionException(ErrorMessage("timeout"),"copilot_timeout");throw;}
     finally{process.Stop();Observe(drain);}
    }
   }
  }
 }
 public sealed class CopilotConnection {
  readonly ICopilotRuntime runtime;readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);public CopilotSnapshot Account{get;private set;}
  public CopilotConnection(ICopilotRuntime adapter=null){runtime=adapter??new CopilotRuntime();}
  public void Forget(){Account=null;}
  public async Task<CopilotSnapshot> Check(string host,bool login,CancellationToken token){host=CopilotHost.Normalize(host);await gate.WaitAsync(token);try{var reply=await runtime.Run(new CopilotCommand{action=login?"login":"status",host=host},token,null);if(reply.account==null)throw new AiConnectionException("Invalid Copilot account response.");if(reply.account.authenticated&&(CopilotHost.Normalize(reply.account.host)!=host||String.IsNullOrWhiteSpace(reply.account.login)))throw new AiConnectionException(CopilotRuntime.ErrorMessage("account_changed"),"copilot_account_changed");if(!reply.account.catalogLoaded&&Account!=null&&Account.authenticated&&reply.account.authenticated&&Account.host==reply.account.host&&Account.login==reply.account.login){reply.account.models=Account.models;reply.account.catalogLoaded=Account.catalogLoaded;}Account=reply.account;return Account;}finally{gate.Release();}}
  public async Task SignOut(string host,string login,CancellationToken token){host=CopilotHost.Normalize(host);await gate.WaitAsync(token);try{await runtime.Run(new CopilotCommand{action="logout",host=host,login=login},token,null);Account=null;}finally{gate.Release();}}
  public async Task<string> Generate(string host,string model,string prompt,byte[] image,CancellationToken token,Action<string> progress){
   if(Account==null||!Account.authenticated)throw new AiConnectionException(CopilotRuntime.ErrorMessage("login_required"),"copilot_login_required");if(!AiModelSelection.Valid(model))throw new AiConnectionException("Choose a valid Copilot model.");if(image!=null&&image.Length>20*1024*1024)throw new AiConnectionException("This image exceeds the 20 MB request limit.");
   if(Account.host!=CopilotHost.Normalize(host))throw new AiConnectionException(CopilotRuntime.ErrorMessage("account_changed"),"copilot_account_changed");var expected=Account;await gate.WaitAsync(token);try{var reply=await runtime.Run(new CopilotCommand{action="generate",host=CopilotHost.Normalize(host),login=expected.login,model=model,prompt=prompt,image=image==null?null:Convert.ToBase64String(image)},token,progress);if(reply.text==null)throw new AiConnectionException("Copilot returned no result. Your previous text was kept.");return reply.text;}catch(AiConnectionException ex){if(ex.Code=="copilot_account_changed"||ex.Code=="copilot_login_required")Account=null;throw;}finally{gate.Release();}
  }
 }
 public sealed class CopilotPreferences {
  public string Host="github.com",TranslationModel="auto",ExtractionModel="auto";
  public static CopilotPreferences Load(){try{var p=new JavaScriptSerializer().Deserialize<CopilotPreferences>(File.ReadAllText(Path.Combine(AppStorage.SettingsDirectory,"copilot-settings.json")));p.Host=CopilotHost.Normalize(p.Host);if(!AiModelSelection.Valid(p.TranslationModel))p.TranslationModel="auto";if(!AiModelSelection.Valid(p.ExtractionModel))p.ExtractionModel="auto";return p;}catch{return new CopilotPreferences();}}
  public void Save(){string file=Path.Combine(AppStorage.SettingsDirectory,"copilot-settings.json"),tmp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(tmp,new JavaScriptSerializer().Serialize(this));if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
 }
}
