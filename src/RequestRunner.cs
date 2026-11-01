using System;using System.Collections;using System.Collections.Generic;using System.ComponentModel;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Text;using System.Threading;using System.Threading.Tasks;using Microsoft.Win32.SafeHandles;
namespace ScreenText {
 // Start suspended, assign to an owned Windows job, then resume. Descendants cannot escape this job.
 internal sealed class RequestProcess:IDisposable {
  sealed class KernelHandle:SafeHandleZeroOrMinusOneIsInvalid {public KernelHandle():base(true){}public KernelHandle(IntPtr value):base(true){SetHandle(value);}protected override bool ReleaseHandle(){return CloseHandle(handle);}}
  [StructLayout(LayoutKind.Sequential)]struct SecurityAttributes{public int Length;public IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct StartupInfo{public int Size;public string Reserved,Desktop,Title;public int X,Y,XSize,YSize,XChars,YChars,Fill,Flags;public short Show,ReservedBytes;public IntPtr ReservedPointer,Input,Output,Error;}
  [StructLayout(LayoutKind.Sequential)]struct StartupInfoEx{public StartupInfo Startup;public IntPtr Attributes;}
  [StructLayout(LayoutKind.Sequential)]struct ProcessInfo{public IntPtr Process,Thread;public uint Id,ThreadId;}
  [StructLayout(LayoutKind.Sequential)]struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinimumWorkingSet,MaximumWorkingSet;public uint ActiveProcesses;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)]struct IoCounters{public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
  [StructLayout(LayoutKind.Sequential)]struct ExtendedLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool CloseHandle(IntPtr value);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr security,string name);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(KernelHandle job,int type,ref ExtendedLimits limits,uint length);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(KernelHandle job,KernelHandle process);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateJobObject(KernelHandle job,uint code);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(KernelHandle process,uint code);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool CreatePipe(out IntPtr read,out IntPtr write,ref SecurityAttributes security,uint size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,int flags,ref IntPtr size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,IntPtr attribute,IntPtr value,IntPtr size,IntPtr previous,IntPtr returned);
  [DllImport("kernel32.dll")]static extern void DeleteProcThreadAttributeList(IntPtr list);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateProcess(string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfoEx startup,out ProcessInfo process);
  [DllImport("kernel32.dll",SetLastError=true)]static extern uint ResumeThread(KernelHandle thread);
  [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(KernelHandle handle,uint milliseconds);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetExitCodeProcess(KernelHandle process,out uint code);
  KernelHandle job,process;public StreamReader Output,Error;public StreamWriter Input;
  public bool HasExited{get{return WaitForSingleObject(process,0)==0;}}
  public int ExitCode{get{uint code;if(!GetExitCodeProcess(process,out code))throw new Win32Exception();return unchecked((int)code);}}
  static void Check(bool ok){if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error());}
  static void Pipe(out KernelHandle read,out KernelHandle write,bool parentReads){var security=new SecurityAttributes{Length=Marshal.SizeOf(typeof(SecurityAttributes)),Inherit=true};IntPtr r,w;Check(CreatePipe(out r,out w,ref security,0));read=new KernelHandle(r);write=new KernelHandle(w);Check(SetHandleInformation(parentReads?r:w,1,0));}
  public static RequestProcess Start(ProcessStartInfo start){
   var owned=new RequestProcess();KernelHandle inputRead=null,inputWrite=null,outputRead=null,outputWrite=null,errorRead=null,errorWrite=null,thread=null;IntPtr attributes=IntPtr.Zero,handles=IntPtr.Zero,environment=IntPtr.Zero;bool initialized=false,resumed=false;
   try{
    owned.job=new KernelHandle(CreateJobObject(IntPtr.Zero,null));Check(!owned.job.IsInvalid);var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;Check(SetInformationJobObject(owned.job,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits))));
    Pipe(out inputRead,out inputWrite,false);Pipe(out outputRead,out outputWrite,true);Pipe(out errorRead,out errorWrite,true);
    IntPtr length=IntPtr.Zero;InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref length);attributes=Marshal.AllocHGlobal(length);Check(InitializeProcThreadAttributeList(attributes,1,0,ref length));initialized=true;handles=Marshal.AllocHGlobal(IntPtr.Size*3);Marshal.WriteIntPtr(handles,0,inputRead.DangerousGetHandle());Marshal.WriteIntPtr(handles,IntPtr.Size,outputWrite.DangerousGetHandle());Marshal.WriteIntPtr(handles,IntPtr.Size*2,errorWrite.DangerousGetHandle());Check(UpdateProcThreadAttribute(attributes,0,new IntPtr(0x20002),handles,new IntPtr(IntPtr.Size*3),IntPtr.Zero,IntPtr.Zero));
    var variables=start.EnvironmentVariables.Keys.Cast<string>().OrderBy(k=>k,StringComparer.OrdinalIgnoreCase).Select(k=>k+"="+start.EnvironmentVariables[k]);environment=Marshal.StringToHGlobalUni(String.Join("\0",variables)+"\0\0");
    var startup=new StartupInfoEx();startup.Startup.Size=Marshal.SizeOf(typeof(StartupInfoEx));startup.Startup.Flags=0x100;startup.Startup.Input=inputRead.DangerousGetHandle();startup.Startup.Output=outputWrite.DangerousGetHandle();startup.Startup.Error=errorWrite.DangerousGetHandle();startup.Attributes=attributes;ProcessInfo info;
    Check(CreateProcess(start.FileName,new StringBuilder(Backend.Quote(start.FileName)+" "+start.Arguments),IntPtr.Zero,IntPtr.Zero,true,0x08000000|0x00080000|0x00000400|0x4,environment,start.WorkingDirectory,ref startup,out info));owned.process=new KernelHandle(info.Process);thread=new KernelHandle(info.Thread);
    Check(AssignProcessToJobObject(owned.job,owned.process));
    owned.Input=new StreamWriter(new FileStream(new SafeFileHandle(inputWrite.DangerousGetHandle(),true),FileAccess.Write,4096,false),new UTF8Encoding(false));inputWrite.SetHandleAsInvalid();
    owned.Output=new StreamReader(new FileStream(new SafeFileHandle(outputRead.DangerousGetHandle(),true),FileAccess.Read,4096,false),Encoding.UTF8,true);outputRead.SetHandleAsInvalid();owned.Error=new StreamReader(new FileStream(new SafeFileHandle(errorRead.DangerousGetHandle(),true),FileAccess.Read,4096,false),Encoding.UTF8,true);errorRead.SetHandleAsInvalid();
    Check(ResumeThread(thread)!=UInt32.MaxValue);resumed=true;return owned;
   }catch{if(owned.process!=null&&!resumed)TerminateProcess(owned.process,1);owned.Dispose();throw;}
   finally{if(thread!=null)thread.Dispose();foreach(var handle in new[]{inputRead,inputWrite,outputRead,outputWrite,errorRead,errorWrite})if(handle!=null)handle.Dispose();if(initialized)DeleteProcThreadAttributeList(attributes);if(attributes!=IntPtr.Zero)Marshal.FreeHGlobal(attributes);if(handles!=IntPtr.Zero)Marshal.FreeHGlobal(handles);if(environment!=IntPtr.Zero)Marshal.FreeHGlobal(environment);}
  }
  public void Stop(){if(job!=null&&!job.IsClosed&&!job.IsInvalid)TerminateJobObject(job,1);}
  public void Dispose(){Stop();if(process!=null&&!process.IsInvalid)WaitForSingleObject(process,2000);foreach(var stream in new IDisposable[]{Input,Output,Error})if(stream!=null)try{stream.Dispose();}catch{}if(process!=null)process.Dispose();if(job!=null)job.Dispose();}
 }
 public static class RequestRunner {
  static bool Local(string exe){return Path.GetFileName(exe).Equals("powershell.exe",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(exe).Equals("OneOcrWorker.exe",StringComparison.OrdinalIgnoreCase);}
  static Exception Timeout(string exe){return Local(exe)?(Exception)new TimeoutException("Text extraction took too long. Please try again."):new CodexException(CodexIssue.Timeout,"Codex did not respond before the timeout. Please try again.");}
  static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);}
  public static async Task<string> Run(string exe,string arguments,string input,string cwd,CancellationToken token,int timeoutSeconds){
   token.ThrowIfCancellationRequested();var start=new ProcessStartInfo(exe,arguments){WorkingDirectory=cwd};foreach(string key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())if((key.StartsWith("CODEX_",StringComparison.OrdinalIgnoreCase)&&key!="CODEX_HOME")||key.StartsWith("OPENAI_",StringComparison.OrdinalIgnoreCase)||key=="CHATGPT_BASE_URL")start.EnvironmentVariables.Remove(key);
   using(var process=RequestProcess.Start(start))using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
    deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));using(deadline.Token.Register(process.Stop)){
     var output=process.Output.ReadToEndAsync();var error=process.Error.ReadToEndAsync();Observe(output);Observe(error);
     try{if(input!=null){var bytes=new UTF8Encoding(false).GetBytes(input);await process.Input.BaseStream.WriteAsync(bytes,0,bytes.Length);}process.Input.Close();while(!process.HasExited){deadline.Token.ThrowIfCancellationRequested();await Task.Delay(40);}token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw Timeout(exe);process.Stop();var stdout=await output;var stderr=await error;if(process.ExitCode!=0){if(Local(exe))throw new Exception(stderr.Trim());throw CodexDiagnostics.Failure(String.IsNullOrWhiteSpace(stderr)?stdout:stderr);}return arguments=="login status"?(stdout+" "+stderr).Trim():stdout.Trim();}
     catch{token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested)throw Timeout(exe);throw;}
     finally{process.Stop();}
    }
   }
  }
 }
}