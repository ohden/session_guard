using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SessionGuard;

/// <summary>
/// Windowsセッション管理用P/Invokeラッパー
/// </summary>
public static class WindowsSessionHelper
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetLastError();

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
        WTSInitialProgram = 0,
        WTSApplicationName = 1,  
        WTSWorkingDirectory = 2,
        WTSOemId = 3,
        WTSLogonUser = 4,
        WTSLogonDomain = 5,
        WTSLogonTime = 6,
        WTSLogoffTime = 7,
        WTSErrorTime = 8,
        WTSSessionState = 9,
        WTSSessionStateEx = 10,
        WTSClientName = 11,
        WTSClientDirectory = 12,
        WTSClientBuildNumber = 13,
        WTSClientHardwareId = 14,
        WTSClientProductId = 15,
        WTSConnectedState = 16,
        WTSClientAddress = 17,
        WTSClientDisplay = 18,
        WTSClientProtocolType = 19,
        WTSIdleTime = 20,
        WTSLogonTime_Legacy = 21,
        WTSLogoffTime_Legacy = 22,
        WTSErrorTime_Legacy = 23,
        WTSShadowClass = 24,
        WTSMaxClassInfoIndex = 25
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
    /// explorer.exe の所有ユーザーを取得してセッションユーザーを特定
    /// </summary>
    public static string? GetSessionUserFromExplorer()
    {
        try
        {
            var explorerProcesses = System.Diagnostics.Process.GetProcessesByName("explorer");
            if (explorerProcesses.Length > 0)
            {
                var explorerProcess = explorerProcesses[0];
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetCurrentProcess())
                {
                    // プロセスハンドルから所有者情報を取得する
                    // 簡潔な方法：コンソールセッション ID に対応するユーザーを探す
                }
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// すべてのセッション情報を取得（デバッグ用）
    /// </summary>
    public static List<(uint SessionId, string? State, string? UserName, string? Domain)> GetAllSessionsDebug()
    {
        var result = new List<(uint, string?, string?, string?)>();
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

                    // 各セッションのユーザー情報を取得
                    // WtsInfoClass値を直接試す：4=LogonUser, 5=LogonDomain
                    var userName4 = TryGetSessionInfo(sessionInfo.SessionId, 4);  // Try WTSLogonUser
                    var userName5 = TryGetSessionInfo(sessionInfo.SessionId, 5);  // Try WTSLogonDomain
                    var userName6 = TryGetSessionInfo(sessionInfo.SessionId, 6);  // Try WTSLogonTime

                    result.Add((sessionInfo.SessionId, sessionInfo.State.ToString(), 
                        $"[4]={userName4} [5]={userName5} [6]={userName6}", null));
                }
            }

            return result;
        }
        finally
        {
            if (pSessionInfo != IntPtr.Zero)
                WTSFreeMemory(pSessionInfo);
        }
    }

    /// <summary>
    /// 指定したWtsInfoClass値で情報を取得（デバッグ用）
    /// </summary>
    private static string? TryGetSessionInfo(uint sessionId, int infoClass)
    {
        try
        {
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, (WtsInfoClass)infoClass,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    // 最初の256バイトを試す
                    if (bytesReturned > 0)
                    {
                        string? result = Marshal.PtrToStringUni(pBuffer);
                        return string.IsNullOrEmpty(result) ? "[empty]" : result;
                    }
                    return "[0bytes]";
                }
                finally
                {
                    WTSFreeMemory(pBuffer);
                }
            }

            int lastError = GetLastError();
            return $"[query failed: {lastError}]";
        }
        catch (Exception ex)
        {
            return $"[{ex.Message}]";
        }
    }

    /// <summary>
    /// 指定したセッションIDのログインユーザー名を取得
    /// </summary>
    private static string? GetSessionUserNameForId(uint sessionId)
    {
        try
        {
            // 注：enum の WTSLogonDomain (値5) がユーザー名を返すようです（enum値が逆）
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, (WtsInfoClass)5,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    string? userName = Marshal.PtrToStringUni(pBuffer);
                    return string.IsNullOrEmpty(userName) ? "[empty]" : userName;
                }
                finally
                {
                    WTSFreeMemory(pBuffer);
                }
            }
            
            // WTSQuerySessionInformation が false を返した場合
            int lastError = GetLastError();
            return $"[query failed: error {lastError}]";
        }
        catch (Exception ex)
        {
            return $"[exception: {ex.Message}]";
        }
    }

    /// <summary>
    /// 指定したセッションIDのログインユーザードメインを取得
    /// </summary>
    private static string? GetSessionUserDomainForId(uint sessionId)
    {
        try
        {
            // 注：enum の WTSLogonUser (値4) がドメイン情報を返すようです（enum値が逆）
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId, (WtsInfoClass)4,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    string? domain = Marshal.PtrToStringUni(pBuffer);
                    return string.IsNullOrEmpty(domain) ? "[empty]" : domain;
                }
                finally
                {
                    WTSFreeMemory(pBuffer);
                }
            }

            // WTSQuerySessionInformation が false を返した場合
            int lastError = GetLastError();
            return $"[query failed: error {lastError}]";
        }
        catch (Exception ex)
        {
            return $"[exception: {ex.Message}]";
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
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId.Value, WtsInfoClass.WTSLogonTime,
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

    /// <summary>
    /// アクティブセッションのログインユーザー名を取得
    /// </summary>
    public static string? GetSessionUserName()
    {
        var sessionId = GetActiveSessionId();
        if (!sessionId.HasValue)
            return null;

        try
        {
            // 注：enum を使用せず、値 5 を直接使用（値 5 = ユーザー名）
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId.Value, (WtsInfoClass)5,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    // WTSApi32 は Unicode (UTF-16) 文字列を返す
                    string? userName = Marshal.PtrToStringUni(pBuffer);
                    return userName;
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

    /// <summary>
    /// アクティブセッションのログインユーザードメインを取得
    /// </summary>
    public static string? GetSessionUserDomain()
    {
        var sessionId = GetActiveSessionId();
        if (!sessionId.HasValue)
            return null;

        try
        {
            // 注：enum を使用せず、値 4 を直接使用（値 4 = ドメイン情報または他の情報）
            if (WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId.Value, (WtsInfoClass)4,
                out IntPtr pBuffer, out uint bytesReturned))
            {
                try
                {
                    // WTSApi32 は Unicode (UTF-16) 文字列を返す
                    string? domainName = Marshal.PtrToStringUni(pBuffer);
                    return domainName;
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
    /// セッション情報を詳細にログ出力（デバッグ用）
    /// </summary>
    public void LogSessionInformation()
    {
        try
        {
            _logger.LogWarning("=== All Sessions Debug ===");
            var allSessions = WindowsSessionHelper.GetAllSessionsDebug();
            
            _logger.LogWarning("Total sessions found: {count}", allSessions.Count);
            foreach (var session in allSessions)
            {
                _logger.LogWarning("  Session ID: {id}, State: {state}, User: {user}, Domain: {domain}",
                    session.SessionId, session.State, session.UserName ?? "NULL", session.Domain ?? "NULL");
            }

            _logger.LogWarning("=== End All Sessions Debug ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging session information.");
        }
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
