using SkullKing.Domain.Cards;

namespace SkullKing.Domain.Rules;

/// <summary>一墩的结算结果。</summary>
/// <param name="WinnerSeat">收下这一墩的座位。</param>
/// <param name="WinningPlay">决定胜负的那张牌。</param>
/// <param name="Reason">胜负原因，用于前端展示与测试断言。</param>
public sealed record TrickOutcome(int WinnerSeat, PlayedCard WinningPlay, TrickWinReason Reason);

public enum TrickWinReason
{
    /// <summary>美人鱼擒获骷髅王。</summary>
    MermaidCapturesSkullKing,
    SkullKing,
    Pirate,
    Mermaid,

    /// <summary>黑色王牌最大。</summary>
    Trump,

    /// <summary>跟牌花色最大。</summary>
    LeadSuit,

    /// <summary>全员逃跑，首家收墩。</summary>
    AllEscaped
}

public static class TrickResolver
{
    /// <summary>
    /// 跟牌花色：本墩第一张数字牌的花色。逃跑牌和特殊牌都不定花色，
    /// 所以首家出海盗时后手可以随意出，直到有人打出数字牌才锁定花色。
    /// </summary>
    public static Suit? LeadSuit(IReadOnlyList<PlayedCard> plays)
    {
        foreach (var play in plays)
        {
            if (play.EffectiveKind == CardKind.Number)
            {
                return play.Card.Suit;
            }
        }

        return null;
    }

    public static TrickOutcome Resolve(IReadOnlyList<PlayedCard> plays)
    {
        ArgumentNullException.ThrowIfNull(plays);

        if (plays.Count == 0)
        {
            throw new ArgumentException("空墩无法结算", nameof(plays));
        }

        var skullKing = FirstOfKind(plays, CardKind.SkullKing);
        var firstMermaid = FirstOfKind(plays, CardKind.Mermaid);
        var firstPirate = FirstOfKind(plays, CardKind.Pirate);

        // 三角克制：骷髅王压海盗、海盗压美人鱼、美人鱼压骷髅王。
        // 三者同时在场时美人鱼赢，这是官方明确规定的破环点。
        if (skullKing is not null && firstMermaid is not null)
        {
            return new TrickOutcome(firstMermaid.Seat, firstMermaid, TrickWinReason.MermaidCapturesSkullKing);
        }

        if (skullKing is not null)
        {
            return new TrickOutcome(skullKing.Seat, skullKing, TrickWinReason.SkullKing);
        }

        if (firstPirate is not null)
        {
            return new TrickOutcome(firstPirate.Seat, firstPirate, TrickWinReason.Pirate);
        }

        if (firstMermaid is not null)
        {
            return new TrickOutcome(firstMermaid.Seat, firstMermaid, TrickWinReason.Mermaid);
        }

        var bestTrump = HighestNumber(plays, Suit.JollyRoger);

        if (bestTrump is not null)
        {
            return new TrickOutcome(bestTrump.Seat, bestTrump, TrickWinReason.Trump);
        }

        var leadSuit = LeadSuit(plays);

        if (leadSuit is not null)
        {
            var bestLead = HighestNumber(plays, leadSuit.Value)
                ?? throw new InvalidOperationException("跟牌花色已确定却找不到该花色的牌");

            return new TrickOutcome(bestLead.Seat, bestLead, TrickWinReason.LeadSuit);
        }

        var first = plays[0];
        return new TrickOutcome(first.Seat, first, TrickWinReason.AllEscaped);
    }

    private static PlayedCard? FirstOfKind(IReadOnlyList<PlayedCard> plays, CardKind kind)
    {
        foreach (var play in plays)
        {
            if (play.EffectiveKind == kind)
            {
                return play;
            }
        }

        return null;
    }

    private static PlayedCard? HighestNumber(IReadOnlyList<PlayedCard> plays, Suit suit)
    {
        PlayedCard? best = null;

        foreach (var play in plays)
        {
            if (play.EffectiveKind != CardKind.Number || play.Card.Suit != suit)
            {
                continue;
            }

            if (best is null || play.Card.Rank > best.Card.Rank)
            {
                best = play;
            }
        }

        return best;
    }
}
