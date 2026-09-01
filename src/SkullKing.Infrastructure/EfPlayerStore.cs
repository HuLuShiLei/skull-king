using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SkullKing.Application.Abstractions;

namespace SkullKing.Infrastructure;

public sealed class EfPlayerStore(IDbContextFactory<SkullKingDbContext> factory) : IPlayerStore
{
    public async Task<PlayerIdentity> CreateAnonymousAsync(string nickname, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = new PlayerRow
        {
            Id = Guid.NewGuid().ToString("N"),
            Nickname = Sanitize(nickname),
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };

        db.Players.Add(row);
        await db.SaveChangesAsync(ct);

        return new PlayerIdentity(row.Id, row.Nickname, row.Token);
    }

    public async Task<PlayerIdentity?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Token == token, ct);

        return row is null ? null : new PlayerIdentity(row.Id, row.Nickname, row.Token);
    }

    public async Task<PlayerIdentity?> FindByIdAsync(string playerId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var row = await db.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId, ct);

        return row is null ? null : new PlayerIdentity(row.Id, row.Nickname, row.Token);
    }

    public async Task RenameAsync(string playerId, string nickname, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Players
            .Where(p => p.Id == playerId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Nickname, Sanitize(nickname)), ct);
    }

    public async Task TouchAsync(string playerId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Players
            .Where(p => p.Id == playerId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastSeenAt, DateTimeOffset.UtcNow), ct);
    }

    private static string Sanitize(string nickname)
    {
        var trimmed = (nickname ?? string.Empty).Trim();

        if (trimmed.Length == 0)
        {
            return "匿名同事";
        }

        return trimmed.Length > 20 ? trimmed[..20] : trimmed;
    }
}
