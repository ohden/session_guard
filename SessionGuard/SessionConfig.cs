namespace SessionGuard;

/// <summary>
/// ログアウト時間帯を定義するクラス
/// </summary>
public class LogoutTimeWindow
{
    /// <summary>
    /// 時間帯の開始時刻 (HH:mm形式)
    /// </summary>
    public string StartTime { get; set; } = "00:00";

    /// <summary>
    /// 時間帯の終了時刻 (HH:mm形式)
    /// </summary>
    public string EndTime { get; set; } = "23:59";

    /// <summary>
    /// この時間帯の説明
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// セッション管理の設定を保持するクラス
/// </summary>
public class SessionConfig
{
    /// <summary>
    /// ログアウト対象の時間帯リスト
    /// 複数の時間帯を設定可能
    /// 例: 18:00～09:00, 12:00～13:00 (昼休み)
    /// </summary>
    public List<LogoutTimeWindow> LogoutTimeWindows { get; set; } = new();

    /// <summary>
    /// 最大連続稼働時間（時間単位、小数対応）
    /// この時間を超えて稼働している場合、ログアウトする
    /// 例: 1 (1時間), 0.5 (30分), 1.5 (1時間30分)
    /// </summary>
    public double MaxContinuousUptime { get; set; } = 8;

    /// <summary>
    /// ログアウト実行を有効にするかどうか
    /// </summary>
    public bool EnableLogout { get; set; } = true;

    /// <summary>
    /// ステータス確認間隔（秒単位）
    /// </summary>
    public int CheckInterval { get; set; } = 60;

    /// <summary>
    /// 強制ログアウト対象ユーザーのリスト
    /// ここに記載されたユーザーのみ強制ログアウト判定の対象になります
    /// 例: ["user1", "user2", "DOMAIN\\user3"]
    /// 空の場合はすべてのユーザーが対象になります
    /// </summary>
    public List<string> TargetUsers { get; set; } = new();
}
