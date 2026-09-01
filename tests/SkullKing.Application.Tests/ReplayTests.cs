using Microsoft.Extensions.Logging.Abstractions;
using SkullKing.Application.Replay;
using SkullKing.Contracts;

namespace SkullKing.Application.Tests;

public class ReplayTests
{
    private static async Task<(Harness Harness, GameReplayService Replays, Guid GameId)> FinishedGameAsync(int maxRounds = 3)
    {
        var harness = new Harness();
        await harness.CreateRoomAsync(maxRounds: maxRounds);
        await harness.SeatAndReadyAsync(1, 2);
        await harness.StartAsync();

        var gameId = harness.Room.GameId!.Value;

        await harness.PlayToEndAsync();

        var replays = new GameReplayService(harness.Archive, NullLogger<GameReplayService>.Instance);

        return (harness, replays, gameId);
    }

    [Fact]
    public async Task 回放重跑出和当时广播一致的事件序列()
    {
        var (harness, replays, gameId) = await FinishedGameAsync();

        var replay = await replays.BuildAsync(gameId);

        Assert.NotNull(replay);

        // 实时广播里还夹着系统提示（加入、掉线等），回放只重跑规则事件，所以要先滤掉。
        var live = harness.Notifier.Events
            .Where(e => e.Type != GameEventTypes.SystemNotice)
            .ToList();

        Assert.Equal(live.Select(e => e.Type), replay.Events.Select(e => e.Type));
        Assert.Equal(live.Select(e => e.Card?.Id), replay.Events.Select(e => e.Card?.Id));
        Assert.Equal(live.Select(e => e.WinnerSeat), replay.Events.Select(e => e.WinnerSeat));
    }

    [Fact]
    public async Task 回放带上了座位名单和轮数()
    {
        var (harness, replays, gameId) = await FinishedGameAsync();

        var replay = await replays.BuildAsync(gameId);

        Assert.NotNull(replay);
        Assert.Equal(harness.Room.Code, replay.RoomCode);
        Assert.Equal(3, replay.PlayerCount);
        Assert.Equal(3, replay.TotalRounds);
        Assert.Equal([0, 1, 2], replay.Seats.Select(s => s.Seat));
        Assert.All(replay.Seats, s => Assert.StartsWith("同事", s.Nickname));
    }

    [Fact]
    public async Task 回放最后一条是结束事件且分数与实时一致()
    {
        var (harness, replays, gameId) = await FinishedGameAsync();

        var replay = await replays.BuildAsync(gameId);

        var ended = Assert.Single(replay!.Events, e => e.Type == GameEventTypes.GameEnded);

        Assert.Equal(harness.Room.Game!.TotalScores.ToArray(), ended.TotalScores);
        Assert.Equal(GameEventTypes.GameEnded, replay.Events[^1].Type);
    }

    [Fact]
    public async Task 进行中的对局不给回放()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.SeatAndReadyAsync(1, 2);
        await harness.StartAsync();

        var replays = new GameReplayService(harness.Archive, NullLogger<GameReplayService>.Instance);

        Assert.Null(await replays.BuildAsync(harness.Room.GameId!.Value));
    }

    [Fact]
    public async Task 不存在的对局返回空()
    {
        var harness = new Harness();
        var replays = new GameReplayService(harness.Archive, NullLogger<GameReplayService>.Instance);

        Assert.Null(await replays.BuildAsync(Guid.NewGuid()));
    }
}
