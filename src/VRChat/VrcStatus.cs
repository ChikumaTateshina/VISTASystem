namespace VISTASystem.VRChat;

/// <summary>VRChat のステータス。画面表示名と API 値の対応表。</summary>
internal static class VrcStatus
{
    private static readonly (string Display, string ApiValue)[] All =
    [
        ("Join Me",        "join me"),
        ("Online",         "active"),
        ("Ask Me",         "ask me"),
        ("Do Not Disturb", "busy"),
    ];

    /// <summary>コンボボックスに並べる表示名。</summary>
    public static IEnumerable<string> Displays => All.Select(s => s.Display);

    /// <summary>表示名を API 値へ変換する。未知の表示名はそのまま返す。</summary>
    public static string ToApiValue(string display) =>
        All.FirstOrDefault(s => s.Display == display).ApiValue ?? display;
}
