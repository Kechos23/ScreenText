using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace ScreenText {
    public sealed class AiConnectionException : Exception {
        public readonly string Code, RequestId; public readonly int HttpStatus;
        public AiConnectionException(string message,string code="",int status=0,string requestId=""):base(message){Code=code;HttpStatus=status;RequestId=requestId;}
    }
    public sealed class ChatGptModel { public string Id,Name; public override string ToString(){return Name;} }
    public sealed class ChatGptAccount { public string Id,Label; public bool Connected; public override string ToString(){return Label;} }
    // Own OAuth tokens only: never open, import or modify Codex's credential store.
    public sealed class ChatGptConnection : IDisposable {
        const string Auth="https://auth.openai.com", Resource="https://api.openai.com/v1";
        const string Scopes="openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
        static readonly byte[] Entropy=Encoding.UTF8.GetBytes("ScreenText ChatGPT connection v1");
        sealed class Registration { public Registration(){}
            public string ClientId,Subject,Email,Access,Refresh,IdToken,Scope; public long Expires;
        }
        sealed class Vault { public Vault(){} public string Host,Active,PendingClient; public List<Registration> Accounts=new List<Registration>(); }
        readonly string directory; readonly HttpClient http; readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        Vault vault; bool disposed;
        public Action<string> BrowserLauncher=uri=>Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});
        public ChatGptConnection(string storageDirectory=null,HttpMessageHandler handler=null){
            directory=storageDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenText","ChatGPT");
            if(handler==null)ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            http=handler==null?new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}):new HttpClient(handler);
            http.Timeout=Timeout.InfiniteTimeSpan; vault=ReadVault(false);
        }
        JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue};}
        static long Now(){return (long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;}
        public bool StorageUnreadable {get;private set;}
        Vault ReadVault(bool strict=true){
            string file=Path.Combine(directory,"connection.dat");if(!File.Exists(file))return new Vault();
            try{var decoded=ProtectedData.Unprotect(File.ReadAllBytes(file),Entropy,DataProtectionScope.CurrentUser);try{var value=Json().Deserialize<Vault>(Encoding.UTF8.GetString(decoded));if(value==null||value.Accounts==null||value.Accounts.Any(a=>String.IsNullOrEmpty(a.ClientId)||String.IsNullOrEmpty(a.Subject)))throw new Exception();return value;}finally{Array.Clear(decoded,0,decoded.Length);}}
            catch{StorageUnreadable=true;if(!strict)return new Vault();throw new AiConnectionException("The saved ChatGPT connection could not be opened for this Windows user. Reconnect from AI Providers settings.","storage_unreadable");}
        }
        void Save(){
            Directory.CreateDirectory(directory);string file=Path.Combine(directory,"connection.dat"),temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            byte[] plain=Encoding.UTF8.GetBytes(Json().Serialize(vault));
            try{File.WriteAllBytes(temp,ProtectedData.Protect(plain,Entropy,DataProtectionScope.CurrentUser));if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}
            finally{Array.Clear(plain,0,plain.Length);if(File.Exists(temp))File.Delete(temp);}
        }
        async Task<FileStream> Lock(CancellationToken token){
            Directory.CreateDirectory(directory);var watch=Stopwatch.StartNew();
            while(true){token.ThrowIfCancellationRequested();try{return new FileStream(Path.Combine(directory,"connection.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(watch.Elapsed.TotalSeconds>45)throw new AiConnectionException("Another ScreenText request is updating the connection. Try again shortly.");}await Task.Delay(80,token);}
        }
        Registration Active(){return vault.Accounts.FirstOrDefault(a=>a.ClientId==vault.Active);}
        public ChatGptAccount[] Accounts {get{return vault.Accounts.Select(a=>new ChatGptAccount{Id=a.ClientId,Label=(String.IsNullOrEmpty(a.Email)?"ChatGPT account":a.Email)+" · "+a.ClientId.Substring(Math.Max(0,a.ClientId.Length-6)),Connected=!String.IsNullOrEmpty(a.Access)}).ToArray();}}
        public string ActiveId {get{return vault.Active;}}
        public bool Connected {get{var a=Active();return a!=null&&!String.IsNullOrEmpty(a.Access)&&HasScope(a.Scope);}}
        static bool HasScope(string scope){return (scope??"").Split(' ').Contains("chatgpt.tokens.use.direct");}
        public async Task SelectAccount(string id,CancellationToken token){await gate.WaitAsync(token);try{using(await Lock(token)){vault=ReadVault();if(!vault.Accounts.Any(a=>a.ClientId==id))throw new AiConnectionException("Select a saved ChatGPT account.");vault.Active=id;Save();}}finally{gate.Release();}}
        static string Random(){byte[] value=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(value);return B64(value);}
        static string B64(byte[] bytes){return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
        static byte[] Decode(string text){text=text.Replace('-','+').Replace('_','/');return Convert.FromBase64String(text.PadRight((text.Length+3)/4*4,'='));}
        static bool Equal(string a,string b){if(a==null||b==null||a.Length!=b.Length)return false;int difference=0;for(int i=0;i<a.Length;i++)difference|=a[i]^b[i];return difference==0;}
        static string Query(Dictionary<string,string> values){return String.Join("&",values.Select(v=>Uri.EscapeDataString(v.Key)+"="+Uri.EscapeDataString(v.Value)));}
        static string Get(Dictionary<string,object> data,string name){object value;return data!=null&&data.TryGetValue(name,out value)?value as string:null;}
        static Dictionary<string,object> Object(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value as Dictionary<string,object>:null;}
        static string SafeCode(string value){return value!=null&&System.Text.RegularExpressions.Regex.IsMatch(value,"^[a-zA-Z0-9_.:-]{1,160}$")?value:"";}
        public static AiConnectionException Failure(string code,int status=0,string requestId=""){
            string message;
            switch(code){
                case "invalid_grant":case "subscription_sharing_invalid_user": message="Your ChatGPT connection has expired or was revoked. Reconnect in AI Providers settings.";break;
                case "subscription_sharing_usage_limit_exceeded":message="The ChatGPT plan limit for this app or account has been reached. Review your usage in ChatGPT Settings.";break;
                case "subscription_sharing_user_not_eligible":message="ChatGPT plan access is unavailable for this account or workspace.";break;
                case "subscription_sharing_usage_unavailable":case "subscription_sharing_user_unavailable":message="ChatGPT could not check plan availability. Try again later. Your connection was kept.";break;
                case "subscription_sharing_unsupported_capability":case "model_not_found":message="The selected model or input is not supported. Refresh models or choose another model.";break;
                case "chatpass_v2_scope_not_authorized":case "chatpass_v2_invalid_authorization_context":message="The ChatGPT connection does not authorize this operation. Reconnect and allow plan usage.";break;
                default:message=status==401?"ChatGPT could not authorize the connection. Reconnect in AI Providers settings.":status==403?"ChatGPT access was restricted by account, workspace or region policy.":status==429?"ChatGPT is limiting requests. Wait before trying again.":status>=500?"ChatGPT is temporarily unavailable. Try again later. Your connection was kept.":"ChatGPT could not complete the request. Your previous text and image were kept.";break;
            }
            return new AiConnectionException(message,SafeCode(code),status,SafeCode(requestId));
        }
        async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken token){
            try{var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);if(response.IsSuccessStatusCode)return response;
                using(response)using(token.Register(response.Dispose)){string body=await response.Content.ReadAsStringAsync();Dictionary<string,object> error=null;try{error=Json().Deserialize<Dictionary<string,object>>(body);}catch{}var nested=Object(error,"error");string id=response.Headers.Contains("x-request-id")?response.Headers.GetValues("x-request-id").FirstOrDefault():"";throw Failure(Get(nested,"code")??Get(error,"error"),(int)response.StatusCode,id);}}
            catch(HttpRequestException){token.ThrowIfCancellationRequested();throw new AiConnectionException("Could not reach ChatGPT. Check your internet connection and try again. Your saved connection was kept.","network");}
        }
        async Task<Dictionary<string,object>> Fetch(string url,CancellationToken token,Dictionary<string,string> form=null,string access=null){
            using(var request=new HttpRequestMessage(form==null?HttpMethod.Get:HttpMethod.Post,url)){if(form!=null)request.Content=new FormUrlEncodedContent(form);if(access!=null)request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",access);
                using(var response=await Send(request,token))using(token.Register(response.Dispose)){token.ThrowIfCancellationRequested();return Json().Deserialize<Dictionary<string,object>>(await response.Content.ReadAsStringAsync());}}
        }
        async Task<Dictionary<string,object>> ValidateIdentity(string encoded,string client,string nonce,CancellationToken token){
            try{
                var parts=encoded.Split('.');if(parts.Length!=3)throw new Exception();var head=Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Decode(parts[0])));var claims=Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(Decode(parts[1])));
                if(Get(head,"alg")!="RS256")throw new Exception();var discovery=await Fetch(Auth+"/.well-known/openid-configuration",token);var uri=new Uri(Get(discovery,"jwks_uri"));if(uri.Scheme!="https"||uri.Host!="auth.openai.com"||Get(discovery,"issuer")!=Auth)throw new Exception();
                var keys=await Fetch(uri.AbsoluteUri,token);var key=((System.Collections.IEnumerable)keys["keys"]).Cast<Dictionary<string,object>>().Single(k=>Get(k,"kid")==Get(head,"kid")&&Get(k,"kty")=="RSA"&&(Get(k,"use")==null||Get(k,"use")=="sig")&&(Get(k,"alg")==null||Get(k,"alg")=="RS256"));
                using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.ImportParameters(new RSAParameters{Modulus=Decode(Get(key,"n")),Exponent=Decode(Get(key,"e"))});if(rsa.KeySize<2048||!rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0]+"."+parts[1]),CryptoConfig.MapNameToOID("SHA256"),Decode(parts[2])))throw new Exception();}
                var audience=claims["aud"] as string;var audiences=audience==null?((System.Collections.IEnumerable)claims["aud"]).Cast<object>().Select(o=>o.ToString()).ToArray():new[]{audience};
                if(Get(claims,"iss")!=Auth||!audiences.Contains(client)||(audiences.Length>1&&Get(claims,"azp")!=client)||Convert.ToInt64(claims["exp"])<Now()-30||String.IsNullOrEmpty(Get(claims,"sub")))throw new Exception();
                if(claims.ContainsKey("nbf")&&Convert.ToInt64(claims["nbf"])>Now()+30)throw new Exception();if(claims.ContainsKey("iat")&&Convert.ToInt64(claims["iat"])>Now()+30)throw new Exception();if(nonce!=null&&!Equal(Get(claims,"nonce"),nonce))throw new Exception();return claims;
            }catch(OperationCanceledException){throw;}catch(AiConnectionException){throw;}catch{throw new AiConnectionException("ChatGPT identity verification failed. No connection was saved.","identity_invalid");}
        }
        // Minimal loopback HTTP callback: bounded headers, no request/URL logging, no remote bind.
        async Task<Dictionary<string,string>> Callback(TcpListener listener,int port,string state,CancellationToken token){
            while(true){token.ThrowIfCancellationRequested();using(var client=await listener.AcceptTcpClientAsync())using(var stream=client.GetStream())using(var stop=token.Register(()=>client.Close()))using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true)){
                string line=await reader.ReadLineAsync();int bytes=line==null?0:line.Length;string host="";string header;while((header=await reader.ReadLineAsync())!=null&&header.Length>0){bytes+=header.Length;if(bytes>8192)break;if(header.StartsWith("Host:",StringComparison.OrdinalIgnoreCase))host=header.Substring(5).Trim();}
                var request=(line??"").Split(' ');Uri url;bool valid=bytes<=8192&&host=="127.0.0.1:"+port&&request.Length==3&&request[0]=="GET"&&Uri.TryCreate("http://127.0.0.1:"+port+request[1],UriKind.Absolute,out url);Dictionary<string,string> values=null;
                if(valid){url=new Uri("http://127.0.0.1:"+port+request[1]);var query=HttpUtility.ParseQueryString(url.Query);valid=url.AbsolutePath=="/auth/callback"&&Equal(query["state"],state)&&query.AllKeys.All(k=>k!=null&&query.GetValues(k).Length==1);if(valid)values=query.AllKeys.ToDictionary(k=>k,k=>query[k]);}
                string body=valid?"ScreenText received the authorization. You can close this tab.":"Invalid authorization callback. Return to ScreenText.";var data=Encoding.UTF8.GetBytes(body);var head=Encoding.ASCII.GetBytes("HTTP/1.1 "+(valid?"200 OK":"400 Bad Request")+"\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\n\r\n");await stream.WriteAsync(head,0,head.Length,token);await stream.WriteAsync(data,0,data.Length,token);if(valid)return values;
            }}
        }
        public async Task SignIn(bool newAccount,CancellationToken cancellation){
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){timeout.CancelAfter(TimeSpan.FromMinutes(5));var token=timeout.Token;await gate.WaitAsync(token);
                try{using(await Lock(token)){
                    vault=ReadVault(false);if(String.IsNullOrEmpty(vault.Host)){vault.Host="urn:uuid:"+Guid.NewGuid();Save();}var previous=newAccount?null:Active();string requestedClient=previous!=null?previous.ClientId:!newAccount&&!String.IsNullOrEmpty(vault.PendingClient)?vault.PendingClient:"dynamic_agent_client";string state=Random(),nonce=Random(),verifier=Random();string challenge;using(var sha=SHA256.Create())challenge=B64(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
                    var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;string redirect="http://127.0.0.1:"+port+"/auth/callback";Dictionary<string,string> callback;
                    try{using(token.Register(()=>listener.Stop())){
                        var values=new Dictionary<string,string>{{"client_id",requestedClient},{"ext_agent_host_id",vault.Host},{"response_type","code"},{"redirect_uri",redirect},{"scope",Scopes},{"resource",Resource},{"state",state},{"nonce",nonce},{"code_challenge_method","S256"},{"code_challenge",challenge}};
                        if(requestedClient=="dynamic_agent_client")values["agent_name_hint"]="ScreenText";else if(previous!=null&&!String.IsNullOrEmpty(previous.IdToken))values["id_token_hint"]=previous.IdToken;
                        BrowserLauncher(Auth+"/api/accounts/authorize?"+Query(values));callback=await Callback(listener,port,state,token);
                    }}finally{listener.Stop();}
                    if(callback.ContainsKey("error"))throw new AiConnectionException("ChatGPT sign-in was declined or unavailable. Your previous connection was kept.","consent_declined");
                    string client=callback.ContainsKey("client_id")?callback["client_id"]:requestedClient=="dynamic_agent_client"?null:requestedClient;
                    if(String.IsNullOrEmpty(client)||client=="dynamic_agent_client"||client.Length>256||(requestedClient!="dynamic_agent_client"&&client!=requestedClient)||!callback.ContainsKey("code"))throw new AiConnectionException("ChatGPT registration did not complete. Please try again.");
                    if(previous==null){vault.PendingClient=client;Save();}
                    var tokens=await Fetch(Auth+"/api/accounts/oauth/token",token,new Dictionary<string,string>{{"grant_type","authorization_code"},{"client_id",client},{"code",callback["code"]},{"code_verifier",verifier},{"redirect_uri",redirect},{"resource",Resource}});
                    var identity=await ValidateIdentity(Get(tokens,"id_token"),client,nonce,token);if(previous!=null&&previous.Subject!=Get(identity,"sub"))throw new AiConnectionException("The signed-in account differs from the selected account. Use Add account instead.");
                    if(!HasScope(Get(tokens,"scope"))||!String.Equals(Get(tokens,"token_type"),"Bearer",StringComparison.OrdinalIgnoreCase))throw new AiConnectionException("ChatGPT plan permission was not granted. Reconnect and allow plan usage.","plan_permission_missing");
                    var entry=new Registration{ClientId=client,Subject=Get(identity,"sub"),Email=Get(identity,"email"),Access=Get(tokens,"access_token"),Refresh=Get(tokens,"refresh_token"),IdToken=Get(tokens,"id_token"),Scope=Get(tokens,"scope"),Expires=Now()+Convert.ToInt64(tokens["expires_in"])};
                    if(String.IsNullOrEmpty(entry.Access)||String.IsNullOrEmpty(entry.Refresh))throw new AiConnectionException("The renewable ChatGPT connection was not returned. Try signing in again.");vault.Accounts.RemoveAll(a=>a.ClientId==client);vault.Accounts.Add(entry);vault.Active=client;vault.PendingClient=null;Save();StorageUnreadable=false;
                }}catch(Exception){cancellation.ThrowIfCancellationRequested();if(timeout.IsCancellationRequested)throw new AiConnectionException("ChatGPT sign-in timed out. Select Continue with ChatGPT to try again.","login_timeout");throw;}finally{gate.Release();}
            }
        }
        async Task<string> Access(CancellationToken token){
            await gate.WaitAsync(token);try{using(await Lock(token)){
                vault=ReadVault();var entry=Active();if(entry==null||String.IsNullOrEmpty(entry.Access)||!HasScope(entry.Scope))throw new AiConnectionException("Connect your ChatGPT account in Settings > AI Providers first.","login_required");
                if(entry.Expires>Now()+90)return entry.Access;
                Dictionary<string,object> tokens;
                try{tokens=await Fetch(Auth+"/api/accounts/oauth/token",token,new Dictionary<string,string>{{"grant_type","refresh_token"},{"client_id",entry.ClientId},{"refresh_token",entry.Refresh},{"resource",Resource}});}
                catch(AiConnectionException error){if(error.Code=="invalid_grant"){entry.Access=entry.Refresh=entry.IdToken=null;Save();}throw;}
                string access=Get(tokens,"access_token"),scope=Get(tokens,"scope")??entry.Scope;if(String.IsNullOrEmpty(access)||!HasScope(scope))throw new AiConnectionException("ChatGPT plan permission could not be renewed. Reconnect.");
                string identity=Get(tokens,"id_token");if(identity!=null){var claims=await ValidateIdentity(identity,entry.ClientId,null,token);if(Get(claims,"sub")!=entry.Subject)throw new AiConnectionException("ChatGPT returned a different account during renewal. Reconnect.");}
                entry.Access=access;entry.Refresh=Get(tokens,"refresh_token")??entry.Refresh;entry.IdToken=identity??entry.IdToken;entry.Scope=scope;entry.Expires=Now()+Convert.ToInt64(tokens["expires_in"]);Save();return entry.Access;
            }}finally{gate.Release();}
        }
        public async Task<List<ChatGptModel>> Models(CancellationToken token){using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(35));try{string access=await Access(deadline.Token);var data=await Fetch(Resource+"/models",deadline.Token,null,access);object models;if(!data.TryGetValue("models",out models))throw new AiConnectionException("ChatGPT returned an unreadable model catalog.");return ((System.Collections.IEnumerable)models).Cast<Dictionary<string,object>>().Where(m=>Get(m,"visibility")=="list"&&!String.IsNullOrEmpty(Get(m,"slug"))).Select(m=>new ChatGptModel{Id=Get(m,"slug"),Name=Get(m,"display_name")??Get(m,"slug")}).ToList();}catch(Exception){token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new AiConnectionException("ChatGPT model refresh timed out. Try again.","timeout");throw;}}}
        public async Task<bool> SignOut(CancellationToken cancellation){
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){
                deadline.CancelAfter(TimeSpan.FromSeconds(35));var token=deadline.Token;await gate.WaitAsync(token);
                try{using(await Lock(token)){
                    vault=ReadVault();var entry=Active();if(entry==null)return true;bool revoked=String.IsNullOrEmpty(entry.Refresh);
                    for(int attempt=0;!revoked&&attempt<2;attempt++){
                        bool retry=false;
                        try{
                            var discovery=await Fetch(Auth+"/.well-known/openid-configuration",token);var uri=new Uri(Get(discovery,"revocation_endpoint"));if(uri.Scheme!="https"||uri.Host!="auth.openai.com")throw new Exception();
                            using(var request=new HttpRequestMessage(HttpMethod.Post,uri)){request.Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"token",entry.Refresh},{"token_type_hint","refresh_token"},{"client_id",entry.ClientId}});using(var response=await Send(request,token))revoked=true;}
                        }catch(AiConnectionException error){retry=error.Code=="network"||error.HttpStatus>=500;}catch{}
                        if(!retry||token.IsCancellationRequested||attempt==1)break;
                        try{await Task.Delay(250,token);}catch{break;}
                    }
                    entry.Access=entry.Refresh=entry.IdToken=null;Save();return revoked;
                }}finally{gate.Release();}
            }
        }        public async Task<string> Generate(string model,string prompt,byte[] image,CancellationToken token,Action<string> progress=null){
            if(String.IsNullOrWhiteSpace(model))throw new AiConnectionException("Select an OpenAI model in AI Providers settings first.");if(image!=null&&image.Length>20*1024*1024)throw new AiConnectionException("This image exceeds the 20 MB request limit. Capture a smaller region.");
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(180));try{
                string access=await Access(deadline.Token);var content=new List<object>{new{type="input_text",text=prompt}};if(image!=null)content.Add(new{type="input_image",image_url="data:image/png;base64,"+Convert.ToBase64String(image)});
                var body=new{model=model,store=false,stream=true,input=new[]{new{role="user",content=content}}};
                using(var request=new HttpRequestMessage(HttpMethod.Post,Resource+"/responses")){request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",access);request.Content=new StringContent(Json().Serialize(body),Encoding.UTF8,"application/json");
                    using(var response=await Send(request,deadline.Token))using(deadline.Token.Register(response.Dispose))using(var stream=await response.Content.ReadAsStreamAsync())using(var reader=new StreamReader(stream,Encoding.UTF8)){
                        var text=new StringBuilder();var data=new StringBuilder();bool complete=false;string line;
                        while((line=await reader.ReadLineAsync())!=null){deadline.Token.ThrowIfCancellationRequested();if(line.Length>0){if(line.StartsWith("data:")){if(data.Length>0)data.Append('\n');data.Append(line.Substring(5).TrimStart());}continue;}if(data.Length==0)continue;string json=data.ToString();data.Clear();if(json=="[DONE]")continue;
                            var item=Json().Deserialize<Dictionary<string,object>>(json);string type=Get(item,"type");if(type=="response.output_text.delta"){text.Append(Get(item,"delta"));if(progress!=null)progress(text.ToString());}
                            else if(type=="response.completed"){complete=true;break;}
                            else if(type=="response.failed"||type=="response.incomplete"||type=="error"){var error=Object(Object(item,"response"),"error")??Object(item,"error")??item;throw Failure(Get(error,"code"));}
                        }
                        deadline.Token.ThrowIfCancellationRequested();if(!complete)throw new AiConnectionException("ChatGPT stopped before completing the response. Your previous text was kept.","incomplete");return text.ToString();
                    }
                }
            }catch(Exception error){token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw new AiConnectionException("ChatGPT did not finish before the timeout. Try again.","timeout");if(error is AiConnectionException)throw;throw new AiConnectionException("The ChatGPT response was interrupted. Your previous text and image were kept.","interrupted");}}
        }
        public void Dispose(){if(disposed)return;disposed=true;http.Dispose();}
    }
}
