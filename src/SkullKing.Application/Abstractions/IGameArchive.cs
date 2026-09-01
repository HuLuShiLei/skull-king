using SkullKing.Contracts;

namespace SkullKing.Application.Abstractions;

public static class MoveKinds
{
    public const string Bid = "bid";
    public const string Play = "play";
}

/// <summary>
/// 一步操作。存命令而不是存状态快照，是因为规则引擎是确定性的：
/// 同一个种子加同一串命令必然重现同一局，恢复时重放即可。
/// </summary>
public sealed record PersistedMove(long Seq, string Kind, int Seat, int? Bid, string? CardId, string? TigressMode);

public sealed record PersistedMember(string PlayerId, string Nickname, int Seat);

public sealed record PersistedGame(
    Guid Id,
    ulong Seed,
    int PlayerCount,
    int TotalRounds,
    IReadOnlyList<PersistedMember> Seats,
    IReadOnlyList<PersistedMove> Moves);

/// <summary>回放用：命令日志之外还要带上房间信息和起止时间。</summary>
public sealed record PersistedGameDetail(
    PersistedGame Game,
    string RoomCode,
    string RoomName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);

public sealed record PersistedRoom(
    Guid Id,
    string Code,
    string Name,
    bool IsPublic,
    int MaxPlayers,
    int MaxRounds,
    int TurnSeconds,
    string? PasswordHash,
    string HostPlayerId,
    RoomStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PersistedMember> Members,
    PersistedGame? Game);

public sealed record GameHistoryEntry(
    Guid GameId,
    string RoomCode,
    string RoomName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int YourSeat,
    int YourScore,
    bool YouWon,
    IReadOnlyList<string> Nicknames,
    IReadOnlyList<int> FinalScores);

public interface IGameArchive
{
    Task UpsertRoomAsync(PersistedRoom room, CancellationToken ct = default);

    Task UpdateRoomStatusAsync(Guid roomId, RoomStatus status, CancellationToken ct = default);

    Task ReplaceMembersAsync(Guid roomId, IReadOnlyList<PersistedMember> members, CancellationToken ct = default);

    /// <summary>进程启动时捞回未结束的房间，用于重建内存状态。</summary>
    Task<IReadOnlyList<PersistedRoom>> LoadResumableRoomsAsync(CancellationToken ct = default);

    /// <summary>座位随对局一起快照下来，房间成员后续怎么变都不影响战绩查询。</summary>
    Task CreateGameAsync(
        Guid gameId,
        Guid roomId,
        ulong seed,
        int totalRounds,
        IReadOnlyList<PersistedMember> seats,
        CancellationToken ct = default);

    Task AppendMovesAsync(Guid gameId, IReadOnlyList<PersistedMove> moves, CancellationToken ct = default);

    Task SaveRoundScoresAsync(Guid gameId, int roundNumber, IReadOnlyList<PlayerRoundScoreDto> scores, CancellationToken ct = default);

    Task EndGameAsync(Guid gameId, IReadOnlyList<int> finalScores, IReadOnlyList<int> winnerSeats, CancellationToken ct = default);

    Task SaveChatAsync(Guid roomId, ChatMessageDto message, CancellationToken ct = default);

    Task<IReadOnlyList<GameHistoryEntry>> GetHistoryAsync(string playerId, int limit, CancellationToken ct = default);

    Task<PersistedGameDetail?> LoadGameAsync(Guid gameId, CancellationToken ct = default);
}
