using Microsoft.Extensions.Logging.Abstractions;
using SkullKing.Application.Rooms;
using SkullKing.Contracts;

namespace SkullKing.Application.Tests;

internal sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>把「建房 + 拉人 + 准备 + 开局」这一串固定流程收在一起，让测试只关心要验的那一点。</summary>
internal sealed class Harness
{
    public FakeArchive Archive { get; } = new();

    public RecordingNotifier Notifier { get; } = new();

    public TestClock Clock { get; } = new();

    public RoomService Service { get; }

    public string Code { get; private set; } = string.Empty;

    public Harness()
    {
        Service = new RoomService(Archive, Notifier, NullLogger<RoomService>.Instance, Clock);
    }

    public static string PlayerId(int index) => $"player-{index}";

    public static string Nickname(int index) => $"同事{index}";

    public async Task<string> CreateRoomAsync(int turnSeconds = 60, int maxRounds = 10, string? password = null)
    {
        var room = await Service.CreateRoomAsync(PlayerId(0), Nickname(0), new CreateRoomRequest
        {
            Name = "需求评审组",
            MaxPlayers = 6,
            MaxRounds = maxRounds,
            TurnSeconds = turnSeconds,
            Password = password
        });

        Code = room.Code;

        // 房主是在 REST 接口里建的房，还得再走一次 Join 才算连上。
        await Service.JoinAsync(Code, PlayerId(0), Nickname(0), password, "conn-0");

        return Code;
    }

    public Task<RoomActionResult> JoinAsync(int index, string? password = null) =>
        Service.JoinAsync(Code, PlayerId(index), Nickname(index), password, $"conn-{index}");

    public async Task SeatAndReadyAsync(params int[] indexes)
    {
        foreach (var index in indexes)
        {
            await JoinAsync(index);
            await Service.SetReadyAsync(Code, PlayerId(index), true);
        }
    }

    public async Task<RoomActionResult> StartAsync() => await Service.StartGameAsync(Code, PlayerId(0));

    public Room Room => Service.Find(Code)!;

    public RoomStateDto StateOf(int index) => Notifier.LatestState[PlayerId(index)];

    public GameViewDto GameOf(int index) => StateOf(index).Game!;

    /// <summary>按座位顺序让所有人叫牌。</summary>
    public async Task BidAllAsync(params int[] bids)
    {
        for (var seat = 0; seat < bids.Length; seat++)
        {
            await Service.PlaceBidAsync(Code, PlayerIdAtSeat(seat), bids[seat]);
        }
    }

    public string PlayerIdAtSeat(int seat) => Room.MemberAtSeat(seat)!.PlayerId;

    public int IndexAtSeat(int seat) => int.Parse(PlayerIdAtSeat(seat).Split('-')[1]);

    /// <summary>让当前该出牌的人打出第一张合法牌。</summary>
    public async Task<RoomActionResult> PlayFirstLegalAsync()
    {
        var game = Room.Game!;
        var seat = game.CurrentSeat;
        var playerId = PlayerIdAtSeat(seat);
        var view = Notifier.LatestState[playerId].Game!;

        return await Service.PlayCardAsync(Code, playerId, view.PlayableCardIds[0], null);
    }

    /// <summary>把整局打完，用于验证编排层不会在中途卡住。</summary>
    public async Task PlayToEndAsync()
    {
        var guard = 0;

        while (Room.Status == RoomStatus.Playing && guard++ < 5000)
        {
            var game = Room.Game!;

            if (game.Phase == Domain.Game.GamePhase.Bidding)
            {
                for (var seat = 0; seat < game.PlayerCount; seat++)
                {
                    if (!game.Bids[seat].HasValue)
                    {
                        await Service.PlaceBidAsync(Code, PlayerIdAtSeat(seat), 0);
                    }
                }

                continue;
            }

            await PlayFirstLegalAsync();
        }
    }
}
