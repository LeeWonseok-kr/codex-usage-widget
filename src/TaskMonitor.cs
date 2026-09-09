using System;using System.IO;using System.Threading;
namespace CodexUsage {
 public sealed class TaskSnapshot {
  public readonly long? Tokens;public readonly DateTimeOffset? RecordedAt;public readonly bool Failed;
  public readonly long? Input,Output,Cached;
  public TaskSnapshot(long? tokens,DateTimeOffset? recordedAt,bool failed,long? input=null,long? output=null,long? cached=null){Tokens=tokens;RecordedAt=recordedAt;Failed=failed;Input=input;Output=output;Cached=cached;}
 }
 public sealed class TaskMonitor : IDisposable {
  readonly string path;readonly ManualResetEvent stop=new ManualResetEvent(false);readonly Thread worker;
  volatile TaskSnapshot snapshot=new TaskSnapshot(null,null,false);
  long lastLength=-1;DateTime lastWrite=DateTime.MinValue;
  public TaskSnapshot Snapshot {get{return snapshot;}}
  public TaskMonitor(string path){this.path=path;worker=new Thread(Run){IsBackground=true,Name="Codex current task tokens"};}
  public void Start(){worker.Start();}
  void Run(){try{do{Poll();}while(!stop.WaitOne(2000));}finally{stop.Dispose();}}
  void Poll(){try{
   if(String.IsNullOrEmpty(path))return;
   var file=new FileInfo(path);if(!file.Exists)throw new FileNotFoundException();
   long length=file.Length;DateTime write=file.LastWriteTimeUtc;
   if(length==lastLength && write==lastWrite && !snapshot.Failed)return;
   var usage=Reader.ReadTokens(path);
   snapshot=new TaskSnapshot(usage.Total,usage.TokenTime,false,usage.Input,usage.Output,usage.Cached);lastLength=length;lastWrite=write;
  }catch(IOException){Failed();}catch(UnauthorizedAccessException){Failed();}}
  void Failed(){var s=snapshot;snapshot=new TaskSnapshot(s.Tokens,s.RecordedAt,true,s.Input,s.Output,s.Cached);}
  public void Dispose(){if(worker.IsAlive){stop.Set();worker.Join(3000);}else if(worker.ThreadState==ThreadState.Unstarted)stop.Dispose();}
 }
}
