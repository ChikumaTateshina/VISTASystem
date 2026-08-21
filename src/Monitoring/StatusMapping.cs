namespace VISTASystem.Monitoring;

/// <summary>監視対象のプロセスと、切り替え先の VRChat ステータス。</summary>
internal sealed record StatusMapping(string Status, string Message);
