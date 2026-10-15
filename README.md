# Risk Game Backend

Backend API for the online Risk board game, built with **.NET 10**, **C#**, and **PostgreSQL** following **Clean Architecture** and **Domain-Driven Design (DDD)** principles.

---

## Architecture Overview

```text
Risk-backend/
├── Risk.Domain/          # Core entities, value objects, and domain rules
├── Risk.Application/     # Use cases, interfaces (repositories/services), and business logic
├── Risk.Infrastructure/  # EF Core AppDbContext, repository implementations, PostgreSQL mapping
└── Risk.Api/             # Presentation layer, controllers, DI setup, and middleware
```

### Core Entities
* **Game & Player:** Aggregates managing game state, turn phases, and participants.
* **Map & Board:** Continent, Territory, and TerritoryBorder (borders between adjacent regions).
* **Cards:** BonusCard and MissionCard.
* **Authentication:** ASP.NET Core Identity with PostgreSQL storage.

---

## Prerequisites

* .NET 10 SDK
* PostgreSQL (running locally or via Docker)
* EF Core CLI Tools (dotnet tool install --global dotnet-ef)

---

## Getting Started

### 1. Configure Local Database Credentials
To avoid committing passwords to Git, use **.NET User Secrets** for your local connection string:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=RiskDB;Username=postgres;Password=<YOUR_LOCAL_PASSWORD>" --project Risk.Api
```

### 2. Apply Database Migrations
Update your local PostgreSQL database with the latest schema:

```bash
dotnet ef database update --project Risk.Infrastructure --startup-project Risk.Api
```

### 3. Build and Run
```bash
dotnet build
dotnet run --project Risk.Api
```

---

## Common EF Core Commands

* Add a new migration:
  `dotnet ef migrations add <MigrationName> --project Risk.Infrastructure --startup-project Risk.Api`
* Update database:
  `dotnet ef database update --project Risk.Infrastructure --startup-project Risk.Api`

* Check if a migration is needed:
  `dotnet ef migrations has-pending-model-changes --project Risk.Infrastructure --startup-project Risk.Api`
