using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace VISTASystem.VRChat;

/// <summary>VRChat API へのアクセスを担当する。Cookie でセッションを維持する。</summary>
internal sealed class VRChatApiClient : IDisposable
{
    private const string ApiBase = "https://api.vrchat.cloud/api/1";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>アセンブリのバージョンから組み立てた User-Agent。</summary>
    private static readonly string UserAgent =
        $"VISTASystem/{Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "1.0"}";
    private readonly HttpClient        _http;
    private readonly HttpClientHandler _handler;
    private readonly Action<string>    _log;

    public VRChatApiClient(Action<string> log)
    {
        _log     = log;
        _handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
        _http    = new HttpClient(_handler) { Timeout = RequestTimeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public string GetSerializedCookies()
    {
        try
        {
            var list = _handler.CookieContainer.GetAllCookies()
                .Where(c => !c.Expired)
                .Select(c => new
                {
                    c.Name, c.Value, c.Domain, c.Path,
                    Expires  = c.Expires == DateTime.MinValue ? "" : c.Expires.ToString("O"),
                    c.HttpOnly, c.Secure,
                })
                .ToList();
            return JsonSerializer.Serialize(list);
        }
        catch { return string.Empty; }
    }

    public void RestoreCookies(string serialized)
    {
        if (string.IsNullOrEmpty(serialized)) return;
        try
        {
            using var doc = JsonDocument.Parse(serialized);
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string name   = item.GetProperty("Name").GetString()   ?? "";
                string value  = item.GetProperty("Value").GetString()  ?? "";
                string domain = item.GetProperty("Domain").GetString() ?? "";
                string path   = item.GetProperty("Path").GetString()   ?? "/";
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(domain)) continue;

                var cookie = new Cookie(name, value, path, domain);
                string exp = item.TryGetProperty("Expires", out var expEl) ? expEl.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(exp) && DateTime.TryParse(exp, out var dt) && dt > DateTime.UtcNow)
                    cookie.Expires = dt;
                if (item.TryGetProperty("HttpOnly", out var ho)) cookie.HttpOnly = ho.GetBoolean();
                if (item.TryGetProperty("Secure",   out var se)) cookie.Secure   = se.GetBoolean();

                try { _handler.CookieContainer.Add(cookie); } catch { }
            }
        }
        catch { }
    }

    public async Task<LoginResult> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync($"{ApiBase}/auth/user", cancellationToken);
            if (!response.IsSuccessStatusCode) return LoginResult.Failed;

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("requiresTwoFactorAuth", out var needs2fa)
                && needs2fa.ValueKind == JsonValueKind.Array
                && needs2fa.GetArrayLength() > 0)
            {
                _log("[情報] セッション復元: 二段階認証が必要です。");
                return LoginResult.Requires2FA;
            }
            if (root.TryGetProperty("id", out var id))
            {
                _log($"[成功] セッションを復元しました。ユーザーID: {id.GetString()}");
                return LoginResult.Success(id.GetString()!);
            }
            return LoginResult.Failed;
        }
        catch (Exception ex)
        {
            _log($"[例外] セッション復元中にエラー: {ex.Message}");
            return LoginResult.Failed;
        }
    }

    public async Task<LoginResult> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        string encoded = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{Uri.EscapeDataString(password)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/auth/user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            string body  = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log($"[エラー] ログイン失敗: {response.StatusCode}");
                return LoginResult.Failed;
            }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("requiresTwoFactorAuth", out var needs2fa)
                && needs2fa.ValueKind == JsonValueKind.Array
                && needs2fa.GetArrayLength() > 0)
            {
                _log("[情報] 二段階認証が必要です。");
                return LoginResult.Requires2FA;
            }
            if (root.TryGetProperty("id", out var id))
            {
                _log($"[成功] ログインしました。ユーザーID: {id.GetString()}");
                return LoginResult.Success(id.GetString()!);
            }
            return LoginResult.Failed;
        }
        catch (Exception ex)
        {
            _log($"[例外] ログイン中にエラー: {ex.Message}");
            return LoginResult.Failed;
        }
    }

    public async Task<string?> Verify2FAAsync(
        string code, bool isEmail, CancellationToken cancellationToken = default)
    {
        string endpoint = isEmail ? "emailotp" : "totp";
        using var content = new StringContent(
            JsonSerializer.Serialize(new { code }), Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync(
                $"{ApiBase}/auth/twofactorauth/{endpoint}/verify", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _log($"[エラー] 二段階認証失敗: {response.StatusCode}");
                return null;
            }
            using var userResponse = await _http.GetAsync($"{ApiBase}/auth/user", cancellationToken);
            if (!userResponse.IsSuccessStatusCode) return null;
            string body = await userResponse.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("id", out var id))
            {
                _log($"[成功] 二段階認証を通過しました。ユーザーID: {id.GetString()}");
                return id.GetString()!;
            }
        }
        catch (Exception ex) { _log($"[例外] 二段階認証中にエラー: {ex.Message}"); }
        return null;
    }

    public async Task UpdateStatusAsync(
        string userId, string status, string description,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return;
        using var content = new StringContent(
            JsonSerializer.Serialize(new { status, statusDescription = description }),
            Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PutAsync(
                $"{ApiBase}/users/{Uri.EscapeDataString(userId)}", content, cancellationToken);
            if (response.IsSuccessStatusCode)
                _log($"[情報] ステータスを更新しました。({status} / {description})");
            else
                _log($"[エラー] ステータス更新失敗: {response.StatusCode}");
        }
        catch (Exception ex) { _log($"[例外] ステータス更新中にエラー: {ex.Message}"); }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.DeleteAsync($"{ApiBase}/auth/session", cancellationToken);
        }
        catch { }
    }

    public void Dispose() => _http.Dispose();
}
