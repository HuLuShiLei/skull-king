using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Replay;
using SkullKing.Application.Rooms;
using SkullKing.Infrastructure;
using SkullKing.Server.Auth;
using SkullKing.Server.Endpoints;
using SkullKing.Server.Hubs;

const string CorsPolicy = "skullking";

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Data Source=skullking.db";

builder.Services.AddSkullKingPersistence(connectionString);

builder.Services.AddSingleton<RoomService>();
builder.Services.AddScoped<GameReplayService>();
builder.Services.AddSingleton<IRoomNotifier, SignalRRoomNotifier>();
builder.Services.AddSingleton<IUserIdProvider, PlayerIdUserIdProvider>();
builder.Services.AddHostedService<TurnTimeoutService>();

builder.Services.AddSignalR();

builder.Services
    .AddAuthentication(PlayerTokenDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, PlayerTokenAuthenticationHandler>(PlayerTokenDefaults.Scheme, _ => { });

builder.Services.AddAuthorization();

// 跑在 Traefik 之类的反代后面时，靠这几个头才能拿到访客真实 IP 和外部 scheme。
// 容器网络里代理的 IP 不固定，所以不做来源限制——前提是后端容器只对内网开放。
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedProto
                               | ForwardedHeaders.XForwardedHost;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 前端和后端同源时（反代按路径分流）压根不需要 CORS；分成两个域名部署时，
// 用 Cors__AllowedOrigins 把前端地址列进来，多个用逗号或分号隔开。
var allowedOrigins = SplitOrigins(builder.Configuration["Cors:AllowedOrigins"])
                     ?? (builder.Environment.IsDevelopment()
                         ? ["http://localhost:5173", "http://127.0.0.1:5173"]
                         : []);

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

await app.Services.MigrateAsync();
await app.Services.GetRequiredService<RoomService>().RestoreAsync();

app.UseForwardedHeaders();

if (allowedOrigins.Length > 0)
{
    app.UseCors(CorsPolicy);
    app.Logger.LogInformation("已放行跨域来源：{Origins}", string.Join(", ", allowedOrigins));
}

// 单体部署时前端产物在 wwwroot 里；拆成两个容器时后端镜像没有这个目录，
// 此时既不注册静态文件也不注册 SPA 回退，免得把 404 伪装成首页。
var hasFrontend = app.Environment.WebRootPath is { Length: > 0 } webRoot
                  && File.Exists(Path.Combine(webRoot, "index.html"));

if (hasFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapApi();
app.MapHub<GameHub>("/hub/game");

// 两个路径同一个探针：/healthz 给容器内的 HEALTHCHECK 用，
// /api/healthz 是为了同源部署时反代按 /api 前缀分流后，从外面也探得到。
var health = () => Results.Ok(new { status = "ok" });

app.MapGet("/healthz", health);
app.MapGet("/api/healthz", health);

if (hasFrontend)
{
    // 邀请链接和前端路由都交给 SPA 自己处理，但别把 API 和 Hub 吞掉。
    app.MapFallbackToFile("index.html");
}
else
{
    app.Logger.LogInformation("未发现前端产物，本进程只提供 API 与 Hub");
}

app.Run();

static string[]? SplitOrigins(string? raw) =>
    string.IsNullOrWhiteSpace(raw)
        ? null
        : [.. raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
