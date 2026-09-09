using System;using System.Drawing;using System.Windows.Forms;using System.Runtime.InteropServices;using Microsoft.Win32;
namespace CodexUsage {
 public sealed class Palette {
  public bool Light;public Color Surface,Text,Muted,Accent,Button,Track;
  public static Palette Read(bool taskbar){
   bool light=false;try{object value=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",taskbar ? "SystemUsesLightTheme" : "AppsUseLightTheme",0);light=Convert.ToInt32(value)==1;}catch{}
   var p=new Palette {Light=light,Surface=light ? Color.FromArgb(249,249,249) : Color.FromArgb(32,32,32),Text=light ? Color.FromArgb(26,26,26) : Color.FromArgb(242,242,242),Muted=light ? Color.FromArgb(96,96,96) : Color.FromArgb(170,170,170),Accent=light ? Color.FromArgb(0,95,184) : Color.FromArgb(96,172,255),Button=light ? Color.FromArgb(239,239,239) : Color.FromArgb(45,45,45),Track=light ? Color.FromArgb(221,221,221) : Color.FromArgb(65,65,65)};
   if(SystemInformation.HighContrast){p.Surface=SystemColors.Window;p.Text=SystemColors.WindowText;p.Muted=p.Text;p.Accent=SystemColors.Highlight;p.Button=SystemColors.Control;p.Track=SystemColors.ControlDark;}
   return p;
  }
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  public void Frame(Form form,bool small){if(!form.IsHandleCreated)return;try{int dark=Light ? 0 : 1;DwmSetWindowAttribute(form.Handle,20,ref dark,4);int radius=small ? 3 : 2;DwmSetWindowAttribute(form.Handle,33,ref radius,4);int color=Surface.R|(Surface.G<<8)|(Surface.B<<16);DwmSetWindowAttribute(form.Handle,35,ref color,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
 }
}
