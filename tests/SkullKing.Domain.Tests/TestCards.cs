using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Tests;

internal static class TestCards
{
    public static Card Green(int rank) => Card.Number(Suit.Parrot, rank);

    public static Card Yellow(int rank) => Card.Number(Suit.TreasureChest, rank);

    public static Card Purple(int rank) => Card.Number(Suit.TreasureMap, rank);

    public static Card Black(int rank) => Card.Number(Suit.JollyRoger, rank);

    public static Card Escape(int n = 1) => Card.Special(CardKind.Escape, $"E{n}");

    public static Card Pirate(int n = 1) => Card.Special(CardKind.Pirate, $"R{n}");

    public static Card Mermaid(int n = 1) => Card.Special(CardKind.Mermaid, $"M{n}");

    public static Card King() => Card.Special(CardKind.SkullKing, "SK");

    public static Card Tigress() => Card.Special(CardKind.Tigress, "TG");

    /// <summary>按出牌顺序构造一墩，座位号即出牌次序。</summary>
    public static List<PlayedCard> Trick(params Card[] cards)
        => [.. cards.Select((c, i) => new PlayedCard(i, c))];

    public static List<PlayedCard> TrickWith(params (Card Card, TigressMode? Mode)[] plays)
        => [.. plays.Select((p, i) => new PlayedCard(i, p.Card, p.Mode))];
}
