using VISTASystem.Interop;
using VISTASystem.Monitoring;
using VISTASystem.Settings;
using VISTASystem.VRChat;

namespace VISTASystem.Ui;

/// <summary>
/// メインウィンドウ。ログイン状態の管理、プロセスとステータスの対応付け、
/// アクティブウィンドウの監視を担当する。
/// コントロールの生成とレイアウトは MainForm.Layout.cs 側にある。
/// </summary>
internal partial class MainForm : Form
{
    /// <summary>二重起動時に、既存インスタンスへウィンドウ表示を要求するメッセージ名。</summary>
    internal const string ShowWindowMessage = "VISTASystem_ShowWindow";

    private static readonly uint ShowWindowMsgId =
        NativeMethods.RegisterWindowMessage(ShowWindowMessage);

    private const int LogMaxLines    = 500;    // ログ欄に保持する最大行数
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private AppSettings _settings = new();
    private VRChatApiClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;
    private string _userId      = string.Empty;
    private int    _logLineCount;
    private Dictionary<string, StatusMapping> _mappings =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ActiveApplicationMonitor _monitor = new(PollInterval);
    private string _savedCookieJson = string.Empty;

    private TextBox          _txtUsername  = null!;
    private TextBox          _txtPassword  = null!;
    private TextBox          _txt2FACode   = null!;
    private TextBox          _txtLog       = null!;
    private Button           _btnLogin     = null!;
    private Button           _btnVerify    = null!;
    private Button           _btnStart     = null!;
    private Button           _btnStop      = null!;
    private Button           _btnPickProc  = null!;
    private Button           _btnLogout    = null!;
    private CheckBox         _chkEmail     = null!;
    private Label            _lblStatus    = null!;
    private DataGridView     _dgv          = null!;
    private NotifyIcon       _notifyIcon   = null!;
    private ContextMenuStrip _titleBarMenu = null!;
    private bool             _reallyClose;

    public MainForm() => InitializeComponents();

    // ── プロセス一覧 ─────────────────────────────────────────────────────
    private void PopulateProcessColumn(AppEntry[] entries)
    {
        if (_dgv.Columns[ColProcess] is not DataGridViewComboBoxColumn column) return;
        column.Items.Clear();
        foreach (var entry in entries) column.Items.Add(entry.ProcessName);
    }

    private async Task RefreshProcessListAsync()
    {
        var entries = await Task.Run(ActiveWindowDetector.GetRunningApps);
        PopulateProcessColumn(entries);
        Log($"プロセス一覧を更新しました。{entries.Length} 件");
    }

