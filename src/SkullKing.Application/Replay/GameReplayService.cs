using Microsoft.Extensions.Logging;
using SkullKing.Application.Abstractions;
using SkullKing.Application.Projection;
using SkullKing.Contracts;
using SkullKing.Domain.Game;

namespace SkullKing.Application.Replay;

/// <summary>
/// 回放不存事件，而是拿种子和命令日志重跑一遍规则引擎。
/// 引擎是确定性的，所以重跑出来的事件序列和当时广播的完全一致，
/// 前端可以直接复用房间里那套渲染。
/// </summary>
public sealed class GameReplayService(IGameArchive archive, ILogger<GameReplayService> logger)
{
    public async Task<GameReplayDto?> BuildAsync(Guid gameId, CancellationToken ct = default)
    {
        var detail = await archive.LoadGameAsync(gameId, ct);

        if (detail is null)
        {
            return null;
        }

        // 进行中的对局不给回放，否则等于把别人的手牌提前摊开。
        if (detail.EndedAt is null)
        {
            return null;
        }

        var game = detail.Game;

        try
        {
            var events = Rerun(game);

            return new GameReplayDto
            {
                GameId = game.Id,
                RoomCode = detail.RoomCode,
                RoomName = detail.RoomName,
                PlayerCount = game.PlayerCount,
                TotalRounds = game.TotalRounds,
                StartedAt = detail.StartedAt,
                EndedAt = detail.EndedAt,
                Seats = [.. game.Seats.OrderBy(s => s.Seat).Select(s => new ReplaySeatDto(s.Seat, s.Nickname))],
                Events = events
            };
        }
        catch (Exception ex)
        {
            // 命令日志和当前规则不兼容（例如改过规则）时，宁可不给回放也不要给错的。
            logger.LogWarning(ex, "对局 {GameId} 回放失败", gameId);

            return null;
        }
    }

    private static List<GameEventDto> Rerun(PersistedGame game)
    {
        var settings = new GameSettings { MaxRounds = game.TotalRounds };
        var step = GameEngine.Start(game.PlayerCount, game.Seed, settings);

        var events = new List<GameEventDto>();
        var seq = 0L;

        void Collect(IEnumerable<GameEvent> produced)
        {
            foreach (var domainEvent in produced)
            {
                if (GameProjector.ToBroadcastDto(domainEvent, ++seq) is { } dto)
                {
                    events.Add(dto);
                }
            }
        }

        Collect(step.Events);

        var state = step.State;

        foreach (var move in game.Moves.OrderBy(m => m.Seq))
        {
            var applied = GameEngine.Apply(state, ToCommand(move));

            state = applied.State;
            Collect(applied.Events);
        }

        return events;
    }

    private static GameCommand ToCommand(PersistedMove move) => move.Kind switch
    {
        MoveKinds.Bid => new PlaceBidCommand(move.Seat, move.Bid ?? 0),
        MoveKinds.Play => new PlayCardCommand(move.Seat, move.CardId!, ParseTigressMode(move.TigressMode)),
        _ => throw new InvalidOperationException($"未知的命令类型 {move.Kind}")
    };

    private static Domain.Cards.TigressMode? ParseTigressMode(string? raw) =>
        Enum.TryParse<Domain.Cards.TigressMode>(raw, true, out var mode) ? mode : null;
}
