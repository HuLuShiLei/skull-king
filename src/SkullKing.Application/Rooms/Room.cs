using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Rooms;

/// <summary>
/// 房间的运行时对象。对局状态常驻内存，领域事件同步落库，所以进程重启后能回放恢复。
/// 所有会改状态的操作都必须先拿 <see cref="Gate"/>，否则并发出牌会打乱回合顺序。
/// </summary>
public sealed class Room
{
    private const int ChatHistoryLimit = 200;

    public required Guid Id { get; init; }

    public required string Code { get; init; }

    public required RoomSettings Settings { get; set; }

    public required string HostPlayerId { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Waiting;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public Dictionary<string, RoomMember> Members { get; } = [];

    public GameState? Game { get; set; }

    public Guid? GameId { get; set; }

    /// <summary>事件序号，落库和广播共用，客户端可据此丢弃乱序消息。</summary>
    public long EventSeq { get; set; }

    /// <summary>命令序号。恢复对局靠按序重放这些命令。</summary>
    public long MoveSeq { get; set; }

    public List<ChatMessageDto> Chat { get; } = [];

    /// <summary>当前行动方的超时时刻，null 表示不限时或无人待行动。</summary>
    public DateTimeOffset? TurnDeadline { get; set; }

    /// <summary>在此时刻之前不启动托管。服务重启恢复对局后要给玩家留出重连时间。</summary>
    public DateTimeOffset? AutoPlaySuppressedUntil { get; set; }

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public IEnumerable<RoomMember> SeatedMembers =>
        Members.Values.Where(m => !m.IsSpectator).OrderBy(m => m.Seat);

    public IEnumerable<RoomMember> Spectators => Members.Values.Where(m => m.IsSpectator);

    public int SeatedCount => Members.Values.Count(m => !m.IsSpectator);

    public bool IsFull => SeatedCount >= Settings.MaxPlayers;

    public RoomMember? MemberAtSeat(int seat) =>
        seat < 0 ? null : Members.Values.FirstOrDefault(m => m.Seat == seat);

    public RoomMember? Host => Members.GetValueOrDefault(HostPlayerId);

    /// <summary>找一个没人坐的座位号，满了返回 -1。</summary>
    public int FindFreeSeat()
    {
        var taken = Members.Values.Where(m => !m.IsSpectator).Select(m => m.Seat).ToHashSet();

        for (var seat = 0; seat < Settings.MaxPlayers; seat++)
        {
            if (!taken.Contains(seat))
            {
                return seat;
            }
        }

        return -1;
    }

    /// <summary>
    /// 开局前把座位压成 0..N-1 连续编号。规则引擎假定座位连续，
    /// 而中途有人退出会在编号里留洞。
    /// </summary>
    public void CompactSeats()
    {
        var seated = SeatedMembers.ToList();

        for (var i = 0; i < seated.Count; i++)
        {
            seated[i].Seat = i;
        }
    }

    public void AppendChat(ChatMessageDto message)
    {
        Chat.Add(message);

        if (Chat.Count > ChatHistoryLimit)
        {
            Chat.RemoveRange(0, Chat.Count - ChatHistoryLimit);
        }
    }

    public IReadOnlyList<ChatMessageDto> RecentChat(int count = 50) =>
        Chat.Count <= count ? Chat.ToArray() : Chat.Skip(Chat.Count - count).ToArray();

    /// <summary>房间空了且不在对局中就可以回收。</summary>
    public bool IsAbandoned => Members.Values.All(m => !m.IsConnected);

    public void ResetReadyFlags()
    {
        foreach (var member in Members.Values)
        {
            member.IsReady = false;
        }
    }
}
