using SkullKing.Application.Rooms;
using SkullKing.Contracts;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Game;
using SkullKing.Domain.Rules;

namespace SkullKing.Application.Projection;

/// <summary>
/// 把领域状态投影成发给客户端的视图。所有隐私裁剪都集中在这里：
/// 手牌只给本人，叫牌在本轮全员叫完之前只给本人。
/// </summary>
public static class GameProjector
{
    public static CardDto ToDto(Card card) =>
        new(card.Id, card.Kind.ToString(), card.Suit?.ToString(), card.Rank);

    public static PlayedCardDto ToDto(PlayedCard play) =>
        new(play.Seat, ToDto(play.Card), play.TigressMode?.ToString());

    public static PlayerRoundScoreDto ToDto(PlayerRoundScore score) =>
        new(score.Seat, score.Bid, score.TricksWon, score.BaseScore, score.Bonus, score.Total);

    public static GameViewDto Project(Room room, GameState state, int viewerSeat, DateTimeOffset now)
    {
        var isSeated = viewerSeat >= 0 && viewerSeat < state.PlayerCount;
        var myHand = isSeated ? state.Hands[viewerSeat] : [];

        var playable = isSeated && state.Phase == GamePhase.Playing && state.CurrentSeat == viewerSeat
            ? PlayValidator.PlayableCards(myHand, state.CurrentTrick).Select(c => c.Id).ToArray()
            : [];

        var revealed = state.BiddingComplete;

        var bids = new int?[state.PlayerCount];

        for (var seat = 0; seat < state.PlayerCount; seat++)
        {
            bids[seat] = revealed || seat == viewerSeat ? state.Bids[seat] : null;
        }

        var remaining = room.TurnDeadline is { } deadline
            ? Math.Max(0, (int)Math.Ceiling((deadline - now).TotalSeconds))
            : (int?)null;

        return new GameViewDto
        {
            Phase = state.Phase.ToString(),
            RoundNumber = state.RoundNumber,
            TotalRounds = state.TotalRounds,
            TrickNumber = state.TrickNumber,
            CardsPerPlayer = state.CardsPerPlayer,
            MyHand = [.. myHand.Select(ToDto)],
            PlayableCardIds = playable,
            CurrentTrick = [.. state.CurrentTrick.Select(ToDto)],
            LeaderSeat = state.LeaderSeat,
            CurrentSeat = state.CurrentSeat,
            BidsRevealed = revealed,
            Bids = bids,
            HasBid = [.. state.Bids.Select(b => b.HasValue)],
            TricksWon = [.. state.TricksWon],
            TotalScores = [.. state.TotalScores],
            Rounds = [.. state.Rounds.Select(r => new RoundRecordDto(r.RoundNumber, [.. r.Scores.Select(ToDto)]))],
            LastTrick = [.. state.LastTrick.Select(ToDto)],
            LastTrickWinnerSeat = state.LastTrickWinnerSeat,
            TurnSecondsRemaining = remaining
        };
    }

    /// <summary>
    /// 把领域事件转成可以安全广播给全房间的形式。返回 null 表示这个事件不该广播
    /// （例如发牌事件带着所有人的手牌，只能靠随后的单播快照传达）。
    /// </summary>
    public static GameEventDto? ToBroadcastDto(GameEvent domainEvent, long seq) => domainEvent switch
    {
        GameStartedEvent e => new GameEventDto
        {
            Type = GameEventTypes.GameStarted,
            Seq = seq,
            RoundNumber = 1,
            Text = $"共 {e.TotalRounds} 轮"
        },

        RoundStartedEvent e => new GameEventDto
        {
            Type = GameEventTypes.RoundStarted,
            Seq = seq,
            RoundNumber = e.RoundNumber,
            CardsPerPlayer = e.CardsPerPlayer
        },

        // 只播「谁叫完了」，具体数字等全员叫完再一次性揭示。
        BidPlacedEvent e => new GameEventDto
        {
            Type = GameEventTypes.BidPlaced,
            Seq = seq,
            Seat = e.Seat
        },

        BiddingCompletedEvent e => new GameEventDto
        {
            Type = GameEventTypes.BidsRevealed,
            Seq = seq,
            RoundNumber = e.RoundNumber,
            Bids = [.. e.Bids]
        },

        TrickStartedEvent e => new GameEventDto
        {
            Type = GameEventTypes.TrickStarted,
            Seq = seq,
            RoundNumber = e.RoundNumber,
            TrickNumber = e.TrickNumber,
            Seat = e.LeaderSeat
        },

        CardPlayedEvent e => new GameEventDto
        {
            Type = GameEventTypes.CardPlayed,
            Seq = seq,
            Seat = e.Seat,
            Card = ToDto(e.Card),
            TigressMode = e.TigressMode?.ToString()
        },

        TrickResolvedEvent e => new GameEventDto
        {
            Type = GameEventTypes.TrickResolved,
            Seq = seq,
            RoundNumber = e.RoundNumber,
            TrickNumber = e.TrickNumber,
            WinnerSeat = e.WinnerSeat,
            Reason = e.Reason.ToString(),
            Bonus = e.Bonus,
            Plays = [.. e.Plays.Select(ToDto)]
        },

        RoundScoredEvent e => new GameEventDto
        {
            Type = GameEventTypes.RoundScored,
            Seq = seq,
            RoundNumber = e.RoundNumber,
            Scores = [.. e.Scores.Select(ToDto)],
            TotalScores = [.. e.TotalScores]
        },

        GameEndedEvent e => new GameEventDto
        {
            Type = GameEventTypes.GameEnded,
            Seq = seq,
            TotalScores = [.. e.FinalScores],
            WinnerSeats = [.. e.WinnerSeats]
        },

        _ => null
    };

    public static GameEventDto SystemNotice(string text, long seq) =>
        new() { Type = GameEventTypes.SystemNotice, Seq = seq, Text = text };
}
