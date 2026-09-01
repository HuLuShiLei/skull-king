using SkullKing.Application.Rooms;
using SkullKing.Contracts;

namespace SkullKing.Application.Tests;

public class RoomLobbyTests
{
    [Fact]
    public async Task 建房后房主自动坐在零号位()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        var state = harness.StateOf(0);

        Assert.Equal(0, state.YourSeat);
        Assert.Equal(Harness.PlayerId(0), state.HostPlayerId);
        Assert.Single(state.Members);
    }

    [Fact]
    public async Task 房间号是六位且只用不易混淆的字符()
    {
        var harness = new Harness();
        var code = await harness.CreateRoomAsync();

        Assert.Equal(6, code.Length);
        Assert.True(RoomCode.IsValid(code));
        Assert.DoesNotContain(code, c => c is '0' or 'O' or '1' or 'I' or 'L');
    }

    [Fact]
    public async Task 加入不存在的房间会被拒绝()
    {
        var harness = new Harness();

        var result = await harness.Service.JoinAsync("ZZZZZZ", "x", "路人", null, "conn-x");

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 密码不对进不来密码对了能进()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync(password: "6688");

        Assert.False((await harness.JoinAsync(1, "1234")).Ok);
        Assert.True((await harness.JoinAsync(1, "6688")).Ok);
    }

    [Fact]
    public async Task 房里的人能拿到口令明文用来拼邀请链接()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync(password: "6688");

        Assert.Equal("6688", harness.StateOf(0).Settings.Password);
    }

    [Fact]
    public async Task 座位满了之后进来的人只能旁观()
    {
        var harness = new Harness();
        await harness.Service.CreateRoomAsync(Harness.PlayerId(0), Harness.Nickname(0), new CreateRoomRequest { MaxPlayers = 2 });

        var room = harness.Service.ListPublicRooms()[0];
        var code = room.Code;

        await harness.Service.JoinAsync(code, Harness.PlayerId(0), Harness.Nickname(0), null, "conn-0");
        await harness.Service.JoinAsync(code, Harness.PlayerId(1), Harness.Nickname(1), null, "conn-1");
        await harness.Service.JoinAsync(code, Harness.PlayerId(2), Harness.Nickname(2), null, "conn-2");

        var state = harness.Notifier.LatestState[Harness.PlayerId(2)];

        Assert.Equal(-1, state.YourSeat);
        Assert.True(state.Members.Single(m => m.PlayerId == Harness.PlayerId(2)).IsSpectator);
    }

    [Fact]
    public async Task 人不够不能开局()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        var result = await harness.StartAsync();

        Assert.False(result.Ok);
        Assert.Contains("入座", result.Error);
    }

    [Fact]
    public async Task 有人没准备不能开局()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        var result = await harness.StartAsync();

        Assert.False(result.Ok);
        Assert.Contains("没准备", result.Error);
    }

    [Fact]
    public async Task 只有房主能开局()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.SeatAndReadyAsync(1);

        var result = await harness.Service.StartGameAsync(harness.Code, Harness.PlayerId(1));

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 只有房主能改设置()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        var byMember = await harness.Service.UpdateSettingsAsync(harness.Code, Harness.PlayerId(1), new UpdateRoomSettingsRequest { MaxRounds = 5 });
        var byHost = await harness.Service.UpdateSettingsAsync(harness.Code, Harness.PlayerId(0), new UpdateRoomSettingsRequest { MaxRounds = 5 });

        Assert.False(byMember.Ok);
        Assert.True(byHost.Ok);
        Assert.Equal(5, harness.Room.Settings.MaxRounds);
    }

    [Fact]
    public async Task 改名会同步到牌桌上的名字()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        await harness.Service.RenameAsync(Harness.PlayerId(1), "改过的名字");

        Assert.Equal("改过的名字", harness.Room.Members[Harness.PlayerId(1)].Nickname);
        Assert.Contains(harness.StateOf(0).Members, m => m.Nickname == "改过的名字");
    }

    [Fact]
    public async Task 人数上限不能调到低于已入座人数()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.SeatAndReadyAsync(1, 2);

        var result = await harness.Service.UpdateSettingsAsync(harness.Code, Harness.PlayerId(0), new UpdateRoomSettingsRequest { MaxPlayers = 2 });

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 房主可以踢人被踢者收到通知()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        var result = await harness.Service.KickAsync(harness.Code, Harness.PlayerId(0), Harness.PlayerId(1));

        Assert.True(result.Ok);
        Assert.DoesNotContain(Harness.PlayerId(1), harness.Room.Members.Keys);
        Assert.Contains(harness.Notifier.Removals, r => r.PlayerId == Harness.PlayerId(1));
    }

    [Fact]
    public async Task 非房主不能踢人()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);
        await harness.JoinAsync(2);

        var result = await harness.Service.KickAsync(harness.Code, Harness.PlayerId(1), Harness.PlayerId(2));

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task 房主退出后自动转让给下一位()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        await harness.Service.LeaveAsync(harness.Code, Harness.PlayerId(0));

        Assert.Equal(Harness.PlayerId(1), harness.Room.HostPlayerId);
    }

    [Fact]
    public async Task 人走光了房间被回收()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        await harness.Service.LeaveAsync(harness.Code, Harness.PlayerId(0));

        Assert.Null(harness.Service.Find(harness.Code));
        Assert.Empty(harness.Service.ListPublicRooms());
    }

    [Fact]
    public async Task 旁观者可以入座也可以离座()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();
        await harness.JoinAsync(1);

        await harness.Service.StandUpAsync(harness.Code, Harness.PlayerId(1));
        Assert.True(harness.Room.Members[Harness.PlayerId(1)].IsSpectator);

        await harness.Service.SitDownAsync(harness.Code, Harness.PlayerId(1));
        Assert.False(harness.Room.Members[Harness.PlayerId(1)].IsSpectator);
    }

    [Fact]
    public async Task 聊天消息广播给全房间并落库()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        await harness.Service.SendChatAsync(harness.Code, Harness.PlayerId(0), "  这个需求下周再说  ");

        var message = Assert.Single(harness.Notifier.Chats);
        Assert.Equal("这个需求下周再说", message.Text);
        Assert.Single(harness.Archive.Chats);
    }

    [Fact]
    public async Task 不在房间里的人不能发言()
    {
        var harness = new Harness();
        await harness.CreateRoomAsync();

        var result = await harness.Service.SendChatAsync(harness.Code, "路人甲", "让我看看");

        Assert.False(result.Ok);
    }
}
