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
    private string? _previousUserName;  // ユーザー変更検出用

    public Worker(ILogger<Worker> logger, IOptionsMonitor<SessionConfig> configMonitor, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        
        // 設定変更時の監視をセットアップ
        _configChangeToken = _configMonitor.OnChange(OnConfigChanged);
        
        // 初期化
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
        _logger.LogInformation("Configuration changed. Updating SessionManager...");
        
        if (newConfig.LogoutTimeWindows != null && newConfig.LogoutTimeWindows.Count > 0)
        {
            _logger.LogInformation("Logout time windows updated: {count} window(s)", newConfig.LogoutTimeWindows.Count);
            foreach (var window in newConfig.LogoutTimeWindows)
            {
                _logger.LogInformation("  - {startTime}～{endTime}: {description}",
                    window.StartTime, window.EndTime, window.Description);
            }
        }
        _logger.LogInformation("MaxUptime: {max}h", newConfig.MaxContinuousUptime);
        
        // セッションマネージャーを再初期化（新しい設定で）
        // 既存のセッション情報は保持される
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configMonitor.CurrentValue;
        
        _logger.LogInformation("SessionGuard service started at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        
        if (config.LogoutTimeWindows != null && config.LogoutTimeWindows.Count > 0)
        {
            _logger.LogInformation("Configured logout time windows: {count}", config.LogoutTimeWindows.Count);
            foreach (var window in config.LogoutTimeWindows)
            {
                _logger.LogInformation("  - {startTime}～{endTime}: {description}",
                    window.StartTime, window.EndTime, window.Description);
            }
        }
        _logger.LogInformation("MaxContinuousUptime: {max}h, CheckInterval: {interval}s",
            config.MaxContinuousUptime, config.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 現在の設定を取得（リアルタイム）
                var currentConfig = _configMonitor.CurrentValue;

                // セッションマネージャーが初期化されている確認
                if (_sessionManager == null || _logoutHandler == null)
                {
                    _logger.LogError("SessionManager or LogoutHandler is not initialized.");
                    await Task.Delay(currentConfig.CheckInterval * 1000, stoppingToken);
                    continue;
                }

                // デバッグ用：セッション情報をログに出力
                _logoutHandler.LogSessionInformation();

                // セッションステータスをログに出力
                _sessionManager.LogStatus();

                // ユーザー変更検出：ユーザーが変更されたら SessionManager をリセット
                var currentUserName = WindowsSessionHelper.GetSessionUserName();
                if (currentUserName != _previousUserName)
                {
                    if (!string.IsNullOrEmpty(currentUserName))
                    {
                        _logger.LogInformation("Session user changed from '{oldUser}' to '{newUser}'. Resetting SessionManager.", 
                            _previousUserName ?? "[no user]", currentUserName);
                        InitializeManagers(_loggerFactory);
                    }
                    _previousUserName = currentUserName;
                }

                // 現在のユーザーが対象ユーザーか判定
                if (!IsTargetUser(currentConfig))
                {
                    _logger.LogWarning("Current user is not in target users list. Skipping logout check.");
                    await Task.Delay(currentConfig.CheckInterval * 1000, stoppingToken);
                    continue;
                }

                // ログアウトが必要かを判定（常に最新の設定を使用）
                if (_sessionManager.ShouldLogout(currentConfig))
                {
                    _logger.LogCritical("Logout condition detected. Logging out user...");
                    await _logoutHandler.LogoutUserWithWarningAsync(
                        "ご使用のセッションが終了します。\n" +
                        "設定された条件に基づいてログアウトします。"
                    );
                    
                    // ログアウト後、稼働時間を累積して新しいセッション開始時刻をリセット
                    // サービスは継続稼働し、再ログイン時に判定を再開
                    _sessionManager.AccumulateUptimeAndReset();
                    _logger.LogInformation("Session uptime accumulated. Waiting for new session logon...");
                }

                // 設定された間隔で待機
                await Task.Delay(currentConfig.CheckInterval * 1000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SessionGuard service cancellation requested.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SessionGuard worker execution.");
                // エラーが発生した場合でも、サービスを継続させる
                var currentConfig = _configMonitor.CurrentValue;
                await Task.Delay(currentConfig.CheckInterval * 1000, stoppingToken);
            }
        }

        _logger.LogInformation("SessionGuard service stopped at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>
    /// 現在のユーザーが強制ログアウト対象ユーザーか判定
    /// </summary>
    private bool IsTargetUser(SessionConfig config)
    {
        // ターゲットユーザーリストが空の場合は、すべてのユーザーが対象
        if (config.TargetUsers == null || config.TargetUsers.Count == 0)
        {
            return true;
        }

        try
        {
            _logger.LogWarning("=== IsTargetUser Check Started ===");

            // WTSApi32を使ってセッション内のログインユーザーを取得
            var sessionUserName = WindowsSessionHelper.GetSessionUserName();
            var sessionUserDomain = WindowsSessionHelper.GetSessionUserDomain();

            _logger.LogWarning("GetSessionUserName returned: {userName}", sessionUserName ?? "NULL");
            _logger.LogWarning("GetSessionUserDomain returned: {domain}", sessionUserDomain ?? "NULL");

            if (string.IsNullOrEmpty(sessionUserName))
            {
                _logger.LogWarning("Could not retrieve session user name from Windows API. Treating as target user (safe default).");
                // ユーザー名取得に失敗した場合は、安全側に振って対象ユーザーと判定
                return true;
            }

            var sessionUserWithDomain = $"{sessionUserDomain}\\{sessionUserName}";

            _logger.LogWarning("Current session user: {user}, Domain: {domain}, Combined: {combined}", 
                sessionUserName, sessionUserDomain ?? "N/A", sessionUserWithDomain);

            // 対象ユーザーリストで検索
            // "user"、"DOMAIN\user"、"user@domain" など複数の形式に対応
            foreach (var targetUser in config.TargetUsers)
            {
                _logger.LogWarning("Checking against target user: {target}", targetUser);

                if (string.Equals(targetUser, sessionUserName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical("✓ MATCH (username only): {sessionUser} == {targetUser}", sessionUserName, targetUser);
                    _logger.LogWarning("=== IsTargetUser Check Complete: TRUE ===");
                    return true;
                }

                if (string.Equals(targetUser, sessionUserWithDomain, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical("✓ MATCH (with domain): {sessionUser} == {targetUser}", sessionUserWithDomain, targetUser);
                    _logger.LogWarning("=== IsTargetUser Check Complete: TRUE ===");
                    return true;
                }

                if (string.Equals(targetUser, $"{sessionUserName}@{sessionUserDomain}", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogCritical("✓ MATCH (email format): {sessionUser} == {targetUser}", $"{sessionUserName}@{sessionUserDomain}", targetUser);
                    _logger.LogWarning("=== IsTargetUser Check Complete: TRUE ===");
                    return true;
                }
            }

            _logger.LogWarning("✗ NO MATCH: Current session user '{user}' is NOT in target users list.", sessionUserName);
            _logger.LogWarning("=== IsTargetUser Check Complete: FALSE ===");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking target user. Treating as target user (safe default).");
            // エラー時は対象ユーザーと判定して安全側に振る
            return true;
        }
    }



    public override void Dispose()
    {
        _configChangeToken?.Dispose();
        base.Dispose();
    }
}
