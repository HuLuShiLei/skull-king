namespace SkullKing.Domain.Cards;

public enum CardKind
{
    Number = 0,
    Escape = 1,
    Pirate = 2,

    /// <summary>出牌时由玩家指定当作海盗还是逃跑。</summary>
    Tigress = 3,
    Mermaid = 4,
    SkullKing = 5,

    // 以下为扩展牌，一期不发到牌组里，仅保留定义。
    Loot = 6,
    Kraken = 7,
    WhiteWhale = 8
}

/// <summary>Tigress 出牌时选择的形态。</summary>
public enum TigressMode
{
    AsPirate = 0,
    AsEscape = 1
}
