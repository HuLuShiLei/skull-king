using System.Collections.Immutable;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Game;

/// <summary>
/// 领域事件。服务端把这些事件顺序落库，既是回放依据，也是广播给客户端的素材
/// （其中含手牌的事件需要按座位裁剪后再下发）。
/// </summary>
public abstract record GameEvent;

public sealed record GameStartedEvent(int PlayerCount, int TotalRounds) : GameEvent;

/// <param name="Hands">按座位索引的手牌，含全部玩家的私密信息，广播前必须裁剪。</param>
public sealed record RoundStartedEvent(
    int RoundNumber,
    int CardsPerPlayer,
    ImmutableArray<ImmutableArray<Card>> Hands) : GameEvent;

/// <summary>单个玩家完成叫牌。叫牌数在本轮全员叫完前对其他人保密。</summary>
public sealed record BidPlacedEvent(int Seat, int Bid) : GameEvent;

public sealed record BiddingCompletedEvent(int RoundNumber, ImmutableArray<int> Bids) : GameEvent;

public sealed record TrickStartedEvent(int RoundNumber, int TrickNumber, int LeaderSeat) : GameEvent;

public sealed record CardPlayedEvent(int Seat, Card Card, TigressMode? TigressMode, int NextSeat) : GameEvent;

public sealed record TrickResolvedEvent(
    int RoundNumber,
    int TrickNumber,
    int WinnerSeat,
    TrickWinReason Reason,
    ImmutableArray<PlayedCard> Plays,
    int Bonus) : GameEvent;

public sealed record RoundScoredEvent(
    int RoundNumber,
    ImmutableArray<PlayerRoundScore> Scores,
    ImmutableArray<int> TotalScores) : GameEvent;

public sealed record GameEndedEvent(
    ImmutableArray<int> FinalScores,
    ImmutableArray<int> WinnerSeats) : GameEvent;
