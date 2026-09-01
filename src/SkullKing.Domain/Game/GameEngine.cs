using System.Collections.Immutable;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Game;

public sealed record GameStepResult(GameState State, ImmutableArray<GameEvent> Events);

/// <summary>
/// 纯函数状态机。不碰 IO、不读时钟、不用非确定随机源，因此同一个种子加同一串命令
/// 必然重现同一局，这是事件回放和断线恢复的基础。
/// </summary>
public static class GameEngine
{
    public static GameStepResult Start(int playerCount, ulong seed, GameSettings? settings = null)
    {
        settings ??= GameSettings.Default;

        if (playerCount is < GameSettings.MinPlayers or > GameSettings.MaxPlayers)
        {
            throw new GameRuleException(
                $"人数必须在 {GameSettings.MinPlayers}-{GameSettings.MaxPlayers} 之间，当前 {playerCount}");
        }

        var totalRounds = Math.Min(settings.MaxRounds, DeckFactory.MaxRoundsFor(playerCount));

        var state = new GameState
        {
            Settings = settings,
            PlayerCount = playerCount,
            Seed = seed,
            TotalRounds = totalRounds,
            TotalScores = [.. Enumerable.Repeat(0, playerCount)]
        };

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        events.Add(new GameStartedEvent(playerCount, totalRounds));

        state = BeginRound(state, 1, events);

        return new GameStepResult(state, events.ToImmutable());
    }

    public static GameStepResult Apply(GameState state, GameCommand command) => command switch
    {
        PlaceBidCommand bid => ApplyBid(state, bid),
        PlayCardCommand play => ApplyPlay(state, play),
        _ => throw new GameRuleException($"未知命令 {command.GetType().Name}")
    };

