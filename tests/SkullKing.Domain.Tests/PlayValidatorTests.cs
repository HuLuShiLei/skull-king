using System.Collections.Immutable;
using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;
using static SkullKing.Domain.Tests.TestCards;

namespace SkullKing.Domain.Tests;

public class PlayValidatorTests
{
    private static ImmutableArray<Card> Hand(params Card[] cards) => [.. cards];

    [Fact]
    public void 首家可以出任何牌()
    {
        var hand = Hand(Green(3), Yellow(9), King());

        var playable = PlayValidator.PlayableCards(hand, []);

        Assert.Equal(3, playable.Length);
    }

    [Fact]
    public void 有跟牌花色时必须跟()
    {
        var hand = Hand(Green(3), Green(11), Yellow(9), Black(2));
        var trick = Trick(Green(7));

        var playable = PlayValidator.PlayableCards(hand, trick);

        Assert.Equal(2, playable.Length);
        Assert.All(playable, c => Assert.Equal(Suit.Parrot, c.Suit));
    }

    [Fact]
    public void 有跟牌花色时特殊牌仍可出()
    {
        var hand = Hand(Green(3), Yellow(9), Escape(), Pirate(), Tigress());
        var trick = Trick(Green(7));

        var playable = PlayValidator.PlayableCards(hand, trick);

        Assert.Equal(4, playable.Length);
        Assert.DoesNotContain(playable, c => c.Suit == Suit.TreasureChest);
    }

    [Fact]
    public void 没有跟牌花色时可以随意出()
    {
        var hand = Hand(Yellow(9), Black(2), Escape());
        var trick = Trick(Green(7));

        var playable = PlayValidator.PlayableCards(hand, trick);

        Assert.Equal(3, playable.Length);
    }

    [Fact]
    public void 首家出海盗后花色未定可随意出()
    {
        var hand = Hand(Green(3), Yellow(9));
        var trick = Trick(Pirate());

        var playable = PlayValidator.PlayableCards(hand, trick);

        Assert.Equal(2, playable.Length);
    }

    [Fact]
    public void 首家逃跑后由第一张数字牌锁定花色()
    {
        var hand = Hand(Green(3), Yellow(9));
        var trick = Trick(Escape(), Yellow(4));

        var playable = PlayValidator.PlayableCards(hand, trick);

        Assert.Single(playable);
        Assert.Equal(Suit.TreasureChest, playable[0].Suit);
    }

    [Fact]
    public void 王牌也要遵守跟牌规则()
    {
        var hand = Hand(Green(3), Black(14));
        var trick = Trick(Green(7));

        Assert.False(PlayValidator.CanPlay(hand, trick, "J14"));
        Assert.True(PlayValidator.CanPlay(hand, trick, "P03"));
    }
}
