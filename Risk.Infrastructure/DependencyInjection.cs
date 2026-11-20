using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Application.Interfaces.Services;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;
using Risk.Infrastructure.Repositories;
using Risk.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Risk.Application.Auth;

namespace Risk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Identity
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // NIST SP 800-63B: mindst 15 tegn ved én faktor og ingen kompositionsregler.
                options.Password.RequiredLength = 15;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddPasswordValidator<CommonPasswordValidator>()
            .AddEntityFrameworkStores<AppDbContext>();
        // OWASP: mindst 210.000 iterationer for PBKDF2-HMAC-SHA512, som Identity bruger. Standarden er 100.000.
        services.Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000);
        services.AddScoped<IIdentityService, IdentityService>();

        // DataProtection-nøgler i databasen, så de overlever deploys og virker med read-only containere
        services.AddDataProtection()
            .SetApplicationName("Risk")
            .PersistKeysToDbContext<AppDbContext>();

        // JWT token generation
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton(sp =>
            new AuthTokenSettings(sp.GetRequiredService<IOptions<JwtOptions>>().Value.RefreshTokenLifetime));

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITerritoryRepository, TerritoryRepository>();
        services.AddScoped<IMapRepository, MapRepository>();
        services.AddScoped<IBonusCardRepository, BonusCardRepository>();
        services.AddScoped<IMissionCardRepository, MissionCardRepository>();

        services.AddSingleton<IDiceRoller, DiceRoller>();

        return services;
    }
}
