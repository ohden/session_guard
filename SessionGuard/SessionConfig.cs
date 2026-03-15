namespace SessionGuard;

/// <summary>
/// 禁止時間帯を定義するクラス
/// </summary>
public class ProhibitedTimeWindow
{
    /// <summary>
    /// 禁止時間帯の開始時刻 (HH:mm形式)
    /// </summary>
    public string StartTime { get; set; } = "00:00";

    /// <summary>
    /// 禁止時間帯の終了時刻 (HH:mm形式)
    /// </summary>
    public string EndTime { get; set; } = "00:00";

    /// <summary>
    /// この禁止時間帯の備考
    /// </summary>
    public string Memo { get; set; } = string.Empty;
}

/// <summary>
/// ユーザー個別の設定を保持するクラス
/// </summary>
public class UserConfig
{
    /// <summary>
    /// 強制ログアウト対象ユーザー名（domain\user, user@domain, user のいずれかの形式）
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 1日の最大累積利用時間（分単位）。0以下の場合は上限なし
    /// </summary>
    public int MaxDailyUsageMinutes { get; set; } = 120;

    /// <summary>
    /// 禁止時間帯のリスト（複数設定可能）
    /// </summary>
    public List<ProhibitedTimeWindow> ProhibitedTimeWindows { get; set; } = new();
}

/// <summary>
/// セッション管理の設定を保持するクラス
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// ユーザー別設定のリスト。ここに記載されたユーザーのみ強制ログアウト判定の対象になります
    /// </summary>
    public List<UserConfig> Users { get; set; } = new();

    /// <summary>
    /// ログアウト実行を有効にするかどうか
    /// </summary>
    public bool EnableLogout { get; set; } = true;

    /// <summary>
    /// ステータス確認間隔（秒単位）
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;
}
