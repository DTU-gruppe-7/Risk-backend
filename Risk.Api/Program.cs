using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using Scalar.AspNetCore;
using Risk.Api.Auth;
using Risk.Api.BackgroundServices;
using Risk.Api.ErrorHandling;
using Risk.Api.Realtime;
using Risk.Application;
using Risk.Application.Interfaces;
using Risk.Infrastructure;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Risk.Api.Controllers;
using System.Threading.RateLimiting;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);
var isDesignTime = Assembly.GetEntryAssembly() != Assembly.GetExecutingAssembly();

// I drift mountes secrets som filer i /run/secrets (Docker Compose secrets).
// Filnavnet er nøglen: "Jwt__Key" bliver til Jwt:Key. Mappen findes ikke lokalt, så den er valgfri.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

// Kun lokalt: samme .env som docker compose, så "dotnet run" og "dotnet ef" ikke kræver et setup-script.
// Filen kommer ikke med i imaget (.dockerignore), så i containere sker der intet.
if (builder.Environment.IsDevelopment())
{
    var envFile = Path.Combine(builder.Environment.ContentRootPath, "..", ".env");
    if (File.Exists(envFile))
    {
        var env = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(envFile).Select(l => l.Trim()))
        {
            if (line.Length == 0 || line.StartsWith('#') || !line.Contains('=')) continue;
            var parts = line.Split('=', 2);
            env[parts[0].Trim()] = parts[1].Trim().Trim('"', '\'');
        }

        string Require(string key) => env.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : throw new InvalidOperationException($"{key} mangler i {Path.GetFullPath(envFile)}.");

        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "localhost",
            Port = int.Parse(env.GetValueOrDefault("POSTGRES_PORT", "5433")),
            Database = Require("POSTGRES_DB"),
            Username = "risk_app",
            Password = Require("POSTGRES_APP_PASSWORD"),
        };

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connection.ConnectionString,
            ["Jwt:Key"] = Require("JWT_KEY"),
        });
    }
}

// Samme JSON-regler for controllernes svar (MVC) og for OpenAPI-dokumentet. Generatoren læser
// de generelle HTTP-indstillinger, ikke MVC's, så en converter kun på MVC gav et dokument, der
// beskrev enums som tal, selvom API'et sendte strenge (opgave 19).
// - Enums som strenge: "InProgress" i stedet for 1.
// - Strict: tal skal være tal. Uden den accepterer .NET "5" som 5, og dokumentet beskriver
//   alle heltal som "integer eller string", så frontendens typer bliver number | string.
static void ConfigureJson(JsonSerializerOptions options)
{
    options.Converters.Add(new JsonStringEnumConverter());
    options.NumberHandling = JsonNumberHandling.Strict;
}

builder.Services
    .AddControllers()
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));

// XML-kommentarer over flere linjer får styresystemets linjeskift: \r\n på Windows og \n på Linux.
// Uden dette afhænger openapi.json af, hvem der byggede den, og CI's tjek af filen fejler.
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        foreach (var path in document.Paths.Values)
            if (path.Operations is not null)
            foreach (var operation in path.Operations.Values)
            {
                operation.Summary = operation.Summary?.ReplaceLineEndings("\n");
                operation.Description = operation.Description?.ReplaceLineEndings("\n");
            }
        return Task.CompletedTask;
    }));
    
builder.Services.AddRealtime();
builder.Services.AddApplication();
// Build tools (OpenAPI generation, dotnet ef) construct the host without running it, and must not start timers.
if (!isDesignTime)
    builder.Services.AddHostedService<TurnTimeoutWorker>();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Domain/application exceptions → ProblemDetails (RFC 7807), see ProblemDetailsExceptionHandler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// Kun til lokal udvikling. I drift serveres frontend og API fra samme origin via Caddy.
builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Bag Caddy: stol kun på X-Forwarded-* fra proxy-netværket (sættes i compose).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    var proxyNetwork = builder.Configuration["ReverseProxy:Network"];
    if (!string.IsNullOrWhiteSpace(proxyNetwork))
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(proxyNetwork));
});

// /healthz = processen kører, /readyz = processen kan nå databasen.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"]);

// Login og registrering: Højst N forsøg i minuttet pr. IP. Lockout pr. konto sker i Identity.
// IP'en er klientens og ikke Caddy, fordi UseForwardedHeaders kører først.
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthController.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }
    ));
    options.OnRejected = async (context, _) =>
    {
        var http = context.HttpContext;
        http.RequestServices.GetRequiredService<ILogger<Program>>()
        .LogWarning("Too many auth requests from {RemoteIp} to {Path}.",
        http.Connection.RemoteIpAddress, http.Request.Path);

        await http.RequestServices.GetRequiredService<IProblemDetailsService>()
        .WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails =
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = "Too many attempts. Wait a minute and try again.",
                },
            }
        );
    };
});

// Metrics sendes med OTLP til Prometheus på serveren. Lokalt og i testene er OTEL_EXPORTER_OTLP_ENDPOINT
// ikke sat, og så registreres intet. Adresse, protokol, servicenavn og miljø kommer fra OTEL_*-variablerne.
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics
        .AddMeter(
            "Microsoft.AspNetCore.Hosting",          // requests, svartider og statuskoder
            "Microsoft.AspNetCore.Server.Kestrel",   // forbindelser
            "Microsoft.AspNetCore.Http.Connections", // SignalR-forbindelser
            "Microsoft.AspNetCore.RateLimiting",     // afviste forsøg på login og registrering
            "System.Runtime",                        // hukommelse, GC og tråde
            AuthMetrics.MeterName)                   // loginforsøg fordelt på resultat
        .AddOtlpExporter());
}

// Både "dotnet build" (OpenAPI-generering), "dotnet ef" og migration bundles bygger
// host'en uden at starte den. Kendetegnet er at entry-assembly er værktøjet, ikke API'et selv.
// Samme JwtOptions som JwtTokenService bruger, så udstedelse og validering aldrig kan glide fra hinanden.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (isDesignTime)
{
    if (string.IsNullOrWhiteSpace(jwt.Key))
        jwt.Key = "design-time-placeholder-bruges-aldrig-til-rigtige-tokens";
}
else
{
    // Fejl tidligt ved opstart i stedet for ved første login.
    jwt.Validate();
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = jwt.CreateValidationParameters();
        options.Events = RealtimeEndpoints.ReadTokenFromQueryOnHubs();
    });

// Deny by default: et endpoint uden [Authorize] eller [AllowAnonymous] kræver login.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();                 // dokumentet på /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();      // brugerfladen på /scalar/v1
    app.UseCors("frontend");
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapControllers();
app.MapRealtimeHubs();

app.Run();

// Lets the integration tests start the API with WebApplicationFactory<Program>.
public partial class Program;
