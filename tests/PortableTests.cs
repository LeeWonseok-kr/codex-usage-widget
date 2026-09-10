using System;using System.IO;using System.Linq;using System.Drawing;using System.Threading;using System.Reflection;using System.Runtime.InteropServices;using System.Web.Script.Serialization;using CodexUsage;
class PortableTests {
 static int count;static void Check(bool condition,string message){if(!condition)throw new Exception(message);count++;}
 static void Wait(Func<bool> condition){for(int i=0;i<40;i++){if(condition())return;Thread.Sleep(100);}throw new Exception("Monitor update timed out");}
 static int Main(){try{
  var json=new JavaScriptSerializer();var now=DateTimeOffset.UtcNow;
  var state=new AccountState(null);state.ApplyUsage(Reader.Object(json.DeserializeObject("{\"summary\":{\"lifetimeTokens\":1000},\"dailyUsageBuckets\":[{\"startDate\":\"2026-01-02\",\"tokens\":0},{\"startDate\":\"2026-01-01\",\"tokens\":100},{\"startDate\":\"2026-01-03\",\"tokens\":null}]}")),now);
  Check(state.Current.Daily.Count==2 && state.Current.Daily[1].Tokens==0,"Daily unknowns omitted and measured zero retained");
  var view=UsageView.Build(state.Current,now);Check(view.Lifetime=="1,000" && view.BucketDate=="2026/01/03","Lifetime and bucket dates remain separate");
  var home=Path.GetFullPath(Path.Combine(".test-work",Guid.NewGuid().ToString("N")));var sessions=Path.Combine(home,"sessions");Directory.CreateDirectory(sessions);
  string id="00000000-0000-0000-0000-000000000001";string file=Path.Combine(sessions,"rollout-2026-01-01T00-00-00-"+id+".jsonl");
  File.WriteAllText(Path.Combine(home,"session_index.jsonl"),"{\"id\":\""+id+"\",\"thread_name\":\"Sample task\"}\n");
  File.WriteAllText(file,"{\"type\":\"event_msg\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":1000,\"input_tokens\":900,\"output_tokens\":100,\"cached_input_tokens\":700},\"last_token_usage\":{\"total_tokens\":10}}}}\n");
  File.WriteAllText(Path.Combine(sessions,"rollout-2026-01-01T00-00-00-"+id+"-agent.jsonl"),"");
  var recent=TaskCatalog.Recent(home);Check(recent.Length==1 && recent[0].Title=="Sample task","Discover this PC's tasks and omit agent files");
  using(var monitor=new TaskMonitor(file)){monitor.Start();Wait(()=>monitor.Snapshot.Tokens==1000);Check(monitor.Snapshot.Input==900 && monitor.Snapshot.Output==100 && monitor.Snapshot.Cached==700,"Keep token subsets distinct");File.Delete(file);Wait(()=>monitor.Snapshot.Failed);Check(monitor.Snapshot.Tokens==1000,"Preserve marked last value on read failure");}
  using(var empty=new TaskMonitor(null)){empty.Start();Check(empty.Snapshot.Tokens==null,"A new PC without sessions is unknown, not zero");}
  Check(CompactDisplay.LeftTaskbarPosition(new Rectangle(0,0,1920,1080),new Rectangle(0,0,1920,1032),new Size(220,40))==new Point(6,1036),"Retain left taskbar location");
  var screen=new Rectangle(0,0,1920,1080);var area=new Rectangle(0,0,1920,1032);
  Check(CompactDisplay.TaskbarPosition(screen,area,new Size(220,40),"center",1650)==new Point(850,1036),"Center horizontally without changing taskbar height");
  Check(CompactDisplay.TaskbarPosition(screen,area,new Size(220,40),"right",1650)==new Point(1424,1036),"Right position leaves notification area clear");
  Check(CompactDisplay.TaskbarPosition(screen,area,new Size(320,40),"right",1650)==new Point(1324,1036),"Wider live text preserves notification gap");
  Check(CompactDisplay.TaskbarPosition(screen,area,new Size(220,40),"right",null)==new Point(1414,1036),"Reserve notification space when Explorer does not expose tray");
  Check(CompactDisplay.TaskbarPosition(screen,screen,new Size(220,40),"right",null)==new Point(1694,1036),"Auto-hidden taskbar keeps display in work area");
  Check(CompactDisplay.TaskbarPosition(new Rectangle(-1920,0,1920,1080),new Rectangle(-1920,0,1920,1032),new Size(220,40),"center",null)==new Point(-1070,1036),"Respect negative monitor origin");
  var prefs=new Preferences{compactX=40,compactY=60};prefs.SetAlignment("right");Check(!prefs.compactX.HasValue && !prefs.compactY.HasValue,"Alignment clears manually dragged position");
  string settings=Path.Combine(home,"settings.json");prefs.Save(settings);Check(Preferences.Load(settings).compactAlignment=="right","Alignment persists across launch");
  prefs.SetAlignment("invalid");Check(prefs.compactAlignment=="left","Invalid alignment falls back to left");
  string link=Path.Combine(home,"startup-test.lnk"),exe=Path.Combine(home,"Folder With Spaces","CodexUsage.exe");StartupRegistration.CreateShortcut(link,exe);Check(File.Exists(link),"Create startup shortcut in isolated test directory");
  object shell=null,shortcut=null;try{var type=Type.GetTypeFromProgID("WScript.Shell",true);shell=Activator.CreateInstance(type);shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});var t=shortcut.GetType();Check(String.Equals((string)t.InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null),exe,StringComparison.OrdinalIgnoreCase),"Shortcut preserves executable path with spaces");Check((string)t.InvokeMember("Arguments",BindingFlags.GetProperty,null,shortcut,null)=="--startup","Startup opens without main popup");}finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  Console.WriteLine("PASS: "+count+" portable behavior checks");return 0;
 }catch(Exception e){Console.WriteLine("FAIL: "+e.Message);return 1;}}
}
