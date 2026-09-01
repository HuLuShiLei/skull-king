using SkullKing.Domain.Cards;
using SkullKing.Domain.Game;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Tests;

public class GameEngineTests
{
    private static GameState Start(int players = 4, ulong seed = 12345)
        => GameEngine.Start(players, seed).State;

    private static GameState BidAll(GameState state, params int[] bids)
    {
        for (var seat = 0; seat < state.PlayerCount; seat++)
        {
            state = GameEngine.Apply(state, new PlaceBidCommand(seat, bids[seat])).State;
        }

        return state;
    }

    [Fact]
    public void 开局发第一轮每人一张牌()
    {
        var state = Start();

        Assert.Equal(GamePhase.Bidding, state.Phase);
        Assert.Equal(1, state.RoundNumber);
        Assert.All(state.Hands, hand => Assert.Single(hand));
    }

    [Theory]
    [InlineData(2, 10)]
    [InlineData(6, 10)]
    [InlineData(7, 10)]
    [InlineData(8, 8)]
    public void 轮数受牌量限制(int players, int expectedRounds)
    {
        Assert.Equal(expectedRounds, Start(players).TotalRounds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(9)]
    public void 人数越界开局失败(int players)
    {
        Assert.Throws<GameRuleException>(() => GameEngine.Start(players, 1));
    }

    [Fact]
    public void 同一个种子发出同一副牌()
    {
        var a = Start(seed: 999);
        var b = Start(seed: 999);

        Assert.Equal(a.Hands[0].Select(c => c.Id), b.Hands[0].Select(c => c.Id));
    }

    [Fact]
    public void 不同种子发出不同的牌()
    {
        var a = GameEngine.Start(4, 1, new GameSettings { MaxRounds = 10 }).State;
        var b = GameEngine.Start(4, 2, new GameSettings { MaxRounds = 10 }).State;

        Assert.NotEqual(
            string.Join(",", a.Hands[0].Select(c => c.Id)),
            string.Join(",", b.Hands[0].Select(c => c.Id)));
    }

    [Fact]
    public void 发出的牌互不重复()
    {
        var state = GameEngine.Start(8, 77).State;

        // 直接检查第 8 轮，那是牌量压力最大的一轮。
        var deck = DeckFactory.Shuffle(DeckFactory.BuildCoreDeck(), DeterministicRandom.Derive(77, 8));
        var hands = DeckFactory.Deal(deck, 8, 8);
        var allIds = hands.SelectMany(h => h).Select(c => c.Id).ToList();

        Assert.Equal(64, allIds.Count);
        Assert.Equal(64, allIds.Distinct().Count());
        Assert.Equal(8, state.TotalRounds);
    }

    [Fact]
    public void 核心牌组是七十张且没有重复Id()
    {
        var deck = DeckFactory.BuildCoreDeck();

        Assert.Equal(70, deck.Length);
        Assert.Equal(70, deck.Select(c => c.Id).Distinct().Count());
        Assert.Equal(56, deck.Count(c => c.IsNumber));
        Assert.Equal(5, deck.Count(c => c.Kind == CardKind.Escape));
        Assert.Equal(5, deck.Count(c => c.Kind == CardKind.Pirate));
        Assert.Equal(2, deck.Count(c => c.Kind == CardKind.Mermaid));
        Assert.Equal(1, deck.Count(c => c.Kind == CardKind.Tigress));
        Assert.Equal(1, deck.Count(c => c.Kind == CardKind.SkullKing));
    }

    [Fact]
    public void 全员叫完牌才进入出牌阶段()
    {
        var state = Start();

        state = GameEngine.Apply(state, new PlaceBidCommand(0, 1)).State;
        state = GameEngine.Apply(state, new PlaceBidCommand(1, 0)).State;
        Assert.Equal(GamePhase.Bidding, state.Phase);

        state = GameEngine.Apply(state, new PlaceBidCommand(2, 0)).State;
        var result = GameEngine.Apply(state, new PlaceBidCommand(3, 0));

        Assert.Equal(GamePhase.Playing, result.State.Phase);
        Assert.Contains(result.Events, e => e is BiddingCompletedEvent);
    }

    [Fact]
    public void 叫牌在全员完成前不揭示()
    {
        var state = Start();

        var result = GameEngine.Apply(state, new PlaceBidCommand(0, 1));

        Assert.Contains(result.Events, e => e is BidPlacedEvent);
        Assert.DoesNotContain(result.Events, e => e is BiddingCompletedEvent);
    }

    [Fact]
    public void 不能重复叫牌()
    {
        var state = GameEngine.Apply(Start(), new PlaceBidCommand(0, 1)).State;

        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlaceBidCommand(0, 0)));
    }

    [Fact]
    public void 叫牌不能超过本轮牌数()
    {
        var state = Start();

        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlaceBidCommand(0, 2)));
        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlaceBidCommand(0, -1)));
    }

    [Fact]
    public void 出牌阶段不能叫牌()
    {
        var state = BidAll(Start(), 0, 0, 0, 0);

        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlaceBidCommand(0, 0)));
    }

    [Fact]
    public void 不能抢别人的回合()
    {
        var state = BidAll(Start(), 0, 0, 0, 0);
        var wrongSeat = (state.CurrentSeat + 1) % state.PlayerCount;

        Assert.Throws<GameRuleException>(() =>
            GameEngine.Apply(state, new PlayCardCommand(wrongSeat, state.Hands[wrongSeat][0].Id)));
    }

    [Fact]
    public void 不能出手上没有的牌()
    {
        var state = BidAll(Start(), 0, 0, 0, 0);

        Assert.Throws<GameRuleException>(() =>
            GameEngine.Apply(state, new PlayCardCommand(state.CurrentSeat, "不存在的牌")));
    }

    [Fact]
    public void 打完一轮会自动结算并发下一轮的牌()
    {
        var state = BidAll(Start(), 0, 0, 0, 0);
        var events = new List<GameEvent>();

        for (var i = 0; i < 4; i++)
        {
            var result = GameEngine.Apply(state, new PlayCardCommand(state.CurrentSeat, state.Hands[state.CurrentSeat][0].Id));
            state = result.State;
            events.AddRange(result.Events);
        }

        Assert.Contains(events, e => e is TrickResolvedEvent);
        Assert.Contains(events, e => e is RoundScoredEvent);
        Assert.Contains(events, e => e is RoundStartedEvent { RoundNumber: 2 });
        Assert.Equal(2, state.RoundNumber);
        Assert.Equal(GamePhase.Bidding, state.Phase);
        Assert.All(state.Hands, hand => Assert.Equal(2, hand.Length));
    }

    [Fact]
    public void 每轮首家按座位轮转()
    {
        var state = Start();

        Assert.Equal(0, state.LeaderSeat);

        state = PlayWholeRound(BidAll(state, 0, 0, 0, 0));

        Assert.Equal(2, state.RoundNumber);
        Assert.Equal(1, state.LeaderSeat);
    }

    [Fact]
    public void 上一墩赢家领出下一墩()
    {
        var state = BidAll(GameEngine.Start(3, 42, new GameSettings { MaxRounds = 2 }).State, 0, 0, 0);
        state = PlayWholeRound(state);
        state = BidAll(state, 0, 0, 0);

        var afterFirstTrick = PlayOneTrick(state);

        Assert.Equal(afterFirstTrick.LastTrickWinnerSeat, afterFirstTrick.LeaderSeat);
        Assert.Equal(afterFirstTrick.LeaderSeat, afterFirstTrick.CurrentSeat);
    }

    [Fact]
    public void 一整局跑完后进入结束状态且总分等于各轮之和()
    {
        var state = GameEngine.Start(4, 2024, new GameSettings { MaxRounds = 10 }).State;

        while (state.Phase != GamePhase.Finished)
        {
            state = state.Phase == GamePhase.Bidding
                ? BidAllZero(state)
                : PlayOneTrick(state);
        }

        Assert.Equal(10, state.Rounds.Length);

        for (var seat = 0; seat < state.PlayerCount; seat++)
        {
            var expected = state.Rounds.Sum(r => r.Scores[seat].Total);
            Assert.Equal(expected, state.TotalScores[seat]);
        }

        Assert.NotEmpty(state.WinnerSeats());
        Assert.All(state.Hands, hand => Assert.Empty(hand));
    }

    [Fact]
    public void 结束后不能继续操作()
    {
        var state = GameEngine.Start(2, 5, new GameSettings { MaxRounds = 1 }).State;
        state = BidAll(state, 0, 0);
        state = PlayOneTrick(state);

        Assert.Equal(GamePhase.Finished, state.Phase);
        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlaceBidCommand(0, 0)));
        Assert.Throws<GameRuleException>(() => GameEngine.Apply(state, new PlayCardCommand(0, "SK")));
    }

    [Fact]
    public void 托管在叫牌阶段叫零()
    {
        var state = Start();

        var command = AutoPlayAdvisor.Suggest(state, 0);

        Assert.Equal(new PlaceBidCommand(0, 0), command);
    }

    [Fact]
    public void 托管出的牌一定合法()
    {
        var state = GameEngine.Start(4, 808, new GameSettings { MaxRounds = 10 }).State;

        while (state.Phase != GamePhase.Finished)
        {
            var seat = state.Phase == GamePhase.Bidding
                ? state.Bids.ToList().FindIndex(b => !b.HasValue)
                : state.CurrentSeat;

            state = GameEngine.Apply(state, AutoPlayAdvisor.Suggest(state, seat)).State;
        }

        Assert.Equal(GamePhase.Finished, state.Phase);
    }

    [Fact]
    public void Tigress不指定形态时默认当海盗()
    {
        var state = GameEngine.Start(2, 1, new GameSettings { MaxRounds = 10 }).State;
        state = BidAll(state, 0, 0);

        var seat = state.CurrentSeat;
        var hand = state.Hands[seat];
        var tigress = Card.Special(CardKind.Tigress, "TG");

        // 直接构造一个手里握有 Tigress 的状态，避免依赖洗牌结果。
        state = state with { Hands = state.Hands.SetItem(seat, [tigress, .. hand]) };

        var result = GameEngine.Apply(state, new PlayCardCommand(seat, "TG"));
        var played = Assert.IsType<CardPlayedEvent>(result.Events[0]);

        Assert.Equal(TigressMode.AsPirate, played.TigressMode);
    }

    private static GameState BidAllZero(GameState state)
    {
        for (var seat = 0; seat < state.PlayerCount; seat++)
        {
            state = GameEngine.Apply(state, new PlaceBidCommand(seat, 0)).State;
        }

        return state;
    }

    private static GameState PlayOneTrick(GameState state)
    {
        for (var i = 0; i < state.PlayerCount; i++)
        {
            var seat = state.CurrentSeat;
            var card = PlayValidator.PlayableCards(state.Hands[seat], state.CurrentTrick)[0];
            state = GameEngine.Apply(state, new PlayCardCommand(seat, card.Id)).State;
        }

        return state;
    }

    private static GameState PlayWholeRound(GameState state)
    {
        var round = state.RoundNumber;

        while (state.RoundNumber == round && state.Phase == GamePhase.Playing)
        {
            state = PlayOneTrick(state);
        }

        return state;
    }
}
