using SkullKing.Domain.Cards;
using SkullKing.Domain.Rules;

namespace SkullKing.Domain.Game;

/// <summary>
/// 超时托管与机器人补位共用的兜底决策：叫 0，出第一张合法牌。
/// 策略刻意保守，目的是让掉线的人不拖死整局，而不是替他打好。
/// </summary>
public static class AutoPlayAdvisor
{
    public static GameCommand Suggest(GameState state, int seat) => state.Phase switch
    {
        GamePhase.Bidding => new PlaceBidCommand(seat, 0),
        GamePhase.Playing => SuggestPlay(state, seat),
        _ => throw new GameRuleException("对局已结束，无需托管")
    };

    private static PlayCardCommand SuggestPlay(GameState state, int seat)
    {
        var playable = PlayValidator.PlayableCards(state.Hands[seat], state.CurrentTrick);

        if (playable.IsEmpty)
        {
            throw new GameRuleException("没有可出的牌");
        }

        // 优先出逃跑，其次点数最小的牌，尽量少吃墩。
        var card = playable.FirstOrDefault(c => c.Kind == CardKind.Escape)
            ?? playable.Where(c => c.IsNumber).OrderBy(c => c.Rank).FirstOrDefault()
            ?? playable[0];

        var mode = card.Kind == CardKind.Tigress ? TigressMode.AsEscape : (TigressMode?)null;

        return new PlayCardCommand(seat, card.Id, mode);
    }
}
