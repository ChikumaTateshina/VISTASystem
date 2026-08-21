namespace VISTASystem.Ui;

/// <summary>ダークテーマの配色と、共通コントロールのファクトリ。</summary>
internal static class UI
{
    public static readonly Color Bg       = Color.FromArgb(24, 24, 24);
    public static readonly Color Surface  = Color.FromArgb(36, 36, 36);
    public static readonly Color Elevated = Color.FromArgb(46, 46, 46);
    public static readonly Color Border   = Color.FromArgb(58, 58, 58);
    public static readonly Color Accent   = Color.FromArgb(68, 148, 228);
    public static readonly Color Text     = Color.FromArgb(218, 218, 218);
    public static readonly Color Muted    = Color.FromArgb(105, 105, 105);
    public static readonly Color Good     = Color.FromArgb(86, 196, 86);
    public static readonly Color Warn     = Color.FromArgb(228, 162, 68);
    public static readonly Color BtnStart = Color.FromArgb(38, 86, 50);
    public static readonly Color BtnStop  = Color.FromArgb(86, 38, 38);
    public static readonly Color LogBg    = Color.FromArgb(14, 14, 14);
    public static readonly Color LogFg    = Color.FromArgb(48, 208, 78);

    public static readonly Font Regular = new("Segoe UI", 9f);
    public static readonly Font Mono    = new("Consolas", 9f);

    public static TextBox Input(string placeholder, int width, bool password = false) => new()
    {
        Width                 = width,
        PlaceholderText       = placeholder,
        BackColor             = Surface,
        ForeColor             = Text,
        BorderStyle           = BorderStyle.FixedSingle,
        Font                  = Regular,
        UseSystemPasswordChar = password,
    };

    public static Button Btn(string text, Color bg, int width = 88)
    {
        var b = new Button
        {
            Text      = text,
            Width     = width,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = Text,
            Font      = Regular,
            Cursor    = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize           = 0;
        b.FlatAppearance.MouseOverBackColor   = Lift(bg,  22);
        b.FlatAppearance.MouseDownBackColor   = Lift(bg, -12);
        return b;
    }

    public static Panel Line(int y, int width) => new()
    {
        Location  = new Point(0, y),
        Size      = new Size(width, 1),
        BackColor = Border,
        Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
    };

    private static Color Lift(Color c, int d) => Color.FromArgb(
        Math.Clamp(c.R + d, 0, 255),
        Math.Clamp(c.G + d, 0, 255),
        Math.Clamp(c.B + d, 0, 255));
}
