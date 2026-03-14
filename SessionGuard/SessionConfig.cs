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
/// セッション管理の設定を保持するクラス
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// 強制ログアウト対象ユーザーのリスト
    /// ここに記載されたユーザーのみ強制ログアウト判定の対象になります
    /// 空の場合は誰も対象になりません
    /// </summary>
    public List<string> TargetUsers { get; set; } = new();

    /// <summary>
    /// 禁止時間帯のリスト（複数設定可能）
    /// </summary>
    public List<ProhibitedTimeWindow> ProhibitedTimeWindows { get; set; } = new();

    /// <summary>
    /// 1日の最大累積利用時間（分単位）
    /// この時間を超えた場合、ログアウトする
    /// </summary>
    public int MaxDailyUsageMinutes { get; set; } = 120;

    /// <summary>
    /// ログアウト実行を有効にするかどうか
    /// </summary>
    public bool EnableLogout { get; set; } = true;

    /// <summary>
    /// ステータス確認間隔（秒単位）
    /// </summary>
    public int CheckIntervalSeconds { get; set; } = 60;
}
