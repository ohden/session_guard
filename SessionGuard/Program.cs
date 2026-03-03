using SessionGuard;

var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
if (!Directory.Exists(logDir))
{
    Directory.CreateDirectory(logDir);
}

var builder = Host.CreateApplicationBuilder(args);

// セッション設定をオプションとして登録
// CreateApplicationBuilder はデフォルトで appsettings.json の変更監視を有効にしている
builder.Services.Configure<SessionConfig>(builder.Configuration.GetSection("SessionConfig"));

// ロギング設定
builder.Services.AddLogging(options =>
{
    options.ClearProviders();
    options.AddConsole();
    options.AddEventLog();
    options.AddSimpleConsole(config =>
    {
        config.IncludeScopes = true;
        config.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });
});

// サービスに登録
builder.Services.AddHostedService<Worker>();

// Windowsサービスライフタイムを追加
builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();
