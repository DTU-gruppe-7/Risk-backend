using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Domain.Map;
using Risk.Infrastructure.Identity;

namespace Risk.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IDataProtectionKeyContext
{
      public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    // Bemærk navnet: IdentityDbContext har allerede en Users-property til
    // ApplicationUser, så domæne-brugerne får deres eget navn.
    public DbSet<User> DomainUsers           => Set<User>();
    public DbSet<Game> Games                 => Set<Game>();
    public DbSet<GameSetting> GameSettings   => Set<GameSetting>();
    public DbSet<Player> Players             => Set<Player>();
    public DbSet<Territory> Territories      => Set<Territory>();
    public DbSet<BonusCard> BonusCards       => Set<BonusCard>();
    public DbSet<MissionCard> MissionCards   => Set<MissionCard>();

    // Static board (reference data, seeded from ClassicMap)
    public DbSet<MapContinent> MapContinents => Set<MapContinent>();
    public DbSet<MapTerritory> MapTerritories => Set<MapTerritory>();
    public DbSet<MapBorder> MapBorders       => Set<MapBorder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
      base.OnModelCreating(builder);

      builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");


        // ------------------------------------------------------------------
        // User
        // ------------------------------------------------------------------
        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Username).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        builder.Entity<ApplicationUser>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<ApplicationUser>(a => a.DomainUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ------------------------------------------------------------------
        // Game
        // ------------------------------------------------------------------
        builder.Entity<Game>(entity =>
        {
            entity.ToTable("games");
            entity.HasKey(g => g.Id);

            // Two requests changing the same game: the second one fails instead of overwriting.
            entity.Property(g => g.Version).IsConcurrencyToken();

            entity.Property(g => g.State).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(g => g.CurrentPhase).HasConversion<string>().HasMaxLength(30).IsRequired();

            // Explicit FK from HostId → User
            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(g => g.HostId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Explicit FK from CurrentPlayerId → Player (nullable)
            entity.HasOne<Player>()
                  .WithMany()
                  .HasForeignKey(g => g.CurrentPlayerId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Settings (one-to-one)
            entity.HasOne(g => g.Settings)
                  .WithOne(s => s.Game)
                  .HasForeignKey<GameSetting>(s => s.GameId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Backing fields for IReadOnlyCollection nav properties
            entity.Navigation(g => g.Players).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(g => g.Territories).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasMany(g => g.Players)
                  .WithOne(p => p.Game)
                  .HasForeignKey(p => p.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------------
        // GameSetting
        // ------------------------------------------------------------------
        builder.Entity<GameSetting>(entity =>
        {
            entity.ToTable("game_settings");
            entity.HasKey(s => s.Id);
        });

        // ------------------------------------------------------------------
        // Player
        // ------------------------------------------------------------------
        builder.Entity<Player>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Color).HasMaxLength(30).IsRequired();

            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // A player can hold several bonus cards at once
            entity.Navigation(p => p.BonusCards).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(p => p.BonusCards)
                  .WithOne(b => b.Player)
                  .HasForeignKey(b => b.PlayerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(p => p.MissionCard)
                  .WithOne(m => m.Player)
                  .HasForeignKey<MissionCard>(m => m.PlayerId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            // Unique: one user per game
            entity.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();

            // Backing field for IReadOnlyCollection
            entity.Navigation(p => p.Territories).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasMany(p => p.Territories)
                  .WithOne(t => t.Owner)
                  .HasForeignKey(t => t.OwnerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ------------------------------------------------------------------
        // Static board: seeded with HasData, so every migrated database
        // contains the map. Change ClassicMap + add a migration to change it.
        // ------------------------------------------------------------------
        builder.Entity<MapContinent>(entity =>
        {
            entity.ToTable("map_continents");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(100);
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();

            entity.Navigation(c => c.Territories).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(c => c.Territories)
                  .WithOne(t => t.Continent)
                  .HasForeignKey(t => t.ContinentId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(ClassicMap.Continents);
        });

        builder.Entity<MapTerritory>(entity =>
        {
            entity.ToTable("map_territories");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasMaxLength(100);
            entity.Property(t => t.ContinentId).HasMaxLength(100).IsRequired();

            entity.Navigation(t => t.Borders).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(t => t.Borders)
                  .WithOne()
                  .HasForeignKey(b => b.TerritoryId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(ClassicMap.Territories);
        });

        builder.Entity<MapBorder>(entity =>
        {
            entity.ToTable("map_borders");
            entity.HasKey(b => new { b.TerritoryId, b.NeighborId });
            entity.Property(b => b.TerritoryId).HasMaxLength(100);
            entity.Property(b => b.NeighborId).HasMaxLength(100);

            entity.HasOne<MapTerritory>()
                  .WithMany()
                  .HasForeignKey(b => b.NeighborId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(ClassicMap.Borders);
        });

        // ------------------------------------------------------------------
        // Territory (per-game state of a map territory)
        // ------------------------------------------------------------------
        builder.Entity<Territory>(entity =>
        {
            entity.ToTable("territories");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Identifier).HasMaxLength(100).IsRequired();

            entity.HasOne(t => t.Game)
                  .WithMany(g => g.Territories)
                  .HasForeignKey(t => t.GameId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Every row must point at a real map territory
            entity.HasOne<MapTerritory>()
                  .WithMany()
                  .HasForeignKey(t => t.Identifier)
                  .OnDelete(DeleteBehavior.Restrict);

            // A map territory appears once per game
            entity.HasIndex(t => new { t.GameId, t.Identifier }).IsUnique();
        });

        // ------------------------------------------------------------------
        // BonusCard
        // ------------------------------------------------------------------
        builder.Entity<BonusCard>(entity =>
        {
            entity.ToTable("bonus_cards");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(b => b.TerritoryIdentifier).HasMaxLength(100);

            // Wild cards have no territory; the rest must depict a real one
            entity.HasOne<MapTerritory>()
                  .WithMany()
                  .HasForeignKey(b => b.TerritoryIdentifier)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Game)
                  .WithMany()
                  .HasForeignKey(b => b.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------------
        // MissionCard
        // ------------------------------------------------------------------
        builder.Entity<MissionCard>(entity =>
        {
            entity.ToTable("mission_cards");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Title).HasMaxLength(200).IsRequired();
            entity.Property(m => m.Description).HasMaxLength(1000).IsRequired();

            entity.HasOne(m => m.Game)
                  .WithMany()
                  .HasForeignKey(m => m.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ------------------------------------------------------------------
        // Ids are generated by the domain (Guid.CreateVersion7), never by EF.
        // Without this, an entity added through a navigation (game.AddPlayer)
        // already has a key, so EF would treat it as existing and UPDATE it.
        // ------------------------------------------------------------------
        builder.Entity<User>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<Game>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<GameSetting>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<Player>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<Territory>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<BonusCard>().Property(e => e.Id).ValueGeneratedNever();
        builder.Entity<MissionCard>().Property(e => e.Id).ValueGeneratedNever();
    }
}