    private static GameStepResult ApplyBid(GameState state, PlaceBidCommand command)
    {
        if (state.Phase != GamePhase.Bidding)
        {
            throw new GameRuleException("当前不是叫牌阶段");
        }

        RequireValidSeat(state, command.Seat);

        if (state.Bids[command.Seat].HasValue)
        {
            throw new GameRuleException("本轮已经叫过牌了");
        }

        if (command.Bid < 0 || command.Bid > state.CardsPerPlayer)
        {
            throw new GameRuleException($"叫牌必须在 0-{state.CardsPerPlayer} 之间");
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        events.Add(new BidPlacedEvent(command.Seat, command.Bid));

        var next = state with { Bids = state.Bids.SetItem(command.Seat, command.Bid) };

        // 叫牌是同时进行的，凑齐最后一个才一次性揭示，避免后叫的人偷看。
        if (next.BiddingComplete)
        {
            var revealed = next.Bids.Select(b => b!.Value).ToImmutableArray();
            events.Add(new BiddingCompletedEvent(next.RoundNumber, revealed));

            next = next with { Phase = GamePhase.Playing };
            events.Add(new TrickStartedEvent(next.RoundNumber, next.TrickNumber, next.LeaderSeat));
        }

        return new GameStepResult(next, events.ToImmutable());
    }

    private static GameStepResult ApplyPlay(GameState state, PlayCardCommand command)
    {
        if (state.Phase != GamePhase.Playing)
        {
            throw new GameRuleException("当前不是出牌阶段");
        }

        RequireValidSeat(state, command.Seat);

        if (state.CurrentSeat != command.Seat)
        {
            throw new GameRuleException("还没轮到你出牌");
        }

        var hand = state.Hands[command.Seat];
        var card = hand.FirstOrDefault(c => c.Id == command.CardId)
            ?? throw new GameRuleException($"手牌里没有 {command.CardId}");

        if (!PlayValidator.CanPlay(hand, state.CurrentTrick, card.Id))
        {
            throw new GameRuleException("必须跟出首攻花色");
        }

        var mode = card.Kind == CardKind.Tigress
            ? command.TigressMode ?? TigressMode.AsPirate
            : (TigressMode?)null;

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var trick = state.CurrentTrick.Add(new PlayedCard(command.Seat, card, mode));

        var next = state with
        {
            Hands = state.Hands.SetItem(command.Seat, hand.Remove(card)),
            CurrentTrick = trick
        };

        var trickComplete = trick.Length == state.PlayerCount;
        var nextSeat = trickComplete ? -1 : NextSeat(state, command.Seat);

        events.Add(new CardPlayedEvent(command.Seat, card, mode, nextSeat));

        if (!trickComplete)
        {
            return new GameStepResult(next with { CurrentSeat = nextSeat }, events.ToImmutable());
        }

        next = ResolveTrick(next, events);

        return new GameStepResult(next, events.ToImmutable());
    }

    private static GameState ResolveTrick(GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        var outcome = TrickResolver.Resolve(state.CurrentTrick);
        var bonus = BonusCalculator.ForTrick(outcome, state.CurrentTrick);

        events.Add(new TrickResolvedEvent(
            state.RoundNumber,
            state.TrickNumber,
            outcome.WinnerSeat,
            outcome.Reason,
            state.CurrentTrick,
            bonus));

        var next = state with
        {
            TricksWon = state.TricksWon.SetItem(outcome.WinnerSeat, state.TricksWon[outcome.WinnerSeat] + 1),
            RoundBonus = state.RoundBonus.SetItem(outcome.WinnerSeat, state.RoundBonus[outcome.WinnerSeat] + bonus),
            LastTrick = state.CurrentTrick,
            LastTrickWinnerSeat = outcome.WinnerSeat,
            CurrentTrick = []
        };

        var roundComplete = next.TrickNumber >= next.CardsPerPlayer;

        if (!roundComplete)
        {
            next = next with
            {
                TrickNumber = next.TrickNumber + 1,
                LeaderSeat = outcome.WinnerSeat,
                CurrentSeat = outcome.WinnerSeat
            };

            events.Add(new TrickStartedEvent(next.RoundNumber, next.TrickNumber, next.LeaderSeat));
            return next;
        }

        return ScoreRound(next, events);
    }

    private static GameState ScoreRound(GameState state, ImmutableArray<GameEvent>.Builder events)
    {
        var scores = ImmutableArray.CreateBuilder<PlayerRoundScore>(state.PlayerCount);
        var totals = state.TotalScores.ToBuilder();

        for (var seat = 0; seat < state.PlayerCount; seat++)
        {
            var score = ScoreCalculator.Score(
                seat,
                state.RoundNumber,
                state.Bids[seat] ?? 0,
                state.TricksWon[seat],
                state.RoundBonus[seat]);

            scores.Add(score);
            totals[seat] += score.Total;
        }

        var roundScores = scores.MoveToImmutable();
        var newTotals = totals.ToImmutable();

        var next = state with
        {
            TotalScores = newTotals,
            Rounds = state.Rounds.Add(new RoundRecord(state.RoundNumber, roundScores))
        };

        events.Add(new RoundScoredEvent(state.RoundNumber, roundScores, newTotals));

        if (next.RoundNumber >= next.TotalRounds)
        {
            next = next with { Phase = GamePhase.Finished, CurrentTrick = [] };
            events.Add(new GameEndedEvent(newTotals, next.WinnerSeats()));
            return next;
        }

        return BeginRound(next, next.RoundNumber + 1, events);
    }

    private static GameState BeginRound(GameState state, int roundNumber, ImmutableArray<GameEvent>.Builder events)
    {
        // 每轮从基准种子派生独立子种子，保证回放时逐轮可重现。
        var roundSeed = DeterministicRandom.Derive(state.Seed, roundNumber);
        var deck = DeckFactory.Shuffle(DeckFactory.BuildCoreDeck(), roundSeed);
        var hands = DeckFactory.Deal(deck, state.PlayerCount, roundNumber);

        var next = state with
        {
            Phase = GamePhase.Bidding,
            RoundNumber = roundNumber,
            TrickNumber = 1,
            Hands = hands,
            Bids = [.. Enumerable.Repeat((int?)null, state.PlayerCount)],
            TricksWon = [.. Enumerable.Repeat(0, state.PlayerCount)],
            RoundBonus = [.. Enumerable.Repeat(0, state.PlayerCount)],
            CurrentTrick = [],
            LastTrick = [],
            LastTrickWinnerSeat = null,
            LeaderSeat = (roundNumber - 1) % state.PlayerCount,
            CurrentSeat = (roundNumber - 1) % state.PlayerCount
        };

        events.Add(new RoundStartedEvent(roundNumber, roundNumber, hands));

        return next;
    }

    private static int NextSeat(GameState state, int seat) => (seat + 1) % state.PlayerCount;

    private static void RequireValidSeat(GameState state, int seat)
    {
        if (seat < 0 || seat >= state.PlayerCount)
        {
            throw new GameRuleException($"座位 {seat} 不存在");
        }
    }
}
