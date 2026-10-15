using System.Text;
using System.Text.Json.Serialization;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Risk.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Enums serialiseres som strenge. Uden dette bliver de til rå tal i
// OpenAPI-dokumentet, og frontendens genererede typer bliver "number".
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy => policy
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// Både "dotnet build" (OpenAPI-generering) og "dotnet ef" bygger host'en uden
// at starte den. De kører som Production og indlæser derfor ikke user-secrets.
// Kendetegnet er at entry-assembly er værktøjet, ikke API'et selv.
var isDesignTime = Assembly.GetEntryAssembly() != Assembly.GetExecutingAssembly();

var signingKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(signingKey))
{
    if (!isDesignTime)
    {
        throw new InvalidOperationException(
            "Jwt:Key mangler. Kør .\\setup-dev-secrets.ps1 lokalt, " +
            "eller sæt miljøvariablen Jwt__Key i drift.");
    }

    signingKey = "design-time-placeholder-bruges-aldrig-til-rigtige-tokens";
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // dokumentet på /openapi/v1.json
    app.MapScalarApiReference();      // brugerfladen på /scalar/v1
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
