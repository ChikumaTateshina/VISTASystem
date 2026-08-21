using VISTASystem.Interop;

namespace VISTASystem.Ui;

/// <summary>実行中アプリの一覧から、監視対象のプロセスを絞り込み選択するダイアログ。</summary>
internal sealed class ProcessPickerDialog : Form
{
    public string? SelectedProcess { get; private set; }

    private readonly ListBox    _list;
    private readonly TextBox    _txtSearch;
    private readonly AppEntry[] _all;

    public ProcessPickerDialog(AppEntry[] entries)
    {
        _all          = entries;
        Text          = "アプリを選択";
        ClientSize    = new Size(360, 480);
        BackColor     = UI.Bg;
        ForeColor     = UI.Text;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox   = false;
        MaximizeBox   = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        _txtSearch = new TextBox
        {
            Dock            = DockStyle.Top,
            Height          = 32,
            PlaceholderText = "絞り込み…",
            BackColor       = UI.Surface,
            ForeColor       = UI.Text,
            BorderStyle     = BorderStyle.None,
            Font            = UI.Regular,
        };
        _txtSearch.TextChanged += (_, _) => FilterList();

        _list = new ListBox
        {
            Dock          = DockStyle.Fill,
            SelectionMode = SelectionMode.One,
            BackColor     = UI.Surface,
            ForeColor     = UI.Text,
            BorderStyle   = BorderStyle.None,
            Font          = UI.Regular,
            ItemHeight    = 24,
        };
        _list.Items.AddRange(entries);
        _list.DoubleClick += (_, _) => SelectAndClose();

        var btnOk = UI.Btn("OK", UI.Accent, 360);
        btnOk.Dock   = DockStyle.Bottom;
        btnOk.Height = 38;
        btnOk.Click += (_, _) => SelectAndClose();

        Controls.Add(_list);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UI.Border });
        Controls.Add(_txtSearch);
        Controls.Add(btnOk);
    }

    private void FilterList()
    {
        string q = _txtSearch.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        var src = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(e =>
                e.AppName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                e.ProcessName.Contains(q, StringComparison.OrdinalIgnoreCase));
        foreach (var e in src) _list.Items.Add(e);
        _list.EndUpdate();
    }

    private void SelectAndClose()
    {
        if (_list.SelectedItem is AppEntry entry)
        {
            SelectedProcess = entry.ProcessName;
            DialogResult    = DialogResult.OK;
            Close();
        }
    }
}
