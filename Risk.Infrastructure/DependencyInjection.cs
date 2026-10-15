using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Risk.Application.Interfaces;
using Risk.Application.Interfaces.Repository;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;
using Risk.Infrastructure.Repositories;
using Microsoft.AspNetCore.DataProtection;

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

        // Identity
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        
        // DataProtection-nøgler i databasen, så de overlever deploys og virker med read-only containere
        services.AddDataProtection()
            .SetApplicationName("Risk")
            .PersistKeysToDbContext<AppDbContext>();


        // JWT token generation
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<ITerritoryRepository, TerritoryRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();

        return services;
    }
}
