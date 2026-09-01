using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SkullKing.Application.Abstractions;

namespace SkullKing.Server.Auth;

public static class PlayerTokenDefaults
{
    public const string Scheme = "PlayerToken";

    /// <summary>WebSocket 握手带不了自定义请求头，只能走查询串。</summary>
    public const string QueryKey = "access_token";
}

/// <summary>
/// 匿名玩家的轻量鉴权：一串随机 token 即身份。没有账号密码体系，
/// token 同时承担「断线后认回原座位」的职责。
/// </summary>
public sealed class PlayerTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IPlayerStore players) : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtractToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            return AuthenticateResult.NoResult();
        }

        var player = await players.FindByTokenAsync(token, Context.RequestAborted);

        if (player is null)
        {
            return AuthenticateResult.Fail("凭证无效");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, player.Id),
            new Claim(ClaimTypes.Name, player.Nickname)
        ], PlayerTokenDefaults.Scheme);

        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, PlayerTokenDefaults.Scheme));
    }

    private string? ExtractToken()
    {
        var header = Request.Headers.Authorization.ToString();

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return header["Bearer ".Length..].Trim();
        }

        return Request.Query[PlayerTokenDefaults.QueryKey].FirstOrDefault();
    }
}

/// <summary>让 SignalR 能按 playerId 单播，而不是只能按连接 id。</summary>
public sealed class PlayerIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}

public static class ClaimsPrincipalExtensions
{
    public static string PlayerId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("当前连接没有玩家身份");

    public static string Nickname(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Name) ?? "匿名同事";
}
