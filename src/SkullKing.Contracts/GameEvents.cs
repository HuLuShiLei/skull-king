namespace SkullKing.Contracts;

public static class GameEventTypes
{
    public const string GameStarted = "gameStarted";
    public const string RoundStarted = "roundStarted";
    public const string BidPlaced = "bidPlaced";
    public const string BidsRevealed = "bidsRevealed";
    public const string TrickStarted = "trickStarted";
    public const string CardPlayed = "cardPlayed";
    public const string TrickResolved = "trickResolved";
    public const string RoundScored = "roundScored";
    public const string GameEnded = "gameEnded";
    public const string SystemNotice = "systemNotice";
}

/// <summary>
/// 广播给房间的增量事件，只驱动界面表现（消息气泡、结算提示），
/// 权威状态一律以随后单播的 <see cref="RoomStateDto"/> 为准。
/// 刻意做成扁平结构而非多态类型，方便前端一个 switch 处理完。
/// </summary>
public sealed record GameEventDto
{
    public required string Type { get; init; }

    public long Seq { get; init; }

    public int? Seat { get; init; }

    public CardDto? Card { get; init; }

    public string? TigressMode { get; init; }

    public int? RoundNumber { get; init; }

    public int? TrickNumber { get; init; }

    public int? CardsPerPlayer { get; init; }

    public int? WinnerSeat { get; init; }

    public string? Reason { get; init; }

    public int? Bonus { get; init; }

    public IReadOnlyList<PlayedCardDto>? Plays { get; init; }

    /// <summary>叫牌揭示后才有值，揭示前用 <see cref="Seat"/> 表示谁叫完了。</summary>
    public IReadOnlyList<int>? Bids { get; init; }

    public IReadOnlyList<PlayerRoundScoreDto>? Scores { get; init; }

    public IReadOnlyList<int>? TotalScores { get; init; }

    public IReadOnlyList<int>? WinnerSeats { get; init; }

    /// <summary>系统提示文案，例如某人加入或被托管。</summary>
    public string? Text { get; init; }
}
