using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Risk.Api.Auth;
using Risk.Api.ErrorHandling;
using Risk.Application;
using Risk.Application.Interfaces;
using Risk.Infrastructure;
using Risk.Infrastructure.Identity;
using Risk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// I drift mountes secrets som filer i /run/secrets (Docker Compose secrets).
// Filnavnet er nøglen: "Jwt__Key" bliver til Jwt:Key. Mappen findes ikke lokalt, så den er valgfri.
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

// Enums serialiseres som strenge. Uden dette bliver de til rå tal i
// OpenAPI-dokumentet, og frontendens genererede typer bliver "number".
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddApplication();
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

// Både "dotnet build" (OpenAPI-generering), "dotnet ef" og migration bundles bygger
// host'en uden at starte den. Kendetegnet er at entry-assembly er værktøjet, ikke API'et selv.
var isDesignTime = Assembly.GetEntryAssembly() != Assembly.GetExecutingAssembly();

// Samme JwtOptions som JwtTokenService bruger, så udstedelse og validering aldrig kan glide fra hinanden.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (isDesignTime)
{
    if (string.IsNullOrWhiteSpace(jwt.Key))
        jwt.Key = "design-time-placeholder-bruges-aldrig-til-rigtige-tokens";
}
else if (!jwt.IsComplete)
{
    throw new InvalidOperationException(
        "Jwt:Key, Jwt:Issuer og Jwt:Audience skal være sat. Kør .\\setup-dev-secrets.ps1 lokalt, " +
        "eller sæt Jwt__Key, Jwt__Issuer og Jwt__Audience i drift.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // dokumentet på /openapi/v1.json
    app.MapScalarApiReference();      // brugerfladen på /scalar/v1
    app.UseCors("frontend");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapControllers();

app.Run();