using SkullKing.Contracts;

namespace SkullKing.Infrastructure;

public sealed class PlayerRow
{
    public string Id { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    /// <summary>身份凭证，同时也是断线重连回原座位的依据。</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}

public sealed class RoomRow
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsPublic { get; set; }

    public int MaxPlayers { get; set; }

    public int MaxRounds { get; set; }

    public int TurnSeconds { get; set; }

    public string? PasswordHash { get; set; }

    public string HostPlayerId { get; set; } = string.Empty;

    public RoomStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<RoomMemberRow> Members { get; set; } = [];

    public List<GameRow> Games { get; set; } = [];
}

public sealed class RoomMemberRow
{
    public Guid RoomId { get; set; }

    public string PlayerId { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public int Seat { get; set; }

    public RoomRow? Room { get; set; }
}

public sealed class GameRow
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    /// <summary>洗牌种子。SQLite 没有无符号整型，按位模式存成 long。</summary>
    public long Seed { get; set; }

    public int PlayerCount { get; set; }

    public int TotalRounds { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>逗号分隔，按座位顺序。</summary>
    public string? FinalScores { get; set; }

    public string? WinnerSeats { get; set; }

    public RoomRow? Room { get; set; }

    public List<GameMoveRow> Moves { get; set; } = [];

    public List<GameSeatRow> Seats { get; set; } = [];

    public List<RoundScoreRow> RoundScores { get; set; } = [];
}

/// <summary>开局时的座位快照。房间成员后续怎么变都不影响这一局的战绩归属。</summary>
public sealed class GameSeatRow
{
    public Guid GameId { get; set; }

    public int Seat { get; set; }

    public string PlayerId { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public GameRow? Game { get; set; }
}

/// <summary>一步操作的日志。重放这些命令即可完整恢复对局。</summary>
public sealed class GameMoveRow
{
    public Guid GameId { get; set; }

    public long Seq { get; set; }

    public string Kind { get; set; } = string.Empty;

    public int Seat { get; set; }

    public int? Bid { get; set; }

    public string? CardId { get; set; }

    public string? TigressMode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public GameRow? Game { get; set; }
}

public sealed class RoundScoreRow
{
    public long Id { get; set; }

    public Guid GameId { get; set; }

    public int RoundNumber { get; set; }

    public int Seat { get; set; }

    public int Bid { get; set; }

    public int TricksWon { get; set; }

    public int BaseScore { get; set; }

    public int Bonus { get; set; }

    public int Total { get; set; }

    public GameRow? Game { get; set; }
}

public sealed class ChatMessageRow
{
    public string Id { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    public string PlayerId { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;

    public int Seat { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; }
}
