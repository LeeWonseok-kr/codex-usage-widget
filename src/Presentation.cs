using System;
using System.Globalization;
namespace CodexUsage {
 public sealed class UsageView {
  public string Used,Remaining,Reset,Lifetime,CompactLifetime,ResetTokens,BucketDate,CompactBucketDate;
  public bool Stale,TokensStale;
  public static UsageView Build(AccountSnapshot s,DateTimeOffset now){
   var v=new UsageView();
   v.Used=s.Period==null ? "—" : (100-s.Period.Remaining).ToString("0.#",CultureInfo.InvariantCulture)+"%";
   v.Remaining=s.Period==null ? "—" : s.Period.Remaining.ToString("0.#",CultureInfo.InvariantCulture)+"%";
   v.Reset=s.Period!=null && s.Period.Resets.HasValue ? s.Period.Resets.Value.LocalDateTime.ToString("MM'/'dd HH:mm",CultureInfo.InvariantCulture) : "—";
   v.Lifetime=s.LifetimeTokens.HasValue ? s.LifetimeTokens.Value.ToString("N0",CultureInfo.InvariantCulture) : "—";
   v.CompactLifetime=!s.LifetimeTokens.HasValue ? "—" : s.LifetimeTokens.Value>=1000000000 ? (s.LifetimeTokens.Value/1000000000d).ToString("0.00",CultureInfo.InvariantCulture)+"B" : s.LifetimeTokens.Value>=1000000 ? (s.LifetimeTokens.Value/1000000d).ToString("0.00",CultureInfo.InvariantCulture)+"M" : s.LifetimeTokens.Value>=1000 ? (s.LifetimeTokens.Value/1000d).ToString("0.0",CultureInfo.InvariantCulture)+"K" : s.LifetimeTokens.Value.ToString(CultureInfo.InvariantCulture);
   v.ResetTokens="정확한 집계 미제공";
   DateTime bucket;
   bool hasBucket=DateTime.TryParseExact(s.LastBucketDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out bucket);
   v.BucketDate=hasBucket ? bucket.ToString("yyyy'/'MM'/'dd",CultureInfo.InvariantCulture) : "확인 중";
   v.CompactBucketDate=hasBucket ? bucket.ToString("MM'/'dd",CultureInfo.InvariantCulture)+" 기준" : "집계일 확인 중";
   v.Stale=!s.LimitsFetchedAt.HasValue || (now-s.LimitsFetchedAt.Value).TotalSeconds>30 || !string.IsNullOrEmpty(s.Error);
   v.TokensStale=!s.TokensFetchedAt.HasValue || (now-s.TokensFetchedAt.Value).TotalSeconds>120 || !string.IsNullOrEmpty(s.UsageError);
   return v;
  }
 }
}
