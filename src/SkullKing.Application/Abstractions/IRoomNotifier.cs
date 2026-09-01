using SkullKing.Contracts;

namespace SkullKing.Application.Abstractions;

/// <summary>
/// 推送通道抽象。应用层不直接依赖 SignalR，实现放在服务端。
/// </summary>
public interface IRoomNotifier
{
    /// <summary>单播裁剪后的房间快照，这是客户端渲染的唯一权威数据源。</summary>
    Task SendRoomStateAsync(string playerId, RoomStateDto state, CancellationToken ct = default);

    /// <summary>广播增量事件，只用来驱动界面表现。</summary>
    Task BroadcastEventAsync(string roomCode, GameEventDto gameEvent, CancellationToken ct = default);

    Task BroadcastChatAsync(string roomCode, ChatMessageDto message, CancellationToken ct = default);

    /// <summary>通知某人已被移出房间，客户端据此退回大厅。</summary>
    Task SendRemovedAsync(string playerId, string roomCode, string reason, CancellationToken ct = default);

    Task BroadcastLobbyChangedAsync(CancellationToken ct = default);
}
