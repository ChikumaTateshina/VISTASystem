using System.Security.Cryptography;
using System.Text;

namespace VISTASystem.Settings;

/// <summary>
/// Windows DPAPI による文字列の暗号化。CurrentUser スコープのため、
/// 暗号化した Windows ユーザーアカウント以外では復号できない。
/// 失敗時は空文字を返し、呼び出し側では「未保存」として扱う。
/// </summary>
internal static class DataProtector
{
    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return string.Empty;
        try
        {
            byte[] encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch { return string.Empty; }
    }

    public static string Unprotect(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        try
        {
            byte[] plain = ProtectedData.Unprotect(
                Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return string.Empty; }
    }
}
