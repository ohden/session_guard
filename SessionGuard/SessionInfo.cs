namespace SessionGuard;

/// <summary>
/// ユーザーセッションの情報を管理するクラス
/// </summary>
public class SessionInfo
{
    private readonly ILogger _logger;

    /// <summary>
    /// セッション開始時刻
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// セッション開始日付（日付変更の判定用）
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// 連続稼働時間をリセットした最後の時刻
    /// </summary>
    public DateTime LastResetTime { get; set; }

    /// <summary>
    /// 同じ日内での累計稼働時間（時間単位）
    /// ログアウト→ログインしても、日付が変わるまで累積される
    /// </summary>
    public double AccumulatedUptimeHours { get; set; } = 0.0;

    /// <summary>
    /// 現在の連続稼働時間（時間単位）
    /// = 累計稼働時間 + 現在のセッション稼働時間
    /// </summary>
    public double CurrentUptimeHours
    {
        get
        {
            var currentSessionElapsed = DateTime.Now - StartTime;
            return AccumulatedUptimeHours + currentSessionElapsed.TotalHours;
        }
    }

    /// <summary>
    /// 日付が変更されたかどうかを判定
    /// </summary>
    public bool IsDayChanged
    {
        get
        {
            var currentDate = DateOnly.FromDateTime(DateTime.Now);
            return currentDate != StartDate;
        }
    }

    public SessionInfo(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// セッション情報を初期化（サービス起動時刻を基準に設定）
    /// </summary>
    public void Initialize()
    {
        // サービス起動時刻を基準時刻として設定
        // これにより、サービス起動後は経過時間が0からカウント開始される
        StartTime = DateTime.Now;
        _logger?.LogInformation("Session start time set to service startup time: {startTime}", StartTime.ToString("yyyy-MM-dd HH:mm:ss"));

        StartDate = DateOnly.FromDateTime(DateTime.Now);
        LastResetTime = DateTime.Now;
        // AccumulatedUptimeHours はリセットしない（ログイン時に保持）
    }

    /// <summary>
    /// ログアウト時に現在のセッション稼働時間を累積し、新しいセッションをリセット
    /// </summary>
    public void AccumulateAndResetCurrentSession()
    {
        var currentSessionElapsed = DateTime.Now - StartTime;
        AccumulatedUptimeHours += currentSessionElapsed.TotalHours;

        _logger?.LogInformation(
            "Session accumulated. Previous session: {previousSession:F2}h, Total accumulated: {total:F2}h",
            currentSessionElapsed.TotalHours,
            AccumulatedUptimeHours
        );

        // 新しいセッション開始時刻をリセット
        StartTime = DateTime.Now;
    }

    /// <summary>
    /// 日付変更時にリセット
    /// </summary>
    public void ResetForNewDay()
    {
        StartDate = DateOnly.FromDateTime(DateTime.Now);
        StartTime = DateTime.Now;
        LastResetTime = DateTime.Now;
        AccumulatedUptimeHours = 0.0;  // 日付が変わったら累積時間もリセット

        _logger?.LogInformation("Day changed. Session and accumulated uptime reset.");
    }
}
