using System.Text.Json;

namespace VISTASystem.Settings;

/// <summary>プロセス名 1 件に対する、切り替え先ステータスとステータスメッセージ。</summary>
internal sealed record MappingEntry(string ProcessName, string Status, string Message);

/// <summary>
/// %AppData%\VISTASystem\settings.json の読み書き。
/// パスワードと Cookie はメモリ上では平文で保持し、保存時に <see cref="DataProtector"/> で暗号化する。
/// </summary>
internal sealed class AppSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Cookies  { get; set; } = string.Empty;
    public List<MappingEntry> Mappings { get; set; } = [];

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VISTASystem", "settings.json");

    // 旧名 (VRCStatus) 時代の設定を引き継ぐための移行元
    private static readonly string LegacyFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRChatStatusUpdater", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented        = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>ディスク上の表現。文字列はいずれも暗号化済み、または空。</summary>
    private sealed record Dto(
        string? Username, string? Password, string? Cookies, List<MappingEntry>? Mappings);

    /// <summary>設定を読み込む。存在しない・壊れている場合は既定値を返す。</summary>
    public static AppSettings Load()
    {
        string path = File.Exists(FilePath)       ? FilePath
                    : File.Exists(LegacyFilePath) ? LegacyFilePath
                    : string.Empty;
        if (string.IsNullOrEmpty(path)) return new AppSettings();

        try
        {
            using var stream = File.OpenRead(path);
            var dto = JsonSerializer.Deserialize<Dto>(stream, JsonOpts);
            if (dto is null) return new AppSettings();

            return new AppSettings
            {
                Username = dto.Username ?? string.Empty,
                Password = DataProtector.Unprotect(dto.Password ?? string.Empty),
                Cookies  = DataProtector.Unprotect(dto.Cookies  ?? string.Empty),
                Mappings = [.. (dto.Mappings ?? [])
                    .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.ProcessName))],
            };
        }
        catch { return new AppSettings(); }
    }

    /// <summary>設定を保存する。失敗しても例外は投げない（保存できないだけ）。</summary>
    public void Save()
    {
        string? temporaryPath = null;
        try
        {
            var dto = new Dto(
                Username,
                DataProtector.Protect(Password),
                DataProtector.Protect(Cookies),
                Mappings);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(dto, JsonOpts));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch
        {
            try { if (temporaryPath is not null) File.Delete(temporaryPath); }
            catch { }
        }
    }

    /// <summary>設定ファイルを削除する。旧名時代のファイルも消して復活を防ぐ。</summary>
    public static void Delete()
    {
        foreach (string path in new[] { FilePath, LegacyFilePath })
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