    private async void PickProcess()
    {
        _btnPickProc.Enabled = false;
        try
        {
            var entries = await Task.Run(ActiveWindowDetector.GetRunningApps);
            PopulateProcessColumn(entries);

            // 選択結果を書き込む行がない場合は新しい行を用意する
            if (_dgv.CurrentRow == null || _dgv.CurrentRow.IsNewRow)
            {
                int index = _dgv.Rows.Add();
                _dgv.CurrentCell = _dgv.Rows[index].Cells[ColProcess];
            }

            using var dialog = new ProcessPickerDialog(entries);
            if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedProcess != null)
                _dgv.CurrentRow!.Cells[ColProcess].Value = dialog.SelectedProcess;
        }
        finally { _btnPickProc.Enabled = true; }
    }

    // ── 表示更新 ─────────────────────────────────────────────────────────
    private void SetStatus(string text, Color color)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { Invoke(() => SetStatus(text, color)); return; }

        _lblStatus.Text      = text;
        _lblStatus.ForeColor = color;

        string tray = $"VISTASystem — {text.TrimStart('●').Trim()}";
        _notifyIcon.Text = tray.Length > 63 ? tray[..63] : tray;
    }

    private void Log(string message)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            // 監視ループ（別スレッド）からも呼ばれる。終了直前は破棄済みのことがある
            try { Invoke(() => Log(message)); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        if (_logLineCount >= LogMaxLines)
        {
            // 上限に達したら古い 100 行を切り捨てる
            string text = _txtLog.Text;
            int pos = 0, removed = 0;
            while (pos < text.Length && removed < 100)
                if (text[pos++] == '\n') removed++;
            _txtLog.Text   = text[pos..];
            _logLineCount -= removed;
        }

        _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        _logLineCount++;
    }

    // ── ログイン処理 ─────────────────────────────────────────────────────
    private async Task PerformLoginAsync()
    {
        string username = _txtUsername.Text.Trim();
        string password = _txtPassword.Text;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Log("ユーザー名とパスワードを入力してください。");
            return;
        }

        _client?.Dispose();
        _client           = new VRChatApiClient(Log);
        _btnLogin.Enabled = false;
        SetStatus("● 接続中…", UI.Muted);

        try
        {
            var result = await _client.LoginAsync(username, password);
            if (result.IsSuccess)
            {
                ApplyLoggedInUi(result.UserId!, "● ログイン済み");
            }
            else if (result.Needs2FA)
            {
                SetStatus("● 2FA 入力中", UI.Warn);
                Enable2FASection();
            }
            else
            {
                SetStatus("● 未ログイン", UI.Muted);
                _btnLogin.Enabled = true;
            }
        }
        catch
        {
            SetStatus("● 未ログイン", UI.Muted);
            _btnLogin.Enabled = true;
        }
    }

    private async Task Perform2FAAsync()
    {
        string code = _txt2FACode.Text.Trim();
        if (string.IsNullOrEmpty(code)) { Log("2FAコードを入力してください。"); return; }

        _btnVerify.Enabled = false;
        SetStatus("● 2FA 確認中…", UI.Warn);

        string? userId = await _client!.Verify2FAAsync(code, _chkEmail.Checked);
        if (userId != null)
        {
            ApplyLoggedInUi(userId, "● ログイン済み");
        }
        else
        {
            SetStatus("● 2FA 入力中", UI.Warn);
            _btnVerify.Enabled = true;
        }
    }

    private async Task TryRestoreSessionFromCookiesAsync()
    {
        if (string.IsNullOrEmpty(_savedCookieJson)) return;
        Log("前回のセッションを復元しています...");

        _client = new VRChatApiClient(Log);
        _client.RestoreCookies(_savedCookieJson);
        _savedCookieJson = string.Empty;

        var result = await _client.TryRestoreSessionAsync();
        if (result.IsSuccess)
        {
            ApplyLoggedInUi(result.UserId!, "● セッション復元済み");
        }
        else if (result.Needs2FA)
        {
            SetStatus("● 2FA 入力中", UI.Warn);
            Enable2FASection();
        }
        else
        {
            Log("セッションの有効期限が切れています。再ログインしてください。");
            _client.Dispose();
            _client = null;
        }
    }

    private async Task PerformLogoutAsync()
    {
        _btnLogout.Enabled = false;
        StopMonitoring();

        if (_client != null)
        {
            await _client.LogoutAsync();
            _client.Dispose();
            _client = null;
        }

        ApplyLoggedOutUi();
        Log("ログアウトしました。");
    }

    /// <summary>ログイン済みの画面状態にする。</summary>
    private void ApplyLoggedInUi(string userId, string statusText)
    {
        _userId              = userId;
        _txtUsername.Enabled = false;
        _txtPassword.Enabled = false;
        _btnLogin.Visible    = false;
        _btnLogout.Visible   = true;
        _btnLogout.Enabled   = true;
        _btnStart.Enabled    = true;
        Disable2FASection();
        SetStatus(statusText, UI.Good);
    }

    /// <summary>未ログインの画面状態に戻す。</summary>
    private void ApplyLoggedOutUi()
    {
        _userId              = string.Empty;
        _txtUsername.Enabled = true;
        _txtPassword.Enabled = true;
        _btnLogout.Visible   = false;
        _btnLogin.Visible    = true;
        _btnLogin.Enabled    = true;
        _btnStart.Enabled    = false;
        Disable2FASection();
        SetStatus("● 未ログイン", UI.Muted);
    }

    private void Enable2FASection()  => Set2FASection(enabled: true);
    private void Disable2FASection() => Set2FASection(enabled: false);

    private void Set2FASection(bool enabled)
    {
        _txt2FACode.Enabled = enabled;
        _chkEmail.Enabled   = enabled;
        _chkEmail.ForeColor = enabled ? UI.Text : UI.Muted;
        _btnVerify.Enabled  = enabled;
    }

    // ── トレイ操作 ───────────────────────────────────────────────────────
    private void ShowMainWindow()
    {
        Show();
        WindowState         = FormWindowState.Normal;
        ShowInTaskbar       = true;
        _notifyIcon.Visible = false;
        Activate();
    }

    private void ResetSettings()
    {
        ShowMainWindow();
        var answer = MessageBox.Show(
            "すべての設定（マッピング・認証情報・クッキー）を削除して初期状態に戻します。\nよろしいですか？",
            "設定をリセット", MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.OK) return;

        StopMonitoring();
        if (_client != null)
        {
            _ = _client.LogoutAsync();
            _client.Dispose();
            _client = null;
        }

        _settings         = new AppSettings();
        _savedCookieJson  = string.Empty;
        _txtUsername.Text = string.Empty;
        _txtPassword.Text = string.Empty;
        _dgv.Rows.Clear();
        ApplyLoggedOutUi();
        AppSettings.Delete();

        Log("設定をリセットしました。");
    }

    private void ExitApp()
    {
        _reallyClose = true;
        SaveSettings();
        StopMonitoring();
        Application.Exit();
    }

    // ── 監視制御 ─────────────────────────────────────────────────────────
    /// <summary>グリッドの内容を、プロセス名 → (API ステータス, メッセージ) の辞書に変換する。</summary>
    private Dictionary<string, StatusMapping> LoadMappings()
    {
        var result = new Dictionary<string, StatusMapping>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (row.IsNewRow) continue;
            string? process = row.Cells[ColProcess].Value?.ToString();
            string? display = row.Cells[ColStatus].Value?.ToString();
            string  message = row.Cells[ColMessage].Value?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(process) || string.IsNullOrWhiteSpace(display)) continue;
            result[process] = new StatusMapping(VrcStatus.ToApiValue(display), message);
        }
        return result;
    }

    private void StartMonitoring()
    {
        _mappings = LoadMappings();
        SaveSettings();
        Log($"設定を読み込みました。対象プロセス数: {_mappings.Count}");

        var cts = new CancellationTokenSource();
        _cts                 = cts;
        _dgv.Enabled         = false;
        _btnPickProc.Enabled = false;
        _btnStart.Enabled    = false;
        _btnStop.Enabled     = true;
        SetStatus("● 監視中", UI.Accent);

        _monitorTask = MonitorLoopAsync(cts);
    }

    private void StopMonitoring()
    {
        var cts = _cts;
        if (cts is null) return;
        _cts = null;
        cts.Cancel();   // Dispose は MonitorLoopAsync 側の finally で行う

        _dgv.Enabled         = true;
        _btnPickProc.Enabled = true;
        _btnStart.Enabled    = true;
        _btnStop.Enabled     = false;
        if (!string.IsNullOrEmpty(_userId))
            SetStatus("● ログイン済み", UI.Good);
        Log("監視を停止しました。");
    }

    /// <summary>前面プロセスを一定間隔で確認し、変化したらステータスを更新する。</summary>
    private async Task MonitorLoopAsync(CancellationTokenSource cts)
    {
        var token = cts.Token;
        try
        {
            await _monitor.RunAsync(_mappings, async (process, mapping, ct) =>
            {
                Log($"[検知] '{process}' がアクティブになりました。");
                var client = _client;
                if (client is not null && !string.IsNullOrEmpty(_userId))
                    await client.UpdateStatusAsync(_userId, mapping.Status, mapping.Message, ct);
            }, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log($"[例外] 監視中にエラー: {ex.Message}"); }
        finally
        {
            cts.Dispose();
            if (ReferenceEquals(_cts, cts)) _cts = null;
        }
    }

    // ── 多重起動防止 / タイトルバー右クリック ───────────────────────────
    private const int WM_NCRBUTTONUP = 0x00A5;
    private const int HTCAPTION      = 2;

    protected override void WndProc(ref Message m)
    {
        // 2 つ目のインスタンスから届く「ウィンドウを表示せよ」の通知
        if (ShowWindowMsgId != 0 && m.Msg == (int)ShowWindowMsgId)
        {
            ShowMainWindow();
            return;
        }

        if (m.Msg == WM_NCRBUTTONUP && m.WParam.ToInt32() == HTCAPTION)
        {
            short x = (short)(m.LParam.ToInt32() & 0xFFFF);
            short y = (short)((m.LParam.ToInt32() >> 16) & 0xFFFF);
            _titleBarMenu.Show(new Point(x, y));
            return;
        }

        base.WndProc(ref m);
    }

    // ── 設定の保存・読み込み ─────────────────────────────────────────────
    private void SaveSettings()
    {
        _settings.Username = _txtUsername.Text.Trim();
        _settings.Password = _txtPassword.Text;
        // ログアウト状態では Cookie を残さない
        _settings.Cookies  = _client != null && !string.IsNullOrEmpty(_userId)
            ? _client.GetSerializedCookies()
            : string.Empty;
        _settings.Mappings = ReadMappingsFromGrid();
        _settings.Save();
    }

    private List<MappingEntry> ReadMappingsFromGrid() =>
    [
        .. _dgv.Rows
            .Cast<DataGridViewRow>()
            .Where(row => !row.IsNewRow)
            .Select(row => new MappingEntry(
                row.Cells[ColProcess].Value?.ToString() ?? string.Empty,
                row.Cells[ColStatus].Value?.ToString()  ?? string.Empty,
                row.Cells[ColMessage].Value?.ToString() ?? string.Empty))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ProcessName))
    ];

    private void LoadSettings()
    {
        _settings = AppSettings.Load();

        _txtUsername.Text = _settings.Username;
        _txtPassword.Text = _settings.Password;
        _savedCookieJson  = _settings.Cookies;

        if (_settings.Mappings.Count == 0) return;

        _dgv.Rows.Clear();
        foreach (var mapping in _settings.Mappings)
            _dgv.Rows.Add(mapping.ProcessName, mapping.Status, mapping.Message);

        Log($"設定を読み込みました。{_settings.Mappings.Count} 件のマッピングを復元しました。");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose は MonitorLoopAsync 側で行うため、ここでは取り消しのみ
            _cts?.Cancel();
            _client?.Dispose();
            _notifyIcon?.Dispose();
            _titleBarMenu?.Dispose();
        }
        base.Dispose(disposing);
    }
}
