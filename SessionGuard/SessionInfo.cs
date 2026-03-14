namespace SessionGuard;

/// <summary>
/// ユーザーセッションの情報を管理するクラス
/// </summary>
public class SessionInfo
{
    private static readonly TimeZoneInfo JstZone =
        TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    private static DateTime NowJst =>
        TimeZoneInfo.ConvertTime(DateTime.UtcNow, JstZone);

    private readonly ILogger? _logger;

    /// <summary>
    /// 現在のセッション開始時刻（JST）
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// セッション開始日付（JST、0時リセット判定用）
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// 当日の累積利用時間（分単位）
    /// ログアウト→再ログインしても、日付（JST）が変わるまで累積される
    /// </summary>
    public double AccumulatedUsageMinutes { get; set; } = 0.0;

    /// <summary>
    /// 当日の累計利用時間（分単位）= 累積 + 現在のセッション経過時間
    /// </summary>
    public double CurrentUsageMinutes
    {
        get
        {
            var elapsed = NowJst - StartTime;
            return AccumulatedUsageMinutes + elapsed.TotalMinutes;
        }
    }

    /// <summary>
    /// JST 0時を境に日付が変わったかどうかを判定
    /// </summary>
    public bool IsDayChanged
    {
        get
        {
            var currentDate = DateOnly.FromDateTime(NowJst);
            return currentDate != StartDate;
        }
    }

    public SessionInfo(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// セッション情報を初期化（サービス起動時または別ユーザーログイン時）
    /// 累積利用時間もリセットする
    /// </summary>
    public void Initialize()
    {
        StartTime = NowJst;
        StartDate = DateOnly.FromDateTime(NowJst);
        AccumulatedUsageMinutes = 0.0;
        _logger?.LogInformation("Session initialized at: {startTime} (JST)", StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
    }

    /// <summary>
    /// 同じユーザーの新しいセッション開始（累積利用時間を保持）
    /// 再ログイン時に呼び出す
    /// </summary>
    public void StartNewSession()
    {
        StartTime = NowJst;
        _logger?.LogInformation(
            "New session started at: {startTime} (JST). Accumulated usage preserved: {accumulated:F1}min",
            StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            AccumulatedUsageMinutes);
    }

    /// <summary>
    /// ログアウト時に現在のセッション利用時間を累積し、セッション開始時刻をリセット
    /// </summary>
    public void AccumulateAndResetCurrentSession()
    {
        var elapsed = NowJst - StartTime;
        AccumulatedUsageMinutes += elapsed.TotalMinutes;

        _logger?.LogInformation(
            "Session accumulated. This session: {session:F1}min, Total today: {total:F1}min",
            elapsed.TotalMinutes,
            AccumulatedUsageMinutes);

        StartTime = NowJst;
    }

    /// <summary>
    /// JST 0時を境に日付が変わった際にリセット
    /// </summary>
    public void ResetForNewDay()
    {
        StartDate = DateOnly.FromDateTime(NowJst);
        StartTime = NowJst;
        AccumulatedUsageMinutes = 0.0;
        _logger?.LogInformation("Day changed (JST midnight). Accumulated usage reset.");
    }
}
