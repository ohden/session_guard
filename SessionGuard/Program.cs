using SessionGuard;
using Serilog;

var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

// Serilogの設定
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: Path.Combine(logDir, "SessionGuard-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}\n{Exception}",
        fileSizeLimitBytes: 10_000_000, // 10MB
        retainedFileCountLimit: 7 // 7日間保持
    )
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

// セッション設定をオプションとして登録
builder.Services.Configure<SessionConfig>(builder.Configuration.GetSection("SessionConfig"));

// ロギング設定
builder.Services.AddLogging(options =>
{
    options.ClearProviders();
    options.AddSerilog(Log.Logger);
});

// サービスに登録
builder.Services.AddHostedService<Worker>();

// Windowsサービスライフタイムを追加
builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();
