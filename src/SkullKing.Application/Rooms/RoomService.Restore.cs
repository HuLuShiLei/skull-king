using Microsoft.Extensions.Logging;
using SkullKing.Application.Abstractions;
using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Rooms;

public sealed partial class RoomService
{
    /// <summary>服务重启后给玩家留出的重连窗口，期间不启动托管。</summary>
    private static readonly TimeSpan ResumeGrace = TimeSpan.FromSeconds(90);

    /// <summary>
    /// 进程启动时重建内存状态。对局靠重放命令日志恢复，
    /// 因为规则引擎是确定性的，同一个种子加同一串命令必然回到同一个状态。
    /// </summary>
    public async Task RestoreAsync(CancellationToken ct = default)
    {
        var persisted = await archive.LoadResumableRoomsAsync(ct);
        var restored = 0;

        foreach (var snapshot in persisted)
        {
            try
            {
                var room = Rebuild(snapshot);
                _rooms[room.Code] = room;
                restored++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "房间 {Code} 恢复失败，已跳过", snapshot.Code);
                await archive.UpdateRoomStatusAsync(snapshot.Id, RoomStatus.Finished, ct);
            }
        }

        if (restored > 0)
        {
            logger.LogInformation("已恢复 {Count} 个房间", restored);
        }
    }

    private Room Rebuild(PersistedRoom snapshot)
    {
        var room = new Room
        {
            Id = snapshot.Id,
            Code = snapshot.Code,
            HostPlayerId = snapshot.HostPlayerId,
            CreatedAt = snapshot.CreatedAt,
            Status = snapshot.Status,
            Settings = new RoomSettings
            {
                Name = snapshot.Name,
                IsPublic = snapshot.IsPublic,
                MaxPlayers = snapshot.MaxPlayers,
                MaxRounds = snapshot.MaxRounds,
                TurnSeconds = snapshot.TurnSeconds,
                PasswordHash = snapshot.PasswordHash
            }
        };

        foreach (var member in snapshot.Members)
        {
            room.Members[member.PlayerId] = new RoomMember(member.PlayerId, member.Nickname)
            {
                Seat = member.Seat,
                DisconnectedAt = Now
            };
        }

        if (snapshot.Game is { } game && snapshot.Status == RoomStatus.Playing)
        {
            // 座位以开局快照为准，房间成员表可能已经被后来的进出改动过。
            foreach (var seat in game.Seats)
            {
                room.Members[seat.PlayerId] = new RoomMember(seat.PlayerId, seat.Nickname)
                {
                    Seat = seat.Seat,
                    DisconnectedAt = Now
                };
            }

            room.Game = Replay(game, room.Settings.ToGameSettings());
            room.GameId = game.Id;
            room.MoveSeq = game.Moves.Count == 0 ? 0 : game.Moves.Max(m => m.Seq);
            room.AutoPlaySuppressedUntil = Now + ResumeGrace;

            if (room.Game.Phase == GamePhase.Finished)
            {
                room.Status = RoomStatus.Finished;
            }
        }

        return room;
    }

    private static GameState Replay(PersistedGame game, GameSettings settings)
    {
        var state = GameEngine.Start(game.PlayerCount, game.Seed, settings).State;

        foreach (var move in game.Moves.OrderBy(m => m.Seq))
        {
            state = GameEngine.Apply(state, ToCommand(move)).State;
        }

        return state;
    }

    private static GameCommand ToCommand(PersistedMove move) => move.Kind switch
    {
        MoveKinds.Bid => new PlaceBidCommand(move.Seat, move.Bid ?? 0),
        MoveKinds.Play => new PlayCardCommand(move.Seat, move.CardId!, ParseTigressMode(move.TigressMode)),
        _ => throw new InvalidOperationException($"未知的命令类型 {move.Kind}")
    };
}
