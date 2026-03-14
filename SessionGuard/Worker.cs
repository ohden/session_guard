using Microsoft.Extensions.Options;

namespace SessionGuard;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IOptionsMonitor<SessionConfig> _configMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private SessionManager? _sessionManager;
    private LogoutHandler? _logoutHandler;
    private IDisposable? _configChangeToken;
    private string? _previousUserName;   // 直前チェック時のユーザー名（null = 未ログイン）
    private string? _lastLoggedInUser;   // 最後にログインしていたユーザー名

    public Worker(ILogger<Worker> logger, IOptionsMonitor<SessionConfig> configMonitor, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _configChangeToken = _configMonitor.OnChange(OnConfigChanged);
        InitializeManagers(loggerFactory);
    }

    private void InitializeManagers(ILoggerFactory loggerFactory)
    {
        var sessionManagerLogger = loggerFactory.CreateLogger("SessionManager");
        var logoutHandlerLogger = loggerFactory.CreateLogger("LogoutHandler");

        _sessionManager = new SessionManager(_configMonitor.CurrentValue, sessionManagerLogger);
        _logoutHandler = new LogoutHandler(logoutHandlerLogger);
    }

    private void OnConfigChanged(SessionConfig newConfig)
    {
        _logger.LogInformation("Configuration changed.");
        _logger.LogInformation(
            "ProhibitedTimeWindows: {count}件, MaxDailyUsage: {max}min, CheckInterval: {interval}s",
            newConfig.ProhibitedTimeWindows?.Count ?? 0,
            newConfig.MaxDailyUsageMinutes, newConfig.CheckIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configMonitor.CurrentValue;

        _logger.LogInformation("SessionGuard service started at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _logger.LogInformation(
            "ProhibitedTimeWindows: {count}件, MaxDailyUsage: {max}min, CheckInterval: {interval}s",
            config.ProhibitedTimeWindows?.Count ?? 0,
            config.MaxDailyUsageMinutes, config.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentConfig = _configMonitor.CurrentValue;

                if (_sessionManager == null || _logoutHandler == null)
                {
                    _logger.LogError("SessionManager or LogoutHandler is not initialized.");
                    await Task.Delay(currentConfig.CheckIntervalSeconds * 1000, stoppingToken);
                    continue;
                }

                _sessionManager.LogStatus();

                // ユーザー変更検出
                var currentUserName = WindowsSessionHelper.GetSessionUserName();
                if (currentUserName != _previousUserName)
                {
                    if (string.IsNullOrEmpty(currentUserName))
                    {
                        // ログアウト検出：現在のセッション時間を累積しておく
                        _sessionManager.AccumulateUptimeAndReset();
                        _logger.LogInformation("User logged out. Accumulated usage saved.");
                    }
                    else if (string.IsNullOrEmpty(_previousUserName))
                    {
                        // ログイン検出
                        if (currentUserName == _lastLoggedInUser)
                        {
                            // 同じユーザーが再ログイン：累積利用時間を保持してセッション開始時刻のみリセット
                            _sessionManager.StartNewSession();
                            _logger.LogInformation(
                                "User '{user}' re-logged in. Accumulated usage preserved.", currentUserName);
                        }
                        else
                        {
                            // 別ユーザーがログイン：完全リセット
                            InitializeManagers(_loggerFactory);
                            _logger.LogInformation(
                                "Different user '{user}' logged in. SessionManager reset.", currentUserName);
                        }
                        _lastLoggedInUser = currentUserName;
                    }
                    else
                    {
                        // ログアウトなしでユーザーが直接切り替わった場合：完全リセット
                        InitializeManagers(_loggerFactory);
                        _lastLoggedInUser = currentUserName;
                        _logger.LogInformation(
                            "User changed to '{user}'. SessionManager reset.", currentUserName);
                    }

                    _previousUserName = currentUserName;
                }

                // 対象ユーザーか判定
                if (!IsTargetUser(currentConfig))
                {
                    _logger.LogInformation("Current user is not in target users list. Skipping logout check.");
                    await Task.Delay(currentConfig.CheckIntervalSeconds * 1000, stoppingToken);
                    continue;
                }

                // ログアウト条件判定
                if (_sessionManager.ShouldLogout(currentConfig))
                {
                    _logger.LogCritical("Logout condition detected. Logging out user...");
                    await _logoutHandler.LogoutUserWithWarningAsync(
                        "ご使用のセッションが終了します。\n" +
                        "設定された条件に基づいてログアウトします。"
                    );
                    // ログアウト後の累積はユーザーログアウト検出時（次のチェック）に行われる
                }

                await Task.Delay(currentConfig.CheckIntervalSeconds * 1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SessionGuard service cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SessionGuard worker execution.");
                var currentConfig = _configMonitor.CurrentValue;
                await Task.Delay(currentConfig.CheckIntervalSeconds * 1000, stoppingToken);
            }
        }

        _logger.LogInformation("SessionGuard service stopped at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>
    /// 現在のユーザーが強制ログアウト対象ユーザーか判定
    /// </summary>
    private bool IsTargetUser(SessionConfig config)
    {
        // TargetUsers が空の場合は誰も対象にしない
        if (config.TargetUsers == null || config.TargetUsers.Count == 0)
        {
            return false;
        }

        try
        {
            var sessionUserName = WindowsSessionHelper.GetSessionUserName();
            var sessionUserDomain = WindowsSessionHelper.GetSessionUserDomain();

            if (string.IsNullOrEmpty(sessionUserName))
                return false;

            var sessionUserWithDomain = $"{sessionUserDomain}\\{sessionUserName}";

            foreach (var targetUser in config.TargetUsers)
            {
                if (string.Equals(targetUser, sessionUserName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetUser, sessionUserWithDomain, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(targetUser, $"{sessionUserName}@{sessionUserDomain}", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking target user. Skipping logout check.");
            return false;
        }
    }

    public override void Dispose()
    {
        _configChangeToken?.Dispose();
        base.Dispose();
    }
}
