using VISTASystem.Interop;

namespace VISTASystem.Ui;

/// <summary>タスクトレイ用アイコンの生成。</summary>
internal static class TrayIcon
{
    /// <summary>アクセントカラーに "V" を描いた 16x16 のアイコンを作る。</summary>
    public static Icon Create()
    {
        const int size = 16;

        using var bmp  = new Bitmap(size, size);
        using var g    = Graphics.FromImage(bmp);
        using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
        using var sf   = new StringFormat
        {
            Alignment     = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.Clear(UI.Accent);
        g.DrawString("V", font, Brushes.White, new RectangleF(0, 0, size, size), sf);

        // Icon.FromHandle はハンドルを所有しないため、複製したうえで元のハンドルを破棄する
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally { NativeMethods.DestroyIcon(hIcon); }
    }
}
