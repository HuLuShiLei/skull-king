using Microsoft.Extensions.Logging.Abstractions;
using SkullKing.Application.Rooms;
using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Tests;

public class ResilienceTests
{
    private static async Task<Harness> StartedGameAsync(int turnSeconds = 60)
    {
        var harness = new Harness();
        await harness.CreateRoomAsync(turnSeconds);
        await harness.SeatAndReadyAsync(1, 2);
        await harness.StartAsync();

        return harness;
    }

    [Fact]
    public async Task 超时后系统代为叫零()
    {
        var harness = await StartedGameAsync(turnSeconds: 30);

        harness.Clock.Advance(TimeSpan.FromSeconds(31));
        await harness.Service.TickAsync();

        var game = harness.Room.Game!;

        Assert.All(game.Bids, bid => Assert.Equal(0, bid));
        Assert.Equal(GamePhase.Playing, game.Phase);
        Assert.Contains(harness.Notifier.OfType(GameEventTypes.SystemNotice), e => e.Text!.Contains("超时"));
    }

    [Fact]
    public async Task 没到时间不会被托管()
    {
        var harness = await StartedGameAsync(turnSeconds: 30);

        harness.Clock.Advance(TimeSpan.FromSeconds(29));
        await harness.Service.TickAsync();

        Assert.All(harness.Room.Game!.Bids, Assert.Null);
    }

    [Fact]
    public async Task 不限时的房间永不超时托管()
    {
        var harness = await StartedGameAsync(turnSeconds: 0);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        await harness.Service.TickAsync();

        Assert.All(harness.Room.Game!.Bids, Assert.Null);
    }

    [Fact]
    public async Task 掉线超过宽限期就交给托管()
    {
        var harness = await StartedGameAsync(turnSeconds: 0);

        await harness.Service.DisconnectAsync("conn-1");
        harness.Clock.Advance(TimeSpan.FromSeconds(21));
        await harness.Service.TickAsync();

        var seatOfPlayer1 = harness.Room.Members[Harness.PlayerId(1)].Seat;

        Assert.Equal(0, harness.Room.Game!.Bids[seatOfPlayer1]);
        Assert.Null(harness.Room.Game.Bids[harness.Room.Members[Harness.PlayerId(0)].Seat]);
    }

    [Fact]
    public async Task 对局中掉线保留座位而不是踢出()
    {
        var harness = await StartedGameAsync();

        await harness.Service.DisconnectAsync("conn-1");

        var member = harness.Room.Members[Harness.PlayerId(1)];

        Assert.False(member.IsConnected);
        Assert.False(member.IsSpectator);
        Assert.Equal(3, harness.Room.Game!.PlayerCount);
    }

    [Fact]
    public async Task 对局中可以把限时关掉从此不再托管()
    {
        var harness = await StartedGameAsync(turnSeconds: 30);

        var result = await harness.Service.UpdateSettingsAsync(
            harness.Code, Harness.PlayerId(0), new UpdateRoomSettingsRequest { TurnSeconds = 0 });

        Assert.True(result.Ok);

        // 关掉之后旧的截止时间也得一起清掉，否则下一次巡检照样托管。
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        await harness.Service.TickAsync();

        Assert.All(harness.Room.Game!.Bids, Assert.Null);
    }

    [Fact]
    public async Task 对局中不许改限时以外的设置()
    {
        var harness = await StartedGameAsync();

        var result = await harness.Service.UpdateSettingsAsync(
            harness.Code, Harness.PlayerId(0), new UpdateRoomSettingsRequest { MaxRounds = 3 });

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 等待中掉线先留着座位()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        await harness.Service.DisconnectAsync("conn-1");

        // 刷新页面、切后台被掐掉心跳都会走到这，立刻清人的话回来就没座位了。
        var member = harness.Room.Members[Harness.PlayerId(1)];

        Assert.False(member.IsConnected);
        Assert.NotNull(member.DisconnectedAt);
    }

    [Fact]
    public async Task 一个人的房间掉线后重连还在()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        // 房主独自在房间里，这是最容易被误回收的情形。
        await harness.Service.DisconnectAsync("conn-0");

        harness.Clock.Advance(TimeSpan.FromMinutes(3));
        await harness.Service.TickAsync();

        var result = await harness.Service.JoinAsync(
            harness.Code, Harness.PlayerId(0), Harness.Nickname(0), null, "conn-0-new");

