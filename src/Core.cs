using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;
namespace CodexUsage {
 public class Limit {
  public double Remaining;
  public int Minutes;
  public DateTimeOffset? Resets;
  public string Caption { get { return Minutes==10080 ? "주간" : Minutes==300 ? "5시간" : Minutes%1440==0 ? (Minutes/1440)+"일" : Minutes%60==0 ? (Minutes/60)+"시간" : Minutes+"분"; } }
 }
 public class Usage {
  public long? Total, Input, Cached, Output, Reasoning;
  public DateTimeOffset? TokenTime, LimitTime;
  public double? Remaining;
  public int? WindowMinutes;
  public List<Limit> Limits=new List<Limit>();
 }
 public class Reader {
  public static Dictionary<string,object> Object(object v) { return v as Dictionary<string,object>; }
  public static object Get(Dictionary<string,object> d,string key) { object v; return d!=null && d.TryGetValue(key,out v) ? v : null; }
  static long? Number(Dictionary<string,object> d,string key) { object v=Get(d,key); if(v==null) return null; long n; return long.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),out n) && n>=0 ? (long?)n : null; }
  static DateTimeOffset? Time(object value) { DateTimeOffset t; return DateTimeOffset.TryParse(value as string,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out t) ? (DateTimeOffset?)t : null; }
  static Limit Window(Dictionary<string,object> d) {
   if(d==null || Get(d,"used_percent")==null || Number(d,"window_minutes")==null) return null;
   double used; if(!double.TryParse(Convert.ToString(Get(d,"used_percent"),CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out used) || double.IsNaN(used) || double.IsInfinity(used)) return null;
   var l=new Limit {Remaining=Math.Max(0,Math.Min(100,100-used)),Minutes=(int)Number(d,"window_minutes").Value};
   long? reset=Number(d,"resets_at"); if(reset.HasValue) { try { l.Resets=new DateTimeOffset(1970,1,1,0,0,0,TimeSpan.Zero).AddSeconds(reset.Value); } catch(ArgumentOutOfRangeException) {} }
   return l;
  }
  public static Usage Read(string path) {return Read(path,true);}
  public static Usage ReadTokens(string path) {return Read(path,false);}
  static Usage Read(string path,bool requireLimits) {
   using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
    long size=f.Length; long wanted=Math.Min(size,262144);
    while(true) {
     long start=Math.Max(0,size-wanted); f.Position=start;
     byte[] b=new byte[(int)(size-start)]; int n=0,got;
     while(n<b.Length && (got=f.Read(b,n,b.Length-n))>0) n+=got;
     string tail=Encoding.UTF8.GetString(b,0,n);
     if(start>0) {int split=tail.IndexOf('\n'); tail=split<0 ? "" : tail.Substring(split+1);}
     Usage u=Parse(tail);
     if((u.Total.HasValue && (!requireLimits || u.LimitTime.HasValue)) || start==0 || wanted>=67108864) return u;
     wanted=Math.Min(size,Math.Min(67108864,wanted*2));
    }
   }
  }
  public static Usage Parse(string lines) {
   var u=new Usage(); var json=new JavaScriptSerializer {MaxJsonLength=67108864};
   string[] a=lines.Split('\n'); bool haveLimits=false;
   for(int i=a.Length-1;i>=0;i--) {
    string line=a[i]; if(!line.Contains("token_count")) continue;
    try {
     var e=Object(json.DeserializeObject(line)); if((Get(e,"type") as string)!="event_msg") continue;
     var p=Object(Get(e,"payload")); if((Get(p,"type") as string)!="token_count") continue;
     if(!u.Total.HasValue) {
      var total=Object(Get(Object(Get(p,"info")),"total_token_usage"));
      if(Number(total,"total_tokens").HasValue) {
       u.Total=Number(total,"total_tokens"); u.Input=Number(total,"input_tokens"); u.Cached=Number(total,"cached_input_tokens");
       u.Output=Number(total,"output_tokens"); u.Reasoning=Number(total,"reasoning_output_tokens"); u.TokenTime=Time(Get(e,"timestamp"));
      }
     }
     var limits=Object(Get(p,"rate_limits")); string id=Get(limits,"limit_id") as string;
     if(!haveLimits && limits!=null && (id==null || id=="codex")) {
      foreach(string k in new[]{"primary","secondary"}) {Limit l=Window(Object(Get(limits,k))); if(l!=null) u.Limits.Add(l);}
      haveLimits=true; u.LimitTime=Time(Get(e,"timestamp"));
      foreach(var l in u.Limits) if(!u.Remaining.HasValue || l.Remaining<u.Remaining) {u.Remaining=l.Remaining;u.WindowMinutes=l.Minutes;}
     }
     if(u.Total.HasValue && haveLimits) break;
    } catch(ArgumentException) {} catch(InvalidOperationException) {} catch(FormatException) {}
   }
   return u;
  }
 }
}
