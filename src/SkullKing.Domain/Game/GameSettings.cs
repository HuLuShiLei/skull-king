namespace SkullKing.Domain.Game;

public sealed record GameSettings
{
    public const int MinPlayers = 2;
    public const int MaxPlayers = 8;

    /// <summary>期望轮数，实际轮数还要受牌量限制。</summary>
    public int MaxRounds { get; init; } = 10;

    // 扩展牌开关，一期只留位，不进牌组。
    public bool EnableLoot { get; init; }

    public bool EnableKraken { get; init; }

    public bool EnableWhiteWhale { get; init; }

    public static GameSettings Default { get; } = new();
}
