namespace SkullKing.Contracts;

public sealed record ReplaySeatDto(int Seat, string Nickname);

/// <summary>
/// 一局已结束对局的完整回放。事件序列由服务端重放命令日志生成，
/// 与当时广播的内容一致，所以前端可以直接复用房间里的那套渲染逻辑。
/// </summary>
public sealed record GameReplayDto
{
    public required Guid GameId { get; init; }

    public required string RoomCode { get; init; }

    public required string RoomName { get; init; }

    public int PlayerCount { get; init; }

    public int TotalRounds { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    public required IReadOnlyList<ReplaySeatDto> Seats { get; init; }

    public required IReadOnlyList<GameEventDto> Events { get; init; }
}
