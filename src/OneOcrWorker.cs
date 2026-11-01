// Independent minimal interop with Snipping Tool's undocumented OneOCR exports.
// ABI references: https://github.com/b1tg/win11-oneocr and
// https://github.com/ksasao/SnippingToolOcrSharp . No Microsoft binaries/models bundled.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

internal static class OneOcrWorker {
    [StructLayout(LayoutKind.Sequential)] struct Pixels { public int Format,Width,Height,Reserved; public long Stride; public IntPtr Data; }
    [DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module,string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Create(out long handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Delay(long handle,byte value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Pipeline(IntPtr model,IntPtr key,long init,out long handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Run(long pipeline,ref Pixels pixels,long options,out long result);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Limit(long options,long maximum);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Count(long result,out long count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Line(long result,long index,out long line);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate long Content(long line,out IntPtr text);
    static T Export<T>(IntPtr module,string name) where T:class {var pointer=GetProcAddress(module,name);if(pointer==IntPtr.Zero)throw new InvalidOperationException("Incompatible Snipping Tool OCR export: "+name);return Marshal.GetDelegateForFunctionPointer(pointer,typeof(T)) as T;}
    static void Check(long result){if(result!=0)throw new InvalidOperationException("Snipping Tool OCR returned code "+result+".");}
    static string Discover(){
        // Query the current user's registered package, never enumerate WindowsApps or change ACLs.
        string script="$ErrorActionPreference='Stop'; [Console]::OutputEncoding=New-Object Text.UTF8Encoding($false); $p=Get-AppxPackage -Name Microsoft.ScreenSketch | Where-Object { $_.Architecture -eq 'X64' -and $_.PublisherId -eq '8wekyb3d8bbwe' } | Sort-Object Version -Descending | Select-Object -First 1; if($p){[Console]::Write($p.InstallLocation)}";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        string root;
        using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(12000)){process.Kill();throw new InvalidOperationException("Snipping Tool discovery timed out.");}if(process.ExitCode!=0)throw new InvalidOperationException("Snipping Tool package could not be queried.");root=output.GetAwaiter().GetResult().Trim();error.GetAwaiter().GetResult();}
        if(String.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))throw new InvalidOperationException("Install or update the x64 Windows 11 Snipping Tool from Microsoft Store.");
        foreach(string folder in new[]{Path.Combine(root,"SnippingTool"),root})if(File.Exists(Path.Combine(folder,"oneocr.dll"))&&File.Exists(Path.Combine(folder,"oneocr.onemodel"))&&File.Exists(Path.Combine(folder,"onnxruntime.dll")))return folder;
        throw new InvalidOperationException("This Snipping Tool installation does not contain OneOCR.");
    }
    static string Recognize(string folder,string image){
        // DLL resolution is restricted to the installed package directory and System32.
        if(LoadLibraryEx(Path.Combine(folder,"onnxruntime.dll"),IntPtr.Zero,0x1100)==IntPtr.Zero)throw new InvalidOperationException("Snipping Tool OCR dependencies could not be loaded.");
        var module=LoadLibraryEx(Path.Combine(folder,"oneocr.dll"),IntPtr.Zero,0x1100);if(module==IntPtr.Zero)throw new InvalidOperationException("Snipping Tool OCR could not be loaded.");
        var create=Export<Create>(module,"CreateOcrInitOptions");var delay=Export<Delay>(module,"OcrInitOptionsSetUseModelDelayLoad");var pipeline=Export<Pipeline>(module,"CreateOcrPipeline");var options=Export<Create>(module,"CreateOcrProcessOptions");var run=Export<Run>(module,"RunOcrPipeline");var count=Export<Count>(module,"GetOcrLineCount");var line=Export<Line>(module,"GetOcrLine");var content=Export<Content>(module,"GetOcrLineContent");
        long init,engine,settings,result,total;Check(create(out init));Check(delay(init,0));
        // Model-loader parameter from the public ABI references; not an account/API credential.
        string format="kj)TGtrK>f]b[Piow.gU+nC@s"+new String('"',6)+"4";
        var model=Marshal.StringToHGlobalAnsi(Path.Combine(folder,"oneocr.onemodel"));var key=Marshal.StringToHGlobalAnsi(format);
        try{Check(pipeline(model,key,init,out engine));}finally{Marshal.FreeHGlobal(model);Marshal.FreeHGlobal(key);}
        Check(options(out settings));Check(Export<Limit>(module,"OcrProcessOptionsSetMaxRecognitionLineCount")(settings,1000));
        using(var original=Image.FromFile(image)){
            if(original.Width>16000||original.Height>16000||(long)original.Width*original.Height>64000000)throw new InvalidOperationException("The image is too large. Select a smaller region.");
            using(var bitmap=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppArgb)){
                using(var graphics=Graphics.FromImage(bitmap)){graphics.Clear(Color.White);graphics.DrawImageUnscaled(original,0,0);}
                var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
                try{var pixels=new Pixels{Format=3,Width=bitmap.Width,Height=bitmap.Height,Stride=data.Stride,Data=data.Scan0};Check(run(engine,ref pixels,settings,out result));Check(count(result,out total));if(total<0||total>20000)throw new InvalidOperationException("Invalid OCR result.");var text=new StringBuilder();for(long index=0;index<total;index++){long item;IntPtr value;Check(line(result,index,out item));Check(content(item,out value));if(value==IntPtr.Zero)continue;int size=0;while(size<1048576&&Marshal.ReadByte(value,size)!=0)size++;if(size==1048576)throw new InvalidOperationException("Invalid OCR line.");var bytes=new byte[size];Marshal.Copy(value,bytes,0,size);if(text.Length>0)text.Append("\r\n");text.Append(Encoding.UTF8.GetString(bytes));}return text.ToString();}finally{bitmap.UnlockBits(data);}
            }
        }
        // One request per process: OS teardown releases every native allocation, even on ABI faults.
    }
    static int Main(string[] args){SetErrorMode(0x8003);Console.OutputEncoding=new UTF8Encoding(false);var json=new JavaScriptSerializer{MaxJsonLength=16000000};try{if(args.Length==1&&args[0]=="--probe"){var folder=Discover();Console.Write(json.Serialize(new {Available=true,Version=FileVersionInfo.GetVersionInfo(Path.Combine(folder,"oneocr.dll")).FileVersion}));return 0;}if(args.Length!=2||args[0]!="--image")throw new ArgumentException("Use --probe or --image <path>.");var location=Discover();var text=Recognize(location,Path.GetFullPath(args[1]));Console.Write(json.Serialize(new {Available=true,Text=text}));return 0;}catch(Exception error){Console.Write(json.Serialize(new {Available=false,Error=error.Message}));return 0;}}
}