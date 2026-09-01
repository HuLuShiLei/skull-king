using SkullKing.Domain.Cards;

namespace SkullKing.Domain.Game;

public abstract record GameCommand;

public sealed record PlaceBidCommand(int Seat, int Bid) : GameCommand;

public sealed record PlayCardCommand(int Seat, string CardId, TigressMode? TigressMode = null) : GameCommand;
