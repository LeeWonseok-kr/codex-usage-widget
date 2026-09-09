using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace CodexUsage {
 public sealed class TaskEntry {public string Path,Title;public DateTime Updated;}
 public static class TaskCatalog {
  public static TaskEntry[] Recent(string home){
   var names=new Dictionary<string,string>();string index=System.IO.Path.Combine(home,"session_index.jsonl");
   try{if(File.Exists(index))using(var file=new FileStream(index,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(file)){string line;var json=new JavaScriptSerializer();while((line=reader.ReadLine())!=null){try{var d=Reader.Object(json.DeserializeObject(line));string id=Reader.Get(d,"id") as string,title=Reader.Get(d,"thread_name") as string;if(id!=null && title!=null)names[id]=title;}catch(ArgumentException){}}}}catch(IOException){}catch(UnauthorizedAccessException){}
   var entries=new List<TaskEntry>();string root=System.IO.Path.Combine(home,"sessions");
   try{if(Directory.Exists(root))foreach(var path in Directory.EnumerateFiles(root,"rollout-*.jsonl",SearchOption.AllDirectories)){string name=System.IO.Path.GetFileNameWithoutExtension(path);Guid id;if(name.Length!=64 || !Guid.TryParse(name.Substring(name.Length-36),out id))continue;string title;if(!names.TryGetValue(id.ToString(),out title))title="작업 "+File.GetLastWriteTime(path).ToString("MM/dd HH:mm");entries.Add(new TaskEntry{Path=path,Title=title,Updated=File.GetLastWriteTimeUtc(path)});}}catch(IOException){}catch(UnauthorizedAccessException){}
   return entries.OrderByDescending(e=>e.Updated).Take(15).ToArray();
  }
 }
}
