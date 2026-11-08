using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Risk.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Risk.Tests.Integration;

/// <summary>
/// The real API on a real Postgres (Testcontainers, so Docker must be running), shared by the
/// tests in <see cref="ApiCollection"/>. Same migrations as production.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        // Not "Development": that reads the developer's local .env and database.
        // UseSetting, because Program.cs reads these before the host is built.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:Default", _db.GetConnectionString())
            .UseSetting("Jwt:Key", "integration-tests-only-signing-key-0123456789")
            .UseSetting("Jwt:Issuer", "risk-tests")
            .UseSetting("Jwt:Audience", "risk-tests"));

        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
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
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { userName, email = $"{unique}@example.com", password = "Correct-Horse-Battery-9" });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Register failed: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new TestUser(body.GetProperty("user").GetProperty("id").GetGuid(), userName, token, client);
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
}

public sealed record TestUser(Guid Id, string UserName, string Token, HttpClient Http);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
