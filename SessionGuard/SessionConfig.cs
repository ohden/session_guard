namespace SessionGuard;

/// <summary>
/// セッション管理の設定を保持するクラス
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// ユーザーがログアウトされるべき時間帯の開始時刻 (HH:mm形式)
    /// 例: "22:00" (22時)
    /// </summary>
    public string LogoutStartTime { get; set; } = "22:00";

    /// <summary>
    /// ユーザーがログアウトされるべき時間帯の終了時刻 (HH:mm形式)
    /// 例: "08:00" (8時)
    /// </summary>
    public string LogoutEndTime { get; set; } = "08:00";

    /// <summary>
    /// 最大連続稼働時間（時間単位）
    /// この時間を超えて稼働している場合、ログアウトする
    /// 例: 8 (8時間)
    /// </summary>
    public int MaxContinuousUptime { get; set; } = 8;

    /// <summary>
    /// ログアウト実行を有効にするかどうか
    /// </summary>
    public bool EnableLogout { get; set; } = true;

    /// <summary>
    /// ステータス確認間隔（秒単位）
    /// </summary>
    public int CheckInterval { get; set; } = 60;
}
