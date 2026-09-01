namespace SkullKing.Application.Abstractions;

public sealed record PlayerIdentity(string Id, string Nickname, string Token);

public interface IPlayerStore
{
    Task<PlayerIdentity> CreateAnonymousAsync(string nickname, CancellationToken ct = default);

    Task<PlayerIdentity?> FindByTokenAsync(string token, CancellationToken ct = default);

    Task<PlayerIdentity?> FindByIdAsync(string playerId, CancellationToken ct = default);

    Task RenameAsync(string playerId, string nickname, CancellationToken ct = default);

    Task TouchAsync(string playerId, CancellationToken ct = default);
}
