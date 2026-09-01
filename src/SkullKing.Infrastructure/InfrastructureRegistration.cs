using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkullKing.Application.Abstractions;

namespace SkullKing.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddSkullKingPersistence(this IServiceCollection services, string connectionString)
    {
        // 用工厂而不是作用域实例：RoomService 是单例，且后台巡检和 Hub 会并发访问。
        services.AddDbContextFactory<SkullKingDbContext>(options => options.UseSqlite(connectionString));

        services.AddSingleton<IPlayerStore, EfPlayerStore>();
        services.AddSingleton<IGameArchive, EfGameArchive>();

        return services;
    }

    public static async Task MigrateAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        var factory = services.GetRequiredService<IDbContextFactory<SkullKingDbContext>>();

        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}