        Assert.True(result.Ok);
        Assert.Equal(0, harness.Room.Members[Harness.PlayerId(0)].Seat);
    }

    [Fact]
    public async Task 掉线久到没回来才真的清出去()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        await harness.Service.DisconnectAsync("conn-1");

        harness.Clock.Advance(TimeSpan.FromMinutes(11));
        await harness.Service.TickAsync();

        Assert.DoesNotContain(Harness.PlayerId(1), harness.Room.Members.Keys);
    }

    [Fact]
    public async Task 所有人都久未回来时房间被回收()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        await harness.Service.DisconnectAsync("conn-0");

        harness.Clock.Advance(TimeSpan.FromMinutes(11));
        await harness.Service.TickAsync();

        Assert.Null(harness.Service.Find(harness.Code));
    }

    [Fact]
    public async Task 重连回原座位并拿回手牌()
    {
        var harness = await StartedGameAsync();

        var seat = harness.Room.Members[Harness.PlayerId(1)].Seat;
        var handBefore = harness.GameOf(1).MyHand.Select(c => c.Id).ToList();

        await harness.Service.DisconnectAsync("conn-1");
        await harness.Service.JoinAsync(harness.Code, Harness.PlayerId(1), Harness.Nickname(1), null, "conn-1-new");

        var state = harness.StateOf(1);

        Assert.Equal(seat, state.YourSeat);
        Assert.Equal(handBefore, state.Game!.MyHand.Select(c => c.Id));
        Assert.True(harness.Room.Members[Harness.PlayerId(1)].IsConnected);
    }

    [Fact]
    public async Task 同一个人开两个标签页只算一次在线()
    {
        var harness = await StartedGameAsync();

        await harness.Service.JoinAsync(harness.Code, Harness.PlayerId(1), Harness.Nickname(1), null, "conn-1-second");
        await harness.Service.DisconnectAsync("conn-1");

        Assert.True(harness.Room.Members[Harness.PlayerId(1)].IsConnected);
    }

    [Fact]
    public async Task 重启后靠重放命令恢复到同一个状态()
    {
        var harness = await StartedGameAsync();

        await harness.BidAllAsync(0, 1, 0);
        await harness.PlayFirstLegalAsync();

        var before = harness.Room.Game!;

        // 换一个全新的 RoomService，只带着同一份归档，模拟进程重启。
        var revived = new RoomService(harness.Archive, harness.Notifier, NullLogger<RoomService>.Instance, harness.Clock);
        await revived.RestoreAsync();

        var room = revived.Find(harness.Code);
        Assert.NotNull(room);

        var after = room.Game!;

        Assert.Equal(before.RoundNumber, after.RoundNumber);
        Assert.Equal(before.TrickNumber, after.TrickNumber);
        Assert.Equal(before.CurrentSeat, after.CurrentSeat);
        // ImmutableArray 的相等语义是比较底层数组引用，这里要的是内容相等。
        Assert.Equal(before.Bids.ToArray(), after.Bids.ToArray());
        Assert.Equal(before.TotalScores.ToArray(), after.TotalScores.ToArray());
        Assert.Equal(
            before.CurrentTrick.Select(p => p.Card.Id),
            after.CurrentTrick.Select(p => p.Card.Id));

        for (var seat = 0; seat < before.PlayerCount; seat++)
        {
            Assert.Equal(before.Hands[seat].Select(c => c.Id), after.Hands[seat].Select(c => c.Id));
        }
    }

    [Fact]
    public async Task 恢复后留出重连窗口不立即托管()
    {
        var harness = await StartedGameAsync(turnSeconds: 0);

        var revived = new RoomService(harness.Archive, harness.Notifier, NullLogger<RoomService>.Instance, harness.Clock);
        await revived.RestoreAsync();

        // 恢复出来的成员都是断线状态，但不该马上被系统接管。
        harness.Clock.Advance(TimeSpan.FromSeconds(30));
        await revived.TickAsync();

        Assert.All(revived.Find(harness.Code)!.Game!.Bids, Assert.Null);

        harness.Clock.Advance(TimeSpan.FromSeconds(70));
        await revived.TickAsync();

        Assert.All(revived.Find(harness.Code)!.Game!.Bids, bid => Assert.Equal(0, bid));
    }

    [Fact]
    public async Task 已结束的对局不会被恢复()
    {
        var harness = await StartedGameAsync();
        await harness.PlayToEndAsync();

        var revived = new RoomService(harness.Archive, harness.Notifier, NullLogger<RoomService>.Instance, harness.Clock);
        await revived.RestoreAsync();

        Assert.Null(revived.Find(harness.Code));
    }
}
