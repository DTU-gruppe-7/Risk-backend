using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Risk.Infrastructure.Persistence;

namespace Risk.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class DatabaseSettingsTests(ApiFixture api)
{
    // JIT-kompilering kostede 100+ ms pr. query og fik dev-databasen til at bruge det meste af en CPU
    // (opgave 26). Testen kører mod en rigtig Postgres og tjekker den forbindelse, API'et selv bygger.
    [Fact]
    public async Task The_api_connection_has_jit_turned_off()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jit = await db.Database
            .SqlQueryRaw<string>("SELECT current_setting('jit') AS \"Value\"")
            .SingleAsync();

        Assert.Equal("off", jit);
    }

    // Npgsqls standard er 100 forbindelser, men Postgres har kun 97 ledige. I stresstesten gav det
    // fejlen 53300, og en pulje på 20 gav mere gennemløb på én CPU (opgave 27).
    [Fact]
    public async Task The_api_connection_pool_is_limited()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var builder = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());

        Assert.Equal(20, builder.MaxPoolSize);
    }
}