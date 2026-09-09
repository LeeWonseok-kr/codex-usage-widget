using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace CodexUsage {
 public sealed class DailyUsage {
  public readonly string Date;public readonly long Tokens;
  public DailyUsage(string date,long tokens){Date=date;Tokens=tokens;}
 }
 public sealed class AccountSnapshot {
  public long? LifetimeTokens,DisplayTokens;
  public bool AfterReset,AwaitingBaseline;
  public string AccountId,Error,UsageError,LastBucketDate;
  public DateTimeOffset? LimitsFetchedAt,TokensFetchedAt,ResetDetectedAt;
  public Limit Period;
  public List<Limit> Limits=new List<Limit>();
  public List<DailyUsage> Daily=new List<DailyUsage>();
 }

 // Only the monitor worker changes this state. Each publication owns fresh objects.
 internal sealed class AccountState {
  readonly string stateFile;
  readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=16777216};
  long? baseline;
  string verifiedAccount;
  internal int Generation;
  internal AccountSnapshot Current {get;private set;}
  internal AccountState(string path){stateFile=path;Current=new AccountSnapshot();Load();}
  static object Get(Dictionary<string,object> d,string k){return Reader.Get(d,k);}
  static Dictionary<string,object> Obj(object o){return Reader.Object(o);}
  static long? Number(object o){long n;return o!=null && !(o is bool) && long.TryParse(Convert.ToString(o,CultureInfo.InvariantCulture),NumberStyles.Integer,CultureInfo.InvariantCulture,out n) && n>=0 ? (long?)n : null;}
  static DateTimeOffset? Time(object o){DateTimeOffset d;return DateTimeOffset.TryParse(o as string,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out d)?(DateTimeOffset?)d:null;}
  static string Stamp(DateTimeOffset? t){return t.HasValue?t.Value.ToString("o",CultureInfo.InvariantCulture):null;}
  static Limit Copy(Limit l){return l==null?null:new Limit {Remaining=l.Remaining,Minutes=l.Minutes,Resets=l.Resets};}
  static AccountSnapshot Copy(AccountSnapshot a){
   var b=new AccountSnapshot {LifetimeTokens=a.LifetimeTokens,DisplayTokens=a.DisplayTokens,AfterReset=a.AfterReset,AwaitingBaseline=a.AwaitingBaseline,AccountId=a.AccountId,Error=a.Error,UsageError=a.UsageError,LastBucketDate=a.LastBucketDate,LimitsFetchedAt=a.LimitsFetchedAt,TokensFetchedAt=a.TokensFetchedAt,ResetDetectedAt=a.ResetDetectedAt,Period=Copy(a.Period)};
   foreach(var l in a.Limits)b.Limits.Add(Copy(l));b.Daily=new List<DailyUsage>(a.Daily);return b;
  }
  static Limit Window(object value){
   var d=Obj(value);object v=Get(d,"usedPercent");double used;long? minutes=Number(Get(d,"windowDurationMins"));
   if(v==null || v is bool || !double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out used) || double.IsNaN(used) || double.IsInfinity(used) || used<0 || used>100 || !minutes.HasValue || minutes.Value<=0 || minutes.Value>Int32.MaxValue)return null;
   var l=new Limit {Remaining=100-used,Minutes=(int)minutes.Value};long? reset=Number(Get(d,"resetsAt"));
   if(reset.HasValue)try {l.Resets=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(reset.Value);}catch(ArgumentOutOfRangeException){}
   return l;
  }
  internal bool ApplyLimits(Dictionary<string,object> result,DateTimeOffset now){
   string account=Get(result,"accountId") as string;
   if(!String.IsNullOrWhiteSpace(account) && !String.IsNullOrEmpty(Current.AccountId) && account!=Current.AccountId){Current=new AccountSnapshot {AccountId=account};baseline=null;verifiedAccount=null;Generation++;Save();}
   var map=Obj(Get(result,"rateLimitsByLimitId"));var bucket=Obj(Get(map,"codex"));
   if(bucket==null){var legacy=Obj(Get(result,"rateLimits"));string id=Get(legacy,"limitId") as string;if(id==null || id=="codex")bucket=legacy;}
   var limits=new List<Limit>();foreach(string k in new[]{"primary","secondary"}){var l=Window(Get(bucket,k));if(l!=null)limits.Add(l);}
   if(limits.Count==0){Fail("Account quota is unavailable.",false);return false;}
   var next=Copy(Current);
   if(!String.IsNullOrWhiteSpace(account))next.AccountId=account;
   Limit period=null;foreach(var l in limits)if(period==null || l.Minutes>period.Minutes)period=l;
   var previous=next.Period;
   if(previous!=null && previous.Minutes==period.Minutes && previous.Resets.HasValue && period.Resets.HasValue && previous.Resets.Value!=period.Resets.Value && period.Resets.Value>now && (now>=previous.Resets.Value || period.Remaining>previous.Remaining+0.01)){
    next.AfterReset=true;next.AwaitingBaseline=true;next.DisplayTokens=0;next.ResetDetectedAt=now;baseline=null;Generation++;
   }
   next.Limits=limits;next.Period=Copy(period);next.LimitsFetchedAt=now;next.Error=null;Current=next;Save();return true;
  }
  internal void ClearIdentity(){verifiedAccount=null;}
  internal bool HasVerifiedIdentity {get {return !String.IsNullOrWhiteSpace(verifiedAccount) && verifiedAccount==Current.AccountId;}}
  internal bool ApplyDirectLimits(Dictionary<string,object> result,DateTimeOffset now){
   verifiedAccount=null;string account=Get(result,"accountId") as string;
   if(String.IsNullOrWhiteSpace(account)){Fail("Account identity is unavailable. Keeping saved quota.",false);Fail("Account identity is unavailable. Keeping saved token total.",true);return false;}
   if(!ApplyLimits(result,now))return false;verifiedAccount=account;return true;
  }
  internal bool ApplyVerifiedUsage(Dictionary<string,object> result,DateTimeOffset now,string expectedAccount,int expectedGeneration){
   if(!HasVerifiedIdentity || expectedAccount!=verifiedAccount || expectedGeneration!=Generation){Fail("Account changed while reading usage. Waiting for a verified refresh.",true);return false;}
   return ApplyUsage(result,now);
  }
  internal bool ApplyUsage(Dictionary<string,object> result,DateTimeOffset now){
   long? total=Number(Get(Obj(Get(result,"summary")),"lifetimeTokens"));
   if(!total.HasValue){Fail("Account token total is unavailable.",true);return false;}
   var next=Copy(Current);next.LifetimeTokens=total;next.TokensFetchedAt=now;next.UsageError=null;
   if(next.AwaitingBaseline){baseline=total;next.AwaitingBaseline=false;}
   next.DisplayTokens=next.AfterReset && baseline.HasValue ? Math.Max(0,total.Value-baseline.Value) : total;
   next.LastBucketDate=null;next.Daily=new List<DailyUsage>();var days=new SortedDictionary<string,long>(StringComparer.Ordinal);var buckets=Get(result,"dailyUsageBuckets") as object[];
   if(buckets!=null)foreach(var b in buckets){var bucket=Obj(b);string date=Get(bucket,"startDate") as string;DateTime parsed;if(date!=null && DateTime.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed)){if(next.LastBucketDate==null || String.CompareOrdinal(date,next.LastBucketDate)>0)next.LastBucketDate=date;long? tokens=Number(Get(bucket,"tokens"));if(tokens.HasValue)days[date]=tokens.Value;}}
   foreach(var day in days)next.Daily.Add(new DailyUsage(day.Key,day.Value));
   Current=next;Save();return true;
  }
  internal void Fail(string error,bool usage){var next=Copy(Current);if(usage)next.UsageError=error;else next.Error=error;Current=next;}
  void Save(){
   if(String.IsNullOrEmpty(stateFile))return;
   try {
    var s=Current;var ls=new List<object>();foreach(var l in s.Limits)ls.Add(new {remaining=l.Remaining,minutes=l.Minutes,resets=Stamp(l.Resets)});
    string body=json.Serialize(new {version=1,accountId=s.AccountId,baseline=baseline,afterReset=s.AfterReset,awaitingBaseline=s.AwaitingBaseline,lifetime=s.LifetimeTokens,display=s.DisplayTokens,lastBucketDate=s.LastBucketDate,limitsFetched=Stamp(s.LimitsFetchedAt),tokensFetched=Stamp(s.TokensFetchedAt),resetDetected=Stamp(s.ResetDetectedAt),limits=ls});
    string directory=Path.GetDirectoryName(Path.GetFullPath(stateFile));Directory.CreateDirectory(directory);string temp=stateFile+".tmp";File.WriteAllText(temp,body);
    if(File.Exists(stateFile))File.Replace(temp,stateFile,null);else File.Move(temp,stateFile);
   }catch(IOException){FailPersistence();}catch(UnauthorizedAccessException){FailPersistence();}catch(ArgumentException){FailPersistence();}
  }
  void FailPersistence(){var next=Copy(Current);next.Error="Could not save the token reset baseline.";Current=next;}
  void Load(){
   if(String.IsNullOrEmpty(stateFile) || !File.Exists(stateFile))return;
   try {
    var d=Obj(json.DeserializeObject(File.ReadAllText(stateFile)));if(Number(Get(d,"version"))!=1)return;
    var s=new AccountSnapshot {AccountId=Get(d,"accountId") as string,LifetimeTokens=Number(Get(d,"lifetime")),DisplayTokens=Number(Get(d,"display")),AfterReset=Object.Equals(Get(d,"afterReset"),true),AwaitingBaseline=Object.Equals(Get(d,"awaitingBaseline"),true),LastBucketDate=Get(d,"lastBucketDate") as string,LimitsFetchedAt=Time(Get(d,"limitsFetched")),TokensFetchedAt=Time(Get(d,"tokensFetched")),ResetDetectedAt=Time(Get(d,"resetDetected")),Error="Saved quota; waiting for account refresh.",UsageError="Saved token total; waiting for account refresh."};
    baseline=Number(Get(d,"baseline"));if(s.AfterReset && !baseline.HasValue)s.AwaitingBaseline=true;
    var ls=Get(d,"limits") as object[];if(ls!=null)foreach(var raw in ls){var l=Obj(raw);double remaining;long? minutes=Number(Get(l,"minutes"));if(minutes.HasValue && minutes.Value>0 && minutes.Value<=Int32.MaxValue && Get(l,"remaining")!=null && double.TryParse(Convert.ToString(Get(l,"remaining"),CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out remaining) && remaining>=0 && remaining<=100){var q=new Limit {Minutes=(int)minutes.Value,Remaining=remaining,Resets=Time(Get(l,"resets"))};s.Limits.Add(q);if(s.Period==null || q.Minutes>s.Period.Minutes)s.Period=Copy(q);}}
    if(s.AwaitingBaseline)s.DisplayTokens=0;Current=s;
   }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
  }
 }

 public sealed class AccountMonitor : IDisposable {
  readonly string codexExe,codexDir;
  readonly AccountState state;
  readonly object gate=new object(),queueGate=new object(),processGate=new object();
  readonly AutoResetEvent wake=new AutoResetEvent(false);
  readonly Queue<string> lines=new Queue<string>();
  readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=16777216};
  AccountSnapshot snapshot;
  Thread worker;
  Process process;
  volatile bool stopped,refresh;
  bool streamEnded;
  int id;
  public AccountMonitor(string codexExe,string codexDir,string stateFile){this.codexExe=codexExe;this.codexDir=codexDir;state=new AccountState(stateFile);snapshot=state.Current;}
  public AccountSnapshot Snapshot {get {lock(gate)return snapshot;}}
  void Publish(){lock(gate)snapshot=state.Current;}
  public void Start(){lock(gate){if(stopped || worker!=null)return;worker=new Thread(Run);worker.IsBackground=true;worker.Name="Codex account usage monitor";worker.Start();}}
  public void RefreshNow(){if(stopped)return;refresh=true;wake.Set();}
  void Send(object message){if(stopped)throw new OperationCanceledException();process.StandardInput.WriteLine(json.Serialize(message));process.StandardInput.Flush();}
  void Connect(){
   lock(queueGate){lines.Clear();streamEnded=false;}
   var info=new ProcessStartInfo(codexExe,"app-server --stdio") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
   if(!String.IsNullOrEmpty(codexDir))info.EnvironmentVariables["CODEX_HOME"]=codexDir;
   var p=new Process {StartInfo=info};
   p.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){lock(queueGate){if(e.Data==null)streamEnded=true;else if(lines.Count<4096)lines.Enqueue(e.Data);else streamEnded=true;}wake.Set();};
   p.ErrorDataReceived+=delegate{};
   lock(processGate){if(stopped){p.Dispose();throw new OperationCanceledException();}process=p;if(!p.Start())throw new IOException("Could not start account service.");}
   p.BeginOutputReadLine();p.BeginErrorReadLine();
   Request("initialize",new {clientInfo=new {name="codex_usage_widget",title="Codex Usage Widget",version="2.0"},capabilities=new {experimentalApi=true}});
   Send(new {method="initialized",@params=new {}});
  }
  Dictionary<string,object> Parse(string line){try{return Reader.Object(json.DeserializeObject(line));}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}}
  string Take(){lock(queueGate){if(lines.Count>0)return lines.Dequeue();if(streamEnded)throw new IOException("Account service ended.");return null;}}
  void Notification(Dictionary<string,object> message){
   if((Reader.Get(message,"method") as string)!="account/rateLimits/updated")return;
   var value=Reader.Object(Reader.Get(message,"params"));
   // A notification may carry only one bucket. The parser excludes Spark.
   var bucket=Reader.Object(Reader.Get(value,"rateLimits"));string bucketId=Reader.Get(bucket,"limitId") as string;
   var map=Reader.Object(Reader.Get(value,"rateLimitsByLimitId"));
   if(value!=null && (Reader.Get(map,"codex")!=null || bucketId==null || bucketId=="codex")){state.ApplyLimits(value,DateTimeOffset.UtcNow);Publish();}
  }
  Dictionary<string,object> Request(string method,object parameters){
   int requestId=++id;if(parameters==null)Send(new {id=requestId,method=method});else Send(new {id=requestId,method=method,@params=parameters});DateTime until=DateTime.UtcNow.AddSeconds(25);
   while(!stopped && DateTime.UtcNow<until){
    string line=Take();if(line==null){wake.WaitOne(250);continue;}
    var message=Parse(line);if(message==null)continue;object responseId=Reader.Get(message,"id");
    if(responseId!=null && Convert.ToString(responseId,CultureInfo.InvariantCulture)==requestId.ToString(CultureInfo.InvariantCulture)){
     if(Reader.Get(message,"error")!=null)throw new AccountRequestException();
     var result=Reader.Object(Reader.Get(message,"result"));if(result==null)throw new AccountRequestException();return result;
    }
    Notification(message);
   }
   if(stopped)throw new OperationCanceledException();throw new IOException("Account service response timed out.");
  }
  void Drain(){string line;while((line=Take())!=null){var d=Parse(line);if(d!=null)Notification(d);}}
  void Run(){
   int retry=2;
   while(!stopped){
    try {
     state.ClearIdentity();Connect();DateTime nextLimits=DateTime.MinValue,nextUsage=DateTime.MinValue;int lastUsageGeneration=-1;
     while(!stopped){
      Drain();bool forced=refresh;refresh=false;DateTime now=DateTime.UtcNow;
      if(forced || now>=nextLimits){
       try {if(state.ApplyDirectLimits(Request("account/rateLimits/read",null),DateTimeOffset.UtcNow))retry=2;}
       catch(AccountRequestException){state.ClearIdentity();state.Fail("Account quota request failed. Retrying shortly.",false);}
       nextLimits=DateTime.UtcNow.AddSeconds(10);Publish();
      }
      if(state.HasVerifiedIdentity && (forced || now>=nextUsage || lastUsageGeneration!=state.Generation)){
       int generation=state.Generation;string account=state.Current.AccountId;
       try {var result=Request("account/usage/read",null);state.ApplyVerifiedUsage(result,DateTimeOffset.UtcNow,account,generation);}
       catch(AccountRequestException){state.Fail("Account token request failed. Retrying shortly.",true);}
       lastUsageGeneration=generation;nextUsage=DateTime.UtcNow.AddSeconds(60);Publish();
      }
      // Sleep until work is due; output, refresh and disposal signal the event.
      // An unavailable identity must not make an overdue usage poll spin.
      lock(queueGate){if(lines.Count>0)continue;}
      DateTime due=nextLimits;
      if(state.HasVerifiedIdentity){if(lastUsageGeneration!=state.Generation)due=DateTime.UtcNow;else if(nextUsage<due)due=nextUsage;}
      int waitMilliseconds=(int)Math.Max(1,Math.Min(10000,Math.Ceiling((due-DateTime.UtcNow).TotalMilliseconds)));
      wake.WaitOne(waitMilliseconds);
     }
    }catch(OperationCanceledException){}catch(Exception){if(!stopped){state.Fail("Account connection unavailable. Retrying automatically.",false);state.Fail("Token total may be out of date.",true);Publish();}}
    finally{StopProcess();}
    if(!stopped){wake.WaitOne(retry*1000);retry=Math.Min(60,retry*2);}
   }
  }
  void StopProcess(){
   lock(processGate){var p=process;process=null;if(p==null)return;try{p.StandardInput.Close();}catch{}try{if(!p.WaitForExit(500))p.Kill();p.WaitForExit(1000);}catch{}p.Dispose();}
  }
  public void Dispose(){
   if(stopped)return;stopped=true;wake.Set();StopProcess();Thread t;lock(gate)t=worker;if(t!=null && t!=Thread.CurrentThread)t.Join(2500);
   // Event callbacks can race shutdown; leave the tiny wait handle for finalization.
  }
  sealed class AccountRequestException:Exception {}
 }
}
