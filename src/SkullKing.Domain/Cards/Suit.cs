namespace SkullKing.Domain.Cards;

/// <summary>数字牌花色。JollyRoger（黑）是王牌花色，压过其余三色。</summary>
public enum Suit
{
    /// <summary>绿色鹦鹉</summary>
    Parrot = 1,

    /// <summary>黄色宝箱</summary>
    TreasureChest = 2,

    /// <summary>紫色藏宝图</summary>
    TreasureMap = 3,

    /// <summary>黑色海盗旗，王牌</summary>
    JollyRoger = 4
}
