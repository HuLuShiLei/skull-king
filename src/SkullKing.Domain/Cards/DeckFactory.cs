using System.Collections.Immutable;

namespace SkullKing.Domain.Cards;

/// <summary>
/// 牌组构建与发牌。核心牌组 70 张：4 花色各 1-14（56）+ 5 逃跑 + 5 海盗 + 1 Tigress + 2 美人鱼 + 1 骷髅王。
/// </summary>
public static class DeckFactory
{
    public const int TopRank = 14;
    public const int CoreDeckSize = 70;

    public static ImmutableArray<Card> BuildCoreDeck()
    {
        var builder = ImmutableArray.CreateBuilder<Card>(CoreDeckSize);

        foreach (var suit in new[] { Suit.Parrot, Suit.TreasureChest, Suit.TreasureMap, Suit.JollyRoger })
        {
            for (var rank = 1; rank <= TopRank; rank++)
            {
                builder.Add(Card.Number(suit, rank));
            }
        }

        for (var i = 1; i <= 5; i++)
        {
            builder.Add(Card.Special(CardKind.Escape, $"E{i}"));
        }

        for (var i = 1; i <= 5; i++)
        {
            builder.Add(Card.Special(CardKind.Pirate, $"R{i}"));
        }

        builder.Add(Card.Special(CardKind.Tigress, "TG"));
        builder.Add(Card.Special(CardKind.Mermaid, "M1"));
        builder.Add(Card.Special(CardKind.Mermaid, "M2"));
        builder.Add(Card.Special(CardKind.SkullKing, "SK"));

        return builder.MoveToImmutable();
    }

    /// <summary>
    /// 牌数决定了人数上限下能打几轮：第 N 轮每人发 N 张。
    /// </summary>
    public static int MaxRoundsFor(int playerCount, int deckSize = CoreDeckSize)
    {
        if (playerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        return Math.Max(1, Math.Min(10, deckSize / playerCount));
    }

    /// <summary>Fisher-Yates 洗牌，使用可复现的 PRNG，便于事件回放。</summary>
    public static ImmutableArray<Card> Shuffle(ImmutableArray<Card> deck, ulong seed)
    {
        var rng = new DeterministicRandom(seed);
        var array = deck.ToArray();

        for (var i = array.Length - 1; i > 0; i--)
        {
            var j = (int)rng.NextBelow((uint)(i + 1));
            (array[i], array[j]) = (array[j], array[i]);
        }

        return [.. array];
    }

    /// <summary>按座位顺序发牌，每人 <paramref name="cardsPerPlayer"/> 张。</summary>
    public static ImmutableArray<ImmutableArray<Card>> Deal(
        ImmutableArray<Card> shuffledDeck,
        int playerCount,
        int cardsPerPlayer)
    {
        if (playerCount * cardsPerPlayer > shuffledDeck.Length)
        {
            throw new InvalidOperationException(
                $"牌不够发：{playerCount} 人 × {cardsPerPlayer} 张 > {shuffledDeck.Length} 张");
        }

        var hands = ImmutableArray.CreateBuilder<ImmutableArray<Card>>(playerCount);

        for (var seat = 0; seat < playerCount; seat++)
        {
            hands.Add([.. shuffledDeck.Skip(seat * cardsPerPlayer).Take(cardsPerPlayer)]);
        }

        return hands.MoveToImmutable();
    }
}
