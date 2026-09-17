using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Entities;
using Risk.Infrastructure.Identity;

namespace Risk.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // Bemærk navnet: IdentityDbContext har allerede en Users-property til
    // ApplicationUser, så domæne-brugerne får deres eget navn.
    public DbSet<User> DomainUsers => Set<User>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
        });

        // Én login-konto hører til én domæne-bruger
        builder.Entity<ApplicationUser>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<ApplicationUser>(a => a.DomainUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
