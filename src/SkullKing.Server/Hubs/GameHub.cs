using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SkullKing.Application.Rooms;
using SkullKing.Contracts;
using SkullKing.Server.Auth;

namespace SkullKing.Server.Hubs;

/// <summary>
/// 客户端只能发意图，所有合法性判断都在服务端。手牌永远只单播给本人，
/// 客户端即使改了本地状态也拿不到别人的牌。
/// </summary>
[Authorize(AuthenticationSchemes = PlayerTokenDefaults.Scheme)]
public sealed class GameHub(RoomService rooms) : Hub
{
    public const string LobbyGroup = "__lobby";

    public async Task SubscribeLobby()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);
        await Clients.Caller.SendAsync(HubMethods.LobbyChanged);
    }

    public Task UnsubscribeLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);

    public async Task<RoomActionResult> JoinRoom(string code, string? password)
    {
        var normalized = RoomCode.Normalize(code);

        // 先入组再入房，这样加入过程中产生的系统提示不会漏发给自己。
        await Groups.AddToGroupAsync(Context.ConnectionId, normalized);

        var result = await rooms.JoinAsync(
            normalized,
            Context.User!.PlayerId(),
            Context.User!.Nickname(),
            password,
            Context.ConnectionId);

        if (!result.Ok)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalized);
        }

        return result;
    }

    public async Task<RoomActionResult> LeaveRoom(string code)
    {
        var normalized = RoomCode.Normalize(code);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, normalized);

        return await rooms.LeaveAsync(normalized, Context.User!.PlayerId());
    }

    public Task<RoomActionResult> SetReady(string code, bool ready) =>
        rooms.SetReadyAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), ready);

    public Task<RoomActionResult> SitDown(string code) =>
        rooms.SitDownAsync(RoomCode.Normalize(code), Context.User!.PlayerId());

    public Task<RoomActionResult> StandUp(string code) =>
        rooms.StandUpAsync(RoomCode.Normalize(code), Context.User!.PlayerId());

    public Task<RoomActionResult> UpdateSettings(string code, UpdateRoomSettingsRequest request) =>
        rooms.UpdateSettingsAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), request);

    public Task<RoomActionResult> Kick(string code, string targetPlayerId) =>
        rooms.KickAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), targetPlayerId);

    public Task<RoomActionResult> TransferHost(string code, string targetPlayerId) =>
        rooms.TransferHostAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), targetPlayerId);

    public Task<RoomActionResult> StartGame(string code) =>
        rooms.StartGameAsync(RoomCode.Normalize(code), Context.User!.PlayerId());

    public Task<RoomActionResult> PlaceBid(string code, int bid) =>
        rooms.PlaceBidAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), bid);

    public Task<RoomActionResult> PlayCard(string code, string cardId, string? tigressMode) =>
        rooms.PlayCardAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), cardId, tigressMode);

    public Task<RoomActionResult> SendChat(string code, string text) =>
        rooms.SendChatAsync(RoomCode.Normalize(code), Context.User!.PlayerId(), text);

    /// <summary>重新拉一次快照，用于客户端怀疑自己状态过期时自救。</summary>
    public async Task RequestState(string code)
    {
        var room = rooms.Find(code);

        if (room is null)
        {
            return;
        }

        var state = rooms.BuildStateFor(room, Context.User!.PlayerId());

        if (state is not null)
        {
            await Clients.Caller.SendAsync(HubMethods.RoomState, state);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception) => rooms.DisconnectAsync(Context.ConnectionId);
}

public static class HubMethods
{
    public const string RoomState = "RoomState";
    public const string GameEvent = "GameEvent";
    public const string Chat = "Chat";
    public const string Removed = "Removed";
    public const string LobbyChanged = "LobbyChanged";
}
