using SessionGuard;

var builder = Host.CreateApplicationBuilder(args);

// セッション設定をオプションとして登録
builder.Services.Configure<SessionConfig>(builder.Configuration.GetSection("SessionConfig"));

// サービスに登録
builder.Services.AddLogging();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
