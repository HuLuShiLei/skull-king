using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Projection;
using SkullKing.Contracts;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Rooms;

public sealed partial class RoomService
{
    /// <summary>掉线的人不必等满整个回合限时，但也要留够刷新页面重连的时间。</summary>
    private static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(20);

    /// <summary>
    /// 掉线多久之后才真的把人请出房间。给得比较宽松是因为摸鱼场景下
    /// 切走十几分钟很正常，而没在打牌的房间留着几乎不占资源。
    /// </summary>
    private static readonly TimeSpan AbandonGrace = TimeSpan.FromMinutes(10);

    public async Task<RoomActionResult> StartGameAsync(string code, string playerId, CancellationToken ct = default)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (room.HostPlayerId != playerId)
            {
                return RoomActionResult.Fail("只有房主能开始");
            }

            if (room.Status == RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局已经在进行");
            }

            var seated = room.SeatedMembers.ToList();

            if (seated.Count < GameSettings.MinPlayers)
            {
                return RoomActionResult.Fail($"至少需要 {GameSettings.MinPlayers} 人入座");
            }

            var unready = seated.Where(m => m.PlayerId != room.HostPlayerId && !m.IsReady).ToList();

            if (unready.Count > 0)
            {
                return RoomActionResult.Fail($"还有 {unready.Count} 人没准备");
            }

            room.CompactSeats();

            var seed = (ulong)RandomNumberGenerator.GetInt32(int.MaxValue) << 32
                       | (uint)RandomNumberGenerator.GetInt32(int.MaxValue);

            var step = GameEngine.Start(seated.Count, seed, room.Settings.ToGameSettings());

            room.Game = step.State;
            room.GameId = Guid.NewGuid();
            room.MoveSeq = 0;
            room.Status = RoomStatus.Playing;
            room.ResetReadyFlags();

            var seats = seated.Select(m => new PersistedMember(m.PlayerId, m.Nickname, m.Seat)).ToArray();

            await archive.CreateGameAsync(room.GameId.Value, room.Id, seed, step.State.TotalRounds, seats, ct);
            await archive.UpdateRoomStatusAsync(room.Id, RoomStatus.Playing, ct);
            await archive.ReplaceMembersAsync(room.Id, ToPersistedMembersOf(room), ct);

            await PublishAsync(room, step.Events, ct);

            logger.LogInformation("房间 {Code} 开局，{Count} 人，共 {Rounds} 轮", room.Code, seated.Count, step.State.TotalRounds);

