using VISTASystem.Interop;
using VISTASystem.Ui;

namespace VISTASystem;

/// <summary>エントリポイント。多重起動時は既存インスタンスを前面に出して終了する。</summary>
internal static class Program
{
    private const string MutexName = "VISTASystem_SingleInstance_v1";

    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            uint msg = NativeMethods.RegisterWindowMessage(MainForm.ShowWindowMessage);
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
        mutex.ReleaseMutex();
    }
}
