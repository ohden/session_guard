using Microsoft.Extensions.Options;

namespace SessionGuard;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IOptionsMonitor<SessionConfig> _configMonitor;
    private SessionManager? _sessionManager;
    private LogoutHandler? _logoutHandler;
    private IDisposable? _configChangeToken;

    public Worker(ILogger<Worker> logger, IOptionsMonitor<SessionConfig> configMonitor, ILoggerFactory loggerFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configMonitor = configMonitor ?? throw new ArgumentNullException(nameof(configMonitor));
        
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
        _logger.LogInformation("New Configuration - LogoutStartTime: {start}, LogoutEndTime: {end}, MaxUptime: {max}h",
            newConfig.LogoutStartTime, newConfig.LogoutEndTime, newConfig.MaxContinuousUptime);
        
        // セッションマネージャーを再初期化（新しい設定で）
        // 既存のセッション情報は保持される
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _configMonitor.CurrentValue;
        
        _logger.LogInformation("SessionGuard service started at {time}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        _logger.LogInformation("Configuration - LogoutStartTime: {start}, LogoutEndTime: {end}, MaxUptime: {max}h",
            config.LogoutStartTime, config.LogoutEndTime, config.MaxContinuousUptime);

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

                // セッションステータスをログに出力
                _sessionManager.LogStatus();

                // ログアウトが必要かを判定（常に最新の設定を使用）
                if (_sessionManager.ShouldLogout(currentConfig))
                {
                    _logger.LogCritical("Logout condition detected. Logging out user...");
                    await _logoutHandler.LogoutUserWithWarningAsync(
                        "ご使用のセッションが終了します。\n" +
                        "設定された条件に基づいてログアウトします。"
                    );
                    break; // ログアウト後、サービスを停止
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

    public override void Dispose()
    {
        _configChangeToken?.Dispose();
        base.Dispose();
    }
}
