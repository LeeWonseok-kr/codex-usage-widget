using System;using System.IO;using System.Reflection;using System.Runtime.InteropServices;
namespace CodexUsage {
 public static class StartupRegistration {
  public static string ShortcutPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Codex Usage Widget.lnk");}}
  public static bool IsEnabled {get{return File.Exists(ShortcutPath);}}
  public static void SetEnabled(bool enabled){if(enabled)CreateShortcut(ShortcutPath,System.Reflection.Assembly.GetExecutingAssembly().Location);else if(File.Exists(ShortcutPath))File.Delete(ShortcutPath);}
  public static void CreateShortcut(string shortcutPath,string executable){
   object shell=null,shortcut=null;
   try{Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));var type=Type.GetTypeFromProgID("WScript.Shell",true);shell=Activator.CreateInstance(type);shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcutPath});var target=shortcut.GetType();
    target.InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetFullPath(executable)});
    target.InvokeMember("Arguments",BindingFlags.SetProperty,null,shortcut,new object[]{"--startup"});
    target.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(Path.GetFullPath(executable))});
    target.InvokeMember("Description",BindingFlags.SetProperty,null,shortcut,new object[]{"Codex usage widget"});
    target.InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
   }finally{if(shortcut!=null && Marshal.IsComObject(shortcut))Marshal.FinalReleaseComObject(shortcut);if(shell!=null && Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
  }
 }
}
