import path from 'node:path';import fs from 'node:fs';import {randomUUID} from 'node:crypto';import readline from 'node:readline';import {spawn} from 'node:child_process';import {fileURLToPath} from 'node:url';
const base=path.dirname(fileURLToPath(import.meta.url));const input=readline.createInterface({input:process.stdin,crlfDelay:Infinity});let client,session,sessionMarker,stage='startup';
const markerRoot=path.join(process.env.LOCALAPPDATA??process.env.TEMP,'ScreenText','CopilotSessions');
const send=value=>process.stdout.write(JSON.stringify(value)+'\n');
const normalize=value=>{const uri=new URL(value.includes('://')?value:'https://'+value);if(uri.protocol!=='https:'||uri.port||uri.username||uri.password||!(/^(github\.com|[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.ghe\.com)$/.test(uri.hostname)))throw new Error('account_changed');return uri.hostname;};
const fail=code=>{throw new Error(code);};
try{
 const line=await new Promise(resolve=>input.once('line',resolve));input.close();const command=JSON.parse(line);const host=normalize(command.host);if(!['status','login','logout','generate'].includes(command.action))fail('runtime');
 for(const key of Object.keys(process.env))if(/^(GH_|GITHUB_|COPILOT_|OPENAI_|ANTHROPIC_|AZURE_|GEMINI_|GOOGLE_|AWS_|BEDROCK_|NODE_OPTIONS$|OTEL_)/i.test(key))delete process.env[key];
 // Do not export native diagnostics, OAuth URLs or credentials across the IPC boundary.
 process.stderr.write=()=>true;
 if(command.action==='login'){
  const outcome=await new Promise((resolve,reject)=>{const child=spawn(path.join(base,'copilot.exe'),['--no-auto-update','--log-level','none','login','--web-flow','--host','https://'+host],{cwd:base,env:process.env,windowsHide:true,stdio:['ignore','pipe','pipe']});let transcript='';const read=chunk=>{if(transcript.length<65536)transcript+=chunk.toString();};child.stdout.on('data',read);child.stderr.on('data',read);child.on('error',reject);child.on('close',code=>resolve({code,transcript}));});
  if(outcome.code!==0||!/Signed in successfully/i.test(outcome.transcript))fail(/plaintext|plain text|keychain|credential store/i.test(outcome.transcript)?'storage':'runtime');
 }
 const {CopilotClient}=await import('@github/copilot-sdk');client=new CopilotClient({workingDirectory:base,useLoggedInUser:true,autoRestart:false,logLevel:'none'});await client.start();
 // Reclaim only our recorded session IDs; never enumerate or remove other CLI sessions.
 fs.mkdirSync(markerRoot,{recursive:true});
 if(fs.lstatSync(markerRoot).isSymbolicLink())fail('runtime');
 for(const name of fs.readdirSync(markerRoot)){
  if(!/^screentext-[a-f0-9-]{36}\.owned$/.test(name))continue;
  const marker=path.join(markerRoot,name);if(fs.lstatSync(marker).isSymbolicLink()||fs.readFileSync(marker,'utf8')!=='ScreenText Copilot session v1')continue;
  try{await client.deleteSession(name.slice(0,-6));fs.unlinkSync(marker);}catch(error){if(/not found|does not exist|no such session|unknown session/i.test(String(error?.message??'')))fs.unlinkSync(marker);}
 }
 const auth=await client.getAuthStatus();
 const matches=auth.isAuthenticated&&auth.authType==='user'&&normalize(auth.host??'github.com')===host;
 const account={authenticated:!!matches,host,login:matches?auth.login:'',models:[],accessChecked:false,catalogLoaded:false,accessIssue:matches?'':'No CLI sign-in for this environment. Sign in or check which account the CLI is using.'};
 if(command.action==='generate'||command.action==='logout'){
  if(!matches)fail('login_required');if(auth.login!==command.login)fail('account_changed');
 }
 if(matches&&command.action!=='logout'){
  try{const models=await client.listModels();account.catalogLoaded=true;account.models=models.filter(m=>/^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$/.test(m.id)&&m.policy?.state!=='disabled').map(m=>({id:m.id,name:m.name??m.id,vision:m.capabilities?.supports?.vision===true}));account.accessChecked=account.models.length>0;account.accessIssue=account.accessChecked?'':'No Copilot models are available. Check your plan and company CLI policy.';}catch{account.accessIssue='Could not refresh the Copilot catalog. Check your connection, plan or company CLI policy.';}
 }
 if(command.action==='generate'||command.action==='logout'){
  if(command.action==='generate'&&!account.models.some(m=>m.id===command.model))fail('model_unavailable');
  stage='create-session';const sessionId='screentext-'+randomUUID();sessionMarker=path.join(markerRoot,sessionId+'.owned');fs.writeFileSync(sessionMarker,'ScreenText Copilot session v1',{flag:'wx'});session=await client.createSession({sessionId,enableConfigDiscovery:false,enableSessionStore:false,enableSessionTelemetry:false,enableHostGitOperations:false,enableSkills:false,includedBuiltinSkills:[],skipCustomInstructions:true,customAgentsLocalOnly:true,enableOnDemandInstructionDiscovery:false,mcpOAuthTokenStorage:'in-memory',memory:{enabled:false},infiniteSessions:{enabled:false},embeddingCacheStorage:'in-memory',skipEmbeddingRetrieval:true,disabledMcpServers:['github-mcp-server','githubiq'],model:command.action==='generate'?command.model:undefined,availableTools:[],excludedTools:['builtin:*','mcp:*','custom:*'],enableFileHooks:false,mcpServers:{},customAgents:[],streaming:true,onPermissionRequest:()=>({kind:'denied-no-approval-rule-and-could-not-request-from-user'})});
  stage='verify-session-account';const sessionStatus=(await session.rpc.accounts.get({query:{kind:'status'}})).status;if(!sessionStatus.isAuthenticated||sessionStatus.activeLogin!==command.login||normalize(sessionStatus.activeHost??'github.com')!==host)fail('account_changed');
  if(command.action==='logout'){
   const roster=await session.rpc.accounts.enumerate({query:{kind:'accounts'}});const entry=roster.items?.find(a=>a.login===command.login&&normalize(a.host)===host);if(!entry)fail('account_changed');await session.rpc.accounts.set({command:{kind:'logout',selectionId:entry.selectionId}});send({type:'result',value:{account:{...account,authenticated:false}}});
  }else{
   let draft='';session.on('assistant.message_delta',event=>{draft+=event.data.deltaContent??'';if(draft.length>1048576)fail('runtime');send({type:'progress',text:draft});});
   const attachments=command.image?[{type:'blob',mimeType:'image/png',data:command.image,displayName:'capture.png'}]:undefined;
   stage='send';const answer=await session.sendAndWait({prompt:command.prompt,attachments},150000);const text=answer?.data?.content;if(typeof text!=='string')fail('runtime');send({type:'result',value:{text,account}});
  }
 }else send({type:'result',value:{account}});
}catch(error){const msg=String(error?.message??'');const direct=['account_changed','login_required','model_unavailable','runtime','storage'];const code=direct.includes(msg)?msg:/quota|rate.limit|credit|budget/i.test(msg)?'quota':/subscription|entitle|forbidden|403/i.test(msg)?'access':/timeout|timed out/i.test(msg)?'timeout':'runtime';send({type:'error',code,stage,diagnostic:['invalid','unknown','unsupported','session','tools','memory','infinite','model','filesystem','storage'].filter(word=>msg.toLowerCase().includes(word))});process.exitCode=1;}
finally{if(session){const id=session.sessionId;await session.disconnect().catch(()=>{});try{await client.deleteSession(id);if(sessionMarker)fs.unlinkSync(sessionMarker);}catch{}}if(client)await client.stop().catch(()=>{});input.close();process.stdin.pause();await new Promise(resolve=>process.stdout.write('',resolve));process.exit(process.exitCode??0);}
