using SkullKing.Domain.Cards;

namespace SkullKing.Domain.Rules;

/// <summary>一次出牌。<see cref="TigressMode"/> 只在打出 Tigress 时有值。</summary>
public sealed record PlayedCard(int Seat, Card Card, TigressMode? TigressMode = null)
{
    /// <summary>把 Tigress 折算成它本轮实际扮演的角色，判定时统一按这个来。</summary>
    public CardKind EffectiveKind => Card.Kind == CardKind.Tigress
        ? TigressMode == Cards.TigressMode.AsEscape ? CardKind.Escape : CardKind.Pirate
        : Card.Kind;
}
