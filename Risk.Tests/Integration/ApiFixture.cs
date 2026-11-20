using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Risk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Npgsql;

namespace Risk.Tests.Integration;

/// <summary>
/// The real API on a real Postgres (Testcontainers, so Docker must be running), shared by the
/// tests in <see cref="ApiCollection"/>. Same migrations as production.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;
    public const string Password = "Correct-Horse-Battery-9";

    /// <summary>An HTTP client that is not logged in.</summary>
    public HttpClient CreateClient() => _factory.CreateClient();

    public IServiceProvider Services => _factory.Services;

    private const string AppPassword = "integration-tests-app-password";

    /// <summary>The connection string the API uses: the data-only role, not the owner.</summary>
    public string AppConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Migrationerne kører som databasens ejer, ligesom migratoren gør i drift.
        var ownerOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_db.GetConnectionString()).Options;
        await using (var owner = new AppDbContext(ownerOptions))
        {
            await owner.Database.MigrateAsync();
            await owner.Database.ExecuteSqlRawAsync("ALTER ROLE risk_app PASSWORD '" + AppPassword + "'");
        }

        // API'et kører som risk_app, ligesom i drift. Mangler API'et en rettighed, fejler testene.
        AppConnectionString = new NpgsqlConnectionStringBuilder(_db.GetConnectionString())
        {
            Username = "risk_app",
            Password = AppPassword,
        }.ConnectionString;

        // Not "Development": that reads the developer's local .env and database.
        // UseSetting, because Program.cs reads these before the host is built.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Default", AppConnectionString)
            .UseSetting("RateLimiting:AuthPermitLimit", "1000")
            .UseSetting("Jwt:Key", "integration-tests-only-signing-key-0123456789")
            .UseSetting("Jwt:Issuer", "risk-tests")
            .UseSetting("Jwt:Audience", "risk-tests"));
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Registers a new user and returns an HTTP client logged in as them.</summary>
    public async Task<TestUser> RegisterAsync(string name)
    {
        var client = _factory.CreateClient();
        var unique = $"{name}-{Guid.NewGuid():N}";
        var userName = unique[..Math.Min(unique.Length, 30)];
        var email = $"{unique}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { userName, email, password = Password });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Register failed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new TestUser(body.GetProperty("user").GetProperty("id").GetGuid(), userName, email, token, client);
    }

    /// <summary>
    /// A game hub connection over WebSockets, with the token in the query string exactly like the browser
    /// sends it, so the API's query-token handling is tested too.
    /// </summary>
    public async Task<HubConnection> ConnectGameHubAsync(TestUser user)
    {
        var server = _factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "hubs/game"), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var url = new UriBuilder(context.Uri) { Query = $"access_token={user.Token}" }.Uri;
                    return await server.CreateWebSocketClient().ConnectAsync(url, ct);
                };
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    /// <summary>A separate API instance on the same database, with its own limit on login and registration.</summary>
    public WebApplicationFactory<Program> WithAuthPermitLimit(int permitLimit) =>
    _factory.WithWebHostBuilder(builder =>
        builder.UseSetting("RateLimiting:AuthPermitLimit", permitLimit.ToString()));
}

public sealed record TestUser(Guid Id, string UserName, string Email, string Token, HttpClient Http);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
