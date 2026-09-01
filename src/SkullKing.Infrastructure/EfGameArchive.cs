using Microsoft.EntityFrameworkCore;
using SkullKing.Application.Abstractions;
using SkullKing.Contracts;

namespace SkullKing.Infrastructure;

public sealed class EfGameArchive(IDbContextFactory<SkullKingDbContext> factory) : IGameArchive
{
    public async Task UpsertRoomAsync(PersistedRoom room, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = await db.Rooms.FirstOrDefaultAsync(r => r.Id == room.Id, ct);

        if (row is null)
        {
            row = new RoomRow { Id = room.Id, CreatedAt = room.CreatedAt };
            db.Rooms.Add(row);
        }

        row.Code = room.Code;
        row.Name = room.Name;
        row.IsPublic = room.IsPublic;
        row.MaxPlayers = room.MaxPlayers;
        row.MaxRounds = room.MaxRounds;
        row.TurnSeconds = room.TurnSeconds;
        row.PasswordHash = room.PasswordHash;
        row.Password = room.Password;
        row.HostPlayerId = room.HostPlayerId;
        row.Status = room.Status;

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateRoomStatusAsync(Guid roomId, RoomStatus status, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, status), ct);
    }

    public async Task ReplaceMembersAsync(Guid roomId, IReadOnlyList<PersistedMember> members, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // 房间成员是小集合，整体替换比逐条 diff 更简单也更不容易出错。
        await db.RoomMembers.Where(m => m.RoomId == roomId).ExecuteDeleteAsync(ct);

        db.RoomMembers.AddRange(members.Select(m => new RoomMemberRow
        {
            RoomId = roomId,
            PlayerId = m.PlayerId,
            Nickname = m.Nickname,
            Seat = m.Seat
        }));

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PersistedRoom>> LoadResumableRoomsAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // 连还在等人的房间一起捞回来。只恢复对局中的话，每次发版所有刚开好、
        // 正在等人的房间都会凭空消失，邀请链接也跟着失效。恢复出来的成员都是
        // 掉线状态，真没人回来的话十分钟后会被巡检清空、房间自动回收。
        var rooms = await db.Rooms
            .AsNoTracking()
            .Where(r => r.Status != RoomStatus.Finished)
            .Include(r => r.Members)
            .Include(r => r.Games.Where(g => g.EndedAt == null))
                .ThenInclude(g => g.Moves)
            .Include(r => r.Games.Where(g => g.EndedAt == null))
                .ThenInclude(g => g.Seats)
            // 一次拉多个集合，单条 SQL 会笛卡尔积膨胀，拆成多条更快。
            .AsSplitQuery()
            .ToListAsync(ct);

        return
        [
            .. rooms.Select(r =>
            {
                var game = r.Games.OrderByDescending(g => g.StartedAt).FirstOrDefault();

                return new PersistedRoom(
                    r.Id,
                    r.Code,
                    r.Name,
                    r.IsPublic,
                    r.MaxPlayers,
                    r.MaxRounds,
                    r.TurnSeconds,
                    r.PasswordHash,
                    r.Password,
                    r.HostPlayerId,
                    r.Status,
                    r.CreatedAt,
                    [.. r.Members.Select(m => new PersistedMember(m.PlayerId, m.Nickname, m.Seat))],
                    game is null
                        ? null
                        : new PersistedGame(
                            game.Id,
                            unchecked((ulong)game.Seed),
                            game.PlayerCount,
                            game.TotalRounds,
                            [.. game.Seats.OrderBy(s => s.Seat).Select(s => new PersistedMember(s.PlayerId, s.Nickname, s.Seat))],
                            [.. game.Moves.OrderBy(m => m.Seq).Select(m => new PersistedMove(m.Seq, m.Kind, m.Seat, m.Bid, m.CardId, m.TigressMode))]));
            })
        ];
    }

    public async Task CreateGameAsync(
        Guid gameId,
        Guid roomId,
        ulong seed,
        int totalRounds,
        IReadOnlyList<PersistedMember> seats,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        db.Games.Add(new GameRow
        {
            Id = gameId,
            RoomId = roomId,
            Seed = unchecked((long)seed),
            PlayerCount = seats.Count,
            TotalRounds = totalRounds,
            StartedAt = DateTimeOffset.UtcNow,
            Seats =
            [
                .. seats.Select(s => new GameSeatRow
                {
                    GameId = gameId,
                    Seat = s.Seat,
                    PlayerId = s.PlayerId,
                    Nickname = s.Nickname
                })
            ]
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task AppendMovesAsync(Guid gameId, IReadOnlyList<PersistedMove> moves, CancellationToken ct = default)
    {
        if (moves.Count == 0)
        {
            return;
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        db.GameMoves.AddRange(moves.Select(m => new GameMoveRow
        {
            GameId = gameId,
            Seq = m.Seq,
            Kind = m.Kind,
            Seat = m.Seat,
            Bid = m.Bid,
            CardId = m.CardId,
            TigressMode = m.TigressMode,
            CreatedAt = DateTimeOffset.UtcNow
        }));

        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRoundScoresAsync(
        Guid gameId,
        int roundNumber,
        IReadOnlyList<PlayerRoundScoreDto> scores,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        db.RoundScores.AddRange(scores.Select(s => new RoundScoreRow
        {
            GameId = gameId,
            RoundNumber = roundNumber,
            Seat = s.Seat,
            Bid = s.Bid,
            TricksWon = s.TricksWon,
            BaseScore = s.BaseScore,
            Bonus = s.Bonus,
            Total = s.Total
        }));

        await db.SaveChangesAsync(ct);
    }

    public async Task EndGameAsync(
        Guid gameId,
        IReadOnlyList<int> finalScores,
        IReadOnlyList<int> winnerSeats,
        CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Games
            .Where(g => g.Id == gameId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.EndedAt, DateTimeOffset.UtcNow)
                .SetProperty(g => g.FinalScores, string.Join(',', finalScores))
                .SetProperty(g => g.WinnerSeats, string.Join(',', winnerSeats)), ct);
    }

    public async Task SaveChatAsync(Guid roomId, ChatMessageDto message, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        db.ChatMessages.Add(new ChatMessageRow
        {
            Id = message.Id,
            RoomId = roomId,
            PlayerId = message.PlayerId,
            Nickname = message.Nickname,
            Seat = message.Seat,
            Text = message.Text,
            SentAt = message.SentAt
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GameHistoryEntry>> GetHistoryAsync(string playerId, int limit, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // 从 Games 出发而不是从 GameSeats 投影过来：EF 不允许在 Select 之后再 Include。
        var games = await db.Games
            .AsNoTracking()
            .Where(g => g.EndedAt != null && g.Seats.Any(s => s.PlayerId == playerId))
            .OrderByDescending(g => g.EndedAt)
            .Take(limit)
            .Include(g => g.Seats)
            .Include(g => g.Room)
            .ToListAsync(ct);

        return [.. games.Select(g => ToHistoryEntry(g, playerId))];
    }

    public async Task<PersistedGameDetail?> LoadGameAsync(Guid gameId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var game = await db.Games
            .AsNoTracking()
            .Include(g => g.Moves)
            .Include(g => g.Seats)
            .Include(g => g.Room)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == gameId, ct);

        if (game is null)
        {
            return null;
        }

        var persisted = new PersistedGame(
            game.Id,
            unchecked((ulong)game.Seed),
            game.PlayerCount,
            game.TotalRounds,
            [.. game.Seats.OrderBy(s => s.Seat).Select(s => new PersistedMember(s.PlayerId, s.Nickname, s.Seat))],
            [.. game.Moves.OrderBy(m => m.Seq).Select(m => new PersistedMove(m.Seq, m.Kind, m.Seat, m.Bid, m.CardId, m.TigressMode))]);

        return new PersistedGameDetail(
            persisted,
            game.Room?.Code ?? string.Empty,
            game.Room?.Name ?? string.Empty,
            game.StartedAt,
            game.EndedAt);
    }

    private static GameHistoryEntry ToHistoryEntry(GameRow game, string playerId)
    {
        var seats = game.Seats.OrderBy(s => s.Seat).ToList();
        var scores = ParseInts(game.FinalScores);
        var winners = ParseInts(game.WinnerSeats);
        var mySeat = seats.FirstOrDefault(s => s.PlayerId == playerId)?.Seat ?? -1;

        return new GameHistoryEntry(
            game.Id,
            game.Room?.Code ?? string.Empty,
            game.Room?.Name ?? string.Empty,
            game.StartedAt,
            game.EndedAt,
            mySeat,
            mySeat >= 0 && mySeat < scores.Count ? scores[mySeat] : 0,
            winners.Contains(mySeat),
            [.. seats.Select(s => s.Nickname)],
            scores);
    }

    private static List<int> ParseInts(string? csv) =>
        string.IsNullOrEmpty(csv)
            ? []
            : [.. csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)];
}
