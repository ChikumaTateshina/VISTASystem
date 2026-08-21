namespace VISTASystem.VRChat;

/// <summary>ログイン試行の結果。二段階認証が必要な場合は <see cref="Needs2FA"/> が true。</summary>
internal record LoginResult(bool IsSuccess, bool Needs2FA, string? UserId)
{
    public static readonly LoginResult Failed      = new(false, false, null);
    public static readonly LoginResult Requires2FA = new(false, true,  null);
    public static LoginResult Success(string userId) => new(true, false, userId);
}
