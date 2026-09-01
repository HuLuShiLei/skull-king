using SkullKing.Domain.Game;

namespace SkullKing.Application.Rooms;

public sealed record RoomSettings
{
    public string Name { get; init; } = "项目协作组";

    public bool IsPublic { get; init; } = true;

    public int MaxPlayers { get; init; } = 6;

    public int MaxRounds { get; init; } = 10;

    /// <summary>单步限时秒数，0 表示不限时。</summary>
    public int TurnSeconds { get; init; } = 60;

    public string? PasswordHash { get; init; }

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public GameSettings ToGameSettings() => new() { MaxRounds = MaxRounds };

    public RoomSettings Sanitized() => this with
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "项目协作组" : Name.Trim()[..Math.Min(Name.Trim().Length, 40)],
        MaxPlayers = Math.Clamp(MaxPlayers, GameSettings.MinPlayers, GameSettings.MaxPlayers),
        MaxRounds = Math.Clamp(MaxRounds, 1, 10),
        TurnSeconds = TurnSeconds <= 0 ? 0 : Math.Clamp(TurnSeconds, 15, 600)
    };
}
