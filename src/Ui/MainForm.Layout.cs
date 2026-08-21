using VISTASystem.VRChat;

namespace VISTASystem.Ui;

// メインフォームのうち、コントロールの生成とレイアウトを担当する部分。
// 実際の動作（ログイン・監視・設定）は MainForm.cs 側にある。
internal partial class MainForm
{
    // ── レイアウト定数 ───────────────────────────────────────────────────
    private const int W  = 620;          // クライアント幅
    private const int M  = 10;           // 外周マージン
    private const int CW = W - M * 2;    // コンテンツ幅

    // ── 列名 ─────────────────────────────────────────────────────────────
    private const string ColProcess = "Process";
    private const string ColStatus  = "Status";
    private const string ColMessage = "Message";

    private sealed class BufferedGrid : DataGridView
    {
        public BufferedGrid() { DoubleBuffered = true; }
    }

    private void InitializeComponents()
    {
        ConfigureForm();

        Controls.AddRange([
            .. BuildLoginRow(),
            .. BuildTwoFactorRow(),
            .. BuildMappingArea(),
            .. BuildActionRow(),
            .. BuildLogArea(),
        ]);

        BuildTrayIcon();
        BuildContextMenus();
    }

    private void ConfigureForm()
    {
        Text       = "VISTASystem";
        ClientSize = new Size(W, 500);
        BackColor  = UI.Bg;
        ForeColor  = UI.Text;
        Font       = UI.Regular;

        // 閉じるボタンでは終了せず、トレイへ格納する
        FormClosing += (_, e) =>
        {
            if (!_reallyClose)
            {
                e.Cancel            = true;
                Hide();
                ShowInTaskbar       = false;
                _notifyIcon.Visible = true;
            }
        };

        Load += async (_, _) =>
        {
            MinimumSize = Size;
            LoadSettings();
            await TryRestoreSessionFromCookiesAsync();
            await RefreshProcessListAsync();
        };
    }

    private Control[] BuildLoginRow()
    {
        _txtUsername = UI.Input("Username / Email", 192);
        _txtUsername.Location = new Point(M, 12);

        _txtPassword = UI.Input("Password", 192, password: true);
        _txtPassword.Location = new Point(M + 198, 12);

        _btnLogin = UI.Btn("Login", UI.Accent, 88);
        _btnLogin.Location = new Point(M + 396, 10);
        _btnLogin.Click   += async (_, _) => await PerformLoginAsync();

        // ログイン後に Login と入れ替わる（同じ位置に重ねて配置）
        _btnLogout = UI.Btn("ログアウト", UI.Elevated, 88);
        _btnLogout.Location = new Point(M + 396, 10);
        _btnLogout.Visible  = false;
        _btnLogout.Click   += async (_, _) => await PerformLogoutAsync();

        _lblStatus = new Label
        {
            Text      = "● 未ログイン",
            ForeColor = UI.Muted,
            BackColor = Color.Transparent,
            AutoSize  = true,
            Font      = UI.Regular,
            Location  = new Point(M + 492, 14),
        };

        return [_txtUsername, _txtPassword, _btnLogin, _btnLogout, _lblStatus];
    }

    private Control[] BuildTwoFactorRow()
    {
        _txt2FACode = UI.Input("2FAコード", 110);
        _txt2FACode.Location = new Point(M, 46);
        _txt2FACode.Enabled  = false;

        _chkEmail = new CheckBox
        {
            Text      = "Email OTP",
            BackColor = Color.Transparent,
            ForeColor = UI.Muted,
            AutoSize  = true,
            Font      = UI.Regular,
            Enabled   = false,
            Location  = new Point(M + 116, 48),
        };

        _btnVerify = UI.Btn("Verify", UI.Accent, 80);
        _btnVerify.Location = new Point(M + 222, 46);
        _btnVerify.Enabled  = false;
        _btnVerify.Click   += async (_, _) => await Perform2FAAsync();

        return [_txt2FACode, _chkEmail, _btnVerify];
    }

