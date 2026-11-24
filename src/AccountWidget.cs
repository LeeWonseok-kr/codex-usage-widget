using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace CodexUsage {
 public sealed class Preferences {
  public string codexHome, codexExe,selectedTaskFile;
  public bool taskbarDisplay=true;
  public int compactTextSize=0;
  public int? compactX,compactY;
  public string compactAlignment="left";
  public void SetAlignment(string value){compactAlignment=NormalizeAlignment(value);compactX=null;compactY=null;}
  public static string NormalizeAlignment(string value){return value=="center" || value=="right" ? value : "left";}
  public static Preferences Load(string path) {try{return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(path))??new Preferences();}catch{return new Preferences();}}
  public static Preferences LoadForLaunch(string path,bool startup){var prefs=Load(path);if(startup)prefs.taskbarDisplay=true;return prefs;}
  public void Save(string path) {try{File.WriteAllText(path,new JavaScriptSerializer().Serialize(this));}catch(IOException){}catch(UnauthorizedAccessException){}}
 }
 public sealed class CompactDisplay : Form {
  readonly Label first=new Label(),second=new Label();
  readonly Preferences prefs; readonly Action changed,open;
  bool dragging,pressed; Point startMouse,startWindow;
  Palette palette;DateTime nextAlignment=DateTime.MinValue;
  readonly ToolTip hint=new ToolTip();
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
  [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
  protected override bool ShowWithoutActivation {get{return true;}}
  public CompactDisplay(Preferences p,Action save,Action showMain) {
   prefs=p;changed=save;open=showMain;Text="Codex 작은 상태바";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
   AutoScaleMode=AutoScaleMode.None;Size=new Size(220,40);
   first.SetBounds(5,1,210,19);first.Font=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel);first.TextAlign=ContentAlignment.MiddleLeft;
   second.SetBounds(5,20,210,19);second.Font=new Font("Segoe UI",11,FontStyle.Regular,GraphicsUnit.Pixel);second.TextAlign=ContentAlignment.MiddleLeft;
   Controls.Add(first);Controls.Add(second);ApplyTextSize();
   foreach(Control c in new Control[]{this,first,second})hint.SetToolTip(c,"총 토큰: 계정 전체 기간 누적 · 날짜: 최근 서버 집계일\n누르면 상세 보기 · 드래그로 이동");
   ApplyTheme();HandleCreated+=delegate{ApplyTheme();};FormClosed+=delegate{hint.Dispose();};
   foreach(Control c in new Control[]{this,first,second}) {
    c.Cursor=Cursors.SizeAll;
    c.MouseCaptureChanged+=delegate(object sender,EventArgs e){if(!((Control)sender).Capture){pressed=false;dragging=false;}};
    c.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){pressed=true;dragging=false;startMouse=Cursor.Position;startWindow=Location;((Control)sender).Capture=true;}};
    c.MouseMove+=delegate(object sender,MouseEventArgs e){if(!pressed || e.Button!=MouseButtons.Left)return;Point at=Cursor.Position;int dx=at.X-startMouse.X,dy=at.Y-startMouse.Y;if(Math.Abs(dx)+Math.Abs(dy)>5)dragging=true;if(dragging)Location=new Point(startWindow.X+dx,startWindow.Y+dy);};
    c.MouseUp+=delegate(object sender,MouseEventArgs e){if(e.Button!=MouseButtons.Left || !pressed)return;bool moved=dragging;pressed=false;dragging=false;((Control)sender).Capture=false;if(moved){Location=FitToWorkArea(Screen.FromPoint(Location).Bounds,Location,Size);prefs.compactX=Left;prefs.compactY=Top;changed();}else open();};
   }
   var menu=new ContextMenuStrip();menu.Items.Add("사용량 창 열기",null,delegate{open();});menu.Items.Add("위치 자동 맞춤",null,delegate{ResetPosition();});ContextMenuStrip=menu;first.ContextMenuStrip=menu;second.ContextMenuStrip=menu;
  }
  public void ResetPosition(){prefs.compactX=null;prefs.compactY=null;changed();AlignToTaskbar();}
  public void SetAlignment(string value){prefs.SetAlignment(value);changed();AlignToTaskbar();}
  public static int NormalizeTextSize(int value){return value>=0 && value<=2 ? value : 0;}
  public void ApplyTextSize(){
   int step=NormalizeTextSize(prefs.compactTextSize);
   if(first.Font.Size!=12+step*2){Font old=first.Font;first.Font=new Font("Segoe UI",12+step*2,FontStyle.Regular,GraphicsUnit.Pixel);old.Dispose();}
   if(second.Font.Size!=11+step*2){Font old=second.Font;second.Font=new Font("Segoe UI",11+step*2,FontStyle.Regular,GraphicsUnit.Pixel);old.Dispose();}
   ResizeText();if(IsHandleCreated)AlignToTaskbar();
  }
  void ResizeText(){
   int lineHeight=Math.Max(19,Math.Max(first.Font.Height,second.Font.Height)+1);
   int textWidth=Math.Max(1,Math.Max(TextRenderer.MeasureText(first.Text,first.Font).Width,TextRenderer.MeasureText(second.Text,second.Font).Width));
   Size=new Size(textWidth+10,lineHeight*2+2);first.SetBounds(5,1,textWidth,lineHeight);second.SetBounds(5,lineHeight+1,textWidth,lineHeight);nextAlignment=DateTime.MinValue;
  }
  public void ApplyTheme(){palette=Palette.Read(true);BackColor=palette.Surface;ForeColor=palette.Text;first.ForeColor=palette.Text;second.ForeColor=palette.Muted;palette.Frame(this,true);}
  public static Point FitToWorkArea(Rectangle area,Point location,Size size){return new Point(Math.Max(area.Left,Math.Min(location.X,area.Right-size.Width)),Math.Max(area.Top,Math.Min(location.Y,area.Bottom-size.Height)));}
  public static Point LeftTaskbarPosition(Rectangle screen,Rectangle area,Size size){return TaskbarPosition(screen,area,size,"left",null);}
  public static Point TaskbarPosition(Rectangle screen,Rectangle area,Size size,string alignment,int? trayLeft){
   int barHeight=screen.Bottom-area.Bottom;bool bottom=barHeight>=size.Height && barHeight<=128;
   Rectangle bounds=bottom ? screen : area;int x=bounds.Left+6;
   if(alignment=="center")x=bounds.Left+(bounds.Width-size.Width)/2;
   if(alignment=="right")x=(bottom ? (trayLeft.HasValue ? trayLeft.Value : bounds.Right-280) : bounds.Right)-size.Width-6;
   x=Math.Max(bounds.Left,Math.Min(x,Math.Max(bounds.Left,bounds.Right-size.Width)));
   return new Point(x,bottom ? area.Bottom+(barHeight-size.Height)/2 : Math.Max(area.Top,area.Bottom-size.Height-4));
  }
  public void AlignToTaskbar() {
   if(dragging)return;
   if(prefs.compactX.HasValue && prefs.compactY.HasValue) {
    var saved=new Rectangle(prefs.compactX.Value,prefs.compactY.Value,Width,Height);
    var screen=Screen.AllScreens.FirstOrDefault(s=>s.Bounds.IntersectsWith(saved));
    if(screen!=null){Location=FitToWorkArea(screen.Bounds,saved.Location,Size);return;}
   }
   var primary=Screen.PrimaryScreen;int? trayLeft=null;NativeRect rect;
   var trayWindow=FindWindowEx(FindWindow("Shell_TrayWnd",null),IntPtr.Zero,"TrayNotifyWnd",null);
   if(trayWindow!=IntPtr.Zero && GetWindowRect(trayWindow,out rect) && rect.Left>primary.Bounds.Left && rect.Left<primary.Bounds.Right && rect.Bottom>primary.WorkingArea.Bottom)trayLeft=rect.Left;
   Location=TaskbarPosition(primary.Bounds,primary.WorkingArea,Size,prefs.compactAlignment,trayLeft);
  }
  public void UpdateValues(AccountSnapshot s) {
   var v=UsageView.Build(s,DateTimeOffset.Now);
   string line1="총 "+v.CompactLifetime+" 토큰"+(v.TokensStale ? "*" : "")+" · "+v.CompactBucketDate;
   string line2="잔여 "+v.Remaining+(v.Stale ? "*" : "")+" · 초기화 "+v.Reset;
   if(first.Text!=line1 || second.Text!=line2){first.Text=line1;second.Text=line2;ResizeText();}
   first.ForeColor=palette.Text;second.ForeColor=v.Stale ? palette.Muted : palette.Text;
   if(DateTime.UtcNow>=nextAlignment){AlignToTaskbar();nextAlignment=DateTime.UtcNow.AddSeconds(5);}
   if(Visible && IsHandleCreated && !dragging){SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x13);}
  }
  public static string FormatShort(long n){return n>=1000000000 ? (n/1000000000d).ToString("0.00")+"B" : n>=1000000 ? (n/1000000d).ToString("0.00")+"M" : n>=1000 ? (n/1000d).ToString("0.0")+"K" : n.ToString();}
 }
 public sealed class Widget : Form {
  ChatUsageMonitor chatMonitor;Button startupButton,visibilityButton;
  readonly DetailsPanel dailyPage=new DetailsPanel(true);readonly ChatUsagePanel taskPage=new ChatUsagePanel();
  readonly System.Collections.Generic.List<Control> summaryControls=new System.Collections.Generic.List<Control>();
  readonly Button[] pageButtons=new Button[3];int selectedPage;
  readonly Button[] textSizeButtons=new Button[3];
  readonly Button[] positionButtons=new Button[3];readonly string[] alignments={"left","center","right"};
  readonly Preferences prefs;readonly string prefsPath;
  readonly AccountMonitor monitor;
  readonly NotifyIcon tray=new NotifyIcon();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  readonly Label caption=new Label(),total=new Label(),quota=new Label(),reset=new Label(),status=new Label(),detail=new Label();
  readonly CheckBox compactSwitch=new CheckBox(); readonly CompactDisplay compact;
  Color muted,accent;Palette palette;DateTime nextTheme=DateTime.MinValue;
  readonly Panel progress=new Panel(),track=new Panel();readonly ToolTip hints=new ToolTip();
  AccountSnapshot lastSnapshot;bool lastStale,lastTokensStale;
  bool quitting;
  public Widget() {
   SuspendLayout();
   prefsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json");prefs=Preferences.LoadForLaunch(prefsPath,Program.StartHidden);
   string configured=Environment.GetEnvironmentVariable("CODEX_HOME");if(!string.IsNullOrWhiteSpace(configured))prefs.codexHome=configured;
   if(string.IsNullOrWhiteSpace(prefs.codexHome))prefs.codexHome=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
   if(string.IsNullOrWhiteSpace(prefs.codexExe)||!File.Exists(prefs.codexExe))prefs.codexExe=FindCodex();
   Text="Codex 계정 사용량";Name="CodexUsageWidget";FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;TopMost=true;ShowInTaskbar=true;
   palette=Palette.Read(false);muted=palette.Muted;accent=palette.Accent;
   Text="Codex 사용량";Font=new Font("Segoe UI",9);BackColor=palette.Surface;ForeColor=palette.Text;ClientSize=new Size(400,316);StartPosition=FormStartPosition.Manual;
   string[] pageNames={"요약","일별","채팅별"};for(int i=0;i<3;i++){int page=i;pageButtons[i]=MakeButton(pageNames[i],new Rectangle(20+i*66,10,60,28));pageButtons[i].Click+=delegate{SelectPage(page);};}
   startupButton=MakeButton("",new Rectangle(223,10,111,28));UpdateStartupButton();startupButton.Click+=delegate{try{StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);UpdateStartupButton();}catch(Exception){MessageBox.Show(this,"자동 시작 설정을 저장하지 못했습니다. 설치 폴더와 시작프로그램 폴더의 쓰기 권한을 확인해 주세요.","자동 시작",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
   hints.SetToolTip(startupButton,"클릭하면 Windows 로그인 시 자동 실행을 등록하거나 해제합니다.");
   Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? (Icon)SystemIcons.Application.Clone();
   visibilityButton=MakeButton("",new Rectangle(223,40,111,24));visibilityButton.Font=new Font("Segoe UI",8);visibilityButton.Click+=delegate{compactSwitch.Checked=!compactSwitch.Checked;};hints.SetToolTip(visibilityButton,"작업표시줄의 작은 상태바를 켜거나 끕니다. 자동 시작 시에는 다시 켜집니다.");
   var more=MakeButton("···",new Rectangle(344,10,36,28));
   var refresh=MakeButton("새로고침",new Rectangle(294,276,86,28));
   MakeLabel("총 토큰 사용량",new Rectangle(20,68,300,20),9,muted);
   total.Bounds=new Rectangle(17,88,365,38);total.Font=new Font("Segoe UI",23,FontStyle.Bold);Controls.Add(total);
   detail.Bounds=new Rectangle(20,126,360,18);detail.Font=new Font("Segoe UI",8);detail.Tag="muted";Controls.Add(detail);
   MakeLabel("토큰 집계 기준일",new Rectangle(20,151,150,22),9,muted);
   caption.Bounds=new Rectangle(174,151,206,22);caption.TextAlign=ContentAlignment.MiddleRight;caption.Tag="muted";Controls.Add(caption);
   MakeLabel("잔여 한도",new Rectangle(20,178,150,22),9,muted);
   quota.Bounds=new Rectangle(17,200,170,38);quota.Font=new Font("Segoe UI",23,FontStyle.Bold);Controls.Add(quota);
   MakeLabel("다음 초기화",new Rectangle(202,178,180,22),9,muted);
   reset.Bounds=new Rectangle(202,207,180,27);reset.Font=new Font("Segoe UI",12);Controls.Add(reset);
   track.Bounds=new Rectangle(20,242,360,4);progress.Height=4;track.Controls.Add(progress);Controls.Add(track);
   status.Bounds=new Rectangle(20,274,106,34);status.Font=new Font("Segoe UI",8);status.Tag="muted";Controls.Add(status);
   foreach(Control c in Controls)if(c.Top>=68 && c.Top<246)summaryControls.Add(c);
   Controls.Add(dailyPage);Controls.Add(taskPage);
   chatMonitor=new ChatUsageMonitor(prefs.codexHome);
   compactSwitch.Checked=prefs.taskbarDisplay;
   compact=new CompactDisplay(prefs,delegate{prefs.Save(prefsPath);UpdatePositionButtons();},ShowWidget);
   string[] positionNames={"왼쪽","중앙","오른쪽"};for(int i=0;i<3;i++){int index=i;positionButtons[i]=MakeButton(positionNames[i],new Rectangle(132+i*52,276,48,28));positionButtons[i].Font=new Font("Segoe UI",8);positionButtons[i].Click+=delegate{compact.SetAlignment(alignments[index]);compactSwitch.Checked=true;};hints.SetToolTip(positionButtons[i],"작은 상태바를 "+positionNames[i]+"에 배치하고 위치를 저장합니다.");}
   MakeLabel("글자 크기",new Rectangle(20,251,106,20),8,muted);
   string[] textSizes={"작게","보통","크게"};for(int i=0;i<3;i++){int size=i;textSizeButtons[i]=MakeButton(textSizes[i],new Rectangle(132+i*52,248,48,24));textSizeButtons[i].Font=new Font("Segoe UI",8);textSizeButtons[i].Click+=delegate{prefs.compactTextSize=size;prefs.Save(prefsPath);compact.ApplyTextSize();UpdateTextSizeButtons();};hints.SetToolTip(textSizeButtons[i],"작업표시줄 글자 크기: "+textSizes[i]);}
   UpdateVisibilityButton();
   compact.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;compactSwitch.Checked=false;compact.Hide();}};
   compactSwitch.CheckedChanged+=delegate {prefs.taskbarDisplay=compactSwitch.Checked;UpdateVisibilityButton();prefs.Save(prefsPath);if(prefs.taskbarDisplay){compact.Show();compact.AlignToTaskbar();}else compact.Hide();};
   monitor=new AccountMonitor(prefs.codexExe,prefs.codexHome,null);
   var menu=new ContextMenuStrip();menu.Items.Add("계정 사용량 열기",null,delegate{ShowWidget();});menu.Items.Add("지금 새로고침",null,delegate{monitor.RefreshNow();});menu.Items.Add("작은 상태바 켜기 / 끄기",null,delegate{compactSwitch.Checked=!compactSwitch.Checked;});menu.Items.Add("작은 표시 위치 맞춤",null,delegate{compactSwitch.Checked=true;compact.ResetPosition();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("종료",null,delegate{quitting=true;Close();});
   ContextMenuStrip=menu;more.Click+=delegate{menu.Show(more,new Point(0,more.Height));};
   hints.SetToolTip(caption,"서버가 반환한 가장 최근 일별 토큰 집계 날짜입니다.\n현재 시각까지 사용한 토큰이 모두 반영됐다는 의미는 아닙니다.");
   hints.SetToolTip(total,"계정 전체 기간의 누적 토큰입니다. 한도 초기화 시에도 유지됩니다.");
   hints.SetToolTip(track,"현재 한도의 사용률입니다.");
   tray.Icon=Icon;tray.Text="Codex 계정 사용량";tray.ContextMenuStrip=menu;tray.Visible=true;tray.DoubleClick+=delegate{ShowWidget();};
   refresh.Click+=delegate{monitor.RefreshNow();};
   FormClosing+=delegate(object s,FormClosingEventArgs e){if(!quitting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
   KeyPreview=true;KeyDown+=delegate(object s,KeyEventArgs e){if(e.Control && e.Shift && e.KeyCode==Keys.Q){e.Handled=true;quitting=true;Close();}};
   FormClosed+=delegate{timer.Stop();tray.Visible=false;tray.Dispose();compact.Dispose();monitor.Dispose();chatMonitor.Dispose();hints.Dispose();if(Icon!=null)Icon.Dispose();};
   Shown+=delegate {DockMain();if(prefs.taskbarDisplay){compact.Show();compact.AlignToTaskbar();}if(Program.StartHidden)Hide();};
   timer.Interval=1000;timer.Tick+=delegate{if(Program.ShowSignal!=null && Program.ShowSignal.WaitOne(0))ShowWidget();RefreshDisplay();};
   AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;ResumeLayout(true);
   HandleCreated+=delegate{ApplyTheme();};ApplyTheme();prefs.Save(prefsPath);monitor.Start();chatMonitor.Start();timer.Start();RefreshDisplay();
  }
  string FindCodex(){string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);string root=Path.Combine(local,"OpenAI","Codex","bin");if(Directory.Exists(root)){var matches=Directory.GetFiles(root,"codex.exe",SearchOption.AllDirectories);if(matches.Length>0)return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();}return "codex.exe";}
  void UpdateVisibilityButton(){visibilityButton.Text=compactSwitch.Checked ? "상태바 켜짐" : "상태바 꺼짐";visibilityButton.ForeColor=compactSwitch.Checked ? palette.Accent : palette.Muted;}
  void UpdateTextSizeButtons(){for(int i=0;i<3;i++){var b=textSizeButtons[i];bool selected=CompactDisplay.NormalizeTextSize(prefs.compactTextSize)==i;b.BackColor=selected ? palette.Track : palette.Button;b.ForeColor=selected ? palette.Accent : palette.Muted;}}
  void UpdateStartupButton(){startupButton.Text=StartupRegistration.IsEnabled ? "자동 시작 켜짐" : "자동 시작 꺼짐";}
  void UpdatePositionButtons(){for(int i=0;i<positionButtons.Length;i++){var b=positionButtons[i];if(b==null)continue;bool selected=!prefs.compactX.HasValue && !prefs.compactY.HasValue && Preferences.NormalizeAlignment(prefs.compactAlignment)==alignments[i];b.BackColor=selected ? palette.Track : palette.Button;b.ForeColor=selected ? palette.Accent : palette.Muted;b.AccessibleDescription=selected ? "선택됨" : "작은 상태바 위치 변경";}}
  Label MakeLabel(string text,Rectangle r,float size,Color color){var l=new Label{Text=text,Bounds=r,Font=new Font("Segoe UI",size),ForeColor=color,Tag="muted"};Controls.Add(l);return l;}
  Button MakeButton(string text,Rectangle r){var b=new Button{Text=text,Bounds=r,FlatStyle=FlatStyle.Flat,BackColor=palette.Button,ForeColor=ForeColor};b.FlatAppearance.BorderSize=0;Controls.Add(b);return b;}
  void SelectPage(int page){selectedPage=page;foreach(var control in summaryControls)control.Visible=page==0;dailyPage.Visible=page==1;taskPage.Visible=page==2;for(int i=0;i<pageButtons.Length;i++){pageButtons[i].BackColor=i==page ? palette.Track : palette.Surface;pageButtons[i].ForeColor=i==page ? palette.Accent : palette.Muted;}}
  void ApplyTheme(){palette=Palette.Read(false);muted=palette.Muted;accent=palette.Accent;BackColor=palette.Surface;ForeColor=palette.Text;foreach(Control c in Controls){c.ForeColor=Object.Equals(c.Tag,"muted") ? muted : ForeColor;if(c is Button)c.BackColor=palette.Button;}track.BackColor=palette.Track;progress.BackColor=accent;palette.Frame(this,false);compact.ApplyTheme();dailyPage.ApplyTheme(palette);taskPage.ApplyTheme(palette);SelectPage(selectedPage);UpdatePositionButtons();UpdateTextSizeButtons();UpdateVisibilityButton();lastSnapshot=null;nextTheme=DateTime.UtcNow.AddSeconds(30);}
  void DockMain(){var r=Screen.FromPoint(Cursor.Position).WorkingArea;Location=new Point(r.Right-Width-14,r.Bottom-Height-12);}
  void ShowWidget(){WindowState=FormWindowState.Normal;Show();DockMain();Activate();BringToFront();}
  static string Stamp(DateTimeOffset? t){return t.HasValue ? t.Value.LocalDateTime.ToString("HH:mm:ss") : "대기";}
  void RefreshDisplay(){
   if(DateTime.UtcNow>=nextTheme)ApplyTheme();
   var s=monitor.Snapshot;
   var v=UsageView.Build(s,DateTimeOffset.Now);
   compact.UpdateValues(s);
   dailyPage.Render(s,null);taskPage.Render(s,chatMonitor.Snapshot);
   if(Object.ReferenceEquals(lastSnapshot,s) && v.Stale==lastStale && v.TokensStale==lastTokensStale)return;
   lastSnapshot=s;lastStale=v.Stale;lastTokensStale=v.TokensStale;
   total.Text=v.Lifetime;caption.Text=v.BucketDate;quota.Text=v.Remaining;reset.Text=v.Reset;
   total.ForeColor=v.TokensStale ? muted : palette.Text;quota.ForeColor=v.Stale ? muted : palette.Text;
   progress.Width=s.Period==null ? 0 : (int)Math.Round(track.Width*(100-s.Period.Remaining)/100d);
   progress.BackColor=s.Period!=null && s.Period.Remaining<=10 ? Color.FromArgb(218,135,36) : accent;
   detail.Text="계정 전체 누적 · 서버 집계 반영 지연 가능";
   if(v.TokensStale)detail.Text+=" · 재확인 중";
   status.Text=v.Stale ? "연결 확인 중\n마지막 확인 값" : (s.Period==null ? "" : s.Period.Caption+"\n")+Stamp(s.LimitsFetchedAt)+" 확인";
   hints.SetToolTip(status,"한도는 10초, 총 토큰은 60초마다 조회합니다.\n서버의 토큰 집계는 지연될 수 있습니다.");
   tray.Text="Codex | 총 "+v.CompactLifetime+" tok | 잔여 "+v.Remaining+" | "+v.Reset+(v.Stale || v.TokensStale ? " *" : "");
  }
 }
 static class Program {
  internal static EventWaitHandle ShowSignal;
  internal static bool StartHidden;
  [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
  [STAThread]static void Main(string[] args){try{StartHidden=args.Any(a=>a=="--startup");SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);bool first;using(var mutex=new Mutex(true,"Local\\CodexUsageWidget-x20",out first))using(ShowSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexUsageWidget-x20-show")){if(!first){ShowSignal.Set();return;}Application.Run(new Widget());}}catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"error.log"),DateTime.Now+" "+e.GetType().Name+": "+e.Message);}}
 }
}




