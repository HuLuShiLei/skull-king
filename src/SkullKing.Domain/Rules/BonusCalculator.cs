using SkullKing.Domain.Cards;

namespace SkullKing.Domain.Rules;

/// <summary>
/// 奖励分。只有在轮末叫牌命中时才真正计入总分，所以这里只负责按墩累计。
/// </summary>
public static class BonusCalculator
{
    public const int TopRankBonus = 10;
    public const int TopTrumpBonus = 20;
    public const int SkullKingOverPirateBonus = 30;
    public const int PirateOverSkullKingBonus = 30;
    public const int MermaidOverSkullKingBonus = 50;

    public static int ForTrick(TrickOutcome outcome, IReadOnlyList<PlayedCard> plays)
    {
        var bonus = 0;

        // 收下的墩里每有一张 14 就加分，不要求赢家自己打的是数字牌。
        foreach (var play in plays)
        {
            if (play.EffectiveKind != CardKind.Number || !play.Card.IsTopRank)
            {
                continue;
            }

            bonus += play.Card.IsTrump ? TopTrumpBonus : TopRankBonus;
        }

        switch (outcome.Reason)
        {
            case TrickWinReason.MermaidCapturesSkullKing:
                bonus += MermaidOverSkullKingBonus;
                break;

            case TrickWinReason.SkullKing:
                bonus += plays.Count(p => p.EffectiveKind == CardKind.Pirate) * SkullKingOverPirateBonus;
                break;

            case TrickWinReason.Pirate:
                if (plays.Any(p => p.EffectiveKind == CardKind.SkullKing))
                {
                    bonus += PirateOverSkullKingBonus;
                }

                break;
        }

        return bonus;
    }
}
