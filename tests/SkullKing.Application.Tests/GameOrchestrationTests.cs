using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Tests;

public class GameOrchestrationTests
{
    private static async Task<Harness> StartedGameAsync(int extraPlayers = 2, int turnSeconds = 60, int maxRounds = 10)
    {
        var harness = new Harness();
        await harness.CreateRoomAsync(turnSeconds, maxRounds);
        await harness.SeatAndReadyAsync([.. Enumerable.Range(1, extraPlayers)]);
        await harness.StartAsync();

        return harness;
    }

    [Fact]
    public async Task 开局后每人只看得到自己的手牌()
    {
        var harness = await StartedGameAsync();

        for (var index = 0; index <= 2; index++)
        {
            var view = harness.GameOf(index);

            Assert.Single(view.MyHand);
            Assert.Equal(1, view.CardsPerPlayer);
        }

        // 三个人拿到的牌各不相同，说明服务端确实按座位裁剪过。
        var dealt = Enumerable.Range(0, 3).Select(i => harness.GameOf(i).MyHand[0].Id).ToList();
        Assert.Equal(3, dealt.Distinct().Count());
    }

    [Fact]
    public async Task 半途来观战能立刻拿到前面的流水()
    {
        var harness = await StartedGameAsync(maxRounds: 3);

        await harness.BidAllAsync(0, 1, 0);
        await harness.PlayFirstLegalAsync();

        await harness.JoinAsync(7);

        var state = harness.StateOf(7);

        // 旁观者不用等着一条条重放，进门就该看到前面打成什么样。
        Assert.Equal(-1, state.YourSeat);
        Assert.NotNull(state.Game);
        Assert.Contains(state.RecentEvents, e => e.Type == GameEventTypes.GameStarted);
        Assert.Contains(state.RecentEvents, e => e.Type == GameEventTypes.BidsRevealed);
        Assert.Contains(state.RecentEvents, e => e.Type == GameEventTypes.CardPlayed);

        // 靠时间戳才能和聊天记录排到一起。
        Assert.All(state.RecentEvents, e => Assert.NotNull(e.At));

        // 看得到桌面，但拿不到任何人的手牌。
        Assert.Empty(state.Game.MyHand);
        Assert.Empty(state.Game.PlayableCardIds);
    }

    [Fact]
    public async Task 叫牌在全员完成前对别人保密()
    {
        var harness = await StartedGameAsync();

        await harness.Service.PlaceBidAsync(harness.Code, harness.PlayerIdAtSeat(0), 1);

        var self = harness.GameOf(harness.IndexAtSeat(0));
        var other = harness.GameOf(harness.IndexAtSeat(1));

        Assert.False(other.BidsRevealed);
        Assert.Equal(1, self.Bids[0]);
        Assert.Null(other.Bids[0]);

        // 但「谁已经叫过了」是公开的，否则没人知道在等谁。
        Assert.True(other.HasBid[0]);
        Assert.False(other.HasBid[1]);
    }

    [Fact]
    public async Task 全员叫完后一次性揭示()
    {
        var harness = await StartedGameAsync();

        await harness.BidAllAsync(1, 0, 0);

        var view = harness.GameOf(harness.IndexAtSeat(1));

        Assert.True(view.BidsRevealed);
        Assert.Equal([1, 0, 0], view.Bids);
        Assert.Equal(nameof(GamePhase.Playing), view.Phase);
        Assert.Single(harness.Notifier.OfType(GameEventTypes.BidsRevealed));
    }

    [Fact]
    public async Task 广播的叫牌事件不含具体数字()
    {
        var harness = await StartedGameAsync();

        await harness.Service.PlaceBidAsync(harness.Code, harness.PlayerIdAtSeat(0), 1);

        var placed = Assert.Single(harness.Notifier.OfType(GameEventTypes.BidPlaced));

        Assert.Equal(0, placed.Seat);
        Assert.Null(placed.Bids);
    }

    [Fact]
    public async Task 没轮到的人出牌会被拒绝()
    {
        var harness = await StartedGameAsync();
        await harness.BidAllAsync(0, 0, 0);

        var wrongSeat = (harness.Room.Game!.CurrentSeat + 1) % 3;
        var playerId = harness.PlayerIdAtSeat(wrongSeat);
        var cardId = harness.GameOf(harness.IndexAtSeat(wrongSeat)).MyHand[0].Id;

        var result = await harness.Service.PlayCardAsync(harness.Code, playerId, cardId, null);

        Assert.False(result.Ok);
        Assert.Contains("还没轮到", result.Error);
    }

