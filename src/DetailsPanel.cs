using System;using System.Drawing;using System.Globalization;using System.Linq;using System.Windows.Forms;
namespace CodexUsage {
 public sealed class DetailsPanel : Panel {
  readonly bool daily;readonly Label headline,value,state,aggregate;
  readonly Label[] names=new Label[7],values=new Label[7];
  readonly ToolTip hints=new ToolTip();
  public DetailsPanel(bool daily){this.daily=daily;Bounds=new Rectangle(20,44,360,220);Visible=false;
   headline=LabelAt(daily ? "계정 전체 · 최근 집계된 7일" : "선택한 작업 · 이 PC에 기록된 토큰",0,0,360,21,9,true);
   if(daily){
    LabelAt("집계일",0,27,160,20,8,true);var heading=LabelAt("사용 토큰",180,27,180,20,8,true);heading.TextAlign=ContentAlignment.MiddleRight;
    for(int i=0;i<7;i++){names[i]=LabelAt("",0,51+i*20,130,20,9,false);values[i]=LabelAt("",140,51+i*20,220,20,9,false);values[i].TextAlign=ContentAlignment.MiddleRight;}
    LabelAt("표시된 날짜 합계",0,199,160,21,9,true);aggregate=LabelAt("—",160,199,200,21,10,false);aggregate.TextAlign=ContentAlignment.MiddleRight;
    hints.SetToolTip(headline,"서버의 일별 집계입니다. 아직 반영되지 않은 날짜를 0으로 계산하지 않습니다.");
   }else{
    value=LabelAt("—",-3,23,360,40,23,false);value.Font=new Font("Segoe UI",23,FontStyle.Bold);
    state=LabelAt("기록 확인 중",0,66,360,22,8,true);
    string[] labels={"입력 토큰","출력 토큰","캐시 토큰 · 입력에 포함","마지막 기록"};
    for(int i=0;i<4;i++){names[i]=LabelAt(labels[i],0,99+i*26,190,22,9,true);values[i]=LabelAt("—",186,99+i*26,174,22,9,false);values[i].TextAlign=ContentAlignment.MiddleRight;}
    LabelAt("작업 변경: ··· 메뉴 → 작업 선택",0,204,360,16,8,true);
    hints.SetToolTip(value,"선택한 작업의 기록된 누적 토큰입니다. 입력과 출력을 포함합니다.");
    hints.SetToolTip(names[2],"캐시는 입력 토큰의 일부입니다. 전체 사용량에 중복해서 더하지 않습니다.");
   }
  }
  Label LabelAt(string text,int x,int y,int w,int h,float size,bool muted){var label=new Label{Text=text,Bounds=new Rectangle(x,y,w,h),Font=new Font("Segoe UI",size),Tag=muted ? "muted" : null};Controls.Add(label);return label;}
  static string Number(long? n){return n.HasValue ? n.Value.ToString("N0",CultureInfo.InvariantCulture) : "—";}
  public void ApplyTheme(Palette palette){BackColor=palette.Surface;foreach(Control c in Controls)c.ForeColor=Object.Equals(c.Tag,"muted") ? palette.Muted : palette.Text;}
  public void Render(AccountSnapshot account,TaskSnapshot task){
   if(daily){var days=account.Daily.OrderByDescending(d=>d.Date).Take(7).ToArray();decimal sum=0;
    for(int i=0;i<7;i++){if(i<days.Length){names[i].Text=days[i].Date.Replace('-','/');values[i].Text=Number(days[i].Tokens);sum+=days[i].Tokens;}else{names[i].Text=i==0 ? "집계 대기" : "";values[i].Text="";}}
    aggregate.Text=days.Length==0 ? "—" : sum.ToString("N0",CultureInfo.InvariantCulture);headline.Text="계정 전체 · 최근 집계된 "+days.Length+"일"+(String.IsNullOrEmpty(account.UsageError) ? "" : " · 재확인 중");
   }else{value.Text=Number(task.Tokens)+(task.Failed ? " *" : "");state.Text=task.Failed ? "기록 연결 확인 중 · 마지막 확인 값" : task.Tokens.HasValue ? "기록 확인 정상 · 2초마다 갱신 확인" : "토큰 기록 대기";values[0].Text=Number(task.Input);values[1].Text=Number(task.Output);values[2].Text=Number(task.Cached);values[3].Text=task.RecordedAt.HasValue ? task.RecordedAt.Value.LocalDateTime.ToString("MM/dd HH:mm:ss") : "—";}
  }
  protected override void Dispose(bool disposing){if(disposing)hints.Dispose();base.Dispose(disposing);}
 }
}

