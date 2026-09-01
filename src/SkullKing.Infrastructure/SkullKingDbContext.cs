using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SkullKing.Infrastructure;

public sealed class SkullKingDbContext(DbContextOptions<SkullKingDbContext> options) : DbContext(options)
{
    public DbSet<PlayerRow> Players => Set<PlayerRow>();

    public DbSet<RoomRow> Rooms => Set<RoomRow>();

    public DbSet<RoomMemberRow> RoomMembers => Set<RoomMemberRow>();

    public DbSet<GameRow> Games => Set<GameRow>();

    public DbSet<GameMoveRow> GameMoves => Set<GameMoveRow>();

    public DbSet<GameSeatRow> GameSeats => Set<GameSeatRow>();

    public DbSet<RoundScoreRow> RoundScores => Set<RoundScoreRow>();

    public DbSet<ChatMessageRow> ChatMessages => Set<ChatMessageRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite 不能对 DateTimeOffset 列做 ORDER BY，存成二进制才能在库里排序和比较。
        // 这个转换器是保序的，按它排出来的顺序就是真实时间顺序。
        builder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<PlayerRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(32);
            entity.Property(e => e.Nickname).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Token).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
        });

        builder.Entity<RoomRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).HasMaxLength(8).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(40).IsRequired();
            entity.Property(e => e.HostPlayerId).HasMaxLength(32).IsRequired();
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        builder.Entity<RoomMemberRow>(entity =>
        {
            entity.HasKey(e => new { e.RoomId, e.PlayerId });
            entity.Property(e => e.PlayerId).HasMaxLength(32);
            entity.Property(e => e.Nickname).HasMaxLength(40).IsRequired();

            entity.HasOne(e => e.Room)
                .WithMany(r => r!.Members)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RoomId);

            entity.HasOne(e => e.Room)
                .WithMany(r => r!.Games)
                .HasForeignKey(e => e.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameMoveRow>(entity =>
        {
            entity.HasKey(e => new { e.GameId, e.Seq });
            entity.Property(e => e.Kind).HasMaxLength(8).IsRequired();
            entity.Property(e => e.CardId).HasMaxLength(8);
            entity.Property(e => e.TigressMode).HasMaxLength(16);

            entity.HasOne(e => e.Game)
                .WithMany(g => g!.Moves)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<GameSeatRow>(entity =>
        {
            entity.HasKey(e => new { e.GameId, e.Seat });
            entity.Property(e => e.PlayerId).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Nickname).HasMaxLength(40).IsRequired();
            entity.HasIndex(e => e.PlayerId);

            entity.HasOne(e => e.Game)
                .WithMany(g => g!.Seats)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoundScoreRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GameId, e.RoundNumber, e.Seat }).IsUnique();

            entity.HasOne(e => e.Game)
                .WithMany(g => g!.RoundScores)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessageRow>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(32);
            entity.Property(e => e.Nickname).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Text).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => new { e.RoomId, e.SentAt });
        });
    }
}
