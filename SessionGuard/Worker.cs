using Microsoft.Extensions.Options;

namespace SessionGuard;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IOptionsMonitor<SessionConfig> _configMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LogoutHandler _logoutHandler;
    private readonly Dictionary<string, SessionManager> _sessionManagers =
        new(StringComparer.OrdinalIgnoreCase);
    private IDisposable? _configChangeToken;
    private string? _previousUserName;   // 直前チェック時のユーザー名（null = 未ログイン）

    public Worker(ILogger<Worker> logger, IOptionsMonitor<SessionConfig> configMonitor, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _logoutHandler = new LogoutHandler(loggerFactory.CreateLogger("LogoutHandler"));
        _configChangeToken = _configMonitor.OnChange(OnConfigChanged);
    }

    private void OnConfigChanged(SessionConfig newConfig)
    {
        _logger.LogInformation("Configuration changed.");
        _logger.LogInformation(
            "Users: {count}人, CheckInterval: {interval}s",
            newConfig.Users?.Count ?? 0,
            newConfig.CheckIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configMonitor.CurrentValue;

        _logger.LogInformation("SessionGuard service started at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _logger.LogInformation(
            "Users: {count}人, CheckInterval: {interval}s",
            config.Users?.Count ?? 0,
            config.CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentConfig = _configMonitor.CurrentValue;
                var currentUserName = WindowsSessionHelper.GetSessionUserName();

                // ユーザー変更検出
                if (currentUserName != _previousUserName)
                {
                    // 前のユーザーの利用時間を累積
                    if (!string.IsNullOrEmpty(_previousUserName) &&
                        _sessionManagers.TryGetValue(_previousUserName, out var prevManager))
                    {
                        prevManager.AccumulateUptimeAndReset();
                        _logger.LogInformation("User '{user}' session ended. Accumulated usage saved.", _previousUserName);
                    }

                    // 新しいユーザーのセッション開始
                    if (!string.IsNullOrEmpty(currentUserName))
                    {
                        bool existed = _sessionManagers.ContainsKey(currentUserName);
                        var manager = GetOrCreateSessionManager(currentUserName);
                        if (existed)
                        {
                            manager.StartNewSession();
                            _logger.LogInformation("User '{user}' re-logged in. Accumulated usage preserved.", currentUserName);
                        }
                        else
                        {
                            _logger.LogInformation("User '{user}' logged in. New session started.", currentUserName);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("User logged out.");
                    }

                    _previousUserName = currentUserName;
                }

                // 対象ユーザーか判定
                var userConfig = FindUserConfig(currentConfig, currentUserName);
                if (userConfig == null)
                {
                    _logger.LogInformation("Current user '{user}' is not in target users list. Skipping logout check.",
                        currentUserName ?? "(none)");
                    await Task.Delay(currentConfig.CheckIntervalSeconds * 1000, stoppingToken);
                    continue;
                }

                // ステータスログ
                var sessionManager = GetOrCreateSessionManager(currentUserName!);
                sessionManager.LogStatus();

                // ログアウト条件判定
                if (sessionManager.ShouldLogout(userConfig, currentConfig.EnableLogout))
                {
                    _logger.LogCritical("Logout condition detected for user '{user}'. Logging out...", currentUserName);
                    await _logoutHandler.LogoutUserWithWarningAsync(
                        "ご使用のセッションが終了します。\n" +
                        "設定された条件に基づいてログアウトします。"
                    );
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
    /// ユーザー名に対応する SessionManager を取得または新規作成
    /// </summary>
    private SessionManager GetOrCreateSessionManager(string userName)
    {
        if (!_sessionManagers.TryGetValue(userName, out var manager))
        {
            var logger = _loggerFactory.CreateLogger($"SessionManager[{userName}]");
            manager = new SessionManager(logger);
            _sessionManagers[userName] = manager;
        }
        return manager;
    }

    /// <summary>
    /// 現在のユーザーの UserConfig を取得（対象外なら null）
    /// </summary>
    private UserConfig? FindUserConfig(SessionConfig config, string? userName)
    {
        if (string.IsNullOrEmpty(userName) || config.Users == null || config.Users.Count == 0)
            return null;

        var domain = WindowsSessionHelper.GetSessionUserDomain() ?? string.Empty;

        return config.Users.FirstOrDefault(u =>
            string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.UserName, $"{domain}\\{userName}", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.UserName, $"{userName}@{domain}", StringComparison.OrdinalIgnoreCase));
    }

    public override void Dispose()
    {
        _configChangeToken?.Dispose();
        base.Dispose();
    }
}
