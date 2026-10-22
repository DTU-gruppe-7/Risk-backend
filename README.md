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
* Docker (Desktop or Engine with Compose v2)
* EF Core CLI for the commands below: `dotnet tool restore` (pinned in `.config/dotnet-tools.json`)

---

## Getting Started

### 1. Configure Local Secrets
`.env` is the single source of truth for local secrets (it is git-ignored). Docker Compose reads it,
and so does the API itself when it runs in Development (`dotnet run`, `dotnet watch`, `dotnet ef`):

```powershell
Copy-Item .env.example .env   # then change POSTGRES_PASSWORD and JWT_KEY
```

Changes to `.env` take effect the next time the API starts.

### 2. Start the Database and Apply Migrations
Same flow as on the server: `db` → `migrator` (one-off EF migration bundle) → `api`.

```bash
docker compose up -d --build migrator
```

### 3. Run the API
Either on the host with hot reload:

```bash
dotnet watch --project Risk.Api
```

or the whole backend in Docker (same image and hardening as production):

```bash
docker compose up -d --build
```

Both listen on `http://localhost:5081`, which is where the frontend's Vite proxy points by default
(`npm run dev` in the frontend repo needs no extra configuration). Don't run both at once — they share the port.

The API reference (Scalar) is at `http://localhost:5081/scalar/v1` (Development only).

### 4. Day to Day
* **New migrations** (after `git pull` or `dotnet ef migrations add`): run `docker compose up -d --build migrator` again.
  `dotnet ef database update` (see below) works too.
* **Stop:** `docker compose down` — the database is kept in the `pgdata` volume.
* **Start over with an empty database:** `docker compose down -v`, then step 2 again.

---

## Common EF Core Commands

* Add a new migration:
  `dotnet ef migrations add <MigrationName> --project Risk.Infrastructure --startup-project Risk.Api`
* Update database:
  `dotnet ef database update --project Risk.Infrastructure --startup-project Risk.Api`

* Check if a migration is needed:
  `dotnet ef migrations has-pending-model-changes --project Risk.Infrastructure --startup-project Risk.Api`
