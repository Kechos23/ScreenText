using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace ScreenText {
    public class LocalOcrResult { public string Text,Engine,Warning; }
    public class OneOcrReply { public bool Available; public string Text,Version,Error; }
    public static class OneOcrService {
        public const string Automatic="oneocr-auto";
        public static string WorkerPath { get { return Path.Combine(Backend.Base,"OneOcrWorker.exe"); } }
        public static async Task<OneOcrReply> Probe(CancellationToken token){
            if(!File.Exists(WorkerPath))return new OneOcrReply{Error="OneOCR helper is missing."};
            try{return Parse(await Backend.Run(WorkerPath,"--probe",null,Backend.Base,token,20));}
            catch(OperationCanceledException){throw;}catch{return new OneOcrReply{Error="Snipping Tool OCR could not be checked."};}
        }
        static OneOcrReply Parse(string json){var reply=new JavaScriptSerializer{MaxJsonLength=16000000}.Deserialize<OneOcrReply>(json);if(reply==null)throw new InvalidDataException("Incomplete OneOCR response.");return reply;}
        public static async Task<LocalOcrResult> Read(string image,string language,string fallback,CancellationToken token){
            if(language!=Automatic)return new LocalOcrResult{Text=await Backend.Ocr(image,language,token),Engine="Windows OCR",Warning=""};
            string reason;
            try{
                if(!File.Exists(WorkerPath))throw new FileNotFoundException("OneOCR helper is missing.");
                var reply=Parse(await Backend.Run(WorkerPath,"--image "+Backend.Quote(image),null,Backend.Base,token,45));
                if(!reply.Available)throw new InvalidOperationException(reply.Error??"OneOCR is unavailable.");
                if(reply.Text==null)throw new InvalidDataException("Incomplete OneOCR response.");
                return new LocalOcrResult{Text=reply.Text,Engine="OneOCR",Warning=""};
            }catch(OperationCanceledException){throw;}catch(Exception ex){reason=ex.Message;}
            token.ThrowIfCancellationRequested();
            if(String.IsNullOrWhiteSpace(fallback)||fallback==Automatic)fallback="system";
            try{return new LocalOcrResult{Text=await Backend.Ocr(image,fallback,token),Engine="Windows OCR",Warning="OneOCR unavailable; used Windows OCR."};}
            catch(OperationCanceledException){throw;}catch(Exception ex){throw new InvalidOperationException("OneOCR: "+reason+"\n\nWindows OCR fallback: "+ex.Message+"\nChoose an installed Windows OCR language or Codex. Your image and previous text were kept.");}
        }
    }
}