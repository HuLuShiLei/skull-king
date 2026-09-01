using Microsoft.AspNetCore.Mvc;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Replay;
using SkullKing.Application.Rooms;
using SkullKing.Contracts;
using SkullKing.Server.Auth;

namespace SkullKing.Server.Endpoints;

public static class ApiEndpoints
{
    public static void MapApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        MapAuth(api);
        MapRooms(api);

        api.MapGet("/history", async (
            HttpContext http,
            IGameArchive archive,
            [FromQuery] int? limit,
            CancellationToken ct) =>
        {
            var entries = await archive.GetHistoryAsync(http.User.PlayerId(), Math.Clamp(limit ?? 20, 1, 100), ct);

            return Results.Ok(entries);
        }).RequireAuthorization();

        api.MapGet("/games/{gameId:guid}/replay", async (
            Guid gameId,
            GameReplayService replays,
            CancellationToken ct) =>
        {
            var replay = await replays.BuildAsync(gameId, ct);

            return replay is null ? Results.NotFound() : Results.Ok(replay);
        }).RequireAuthorization();
    }

    private static void MapAuth(IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/anonymous", async (AnonymousLoginRequest request, IPlayerStore players, CancellationToken ct) =>
        {
            var player = await players.CreateAnonymousAsync(request.Nickname, ct);

            return Results.Ok(new AuthResponse(player.Id, player.Nickname, player.Token));
        });

        auth.MapGet("/me", async (HttpContext http, IPlayerStore players, CancellationToken ct) =>
        {
            var player = await players.FindByIdAsync(http.User.PlayerId(), ct);

            if (player is null)
            {
                return Results.Unauthorized();
            }

            await players.TouchAsync(player.Id, ct);

            return Results.Ok(new AuthResponse(player.Id, player.Nickname, player.Token));
        }).RequireAuthorization();

        auth.MapPost("/rename", async (
            RenameRequest request,
            HttpContext http,
            IPlayerStore players,
            RoomService rooms,
            CancellationToken ct) =>
        {
            var playerId = http.User.PlayerId();

            await players.RenameAsync(playerId, request.Nickname, ct);

            var player = await players.FindByIdAsync(playerId, ct);

            if (player is null)
            {
                return Results.Unauthorized();
            }

            // 玩家表改完还得把牌桌上的名字一起换掉。
            await rooms.RenameAsync(playerId, player.Nickname, ct);

            return Results.Ok(new AuthResponse(player.Id, player.Nickname, player.Token));
        }).RequireAuthorization();
    }

    private static void MapRooms(IEndpointRouteBuilder api)
    {
        var roomsGroup = api.MapGroup("/rooms").RequireAuthorization();

        roomsGroup.MapGet("/", (RoomService rooms) => Results.Ok(rooms.ListPublicRooms()));

        roomsGroup.MapPost("/", async (
            CreateRoomRequest request,
            HttpContext http,
            RoomService rooms,
            CancellationToken ct) =>
        {
            var room = await rooms.CreateRoomAsync(http.User.PlayerId(), http.User.Nickname(), request, ct);

            return Results.Ok(new { code = room.Code });
        });

        // 加入前先探一下，好在界面上区分「房间不存在」和「需要密码」。
        roomsGroup.MapGet("/{code}/probe", (string code, RoomService rooms) => Results.Ok(rooms.Probe(code)));
    }
}
