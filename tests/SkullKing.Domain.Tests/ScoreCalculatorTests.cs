using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;
using static SkullKing.Domain.Tests.TestCards;

namespace SkullKing.Domain.Tests;

public class ScoreCalculatorTests
{
    [Theory]
    [InlineData(1, 10)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    public void 叫零成功得轮次乘十(int round, int expected)
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: round, bid: 0, tricksWon: 0, accruedBonus: 0);

        Assert.Equal(expected, score.Total);
        Assert.True(score.BidMet);
    }

    [Theory]
    [InlineData(1, 1, -10)]
    [InlineData(7, 3, -70)]
    public void 叫零失败扣轮次乘十且与多吃几墩无关(int round, int tricksWon, int expected)
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: round, bid: 0, tricksWon: tricksWon, accruedBonus: 0);

        Assert.Equal(expected, score.Total);
        Assert.False(score.BidMet);
    }

    [Fact]
    public void 叫牌命中每墩二十分()
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: 5, bid: 3, tricksWon: 3, accruedBonus: 0);

        Assert.Equal(60, score.Total);
    }

    [Fact]
    public void 叫牌命中才计入奖励分()
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: 5, bid: 2, tricksWon: 2, accruedBonus: 50);

        Assert.Equal(40, score.BaseScore);
        Assert.Equal(50, score.Bonus);
        Assert.Equal(90, score.Total);
    }

    [Fact]
    public void 叫牌落空时奖励分作废()
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: 5, bid: 2, tricksWon: 4, accruedBonus: 80);

        Assert.Equal(0, score.Bonus);
        Assert.Equal(-20, score.Total);
    }

    [Theory]
    [InlineData(3, 1, -20)]
    [InlineData(1, 4, -30)]
    public void 叫牌落空按差额每墩扣十(int bid, int tricksWon, int expected)
    {
        var score = ScoreCalculator.Score(seat: 0, roundNumber: 6, bid: bid, tricksWon: tricksWon, accruedBonus: 0);

        Assert.Equal(expected, score.Total);
    }
}

public class BonusCalculatorTests
{
    private static int Bonus(List<PlayedCard> trick)
        => BonusCalculator.ForTrick(TrickResolver.Resolve(trick), trick);

    [Fact]
    public void 收下普通十四加十分()
    {
        Assert.Equal(10, Bonus(Trick(Green(14), Green(3))));
    }

    [Fact]
    public void 收下黑色十四加二十分()
    {
        Assert.Equal(20, Bonus(Trick(Black(14), Black(3))));
    }

    [Fact]
    public void 多张十四同墩时奖励叠加()
    {
        // 绿 14 起手，黑 14 用王牌吃下，两张 14 的奖励都归赢家。
        Assert.Equal(30, Bonus(Trick(Green(14), Black(14))));
    }

    [Fact]
    public void 用特殊牌吃下含十四的墩同样拿奖励()
    {
        Assert.Equal(10, Bonus(Trick(Green(14), Pirate())));
    }

    [Fact]
    public void 骷髅王每吃一个海盗加三十()
    {
        Assert.Equal(60, Bonus(Trick(Pirate(1), Pirate(2), King())));
    }

    [Fact]
    public void 骷髅王吃Tigress当海盗也算()
    {
        var trick = TrickWith((Tigress(), TigressMode.AsPirate), (King(), null));

        Assert.Equal(30, BonusCalculator.ForTrick(TrickResolver.Resolve(trick), trick));
    }

    [Fact]
    public void 海盗吃骷髅王加三十()
    {
        Assert.Equal(30, Bonus(Trick(Pirate(), King())));
    }

    [Fact]
    public void 美人鱼吃骷髅王加五十()
    {
        Assert.Equal(50, Bonus(Trick(King(), Mermaid())));
    }

    [Fact]
    public void 三角克制时只算美人鱼的五十分()
    {
        // 海盗虽在场，但赢家是美人鱼，海盗相关奖励不生效。
        Assert.Equal(50, Bonus(Trick(Pirate(), King(), Mermaid())));
    }

    [Fact]
    public void 特殊奖励与十四奖励可叠加()
    {
        // 黑 14 的 20 分加上美人鱼擒获骷髅王的 50 分。
        Assert.Equal(70, Bonus(Trick(Black(14), King(), Mermaid())));
    }

    [Fact]
    public void 普通墩没有奖励分()
    {
        Assert.Equal(0, Bonus(Trick(Green(13), Green(2))));
    }
}
