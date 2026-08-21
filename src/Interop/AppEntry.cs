namespace VISTASystem.Interop;

/// <summary>ウィンドウを持つ実行中アプリ 1 件分の表示名とプロセス名。</summary>
internal sealed class AppEntry(string appName, string processName)
{
    public string AppName     { get; } = appName;
    public string ProcessName { get; } = processName;

    public override string ToString() =>
        AppName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase)
            ? ProcessName
            : $"{AppName}  ({ProcessName})";
}
