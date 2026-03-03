using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SessionGuard;

/// <summary>
/// Windowsセッション管理用P/Invokeラッパー
/// </summary>
public static class WindowsSessionHelper
{
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(IntPtr hServer, uint reserved, uint version, out IntPtr ppSessionInfo, out uint pCount);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern void WTSFreeMemory(IntPtr pMemory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSLogoffSession(IntPtr hServer, uint sessionId, bool bWait);

    [DllImport("wtsapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr hServer, uint sessionId, WtsInfoClass wtsInfoClass, 
        out IntPtr ppBuffer, out uint pBytesReturned);

    // セッション情報の構造体
    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public uint SessionId;
        public IntPtr pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }

    // セッション状態
    private enum WTS_CONNECTSTATE_CLASS
    {
        Active,
        Connected,
        ConnectQuery,
        Shadow,
        Disconnected,
        Idle,
        Listen,
        Reset,
        Down,
        Init
    }

    // セッション情報クラス
    private enum WtsInfoClass
    {
        InitialProgram,
        ApplicationName,
        WorkingDirectory,
        OemId,
        LogonUser,
        LogonDomain,
        LogonTime,
        LogoffTime,
        ErrorTime,
        SessionState,
        UserFlags
    }

    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    /// <summary>
    /// アクティブなセッションIDを取得
    /// </summary>
    public static uint? GetActiveSessionId()
    {
        uint count = 0;
        IntPtr pSessionInfo = IntPtr.Zero;

        try
        {
            if (WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out pSessionInfo, out count))
            {
                int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));

                for (int i = 0; i < count; i++)
                {
                    IntPtr sessionInfoPtr = new IntPtr(pSessionInfo.ToInt64() + (dataSize * i));
                    WTS_SESSION_INFO sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(sessionInfoPtr, typeof(WTS_SESSION_INFO));

                    // Activeなセッションを検索
                    if (sessionInfo.State == WTS_CONNECTSTATE_CLASS.Active)
                    {
                        return sessionInfo.SessionId;
                    }
                }
            }

            return null;
        }
        finally
        {
            if (pSessionInfo != IntPtr.Zero)
                WTSFreeMemory(pSessionInfo);
        }
    }

    /// <summary>
    /// セッションをログオフ
    /// </summary>
    public static bool LogoffSession(uint sessionId)
    {
        return WTSLogoffSession(WTS_CURRENT_SERVER_HANDLE, sessionId, false);
    }

    /// <summary>
    /// アクティブセッションのログイン時刻を取得
    /// </summary>
    public static DateTime? GetSessionLogonTime()
    {
        var sessionId = GetActiveSessionId();
        if (!sessionId.HasValue)
            return null;

        try
        {
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId.Value, WtsInfoClass.LogonTime,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    // FILETIME構造体を取得
                    if (bytesReturned >= 8)
                    {
                        long fileTime = Marshal.ReadInt64(pBuffer);
                        if (fileTime > 0)
                        {
                            // FILETIMEをDateTimeに変換
                            return DateTime.FromFileTime(fileTime);
                        }
                    }
                    return null;
                }
                finally
                {
                    WTSFreeMemory(pBuffer);
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            // エラーログはLogoutHandlerで記録されるため、ここではスローしない
            return null;
        }
    }
}

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

            // アクティブなセッションIDを取得（WTSApi32を使用）
            var sessionId = WindowsSessionHelper.GetActiveSessionId();
            
            if (sessionId.HasValue)
            {
                _logger.LogInformation("Active session found: Session ID {sessionId}", sessionId.Value);
                
                // WTSLogoffSessionを使用してセッションをログオフ
                bool logoffResult = WindowsSessionHelper.LogoffSession(sessionId.Value);
                
                if (logoffResult)
                {
                    _logger.LogInformation("Session {sessionId} logoff command successfully issued.", sessionId.Value);
                }
                else
                {
                    _logger.LogError("Failed to logoff session {sessionId}. Error: {error}", sessionId.Value, Marshal.GetLastWin32Error());
                }
            }
            else
            {
                _logger.LogWarning("No active session found to logoff.");
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
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
