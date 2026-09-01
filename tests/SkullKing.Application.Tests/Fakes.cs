using SkullKing.Application.Abstractions;
using SkullKing.Contracts;

namespace SkullKing.Application.Tests;

/// <summary>内存版归档。测试恢复流程时可以把它整个交给新的 RoomService，模拟进程重启。</summary>
internal sealed class FakeArchive : IGameArchive
{
    private readonly Dictionary<Guid, PersistedRoom> _rooms = [];
    private readonly Dictionary<Guid, List<PersistedMove>> _moves = [];
    private readonly Dictionary<Guid, (Guid RoomId, ulong Seed, int TotalRounds, List<PersistedMember> Seats)> _games = [];
    private readonly HashSet<Guid> _endedGames = [];

    private static readonly DateTimeOffset ArchivedAt = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    public List<ChatMessageDto> Chats { get; } = [];

    public Task UpsertRoomAsync(PersistedRoom room, CancellationToken ct = default)
    {
        _rooms[room.Id] = room;
        return Task.CompletedTask;
    }

    public Task UpdateRoomStatusAsync(Guid roomId, RoomStatus status, CancellationToken ct = default)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            _rooms[roomId] = room with { Status = status };
        }

        return Task.CompletedTask;
    }

    public Task ReplaceMembersAsync(Guid roomId, IReadOnlyList<PersistedMember> members, CancellationToken ct = default)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            _rooms[roomId] = room with { Members = members };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PersistedRoom>> LoadResumableRoomsAsync(CancellationToken ct = default)
    {
        var result = _rooms.Values
            .Where(r => r.Status == RoomStatus.Playing)
            .Select(r =>
            {
                var game = _games
                    .Where(g => g.Value.RoomId == r.Id && !_endedGames.Contains(g.Key))
                    .Select(g => new PersistedGame(
                        g.Key,
                        g.Value.Seed,
                        g.Value.Seats.Count,
                        g.Value.TotalRounds,
                        g.Value.Seats,
                        _moves.GetValueOrDefault(g.Key, []).OrderBy(m => m.Seq).ToList()))
                    .FirstOrDefault();

                return r with { Game = game };
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PersistedRoom>>(result);
    }

    public Task CreateGameAsync(Guid gameId, Guid roomId, ulong seed, int totalRounds, IReadOnlyList<PersistedMember> seats, CancellationToken ct = default)
    {
        _games[gameId] = (roomId, seed, totalRounds, [.. seats]);
        _moves[gameId] = [];

        return Task.CompletedTask;
    }

    public Task AppendMovesAsync(Guid gameId, IReadOnlyList<PersistedMove> moves, CancellationToken ct = default)
    {
        _moves.GetValueOrDefault(gameId, [])?.AddRange(moves);
        return Task.CompletedTask;
    }

    public Task SaveRoundScoresAsync(Guid gameId, int roundNumber, IReadOnlyList<PlayerRoundScoreDto> scores, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task EndGameAsync(Guid gameId, IReadOnlyList<int> finalScores, IReadOnlyList<int> winnerSeats, CancellationToken ct = default)
    {
        _endedGames.Add(gameId);
        return Task.CompletedTask;
    }

    public Task SaveChatAsync(Guid roomId, ChatMessageDto message, CancellationToken ct = default)
    {
        Chats.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GameHistoryEntry>> GetHistoryAsync(string playerId, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GameHistoryEntry>>([]);

    public Task<PersistedGameDetail?> LoadGameAsync(Guid gameId, CancellationToken ct = default)
    {
        if (!_games.TryGetValue(gameId, out var game))
        {
            return Task.FromResult<PersistedGameDetail?>(null);
        }

        var persisted = new PersistedGame(
            gameId,
            game.Seed,
            game.Seats.Count,
            game.TotalRounds,
            game.Seats,
            [.. _moves.GetValueOrDefault(gameId, []).OrderBy(m => m.Seq)]);

        var room = _rooms.GetValueOrDefault(game.RoomId);

        return Task.FromResult<PersistedGameDetail?>(new PersistedGameDetail(
            persisted,
            room?.Code ?? string.Empty,
            room?.Name ?? string.Empty,
            ArchivedAt,
            _endedGames.Contains(gameId) ? ArchivedAt : null));
    }

    public int MoveCount(Guid gameId) => _moves.GetValueOrDefault(gameId, []).Count;
}

/// <summary>记录所有推送，测试据此断言「谁看到了什么」。</summary>
internal sealed class RecordingNotifier : IRoomNotifier
{
    public Dictionary<string, RoomStateDto> LatestState { get; } = [];

    public List<GameEventDto> Events { get; } = [];

    public List<ChatMessageDto> Chats { get; } = [];

    public List<(string PlayerId, string Reason)> Removals { get; } = [];

    public Task SendRoomStateAsync(string playerId, RoomStateDto state, CancellationToken ct = default)
    {
        LatestState[playerId] = state;
        return Task.CompletedTask;
    }

    public Task BroadcastEventAsync(string roomCode, GameEventDto gameEvent, CancellationToken ct = default)
    {
        Events.Add(gameEvent);
        return Task.CompletedTask;
    }

    public Task BroadcastChatAsync(string roomCode, ChatMessageDto message, CancellationToken ct = default)
    {
        Chats.Add(message);
        return Task.CompletedTask;
    }

    public Task SendRemovedAsync(string playerId, string roomCode, string reason, CancellationToken ct = default)
    {
        Removals.Add((playerId, reason));
        return Task.CompletedTask;
    }

    public Task BroadcastLobbyChangedAsync(CancellationToken ct = default) => Task.CompletedTask;

    public IEnumerable<GameEventDto> OfType(string type) => Events.Where(e => e.Type == type);
}
