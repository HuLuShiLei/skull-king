using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;
using static SkullKing.Domain.Tests.TestCards;

namespace SkullKing.Domain.Tests;

public class TrickResolverTests
{
    [Fact]
    public void 跟牌花色由第一张数字牌决定()
    {
        var trick = Trick(Escape(), Pirate(), Yellow(3), Green(10));

        Assert.Equal(Suit.TreasureChest, TrickResolver.LeadSuit(trick));
    }

    [Fact]
    public void 全是特殊牌时没有跟牌花色()
    {
        var trick = Trick(Escape(1), Escape(2), Pirate());

        Assert.Null(TrickResolver.LeadSuit(trick));
    }

    [Fact]
    public void 跟牌花色最大者收墩()
    {
        var trick = Trick(Green(5), Green(12), Green(9));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.LeadSuit, outcome.Reason);
    }

    [Fact]
    public void 非跟牌花色的普通牌不参与比大小()
    {
        // 黄 14 没跟绿花色也不是王牌，绿 2 照样赢。
        var trick = Trick(Green(2), Yellow(14));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(0, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.LeadSuit, outcome.Reason);
    }

    [Fact]
    public void 黑色王牌压过跟牌花色()
    {
        var trick = Trick(Green(14), Black(1));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.Trump, outcome.Reason);
    }

    [Fact]
    public void 多张黑牌取最大()
    {
        var trick = Trick(Black(4), Green(14), Black(11), Black(7));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(2, outcome.WinnerSeat);
    }

    [Fact]
    public void 全员逃跑时首家收墩()
    {
        var trick = Trick(Escape(1), Escape(2), Escape(3));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(0, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.AllEscaped, outcome.Reason);
    }

    [Fact]
    public void 海盗压过任何数字牌()
    {
        var trick = Trick(Black(14), Pirate());

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.Pirate, outcome.Reason);
    }

    [Fact]
    public void 多个海盗时先出的赢()
    {
        var trick = Trick(Green(3), Pirate(1), Pirate(2), Pirate(3));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
    }

    [Fact]
    public void 骷髅王压海盗()
    {
        var trick = Trick(Pirate(1), King(), Pirate(2));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.SkullKing, outcome.Reason);
    }

    [Fact]
    public void 海盗压美人鱼()
    {
        var trick = Trick(Mermaid(), Pirate());

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.Pirate, outcome.Reason);
    }

    [Fact]
    public void 美人鱼压骷髅王()
    {
        var trick = Trick(King(), Mermaid());

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.MermaidCapturesSkullKing, outcome.Reason);
    }

    [Fact]
    public void 三角克制同时在场时美人鱼赢()
    {
        // 骷髅王压海盗、海盗压美人鱼，但三者齐聚时官方规定美人鱼收墩。
        var trick = Trick(Pirate(), King(), Mermaid());

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(2, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.MermaidCapturesSkullKing, outcome.Reason);
    }

    [Fact]
    public void 两条美人鱼加骷髅王时先出的美人鱼赢()
    {
        var trick = Trick(Mermaid(1), King(), Mermaid(2));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(0, outcome.WinnerSeat);
    }

    [Fact]
    public void 没有骷髅王时美人鱼只压数字牌()
    {
        var trick = Trick(Black(14), Mermaid());

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.Mermaid, outcome.Reason);
    }

    [Fact]
    public void Tigress当海盗时按海盗判定()
    {
        var trick = TrickWith((Black(14), null), (Tigress(), TigressMode.AsPirate));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.Pirate, outcome.Reason);
    }

    [Fact]
    public void Tigress当逃跑时不吃墩()
    {
        var trick = TrickWith((Green(2), null), (Tigress(), TigressMode.AsEscape));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(0, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.LeadSuit, outcome.Reason);
    }

    [Fact]
    public void Tigress当逃跑时也不确定跟牌花色()
    {
        var trick = TrickWith((Tigress(), TigressMode.AsEscape), (Yellow(5), null));

        Assert.Equal(Suit.TreasureChest, TrickResolver.LeadSuit(trick));
    }

    [Fact]
    public void Tigress当海盗被骷髅王吃掉()
    {
        var trick = TrickWith((Tigress(), TigressMode.AsPirate), (King(), null));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.SkullKing, outcome.Reason);
    }

    [Fact]
    public void 只有逃跑和一张数字牌时数字牌赢()
    {
        var trick = Trick(Escape(1), Green(1), Escape(2));

        var outcome = TrickResolver.Resolve(trick);

        Assert.Equal(1, outcome.WinnerSeat);
        Assert.Equal(TrickWinReason.LeadSuit, outcome.Reason);
    }

    [Fact]
    public void 空墩结算抛异常()
    {
        Assert.Throws<ArgumentException>(() => TrickResolver.Resolve([]));
    }
}
