using System;using System.IO;using System.Linq;using System.Drawing;using System.Threading;using System.Reflection;using System.Runtime.InteropServices;using System.Web.Script.Serialization;using CodexUsage;
class PortableTests {
 // Simulate control geometry and font metrics; this does not change the OS DPI.
 static void ScaleFonts(System.Windows.Forms.Control parent,float factor){foreach(System.Windows.Forms.Control c in parent.Controls){c.Font=new Font(c.Font.FontFamily,c.Font.SizeInPoints*96f/72f*factor,c.Font.Style,GraphicsUnit.Pixel);ScaleFonts(c,factor);}}
 static void CheckPanel(System.Windows.Forms.Panel panel,float factor){panel.Scale(new SizeF(factor,factor));ScaleFonts(panel,factor);foreach(System.Windows.Forms.Control c in panel.Controls){Check(panel.ClientRectangle.Contains(c.Bounds),"Scaled child stays inside panel at "+factor);if(c is System.Windows.Forms.Label)Check(c.Height>=c.Font.Height,"Scaled label fits its font at "+factor+": "+c.Text);}}
 static int count;static void Check(bool condition,string message){if(!condition)throw new Exception(message);count++;}
 static void Wait(Func<bool> condition){for(int i=0;i<80;i++){if(condition())return;Thread.Sleep(100);}throw new Exception("Monitor update timed out");}
 [STAThread] static int Main(){try{System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.ThrowException);
  var fontPrefs=new JavaScriptSerializer().Deserialize<Preferences>("{\"compactTextSize\":2}");Check(new JavaScriptSerializer().Serialize(fontPrefs).Contains("\"compactTextSize\":2"),"Remember selected overlay text size");
  var json=new JavaScriptSerializer();var now=DateTimeOffset.UtcNow;
  var state=new AccountState(null);state.ApplyUsage(Reader.Object(json.DeserializeObject("{\"summary\":{\"lifetimeTokens\":1000},\"dailyUsageBuckets\":[{\"startDate\":\"2026-01-02\",\"tokens\":0},{\"startDate\":\"2026-01-01\",\"tokens\":100},{\"startDate\":\"2026-01-03\",\"tokens\":null}]}")),now);
  Check(state.Current.Daily.Count==2 && state.Current.Daily[1].Tokens==0,"Daily unknowns omitted and measured zero retained");
  var view=UsageView.Build(state.Current,now);Check(view.Lifetime=="1,000" && view.BucketDate=="2026/01/03","Lifetime and bucket dates remain separate");
  Check(CompactDisplay.NormalizeTextSize(-1)==0 && CompactDisplay.NormalizeTextSize(99)==0,"Invalid text size falls back safely");
  var sizePrefs=new Preferences();using(var overlay=new CompactDisplay(sizePrefs,delegate{},delegate{})){int previousWidth=0;for(int level=0;level<3;level++){sizePrefs.compactTextSize=level;overlay.ApplyTextSize();overlay.UpdateValues(state.Current);Check(overlay.Width>previousWidth,"Larger fonts expand text width");previousWidth=overlay.Width;foreach(System.Windows.Forms.Control control in overlay.Controls){Check(control.Height>=control.Font.Height && control.Bottom<=overlay.ClientSize.Height,"Both lines fit within overlay");}Console.WriteLine("Text level="+level+" height="+overlay.Height+" font="+overlay.Controls[0].Font.Height+" taskbar="+(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Bottom-System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Bottom));Check(overlay.Height<=48,"All text sizes fit a standard taskbar height");}sizePrefs.compactTextSize=0;overlay.ApplyTextSize();Check(overlay.Width<previousWidth,"Returning to small shrinks overlay immediately");}
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
  prefs.taskbarDisplay=false;prefs.Save(settings);Check(Preferences.LoadForLaunch(settings,true).taskbarDisplay,"Startup must show overlay even when previously hidden");Check(!Preferences.LoadForLaunch(settings,false).taskbarDisplay,"Manual launch preserves hidden preference");prefs.taskbarDisplay=true;prefs.Save(settings);Check(Preferences.LoadForLaunch(settings,true).taskbarDisplay,"Startup retains enabled overlay");
  prefs.SetAlignment("invalid");Check(prefs.compactAlignment=="left","Invalid alignment falls back to left");
  Check(CompactDisplay.FitToWorkArea(screen,new Point(100,1036),new Size(220,40))==new Point(100,1036),"Taskbar drag retains taskbar coordinates");
  string link=Path.Combine(home,"startup-test.lnk"),exe=Path.Combine(home,"Folder With Spaces","CodexUsage.exe");StartupRegistration.CreateShortcut(link,exe);Check(File.Exists(link),"Create startup shortcut in isolated test directory");
  object shell=null,shortcut=null;try{var type=Type.GetTypeFromProgID("WScript.Shell",true);shell=Activator.CreateInstance(type);shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{link});var t=shortcut.GetType();Check(String.Equals((string)t.InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null),exe,StringComparison.OrdinalIgnoreCase),"Shortcut preserves executable path with spaces");Check((string)t.InvokeMember("Arguments",BindingFlags.GetProperty,null,shortcut,null)=="--startup","Startup opens without main popup");}finally{if(shortcut!=null)Marshal.FinalReleaseComObject(shortcut);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
  string chatsHome=Path.Combine(home,"chat-list"),chatRoot=Path.Combine(chatsHome,"sessions"),archive=Path.Combine(chatsHome,"archived_sessions");Directory.CreateDirectory(chatRoot);Directory.CreateDirectory(archive);
  Func<long,string> tokenLine=n=>"{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":"+n+"}}}}\n";
  string firstChat=null;for(int i=0;i<16;i++){string chatId="00000000-0000-0000-0000-"+(i+1).ToString("D12");string chatFile=Path.Combine(chatRoot,"rollout-2026-01-01T00-00-00-"+chatId+".jsonl");File.WriteAllText(chatFile,tokenLine((i+1)*10));File.AppendAllText(Path.Combine(chatsHome,"session_index.jsonl"),"{\"id\":\""+chatId+"\",\"thread_name\":\"Chat "+i+"\"}\n");if(i==0)firstChat=chatFile;}
  File.Copy(firstChat,Path.Combine(archive,Path.GetFileName(firstChat)));
  File.WriteAllText(Path.Combine(archive,"rollout-2026-01-01T00-00-00-00000000-0000-0000-0000-000000000017.jsonl"),tokenLine(170));
  File.WriteAllText(Path.Combine(chatRoot,"rollout-2026-01-01T00-00-00-00000000-0000-0000-0000-000000000018.jsonl"),"{\"type\":\"session_meta\",\"payload\":{\"source\":{\"subagent\":{\"thread_spawn\":{}}}}}\n"+tokenLine(9999));
  File.WriteAllText(Path.Combine(chatRoot,"rollout-2026-01-01T00-00-00-00000000-0000-0000-0000-000000000019.jsonl"),"");
  Check(TaskCatalog.All(chatsHome).Length==18,"Include archived chats and more than fifteen entries, deduplicate and omit agents");
  using(var chats=new ChatUsageMonitor(chatsHome)){chats.Start();Wait(()=>!chats.Snapshot.Loading);Check(chats.Snapshot.Rows.Length==18,"Publish entire local chat list");Check(chats.Snapshot.Rows.Single(row=>row.Path==firstChat).Tokens==10,"Read each chat cumulative total once");Check(chats.Snapshot.Rows.Count(row=>!row.Tokens.HasValue)==1,"Unknown chat usage is not zero");File.AppendAllText(firstChat,tokenLine(900));Wait(()=>chats.Snapshot.Rows.Single(row=>row.Path==firstChat).Tokens==900);Check(chats.Snapshot.Rows.Single(row=>row.Path==firstChat).Title=="Chat 0","Changed chat refreshes without losing title");}
  foreach(float factor in new[]{1f,1.25f,1.5f,2f}){
   using(var daily=new DetailsPanel(true)){daily.Render(state.Current,null);CheckPanel(daily,factor);}
   using(var panel=new ChatUsagePanel()){
    CheckPanel(panel,factor);var rows=Enumerable.Range(0,25).Select(i=>new ChatUsageRow{Path=i.ToString(),Title="A long chat title that exceeds the available title column",Tokens=9999999999}).ToArray();
    panel.Render(state.Current,new ChatUsageSnapshot(rows));var list=panel.Controls.OfType<System.Windows.Forms.ListView>().Single();var handle=list.Handle;
    panel.Render(state.Current,new ChatUsageSnapshot(rows));int width=list.Columns.Cast<System.Windows.Forms.ColumnHeader>().Sum(c=>c.Width);
    Check(width<=list.ClientSize.Width && width>=list.ClientSize.Width-3,"Chat columns fill available width without overflow at "+factor);
    Check(list.Columns[1].Width>=System.Windows.Forms.TextRenderer.MeasureText("9,999,999,999 *",list.Font).Width,"Token column fits large totals at "+factor);
    Check(list.Items.Count==25,"All chat rows remain accessible at "+factor);
    list.Width-=37;Check(list.Columns.Cast<System.Windows.Forms.ColumnHeader>().Sum(c=>c.Width)<=list.ClientSize.Width,"Column widths follow client resize at "+factor);
   }
   foreach(string alignment in new[]{"left","center","right"}){var bounds=new Rectangle(-1280,0,1280,720);var work=new Rectangle(-1280,0,1280,720-(int)(48*factor));var size=new Size(340,48);var pos=CompactDisplay.TaskbarPosition(bounds,work,size,alignment,null);Check(bounds.Contains(new Rectangle(pos,size)),"Overlay fits smaller screen with negative origin at "+factor+" "+alignment);}
  }
  Console.WriteLine("PASS: "+count+" portable behavior checks");return 0;
 }catch(Exception e){Console.WriteLine("FAIL: "+e);return 1;}}
}
