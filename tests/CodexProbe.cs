using System;using System.IO;using System.Threading;
class CodexProbe{
 static int Main(string[] args){Console.OutputEncoding=new System.Text.UTF8Encoding(false);string root=AppDomain.CurrentDomain.BaseDirectory,mode=File.ReadAllText(Path.Combine(root,"probe-mode.txt")).Trim();File.WriteAllText(Path.Combine(root,"probe-pid.txt"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());bool login=args.Length>1&&args[0]=="login"&&args[1]=="status";File.AppendAllText(Path.Combine(root,"probe-calls.txt"),login?"login\n":"exec\n");
 if(Environment.GetEnvironmentVariable("OPENAI_API_KEY")!=null||Environment.GetEnvironmentVariable("CODEX_THREAD_ID")!=null){Console.Error.WriteLine("Unexpected inherited environment");return 2;}
 if(mode=="hang"||mode=="login-slow"||(mode=="exec-slow"&&!login)){Thread.Sleep(30000);return 0;}
 if(login){if(mode=="logged-out"){Console.Error.WriteLine("Not logged in");return 1;}Console.Error.WriteLine(mode=="api"?"Logged in using API key":"Logged in using ChatGPT");return 0;}
 string error=mode=="quota"?"usage_limit_reached":mode=="rate"?"429 Too Many Requests":mode=="network"?"DNS lookup failed: error sending request":mode=="auth"?"401 unauthorized":mode=="service"?"503 service unavailable":mode=="model"?"model_not_found":null;if(error!=null){Console.Error.WriteLine(error);return 1;}
 File.WriteAllText(Path.Combine(root,"probe-args.json"),new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(args));
 for(int i=0;i<args.Length-1;i++)if(args[i]=="-o"){File.WriteAllText(args[i+1],mode=="invalid"?"not json":"{\"translated_text\":\"Fixture translation\",\"extracted_text\":\"Fixture extraction\"}");break;}return 0;
 }
}
