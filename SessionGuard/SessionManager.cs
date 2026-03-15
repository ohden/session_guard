namespace SessionGuard;

/// <summary>
/// セッション管理とログアウト判定を行うクラス（ユーザー1人分）
/// </summary>
public class SessionManager
{
    private static readonly TimeZoneInfo JstZone =
        TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    private static TimeSpan NowJstTimeOfDay =>
        TimeZoneInfo.ConvertTime(DateTime.UtcNow, JstZone).TimeOfDay;

    private readonly ILogger _logger;
    private readonly SessionInfo _sessionInfo;

    public SessionManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionInfo = new SessionInfo(logger);
        _sessionInfo.Initialize();
    }

    public SessionInfo GetSessionInfo() => _sessionInfo;

    /// <summary>
    /// ログアウトが必要かどうかを判定
    /// </summary>
    public bool ShouldLogout(UserConfig userConfig, bool enableLogout)
    {
        if (userConfig == null)
            throw new ArgumentNullException(nameof(userConfig));

        // 日付（JST 0時）が変更された場合はリセット
        if (_sessionInfo.IsDayChanged)
        {
            _logger.LogInformation("Day changed (JST midnight). Resetting accumulated usage.");
            _sessionInfo.ResetForNewDay();
        }

        // 禁止時間帯チェック
        if (IsWithinProhibitedTimeWindow(userConfig, enableLogout))
        {
            _logger.LogWarning("Current time is within prohibited time window. Logout required.");
            return true;
        }

        // 累積利用時間上限チェック
        if (HasExceededMaxDailyUsage(userConfig))
        {
            _logger.LogWarning("Daily usage limit exceeded. Logout required.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 現在の時刻（JST）がいずれかの禁止時間帯内にあるかを判定
    /// </summary>
    private bool IsWithinProhibitedTimeWindow(UserConfig userConfig, bool enableLogout)
    {
        if (!enableLogout || userConfig.ProhibitedTimeWindows == null || userConfig.ProhibitedTimeWindows.Count == 0)
            return false;

        var now = NowJstTimeOfDay;

        foreach (var window in userConfig.ProhibitedTimeWindows)
        {
            try
            {
                var startTime = TimeSpan.Parse(window.StartTime);
                var endTime = TimeSpan.Parse(window.EndTime);

                // 開始と終了が同じ場合はスキップ
                if (startTime == endTime)
                    continue;

                bool isInWindow;
                if (startTime > endTime)
                {
                    // 日をまたぐ場合 (例: 22:00～08:00)
                    isInWindow = now >= startTime || now < endTime;
                }
                else
                {
                    // 日をまたがない場合 (例: 12:00～13:00)
                    isInWindow = now >= startTime && now < endTime;
                }

                if (isInWindow)
                {
                    _logger.LogWarning("Prohibited time window matched: {start}～{end}", window.StartTime, window.EndTime);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing prohibited time window: {start}～{end}", window.StartTime, window.EndTime);
            }
        }

        return false;
    }

    /// <summary>
    /// 当日の累積利用時間が上限を超えているかを判定
    /// </summary>
    private bool HasExceededMaxDailyUsage(UserConfig userConfig)
    {
        if (userConfig.MaxDailyUsageMinutes <= 0)
            return false;

        var currentUsage = _sessionInfo.CurrentUsageMinutes;
        var exceeded = currentUsage >= userConfig.MaxDailyUsageMinutes;

        _logger.LogInformation(
            "Usage check - Current: {current:F1}min, Limit: {limit}min, Exceeded: {exceeded}",
            currentUsage, userConfig.MaxDailyUsageMinutes, exceeded);

        return exceeded;
    }

    /// <summary>
    /// ステータス情報をログに出力
    /// </summary>
    public void LogStatus()
    {
        var nowJst = TimeZoneInfo.ConvertTime(DateTime.UtcNow, JstZone);
        var accumulated = _sessionInfo.AccumulatedUsageMinutes;
        var currentSessionElapsed = nowJst - _sessionInfo.StartTime;
        var totalToday = _sessionInfo.CurrentUsageMinutes;

        _logger.LogInformation(
            "Session Status - StartTime: {startTime} (JST), Accumulated: {accumulated:F1}min, Current Session: {currentSession:F1}min, Total Today: {total:F1}min",
            _sessionInfo.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            accumulated,
            currentSessionElapsed.TotalMinutes,
            totalToday);
    }

    /// <summary>
    /// 同じユーザーが再ログインした際に呼び出す（累積利用時間を保持）
    /// </summary>
    public void StartNewSession()
    {
        _sessionInfo.StartNewSession();
    }

    /// <summary>
    /// ログアウト時に現在のセッション利用時間を累積してリセット
    /// </summary>
    public void AccumulateUptimeAndReset()
    {
        _sessionInfo.AccumulateAndResetCurrentSession();
    }
}
