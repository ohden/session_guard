using System.Diagnostics;

namespace SessionGuard;

/// <summary>
/// セッション管理とログアウト判定を行うクラス
/// </summary>
public class SessionManager
{
    private readonly ILogger _logger;
    private readonly SessionInfo _sessionInfo;

    public SessionManager(SessionConfig config, ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sessionInfo = new SessionInfo(logger);
        _sessionInfo.Initialize();
    }

    /// <summary>
    /// 現在のセッション情報を取得
    /// </summary>
    public SessionInfo GetSessionInfo() => _sessionInfo;

    /// <summary>
    /// ログアウトが必要かどうかを判定（設定をパラメータで受け取る）
    /// </summary>
    public bool ShouldLogout(SessionConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        // 日付が変更された場合はリセット
        if (_sessionInfo.IsDayChanged)
        {
            _logger.LogInformation("Day changed. Resetting session uptime.");
            _sessionInfo.ResetForNewDay();
        }

        // 設定時間帯内かどうかを確認
        if (IsWithinLogoutTimeWindow(config))
        {
            _logger.LogWarning("Current time is within logout time window. Logout required.");
            return true;
        }

        // 最大稼働時間を超えているかどうかを確認
        if (HasExceededMaxUptime(config))
        {
            _logger.LogWarning("Maximum uptime exceeded. Logout required.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 現在の時刻がログアウト設定時間帯内にあるかを判定
    /// </summary>
    private bool IsWithinLogoutTimeWindow(SessionConfig config)
    {
        if (!config.EnableLogout || config.LogoutTimeWindows == null || config.LogoutTimeWindows.Count == 0)
            return false;

        try
        {
            var now = DateTime.Now.TimeOfDay;

            // 設定されている全ての時間帯をチェック
            foreach (var window in config.LogoutTimeWindows)
            {
                var startTime = TimeSpan.Parse(window.StartTime);
                var endTime = TimeSpan.Parse(window.EndTime);

                // 時間帯の判定
                bool isInWindow;
                if (startTime > endTime)
                {
                    // 日をまたぐ場合 (例: 18:00～09:00)
                    isInWindow = now >= startTime || now < endTime;
                }
                else
                {
                    // 日をまたがない場合 (例: 12:00～13:00)
                    isInWindow = now >= startTime && now < endTime;
                }

                // 任意の時間帯に該当したらログアウト対象
                if (isInWindow)
                {
                    if (!string.IsNullOrEmpty(window.Description))
                    {
                        _logger.LogWarning("Current time is within logout time window: {description}", window.Description);
                    }
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing logout time window configuration.");
            return false;
        }
    }

    /// <summary>
    /// 最大稼働時間を超えているかを判定
    /// </summary>
    private bool HasExceededMaxUptime(SessionConfig config)
    {
        if (config.MaxContinuousUptime <= 0)
            return false;

        var currentUptime = _sessionInfo.CurrentUptimeHours;
        return currentUptime >= config.MaxContinuousUptime;
    }

    /// <summary>
    /// ステータス情報をログに出力
    /// </summary>
    public void LogStatus()
    {
        var currentUptime = _sessionInfo.CurrentUptimeHours;
        var startTime = _sessionInfo.StartTime;

        _logger.LogInformation(
            "Session Status - StartTime: {startTime}, CurrentUptime: {uptime:F2}h",
            startTime.ToString("yyyy-MM-dd HH:mm:ss"),
            currentUptime
        );
    }
}
