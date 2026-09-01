using System.Collections.Immutable;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Game;

public enum GamePhase
{
    Bidding = 0,
    Playing = 1,
    Finished = 2
}

/// <summary>某一轮打完后的完整记录，用于战绩页和回放。</summary>
public sealed record RoundRecord(int RoundNumber, ImmutableArray<PlayerRoundScore> Scores);

/// <summary>
/// 对局的全量状态。不可变，所有变更通过 <see cref="GameEngine"/> 产生新实例。
/// </summary>
public sealed record GameState
{
    public required GameSettings Settings { get; init; }

    public required int PlayerCount { get; init; }

    public required ulong Seed { get; init; }

    public required int TotalRounds { get; init; }

    public GamePhase Phase { get; init; } = GamePhase.Bidding;

    public int RoundNumber { get; init; } = 1;

    /// <summary>本轮第几墩，1-based。</summary>
    public int TrickNumber { get; init; } = 1;

    /// <summary>本轮每人几张牌，等于轮次号。</summary>
    public int CardsPerPlayer => RoundNumber;

    public ImmutableArray<ImmutableArray<Card>> Hands { get; init; } = [];

    /// <summary>本轮叫牌，未叫为 null。</summary>
    public ImmutableArray<int?> Bids { get; init; } = [];

    public ImmutableArray<int> TricksWon { get; init; } = [];

    /// <summary>本轮已累计的奖励分，轮末叫牌命中才计入总分。</summary>
    public ImmutableArray<int> RoundBonus { get; init; } = [];

    public ImmutableArray<int> TotalScores { get; init; } = [];

    public ImmutableArray<PlayedCard> CurrentTrick { get; init; } = [];

    public int LeaderSeat { get; init; }

    /// <summary>轮到谁出牌。叫牌阶段无意义。</summary>
    public int CurrentSeat { get; init; }

    public ImmutableArray<RoundRecord> Rounds { get; init; } = [];

    public ImmutableArray<PlayedCard> LastTrick { get; init; } = [];

    public int? LastTrickWinnerSeat { get; init; }

    public bool BiddingComplete => Bids.All(b => b.HasValue);

    public ImmutableArray<Card> HandOf(int seat) => Hands[seat];

    public ImmutableArray<Card> PlayableCardsOf(int seat)
        => Phase == GamePhase.Playing && CurrentSeat == seat
            ? PlayValidator.PlayableCards(Hands[seat], CurrentTrick)
            : [];

    /// <summary>并列最高分时并列获胜。</summary>
    public ImmutableArray<int> WinnerSeats()
    {
        if (TotalScores.IsDefaultOrEmpty)
        {
            return [];
        }

        var best = TotalScores.Max();
        return [.. Enumerable.Range(0, PlayerCount).Where(seat => TotalScores[seat] == best)];
    }
}
