namespace SkullKing.Domain.Cards;

/// <summary>
/// 一张牌。<see cref="Id"/> 在整副牌里唯一且稳定，客户端出牌时只回传 Id。
/// </summary>
public sealed record Card
{
    public required string Id { get; init; }

    public required CardKind Kind { get; init; }

    /// <summary>仅 <see cref="CardKind.Number"/> 有值。</summary>
    public Suit? Suit { get; init; }

    /// <summary>仅 <see cref="CardKind.Number"/> 有值，取值 1-14。</summary>
    public int? Rank { get; init; }

    public bool IsNumber => Kind == CardKind.Number;

    public bool IsTrump => Suit == Cards.Suit.JollyRoger;

    /// <summary>是否为可触发奖励分的最大点数牌。</summary>
    public bool IsTopRank => Rank == DeckFactory.TopRank;

    public static Card Number(Suit suit, int rank) => new()
    {
        Id = $"{SuitCode(suit)}{rank:D2}",
        Kind = CardKind.Number,
        Suit = suit,
        Rank = rank
    };

    public static Card Special(CardKind kind, string id) => new() { Id = id, Kind = kind };

    private static char SuitCode(Suit suit) => suit switch
    {
        Cards.Suit.Parrot => 'P',
        Cards.Suit.TreasureChest => 'C',
        Cards.Suit.TreasureMap => 'M',
        Cards.Suit.JollyRoger => 'J',
        _ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "未知花色")
    };
}
