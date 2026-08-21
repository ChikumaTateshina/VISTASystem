using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace VISTASystem.Interop;

/// <summary>前面ウィンドウの検出と、ウィンドウを持つ実行中アプリの列挙。</summary>
internal static class ActiveWindowDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int capacity);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static string? GetActiveProcessName()
    {
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hWnd, out uint pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch { return null; }
    }

    public static AppEntry[] GetRunningApps()
    {
        var byPid = new Dictionary<uint, string>();
        var sb    = new StringBuilder(256);

        EnumWindowsProc callback = (hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0) return true;
            sb.Clear();
            if (GetWindowText(hWnd, sb, sb.Capacity) == 0) return true;
            string title = sb.ToString();
            if (!byPid.TryGetValue(pid, out string? prev) || title.Length > prev.Length)
                byPid[pid] = title;
            return true;
        };
        EnumWindows(callback, IntPtr.Zero);

        var entries   = new List<AppEntry>(byPid.Count);
        var seenProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (pid, title) in byPid)
        {
            string name;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
            }
            catch { continue; }
            if (!seenProcs.Add(name)) continue;
            string display = title.Length > 60 ? title[..60] + "…" : title;
            entries.Add(new AppEntry(display, name));
        }

        entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.AppName, b.AppName));
        return [.. entries];
    }
}
