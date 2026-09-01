namespace SkullKing.Application.Rooms;

public sealed class RoomMember(string playerId, string nickname)
{
    public string PlayerId { get; } = playerId;

    public string Nickname { get; set; } = nickname;

    /// <summary>座位号，-1 表示观战。</summary>
    public int Seat { get; set; } = -1;

    public bool IsSpectator => Seat < 0;

    public bool IsReady { get; set; }

    /// <summary>同一玩家可能开了多个标签页，任意一个还连着就算在线。</summary>
    public HashSet<string> ConnectionIds { get; } = [];

    public bool IsConnected => ConnectionIds.Count > 0;

    public DateTimeOffset JoinedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DisconnectedAt { get; set; }
}
