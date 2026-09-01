namespace SkullKing.Contracts;

public sealed record CardDto(string Id, string Kind, string? Suit, int? Rank);

public sealed record PlayedCardDto(int Seat, CardDto Card, string? TigressMode);

public sealed record PlayerRoundScoreDto(int Seat, int Bid, int TricksWon, int BaseScore, int Bonus, int Total);

public sealed record RoundRecordDto(int RoundNumber, IReadOnlyList<PlayerRoundScoreDto> Scores);

/// <summary>
/// 按收件人裁剪后的对局视图。<see cref="MyHand"/> 只含本人手牌，
/// <see cref="Bids"/> 在本轮全员叫完之前只有本人的值可见。
/// </summary>
public sealed record GameViewDto
{
    public required string Phase { get; init; }

    public required int RoundNumber { get; init; }

    public required int TotalRounds { get; init; }

    public required int TrickNumber { get; init; }

    public required int CardsPerPlayer { get; init; }

    public required IReadOnlyList<CardDto> MyHand { get; init; }

    /// <summary>当前可合法打出的牌，客户端置灰逻辑以此为准。</summary>
    public required IReadOnlyList<string> PlayableCardIds { get; init; }

    public required IReadOnlyList<PlayedCardDto> CurrentTrick { get; init; }

    public required int LeaderSeat { get; init; }

    public required int CurrentSeat { get; init; }

    public required bool BidsRevealed { get; init; }

    /// <summary>未揭示时除本人外均为 null。</summary>
    public required IReadOnlyList<int?> Bids { get; init; }

    /// <summary>谁已经叫过牌，即使叫牌内容保密也要让大家看到进度。</summary>
    public required IReadOnlyList<bool> HasBid { get; init; }

    public required IReadOnlyList<int> TricksWon { get; init; }

    public required IReadOnlyList<int> TotalScores { get; init; }

    public required IReadOnlyList<RoundRecordDto> Rounds { get; init; }

    public IReadOnlyList<PlayedCardDto> LastTrick { get; init; } = [];

    public int? LastTrickWinnerSeat { get; init; }

    /// <summary>当前行动方的剩余思考秒数，null 表示不限时。</summary>
    public int? TurnSecondsRemaining { get; init; }
}

public sealed record PlaceBidRequest(string RoomCode, int Bid);

public sealed record PlayCardRequest(string RoomCode, string CardId, string? TigressMode);