    private Control[] BuildMappingArea()
    {
        var separator = UI.Line(78, W);

        _dgv = BuildGrid();
        _dgv.Location = new Point(M, 86);
        _dgv.Size     = new Size(CW, 150);
        _dgv.Anchor   = AnchorStyles.Top | AnchorStyles.Bottom
                      | AnchorStyles.Left | AnchorStyles.Right;
        _dgv.EditingControlShowing += OnDgvEditingControlShowing;
        _dgv.Leave          += (_, _) => _dgv.EndEdit();
        _dgv.CellEndEdit    += (_, _) => SaveSettings();
        _dgv.UserDeletedRow += (_, _) => SaveSettings();

        return [separator, _dgv];
    }

    private Control[] BuildActionRow()
    {
        var separator = UI.Line(244, W);
        separator.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _btnPickProc = UI.Btn("アプリを選択…", UI.Elevated, 148);
        _btnPickProc.Location = new Point(M, 252);
        _btnPickProc.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
        _btnPickProc.Click   += (_, _) => PickProcess();

        _btnStart = UI.Btn("▶  監視開始", UI.BtnStart, 136);
        _btnStart.Location = new Point(M + 154, 252);
        _btnStart.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
        _btnStart.Enabled  = false;
        _btnStart.Click   += (_, _) => StartMonitoring();

        _btnStop = UI.Btn("■  監視停止", UI.BtnStop, 136);
        _btnStop.Location = new Point(M + 296, 252);
        _btnStop.Anchor   = AnchorStyles.Bottom | AnchorStyles.Left;
        _btnStop.Enabled  = false;
        _btnStop.Click   += (_, _) => StopMonitoring();

        return [separator, _btnPickProc, _btnStart, _btnStop];
    }

    private Control[] BuildLogArea()
    {
        var separator = UI.Line(288, W);
        separator.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        _txtLog = new TextBox
        {
            Location    = new Point(M, 296),
            Size        = new Size(CW, 194),
            Multiline   = true,
            ScrollBars  = ScrollBars.Vertical,
            ReadOnly    = true,
            BackColor   = UI.LogBg,
            ForeColor   = UI.LogFg,
            Font        = UI.Mono,
            BorderStyle = BorderStyle.None,
            Anchor      = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
        };

        return [separator, _txtLog];
    }

    private void BuildTrayIcon()
    {
        var trayMenu = new ContextMenuStrip { BackColor = UI.Elevated, ForeColor = UI.Text, Font = UI.Regular };
        trayMenu.Items.Add(new ToolStripMenuItem("VISTASystem") { Enabled = false, ForeColor = UI.Muted });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(MenuItem("ウィンドウを表示", ShowMainWindow));
        trayMenu.Items.Add(MenuItem("設定をリセット...", ResetSettings));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(MenuItem("終了", ExitApp));

        _notifyIcon = new NotifyIcon
        {
            Icon             = TrayIcon.Create(),
            Text             = "VISTASystem",
            ContextMenuStrip = trayMenu,
            Visible          = false,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void BuildContextMenus()
    {
        // ウィンドウ背景の右クリック
        var formMenu = new ContextMenuStrip { BackColor = UI.Elevated, ForeColor = UI.Text, Font = UI.Regular };
        formMenu.Items.Add(MenuItem("設定をリセット...", ResetSettings));
        formMenu.Items.Add(new ToolStripSeparator());
        formMenu.Items.Add(MenuItem("終了", ExitApp));
        ContextMenuStrip = formMenu;

        // タイトルバーの右クリック（WndProc から表示する。毎回作らず使い回す）
        _titleBarMenu = new ContextMenuStrip { BackColor = UI.Elevated, ForeColor = UI.Text, Font = UI.Regular };
        _titleBarMenu.Items.Add(MenuItem("終了", ExitApp));
    }

    private static ToolStripMenuItem MenuItem(string text, Action onClick)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += (_, _) => onClick();
        return item;
    }

    private static BufferedGrid BuildGrid()
    {
        var dgv = new BufferedGrid
        {
            AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows          = true,
            AllowUserToDeleteRows       = true,
            BackgroundColor             = UI.Surface,
            GridColor                   = UI.Border,
            BorderStyle                 = BorderStyle.None,
            EnableHeadersVisualStyles   = false,
            RowHeadersVisible           = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 30,
            CellBorderStyle             = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect                 = false,
        };
        dgv.RowTemplate.Height = 26;

        var cell = new DataGridViewCellStyle
        {
            BackColor          = UI.Surface,
            ForeColor          = UI.Text,
            SelectionBackColor = UI.Accent,
            SelectionForeColor = Color.White,
            Font               = UI.Regular,
        };
        var header = new DataGridViewCellStyle
        {
            BackColor          = UI.Elevated,
            ForeColor          = UI.Muted,
            SelectionBackColor = UI.Elevated,
            SelectionForeColor = UI.Muted,
            Font               = UI.Regular,
            Padding            = new Padding(4, 0, 0, 0),
        };
        dgv.DefaultCellStyle                = cell;
        dgv.ColumnHeadersDefaultCellStyle   = header;
        dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle(cell)
        {
            BackColor = Color.FromArgb(42, 42, 42),
        };

        dgv.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name             = ColProcess,
            HeaderText       = "プロセス名",
            FillWeight       = 32,
            FlatStyle        = FlatStyle.Flat,
            DisplayStyle     = DataGridViewComboBoxDisplayStyle.Nothing,
            SortMode         = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle(cell) { Padding = new Padding(4, 0, 0, 0) },
        });

