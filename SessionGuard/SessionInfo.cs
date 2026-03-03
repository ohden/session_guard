namespace SessionGuard;

/// <summary>
/// ユーザーセッションの情報を管理するクラス
/// </summary>
public class SessionInfo
{
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
    /// 現在の連続稼働時間（時間単位）
    /// </summary>
    public double CurrentUptimeHours
    {
        get
        {
            var elapsed = DateTime.Now - StartTime;
            return elapsed.TotalHours;
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

    /// <summary>
    /// セッション情報を初期化
    /// </summary>
    public void Initialize()
    {
        StartTime = DateTime.Now;
        StartDate = DateOnly.FromDateTime(DateTime.Now);
        LastResetTime = DateTime.Now;
    }

    /// <summary>
    /// 日付変更時にリセット
    /// </summary>
    public void ResetForNewDay()
    {
        StartDate = DateOnly.FromDateTime(DateTime.Now);
        StartTime = DateTime.Now;
        LastResetTime = DateTime.Now;
    }
}
