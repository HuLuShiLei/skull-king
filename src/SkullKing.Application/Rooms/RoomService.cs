using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Projection;
using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Rooms;

/// <summary>
/// 大厅与房间的唯一入口。所有会改状态的操作都在房间锁内完成，
/// 顺序是「校验 → 改内存 → 落库 → 广播事件 → 单播快照」。
/// </summary>
public sealed partial class RoomService(
    IGameArchive archive,
    IRoomNotifier notifier,
    ILogger<RoomService> logger,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    private readonly ConcurrentDictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>connectionId 到「房间号 + 玩家」的反查表，断线时要靠它定位。</summary>
    private readonly ConcurrentDictionary<string, (string Code, string PlayerId)> _connections = new();

    private DateTimeOffset Now => _clock.GetUtcNow();

    public Room? Find(string? code) => code is null ? null : _rooms.GetValueOrDefault(RoomCode.Normalize(code));

    public IReadOnlyList<RoomSummaryDto> ListPublicRooms()
    {
        return
        [
            .. _rooms.Values
                .Where(r => r.Settings.IsPublic)
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(ToSummary)
        ];
    }

    public RoomProbeDto Probe(string code)
    {
        var normalized = RoomCode.Normalize(code);
        var room = Find(normalized);

        return room is null
            ? new RoomProbeDto(normalized, string.Empty, false, false, false, RoomStatus.Waiting)
            : new RoomProbeDto(room.Code, room.Settings.Name, true, room.Settings.HasPassword, room.IsFull, room.Status);
    }

    public async Task<Room> CreateRoomAsync(string hostPlayerId, string hostNickname, CreateRoomRequest request, CancellationToken ct = default)
    {
        var settings = new RoomSettings
        {
            Name = request.Name ?? $"{hostNickname}的协作组",
            IsPublic = request.IsPublic,
            MaxPlayers = request.MaxPlayers,
            MaxRounds = request.MaxRounds,
            TurnSeconds = request.TurnSeconds,
            PasswordHash = PasswordHasher.Hash(request.Password)
        }.Sanitized();

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Code = NextFreeCode(),
            Settings = settings,
            HostPlayerId = hostPlayerId
        };

        var host = new RoomMember(hostPlayerId, hostNickname) { Seat = 0 };
        room.Members[hostPlayerId] = host;

        _rooms[room.Code] = room;

        await archive.UpsertRoomAsync(ToPersisted(room), ct);
        await notifier.BroadcastLobbyChangedAsync(ct);

        logger.LogInformation("房间 {Code} 已创建，房主 {Host}", room.Code, hostNickname);

        return room;
    }

    public async Task<RoomActionResult> JoinAsync(
        string code,
        string playerId,
        string nickname,
        string? password,
        string connectionId,
        CancellationToken ct = default)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在或已解散");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            var existing = room.Members.GetValueOrDefault(playerId);

            if (existing is null)
            {
                if (!PasswordHasher.Verify(room.Settings.PasswordHash, password))
                {
                    return RoomActionResult.Fail("房间密码不正确");
                }

                // 对局进行中或座位满了都只能观战，不能挤掉正在打牌的人。
                var seat = room.Status == RoomStatus.Playing ? -1 : room.FindFreeSeat();

                existing = new RoomMember(playerId, nickname) { Seat = seat };
                room.Members[playerId] = existing;

                await BroadcastNoticeAsync(room, seat >= 0 ? $"{nickname} 加入了群聊" : $"{nickname} 以旁观身份加入", ct);
            }
            else
            {
                existing.Nickname = nickname;
                existing.DisconnectedAt = null;

                if (!existing.IsConnected)
                {
                    await BroadcastNoticeAsync(room, $"{nickname} 重新上线", ct);
                }
            }

            existing.ConnectionIds.Add(connectionId);
            _connections[connectionId] = (room.Code, playerId);

            await archive.ReplaceMembersAsync(room.Id, ToPersistedMembers(room), ct);
            await PushStateAsync(room, ct);
            await notifier.BroadcastLobbyChangedAsync(ct);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public async Task<RoomActionResult> LeaveAsync(string code, string playerId, CancellationToken ct = default)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (!room.Members.TryGetValue(playerId, out var member))
            {
                return RoomActionResult.Success;
            }

            foreach (var connectionId in member.ConnectionIds)
            {
                _connections.TryRemove(connectionId, out _);
            }

            // 对局进行中不能真的删人，否则座位会塌陷；标记成断线由系统托管。
            if (room.Status == RoomStatus.Playing && !member.IsSpectator)
            {
                member.ConnectionIds.Clear();
                member.DisconnectedAt = Now;

                await BroadcastNoticeAsync(room, $"{member.Nickname} 离线，由系统代为处理", ct);
                await PushStateAsync(room, ct);

                return RoomActionResult.Success;
            }

            room.Members.Remove(playerId);
            await BroadcastNoticeAsync(room, $"{member.Nickname} 退出了群聊", ct);

            await HandleHostDepartureAsync(room, playerId, ct);
            await FinalizeMembershipChangeAsync(room, ct);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public async Task DisconnectAsync(string connectionId, CancellationToken ct = default)
    {
        if (!_connections.TryRemove(connectionId, out var link))
        {
            return;
        }

        var room = Find(link.Code);

        if (room is null)
        {
            return;
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (!room.Members.TryGetValue(link.PlayerId, out var member))
            {
                return;
            }

            member.ConnectionIds.Remove(connectionId);

            if (member.IsConnected)
            {
                return;
            }

            member.DisconnectedAt = Now;

            // 没在打牌的房间里断线就直接清出去，免得占着座位不开局。
            if (room.Status != RoomStatus.Playing)
            {
                room.Members.Remove(link.PlayerId);
                await BroadcastNoticeAsync(room, $"{member.Nickname} 已离开", ct);
                await HandleHostDepartureAsync(room, link.PlayerId, ct);
            }
            else
            {
                await BroadcastNoticeAsync(room, $"{member.Nickname} 掉线了", ct);
            }

            await FinalizeMembershipChangeAsync(room, ct);
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public Task<RoomActionResult> SetReadyAsync(string code, string playerId, bool ready, CancellationToken ct = default) =>
        MutateAsync(code, playerId, room =>
        {
            if (room.Status == RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局已经开始了");
            }

            var member = room.Members[playerId];

            if (member.IsSpectator)
            {
                return RoomActionResult.Fail("旁观者不需要准备");
            }

            member.IsReady = ready;

            return RoomActionResult.Success;
        }, ct);

    public Task<RoomActionResult> SitDownAsync(string code, string playerId, CancellationToken ct = default) =>
        MutateAsync(code, playerId, room =>
        {
            if (room.Status == RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局进行中不能入座");
            }

            var member = room.Members[playerId];

            if (!member.IsSpectator)
            {
                return RoomActionResult.Success;
            }

            var seat = room.FindFreeSeat();

            if (seat < 0)
            {
                return RoomActionResult.Fail("座位已满");
            }

            member.Seat = seat;

            return RoomActionResult.Success;
        }, ct);

    public Task<RoomActionResult> StandUpAsync(string code, string playerId, CancellationToken ct = default) =>
        MutateAsync(code, playerId, room =>
        {
            if (room.Status == RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局进行中不能离座");
            }

            var member = room.Members[playerId];
            member.Seat = -1;
            member.IsReady = false;

            return RoomActionResult.Success;
        }, ct);

    public Task<RoomActionResult> UpdateSettingsAsync(
        string code,
        string playerId,
        UpdateRoomSettingsRequest request,
        CancellationToken ct = default) =>
        MutateAsync(code, playerId, room =>
        {
            if (room.HostPlayerId != playerId)
            {
                return RoomActionResult.Fail("只有房主能改设置");
            }

            if (room.Status == RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局进行中不能改设置");
            }

            var updated = room.Settings with
            {
                Name = request.Name ?? room.Settings.Name,
                IsPublic = request.IsPublic ?? room.Settings.IsPublic,
                MaxPlayers = request.MaxPlayers ?? room.Settings.MaxPlayers,
                MaxRounds = request.MaxRounds ?? room.Settings.MaxRounds,
                TurnSeconds = request.TurnSeconds ?? room.Settings.TurnSeconds
            };

            var sanitized = updated.Sanitized();

            if (sanitized.MaxPlayers < room.SeatedCount)
            {
                return RoomActionResult.Fail($"已经有 {room.SeatedCount} 人入座，上限不能再调低");
            }

            room.Settings = sanitized;

            return RoomActionResult.Success;
        }, ct);

    public async Task<RoomActionResult> KickAsync(string code, string hostPlayerId, string targetPlayerId, CancellationToken ct = default)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (room.HostPlayerId != hostPlayerId)
            {
                return RoomActionResult.Fail("只有房主能移出成员");
            }

            if (targetPlayerId == hostPlayerId)
            {
                return RoomActionResult.Fail("不能移出自己");
            }

            if (!room.Members.TryGetValue(targetPlayerId, out var target))
            {
                return RoomActionResult.Fail("该成员不在房间里");
            }

            if (room.Status == RoomStatus.Playing && !target.IsSpectator)
            {
                return RoomActionResult.Fail("对局进行中不能移出参战玩家");
            }

            foreach (var connectionId in target.ConnectionIds)
            {
                _connections.TryRemove(connectionId, out _);
            }

            room.Members.Remove(targetPlayerId);

            await notifier.SendRemovedAsync(targetPlayerId, room.Code, "你已被房主移出群聊", ct);
            await BroadcastNoticeAsync(room, $"{target.Nickname} 已被移出群聊", ct);
            await FinalizeMembershipChangeAsync(room, ct);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public Task<RoomActionResult> TransferHostAsync(string code, string hostPlayerId, string targetPlayerId, CancellationToken ct = default) =>
        MutateAsync(code, hostPlayerId, room =>
        {
            if (room.HostPlayerId != hostPlayerId)
            {
                return RoomActionResult.Fail("只有房主能转让");
            }

            if (!room.Members.TryGetValue(targetPlayerId, out var target))
            {
                return RoomActionResult.Fail("该成员不在房间里");
            }

            room.HostPlayerId = targetPlayerId;

            return RoomActionResult.Success;
        }, ct, room => $"{room.Members[targetPlayerId].Nickname} 成为新的群主");

    public async Task<RoomActionResult> SendChatAsync(string code, string playerId, string text, CancellationToken ct = default)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return RoomActionResult.Success;
        }

        if (trimmed.Length > 500)
        {
            trimmed = trimmed[..500];
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (!room.Members.TryGetValue(playerId, out var member))
            {
                return RoomActionResult.Fail("你不在这个房间里");
            }

            var message = new ChatMessageDto(
                Guid.NewGuid().ToString("N"),
                playerId,
                member.Nickname,
                member.Seat,
                trimmed,
                Now);

            room.AppendChat(message);

            await archive.SaveChatAsync(room.Id, message, ct);
            await notifier.BroadcastChatAsync(room.Code, message, ct);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public RoomStateDto? BuildStateFor(Room room, string playerId)
    {
        if (!room.Members.TryGetValue(playerId, out var viewer))
        {
            return null;
        }

        var members = room.Members.Values
            .OrderBy(m => m.IsSpectator)
            .ThenBy(m => m.Seat)
            .ThenBy(m => m.Nickname, StringComparer.Ordinal)
            .Select(m => new RoomMemberDto(
                m.PlayerId,
                m.Nickname,
                m.Seat,
                m.PlayerId == room.HostPlayerId,
                m.IsReady,
                m.IsConnected,
                m.IsSpectator,
                ScoreOf(room, m.Seat)))
            .ToArray();

        var game = room.Game is null ? null : GameProjector.Project(room, room.Game, viewer.Seat, Now);

        return new RoomStateDto(
            room.Code,
            new RoomSettingsDto
            {
                Name = room.Settings.Name,
                IsPublic = room.Settings.IsPublic,
                MaxPlayers = room.Settings.MaxPlayers,
                MaxRounds = room.Settings.MaxRounds,
                TurnSeconds = room.Settings.TurnSeconds,
                HasPassword = room.Settings.HasPassword
            },
            room.Status,
            room.HostPlayerId,
            playerId,
            viewer.Seat,
            members,
            game,
            room.RecentChat());
    }

    private static int ScoreOf(Room room, int seat) =>
        room.Game is { } game && seat >= 0 && seat < game.PlayerCount ? game.TotalScores[seat] : 0;

    private async Task PushStateAsync(Room room, CancellationToken ct)
    {
        foreach (var member in room.Members.Values.Where(m => m.IsConnected).ToList())
        {
            var state = BuildStateFor(room, member.PlayerId);

            if (state is not null)
            {
                await notifier.SendRoomStateAsync(member.PlayerId, state, ct);
            }
        }
    }

    private async Task BroadcastNoticeAsync(Room room, string text, CancellationToken ct)
    {
        await notifier.BroadcastEventAsync(room.Code, GameProjector.SystemNotice(text, ++room.EventSeq), ct);
    }

    /// <summary>成员变动后的统一收尾：落库、推快照、必要时回收房间。</summary>
    private async Task FinalizeMembershipChangeAsync(Room room, CancellationToken ct)
    {
        if (room.Members.Count == 0)
        {
            _rooms.TryRemove(room.Code, out _);
            await archive.UpdateRoomStatusAsync(room.Id, RoomStatus.Finished, ct);
            await notifier.BroadcastLobbyChangedAsync(ct);

            logger.LogInformation("房间 {Code} 已空，回收", room.Code);
            return;
        }

        await archive.ReplaceMembersAsync(room.Id, ToPersistedMembers(room), ct);
        await PushStateAsync(room, ct);
        await notifier.BroadcastLobbyChangedAsync(ct);
    }

    private async Task HandleHostDepartureAsync(Room room, string departedPlayerId, CancellationToken ct)
    {
        if (room.HostPlayerId != departedPlayerId || room.Members.Count == 0)
        {
            return;
        }

        var successor = room.Members.Values
            .OrderBy(m => m.IsSpectator)
            .ThenBy(m => m.Seat)
            .First();

        room.HostPlayerId = successor.PlayerId;

        await BroadcastNoticeAsync(room, $"{successor.Nickname} 接任群主", ct);
    }

    /// <summary>房间锁 + 落库 + 推快照的通用包装，供那些只改房间元数据的操作复用。</summary>
    private async Task<RoomActionResult> MutateAsync(
        string code,
        string playerId,
        Func<Room, RoomActionResult> mutate,
        CancellationToken ct,
        Func<Room, string>? notice = null)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (!room.Members.ContainsKey(playerId))
            {
                return RoomActionResult.Fail("你不在这个房间里");
            }

            var result = mutate(room);

            if (!result.Ok)
            {
                return result;
            }

            if (notice is not null)
            {
                await BroadcastNoticeAsync(room, notice(room), ct);
            }

            await archive.ReplaceMembersAsync(room.Id, ToPersistedMembers(room), ct);
            await archive.UpsertRoomAsync(ToPersisted(room), ct);
            await PushStateAsync(room, ct);
            await notifier.BroadcastLobbyChangedAsync(ct);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    private string NextFreeCode()
    {
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var code = RoomCode.Generate();

            if (!_rooms.ContainsKey(code))
            {
                return code;
            }
        }

        throw new InvalidOperationException("房间号空间耗尽，无法分配新房间");
    }

    private RoomSummaryDto ToSummary(Room room) => new(
        room.Code,
        room.Settings.Name,
        room.Host?.Nickname ?? "未知",
        room.SeatedCount,
        room.Settings.MaxPlayers,
        room.Status,
        room.Settings.HasPassword,
        room.CreatedAt);

    private static IReadOnlyList<PersistedMember> ToPersistedMembers(Room room) =>
        [.. room.Members.Values.Select(m => new PersistedMember(m.PlayerId, m.Nickname, m.Seat))];

    private static PersistedRoom ToPersisted(Room room) => new(
        room.Id,
        room.Code,
        room.Settings.Name,
        room.Settings.IsPublic,
        room.Settings.MaxPlayers,
        room.Settings.MaxRounds,
        room.Settings.TurnSeconds,
        room.Settings.PasswordHash,
        room.HostPlayerId,
        room.Status,
        room.CreatedAt,
        ToPersistedMembers(room),
        room.GameId is { } gameId && room.Game is { } game
            ? new PersistedGame(gameId, game.Seed, game.PlayerCount, game.TotalRounds, [], [])
            : null);
}
