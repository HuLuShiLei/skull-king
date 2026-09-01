using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Replay;
using SkullKing.Application.Rooms;
using SkullKing.Infrastructure;
using SkullKing.Server.Auth;
using SkullKing.Server.Endpoints;
using SkullKing.Server.Hubs;

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

// 开发时前端跑在 Vite 上，生产环境前端由本服务直接托管，不需要跨域。
var devOrigins = builder.Configuration.GetSection("Cors:DevOrigins").Get<string[]>()
                 ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(options => options.AddPolicy("dev", policy => policy
    .WithOrigins(devOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

await app.Services.MigrateAsync();
await app.Services.GetRequiredService<RoomService>().RestoreAsync();

if (app.Environment.IsDevelopment())
{
    app.UseCors("dev");
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapApi();
app.MapHub<GameHub>("/hub/game");

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// 邀请链接和前端路由都交给 SPA 自己处理，但别把 API 和 Hub 吞掉。
app.MapFallbackToFile("index.html");

app.Run();