            return RoomActionResult.Success;
        }
        finally
        {
            room.Gate.Release();
        }
    }

    public Task<RoomActionResult> PlaceBidAsync(string code, string playerId, int bid, CancellationToken ct = default) =>
        SubmitAsync(code, playerId, seat =>
        (
            new PlaceBidCommand(seat, bid),
            new PersistedMove(0, MoveKinds.Bid, seat, bid, null, null)
        ), ct);

    public Task<RoomActionResult> PlayCardAsync(
        string code,
        string playerId,
        string cardId,
        string? tigressMode,
        CancellationToken ct = default) =>
        SubmitAsync(code, playerId, seat =>
        {
            var mode = ParseTigressMode(tigressMode);

            return (
                new PlayCardCommand(seat, cardId, mode),
                new PersistedMove(0, MoveKinds.Play, seat, null, cardId, mode?.ToString())
            );
        }, ct);

    private async Task<RoomActionResult> SubmitAsync(
        string code,
        string playerId,
        Func<int, (GameCommand Command, PersistedMove Move)> build,
        CancellationToken ct)
    {
        var room = Find(code);

        if (room is null)
        {
            return RoomActionResult.Fail("房间不存在");
        }

        await room.Gate.WaitAsync(ct);

        try
        {
            if (room.Game is null || room.Status != RoomStatus.Playing)
            {
                return RoomActionResult.Fail("对局还没开始");
            }

            if (!room.Members.TryGetValue(playerId, out var member) || member.IsSpectator)
            {
                return RoomActionResult.Fail("旁观者不能操作");
            }

            var (command, move) = build(member.Seat);

            return await ApplyAsync(room, command, move, ct);
        }
        finally
        {
            room.Gate.Release();
        }
    }

    /// <summary>调用方必须已持有房间锁。</summary>
    private async Task<RoomActionResult> ApplyAsync(Room room, GameCommand command, PersistedMove move, CancellationToken ct)
    {
        GameStepResult step;

        try
        {
            step = GameEngine.Apply(room.Game!, command);
        }
        catch (GameRuleException ex)
        {
            return RoomActionResult.Fail(ex.Message);
        }

        room.Game = step.State;

        await archive.AppendMovesAsync(room.GameId!.Value, [move with { Seq = ++room.MoveSeq }], ct);
        await PublishAsync(room, step.Events, ct);

        return RoomActionResult.Success;
    }

    /// <summary>
    /// 把一次命令产生的事件按序广播出去，顺带处理落库和收尾。
    /// 客户端拿到的是「事件流 + 一次全量快照」，事件负责动画，快照负责纠偏。
    /// </summary>
    private async Task PublishAsync(Room room, IReadOnlyList<GameEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            switch (domainEvent)
            {
                case RoundScoredEvent scored:
                    await archive.SaveRoundScoresAsync(
                        room.GameId!.Value,
                        scored.RoundNumber,
                        [.. scored.Scores.Select(GameProjector.ToDto)],
                        ct);
                    break;

                case GameEndedEvent ended:
                    room.Status = RoomStatus.Finished;
                    room.TurnDeadline = null;

                    await archive.EndGameAsync(room.GameId!.Value, [.. ended.FinalScores], [.. ended.WinnerSeats], ct);
                    await archive.UpdateRoomStatusAsync(room.Id, RoomStatus.Finished, ct);
                    break;
            }

            var dto = GameProjector.ToBroadcastDto(domainEvent, ++room.EventSeq);

            if (dto is not null)
            {
                await notifier.BroadcastEventAsync(room.Code, dto, ct);
            }
        }

        RefreshTurnDeadline(room);
        await PushStateAsync(room, ct);
        await notifier.BroadcastLobbyChangedAsync(ct);
    }

    private void RefreshTurnDeadline(Room room)
    {
        room.TurnDeadline = room.Settings.TurnSeconds > 0 && room.Game is { Phase: not GamePhase.Finished }
            ? Now.AddSeconds(room.Settings.TurnSeconds)
            : null;
    }

    /// <summary>
    /// 由后台服务定期调用。超时或掉线过久的玩家交给系统代打，
    /// 目的是不让一个人挂机拖死整桌，而不是替他打好。
    /// </summary>
    public async Task TickAsync(CancellationToken ct = default)
    {
        foreach (var room in _rooms.Values.ToList())
        {
            if (!await room.Gate.WaitAsync(0, ct))
            {
                continue;
            }

            try
            {
                if (room.Status == RoomStatus.Playing && room.Game is not null)
                {
                    await AutoAdvanceAsync(room, ct);
                }
                else
                {
                    await SweepAbandonedAsync(room, ct);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "房间 {Code} 巡检失败", room.Code);
            }
            finally
            {
                room.Gate.Release();
            }
        }
    }

    /// <summary>
    /// 把掉线超过宽限期的人清出房间，房间空了就回收。
    /// 只在不打牌的时候做——对局进行中座位不能塌陷，那种情况交给托管。
    /// </summary>
    private async Task SweepAbandonedAsync(Room room, CancellationToken ct)
    {
        var now = Now;

        var gone = room.Members.Values
            .Where(m => m is { IsConnected: false, DisconnectedAt: { } since } && now - since >= AbandonGrace)
            .ToList();

        if (gone.Count == 0)
        {
            return;
        }

        foreach (var member in gone)
        {
            room.Members.Remove(member.PlayerId);
            await BroadcastNoticeAsync(room, $"{member.Nickname} 长时间没回来，已退出群聊", ct);
            await HandleHostDepartureAsync(room, member.PlayerId, ct);
        }

        logger.LogInformation("房间 {Code} 清理了 {Count} 个久未重连的成员", room.Code, gone.Count);

        await FinalizeMembershipChangeAsync(room, ct);
    }

    private async Task AutoAdvanceAsync(Room room, CancellationToken ct)
    {
        var game = room.Game;

        if (game is null || game.Phase == GamePhase.Finished)
        {
            return;
        }

        if (room.AutoPlaySuppressedUntil is { } until && Now < until)
        {
            return;
        }

        foreach (var seat in SeatsNeedingAutoPlay(room, game))
        {
            var member = room.MemberAtSeat(seat);
            var command = AutoPlayAdvisor.Suggest(room.Game!, seat);

            var move = command switch
            {
                PlaceBidCommand bid => new PersistedMove(0, MoveKinds.Bid, seat, bid.Bid, null, null),
                PlayCardCommand play => new PersistedMove(0, MoveKinds.Play, seat, null, play.CardId, play.TigressMode?.ToString()),
                _ => throw new InvalidOperationException("托管产生了未知命令")
            };

            await BroadcastNoticeAsync(room, $"{member?.Nickname ?? $"{seat} 号位"} 超时，系统代为处理", ct);

            var result = await ApplyAsync(room, command, move, ct);

            if (!result.Ok)
            {
                logger.LogWarning("房间 {Code} 座位 {Seat} 托管失败：{Error}", room.Code, seat, result.Error);
                return;
            }
        }
    }

    private List<int> SeatsNeedingAutoPlay(Room room, GameState game)
    {
        var now = Now;
        var timedOut = room.TurnDeadline is { } deadline && now >= deadline;

        bool ShouldTakeOver(int seat)
        {
            if (timedOut)
            {
                return true;
            }

            var member = room.MemberAtSeat(seat);

            return member is { IsConnected: false, DisconnectedAt: { } since } && now - since >= DisconnectGrace;
        }

        if (game.Phase == GamePhase.Bidding)
        {
            return
            [
                .. Enumerable.Range(0, game.PlayerCount)
                    .Where(seat => !game.Bids[seat].HasValue && ShouldTakeOver(seat))
            ];
        }

        return ShouldTakeOver(game.CurrentSeat) ? [game.CurrentSeat] : [];
    }

    private static TigressMode? ParseTigressMode(string? value) => value switch
    {
        null or "" => null,
        _ when string.Equals(value, nameof(TigressMode.AsEscape), StringComparison.OrdinalIgnoreCase) => TigressMode.AsEscape,
        _ => TigressMode.AsPirate
    };

    private static IReadOnlyList<PersistedMember> ToPersistedMembersOf(Room room) =>
        [.. room.Members.Values.Select(m => new PersistedMember(m.PlayerId, m.Nickname, m.Seat))];
}