        var statusCol = new DataGridViewComboBoxColumn
        {
            Name             = ColStatus,
            HeaderText       = "ステータス",
            FillWeight       = 25,
            FlatStyle        = FlatStyle.Flat,
            DisplayStyle     = DataGridViewComboBoxDisplayStyle.ComboBox,
            SortMode         = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle(cell) { Padding = new Padding(2, 0, 0, 0) },
        };
        foreach (string display in VrcStatus.Displays) statusCol.Items.Add(display);
        dgv.Columns.Add(statusCol);

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name             = ColMessage,
            HeaderText       = "ステータスメッセージ",
            FillWeight       = 43,
            SortMode         = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = new DataGridViewCellStyle(cell) { Padding = new Padding(4, 0, 0, 0) },
        });

        dgv.DataError += (_, _) => { };

        // DataGridView 専用の右クリックメニュー（Form の ContextMenuStrip との競合を防ぐ）
        var dgvMenu    = new ContextMenuStrip { BackColor = UI.Surface, ForeColor = UI.Text, Font = UI.Regular };
        var deleteItem = new ToolStripMenuItem("この行を削除") { ForeColor = Color.FromArgb(220, 80, 80) };
        deleteItem.Click += (_, _) =>
        {
            if (dgv.CurrentRow is { IsNewRow: false } row) dgv.Rows.Remove(row);
        };
        dgvMenu.Items.Add(deleteItem);
        // Opening イベントで対象行を選択、新規行・ヘッダーでは非表示
        dgvMenu.Opening += (_, e) =>
        {
            var pos = dgv.PointToClient(Cursor.Position);
            var hit = dgv.HitTest(pos.X, pos.Y);
            if (hit.RowIndex < 0 || hit.RowIndex >= dgv.Rows.Count - 1)
            {
                e.Cancel = true;
                return;
            }
            dgv.ClearSelection();
            dgv.Rows[hit.RowIndex].Selected = true;
            dgv.CurrentCell = dgv.Rows[hit.RowIndex].Cells[0];
        };
        dgv.ContextMenuStrip = dgvMenu;

        return dgv;
    }

    /// <summary>プロセス名列は一覧からの選択に加えて、手入力と補完も許可する。</summary>
    private void OnDgvEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (_dgv.CurrentCell == null) return;
        if (_dgv.Columns[ColProcess] is not DataGridViewColumn processColumn) return;
        if (_dgv.CurrentCell.ColumnIndex != processColumn.Index) return;
        if (e.Control is not ComboBox combo) return;
        combo.DropDownStyle      = ComboBoxStyle.DropDown;
        combo.AutoCompleteMode   = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteSource = AutoCompleteSource.ListItems;
    }
}
