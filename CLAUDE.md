# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SecopSearch** is an intelligent SECOP II monitoring system that continuously scans Colombian government procurement processes, scores them against a business group's profile, and notifies via Telegram bot. No web frontend — Telegram is the only UI.

**Client:** Colombian business group with 4 units and $14B COP combined financial capacity.

## Stack

- **Language:** C# .NET 8
- **Architecture:** Clean Architecture (Domain → Application → Infrastructure → Api/Worker)
- **Database:** PostgreSQL + pgvector (Supabase)
- **ORM:** Entity Framework Core + Npgsql
- **Embeddings:** OpenAI `text-embedding-3-small`
- **PDF analysis:** Anthropic Claude API (`claude-sonnet-4-20250514`)
- **Data source:** SECOP II API via datos.gov.co (Socrata SODA)
- **Frontend:** Telegram.Bot SDK
- **Messaging:** MediatR
- **Testing:** xunit + Moq + FluentAssertions

## Project Structure

```
Secop.Buscador/
├── src/
│   ├── Secop.Domain/           # Entities, Enums, ValueObjects — zero external deps
│   ├── Secop.Application/      # Use cases (MediatR handlers), interfaces, DTOs
│   ├── Secop.Infrastructure/   # EF Core, external API clients, Telegram, scoring
│   ├── Secop.Api/              # REST endpoints (ASP.NET Core)
│   └── Secop.Worker/           # BackgroundService workers
└── tests/
    ├── Secop.Domain.Tests/
    ├── Secop.Application.Tests/
    └── Secop.Infrastructure.Tests/
```

## Commands

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test tests/Secop.Domain.Tests/

# Run a single test by name
dotnet test --filter "FullyQualifiedName~CalcularPuntaje"

# Apply EF Core migrations
dotnet ef database update --project src/Secop.Infrastructure --startup-project src/Secop.Worker

# Create a new migration
dotnet ef migrations add <NombreMigracion> --project src/Secop.Infrastructure --startup-project src/Secop.Worker

# Run the worker (main process)
dotnet run --project src/Secop.Worker

# Run the API
dotnet run --project src/Secop.Api
```

## Architecture Rules

1. **Domain has zero external dependencies.** No NuGet packages except the BCL. Business rules (e.g., RUP inhabilitante) live here.

2. **Application depends only on Domain.** Use cases call infrastructure only through interfaces (`IEmbeddingService`, `ISecopApiClient`, `IAlertaService`, etc.). Never instantiate `OpenAiEmbeddingService` or `TelegramBotService` directly in Application.

3. **Infrastructure implements Application interfaces.** `DependencyInjection.cs` in Infrastructure is the only place that wires concrete implementations to interfaces.

4. **MediatR for all use case dispatch.** Commands and Queries are in `Application/UseCases/`. Workers and controllers call `IMediator.Send()`.

## Key Business Rules

- **RUP vencido is automatically disqualifying** — `Puntaje.Inhabilitado()` sets total to 0 regardless of other scores. This rule must stay in Domain.
- **Minimum similarity threshold of 0.65** — processes below this skip full scoring (cost optimization).
- **Scoring components (0–100 total):** Similarity 35 + Requirements 25 + Time 20 + Competition 12 + Entity history 8.
- **Labels:** ≥70 → Proponer, ≥40 → Analizar, <40 → Descartar.
- **Embeddings are computed once** — on proveedor registration and on new proceso ingestion. Never recompute per search.

## SECOP II API Gotchas

- Dataset for procesos: `https://www.datos.gov.co/resource/p6dx-8zbt.json`
- The field name is `fecha_de_ultima_publicaci` — truncated without accent, this is a known bug in the dataset. Use it exactly as-is.
- All requests require header `X-App-Token: {SECOP_APP_TOKEN}`.
- Uses Socrata SODA query syntax (`$where`, `$limit`, `$order`, `$offset`).

## Environment Variables

Required in `.env` (never commit this file):

```
TELEGRAM_BOT_TOKEN=
SUPABASE_CONNECTION_STRING=   # include Pooling=true for free tier connection limits
SUPABASE_URL=
SUPABASE_KEY=
OPENAI_API_KEY=
ANTHROPIC_API_KEY=
SECOP_APP_TOKEN=
```

## Telegram Bot

- **Development:** use long polling. **Production (Railway/Render):** use webhook with the public server URL.
- Commands: `/start`, `/procesos`, `/desiertos`, `/alertas`, `/empresa`, `/proceso`, `/estado`, `/ayuda`.
- Inline keyboard buttons (`proponer_`, `analizar_`, `descartar_` callback prefixes) write to the `decisiones` table for future ML feedback.

## Database

- pgvector extension required. Embeddings stored as `vector(1536)`.
- IVFFlat index on `procesos.embedding` with `lists = 100` for efficient cosine similarity search.
- Supabase free tier: always use `Pooling=true` in the connection string.

## Implementation Phases (current status: not started)

1. Domain + EF Core + SECOP API client baseline
2. Embedding engine + scoring
3. Background worker (hourly sync, daily RUP alerts)
4. Telegram bot (alerts + inline actions)
5. Claude PDF pliego analysis
6. Production deployment (Railway/Render + Supabase + webhook)
