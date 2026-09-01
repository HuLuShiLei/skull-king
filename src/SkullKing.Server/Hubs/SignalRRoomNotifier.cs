using Microsoft.AspNetCore.SignalR;
using SkullKing.Application.Abstractions;
using SkullKing.Contracts;

namespace SkullKing.Server.Hubs;

public sealed class SignalRRoomNotifier(IHubContext<GameHub> hub) : IRoomNotifier
{
    public Task SendRoomStateAsync(string playerId, RoomStateDto state, CancellationToken ct = default) =>
        hub.Clients.User(playerId).SendAsync(HubMethods.RoomState, state, ct);

    public Task BroadcastEventAsync(string roomCode, GameEventDto gameEvent, CancellationToken ct = default) =>
        hub.Clients.Group(roomCode).SendAsync(HubMethods.GameEvent, gameEvent, ct);

    public Task BroadcastChatAsync(string roomCode, ChatMessageDto message, CancellationToken ct = default) =>
        hub.Clients.Group(roomCode).SendAsync(HubMethods.Chat, message, ct);

    public Task SendRemovedAsync(string playerId, string roomCode, string reason, CancellationToken ct = default) =>
        hub.Clients.User(playerId).SendAsync(HubMethods.Removed, roomCode, reason, ct);

    public Task BroadcastLobbyChangedAsync(CancellationToken ct = default) =>
        hub.Clients.Group(GameHub.LobbyGroup).SendAsync(HubMethods.LobbyChanged, ct);
}
