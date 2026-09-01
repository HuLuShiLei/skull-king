namespace SkullKing.Contracts;

public enum RoomStatus
{
    Waiting = 0,
    Playing = 1,
    Finished = 2
}

public sealed record RoomSettingsDto
{
    public string Name { get; init; } = "项目协作组";

    public bool IsPublic { get; init; } = true;

    public int MaxPlayers { get; init; } = 6;

    public int MaxRounds { get; init; } = 10;

    /// <summary>单步限时（秒），0 表示不限时。超时后由系统托管代打。</summary>
    public int TurnSeconds { get; init; } = 60;

    public bool HasPassword { get; init; }
}

public sealed record CreateRoomRequest
{
    public string? Name { get; init; }

    public bool IsPublic { get; init; } = true;

    public int MaxPlayers { get; init; } = 6;

    public int MaxRounds { get; init; } = 10;

    public int TurnSeconds { get; init; } = 60;

    public string? Password { get; init; }
}

public sealed record UpdateRoomSettingsRequest
{
    public string? Name { get; init; }

    public bool? IsPublic { get; init; }

    public int? MaxPlayers { get; init; }

    public int? MaxRounds { get; init; }

    public int? TurnSeconds { get; init; }
}

public sealed record RoomSummaryDto(
    string Code,
    string Name,
    string HostNickname,
    int PlayerCount,
    int MaxPlayers,
    RoomStatus Status,
    bool HasPassword,
    DateTimeOffset CreatedAt);

public sealed record RoomMemberDto(
    string PlayerId,
    string Nickname,
    int Seat,
    bool IsHost,
    bool IsReady,
    bool IsConnected,
    bool IsSpectator,
    int TotalScore);

/// <summary>
/// 发给单个玩家的房间快照，<see cref="Game"/> 已按收件人裁剪掉其他人的手牌。
/// </summary>
public sealed record RoomStateDto(
    string Code,
    RoomSettingsDto Settings,
    RoomStatus Status,
    string HostPlayerId,
    string YourPlayerId,
    int YourSeat,
    IReadOnlyList<RoomMemberDto> Members,
    GameViewDto? Game,
    IReadOnlyList<ChatMessageDto> RecentChat);

public sealed record JoinRoomRequest(string Code, string? Password);

public sealed record RoomProbeDto(string Code, string Name, bool Exists, bool HasPassword, bool IsFull, RoomStatus Status);
