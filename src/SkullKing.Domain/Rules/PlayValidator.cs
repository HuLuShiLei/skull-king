using System.Collections.Immutable;
using SkullKing.Domain.Cards;

namespace SkullKing.Domain.Rules;

public static class PlayValidator
{
    /// <summary>
    /// 从手牌里筛出当前可以合法打出的牌。客户端的置灰逻辑完全以服务端算出的这份清单为准。
    /// </summary>
    public static ImmutableArray<Card> PlayableCards(
        ImmutableArray<Card> hand,
        IReadOnlyList<PlayedCard> currentTrick)
    {
        var leadSuit = TrickResolver.LeadSuit(currentTrick);

        if (leadSuit is null)
        {
            return hand;
        }

        var hasLeadSuit = hand.Any(c => c.IsNumber && c.Suit == leadSuit);

        if (!hasLeadSuit)
        {
            return hand;
        }

        // 手上有跟牌花色就必须跟，但特殊牌任何时候都能出。
        return [.. hand.Where(c => !c.IsNumber || c.Suit == leadSuit)];
    }

    public static bool CanPlay(
        ImmutableArray<Card> hand,
        IReadOnlyList<PlayedCard> currentTrick,
        string cardId)
        => PlayableCards(hand, currentTrick).Any(c => c.Id == cardId);
}
