using System.Diagnostics;

namespace SessionGuard;

/// <summary>
/// システムのログアウト処理を実行するクラス
/// </summary>
public class LogoutHandler
{
    private readonly ILogger _logger;

    public LogoutHandler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// ユーザーをログアウトさせる
    /// </summary>
    public async Task LogoutUserAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to logout user...");

            // Windows コマンドでログアウト実行
            // logoff コマンドは現在のセッションをログアウト
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c logoff",
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    // プロセスの終了を待つ（最大5秒）
                    var completed = process.WaitForExit(5000);
                    if (completed)
                    {
                        _logger.LogInformation("User logout command executed successfully.");
                    }
                    else
                    {
                        _logger.LogWarning("Logout command did not complete within timeout period.");
                        process.Kill();
                    }
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing logout command.");
        }
    }

    /// <summary>
    /// ユーザーに警告メッセージを表示してからログアウト
    /// </summary>
    public async Task LogoutUserWithWarningAsync(string reason)
    {
        try
        {
            _logger.LogInformation("Displaying logout warning message: {reason}", reason);

            // Windows メッセージボックスで警告を表示
            var processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c msg * \"{reason}\"",
                RedirectStandardOutput = false,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process != null)
                {
                    process.WaitForExit(3000);
                }
            }

            // 1秒待機してからログアウト
            await Task.Delay(1000);

            await LogoutUserAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying logout warning message.");
        }
    }
}
