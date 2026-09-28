using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Identity;

namespace Risk.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<User> DomainUsers => Set<User>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Continent> Continents => Set<Continent>();
    public DbSet<Territory> Territories => Set<Territory>();
    public DbSet<TerritoryBorder> TerritoryBorders => Set<TerritoryBorder>();
    public DbSet<BonusCard> BonusCards => Set<BonusCard>();
    public DbSet<MissionCard> MissionCards => Set<MissionCard>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Domain User ---
        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        builder.Entity<ApplicationUser>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<ApplicationUser>(a => a.DomainUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Game Aggregate ---
        builder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(g => g.Id);

            entity.Property(g => g.State).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(g => g.CurrentPhase).HasConversion<string>().HasMaxLength(30).IsRequired();

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(g => g.HostId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(g => g.CurrentPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Backing fields for IReadOnlyCollection
            entity.Navigation(g => g.Players).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(g => g.Territories).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(g => g.Continents).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasMany(g => g.Players)
                .WithOne(p => p.Game)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Player ---
        builder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Color).HasMaxLength(30).IsRequired();

            entity.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();

            entity.Navigation(p => p.Territories).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasMany(p => p.Territories)
                .WithOne(t => t.Owner)
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // --- Continent ---
        builder.Entity<Continent>(entity =>
        {
            entity.ToTable("continents");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        // --- Territory ---
        builder.Entity<Territory>(entity =>
        {
            entity.ToTable("territories");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Identifier).HasMaxLength(100).IsRequired();

            entity.HasOne(t => t.Continent)
                .WithMany()
                .HasForeignKey(t => t.ContinentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Territory Borders (Many-to-Many self reference) ---
        builder.Entity<TerritoryBorder>(entity =>
        {
            entity.ToTable("territory_borders");
            entity.HasKey(tb => new { tb.TerritoryId, tb.BorderingTerritoryId });

            entity.HasOne(tb => tb.Territory)
                .WithMany()
                .HasForeignKey(tb => tb.TerritoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tb => tb.BorderingTerritory)
                .WithMany()
                .HasForeignKey(tb => tb.BorderingTerritoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // --- BonusCard ---
        builder.Entity<BonusCard>(entity =>
        {
            entity.ToTable("bonus_cards");
            entity.HasKey(b => b.Id);
        });

        // --- MissionCard ---
        builder.Entity<MissionCard>(entity =>
        {
            entity.ToTable("mission_cards");
            entity.HasKey(m => m.Id);
        });
    }
}