    [Fact]
    public async Task 出别人的牌会被拒绝()
    {
        var harness = await StartedGameAsync();
        await harness.BidAllAsync(0, 0, 0);

        var current = harness.Room.Game!.CurrentSeat;
        var victim = (current + 1) % 3;
        var stolenCard = harness.GameOf(harness.IndexAtSeat(victim)).MyHand[0].Id;

        var result = await harness.Service.PlayCardAsync(harness.Code, harness.PlayerIdAtSeat(current), stolenCard, null);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 旁观者不能出牌()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.SeatAndReadyAsync(1);
        await harness.JoinAsync(2);
        await harness.Service.StandUpAsync(harness.Code, Harness.PlayerId(2));
        await harness.StartAsync();

        var result = await harness.Service.PlaceBidAsync(harness.Code, Harness.PlayerId(2), 0);

        Assert.False(result.Ok);
        Assert.Contains("旁观", result.Error);
    }

    [Fact]
    public async Task 只有当前行动方才拿得到可出牌清单()
    {
        var harness = await StartedGameAsync();
        await harness.BidAllAsync(0, 0, 0);

        var current = harness.Room.Game!.CurrentSeat;

        Assert.NotEmpty(harness.GameOf(harness.IndexAtSeat(current)).PlayableCardIds);
        Assert.Empty(harness.GameOf(harness.IndexAtSeat((current + 1) % 3)).PlayableCardIds);
    }

    [Fact]
    public async Task 一整局能跑到结束并产出名次()
    {
        var harness = await StartedGameAsync();

        await harness.PlayToEndAsync();

        Assert.Equal(RoomStatus.Finished, harness.Room.Status);
        Assert.Equal(GamePhase.Finished, harness.Room.Game!.Phase);
        Assert.Equal(10, harness.Room.Game.Rounds.Length);

        var ended = Assert.Single(harness.Notifier.OfType(GameEventTypes.GameEnded));
        Assert.NotNull(ended.WinnerSeats);
        Assert.NotEmpty(ended.WinnerSeats);
    }

    [Fact]
    public async Task 每一墩都会广播结算事件()
    {
        var harness = await StartedGameAsync(maxRounds: 3);

        await harness.PlayToEndAsync();

        // 3 人 3 轮，每轮墩数等于轮次号，共 1+2+3 = 6 墩。
        Assert.Equal(6, harness.Notifier.OfType(GameEventTypes.TrickResolved).Count());
        Assert.Equal(3, harness.Notifier.OfType(GameEventTypes.RoundScored).Count());
    }

    [Fact]
    public async Task 上一局结束后可以重新准备再开一局()
    {
        var harness = await StartedGameAsync(maxRounds: 1);
        await harness.PlayToEndAsync();

        var firstGameId = harness.Room.GameId;

        // 开局时准备状态被清空了，所以得让非房主重新确认一次。
        Assert.False(harness.Room.Members[Harness.PlayerId(1)].IsReady);
        Assert.True((await harness.Service.SetReadyAsync(harness.Code, Harness.PlayerId(1), true)).Ok);
        Assert.True((await harness.Service.SetReadyAsync(harness.Code, Harness.PlayerId(2), true)).Ok);

        Assert.True((await harness.StartAsync()).Ok);

        Assert.Equal(RoomStatus.Playing, harness.Room.Status);
        Assert.NotEqual(firstGameId, harness.Room.GameId);

        // 新的一局分数从零开始，不会把上一局的比分带过来。
        Assert.All(harness.Room.Game!.TotalScores, score => Assert.Equal(0, score));
    }

    [Fact]
    public async Task 每一步都写进了命令日志()
    {
        var harness = await StartedGameAsync(maxRounds: 2);

        await harness.PlayToEndAsync();

        // 2 人以上 2 轮：叫牌 3×2 次，出牌 (1+2)×3 次。
        Assert.Equal(6 + 9, harness.Archive.MoveCount(harness.Room.GameId!.Value));
    }
}
