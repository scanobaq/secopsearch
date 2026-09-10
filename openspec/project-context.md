# SecopSearch — SDD Project Context

## Purpose

SecopSearch monitors Colombian SECOP II procurement processes, associates them with supplier capabilities, scores viable opportunities, and delivers alerts through Telegram. It has no web frontend beyond REST endpoints used to support the service.

## Observed repository architecture

- Solution: `Secop.Buscador.sln`.
- Clean Architecture dependency direction: `Secop.Domain` (no external dependencies) → `Secop.Application` (use cases, interfaces, DTOs) → `Secop.Infrastructure` (EF Core, external services, persistence, scoring) → `Secop.Api` and `Secop.Worker` (composition/entry points).
- MediatR dispatches commands and queries; Infrastructure registers concrete implementations in `src/Secop.Infrastructure/DependencyInjection.cs`.
- PostgreSQL with pgvector stores process and supplier embeddings. SECOP II is read through Socrata SODA; OpenAI produces embeddings; Telegram carries alerts; Anthropic supports document analysis.

## Runtime and testing

- All project files currently target `net10.0`; the installed SDK is `10.0.111`. This differs from the historical .NET 8 description and should be reconciled deliberately, not changed incidentally.
- Central package versions are managed in `Directory.Packages.props`.
- Tests use xUnit, FluentAssertions, and Moq across Domain, Application, and Infrastructure test projects.
- Standard validation commands: `dotnet build Secop.Buscador.sln` and `dotnet test Secop.Buscador.sln`.

## Engineering rules

1. Keep business invariants in Domain and retain its zero-external-dependency boundary.
2. Application code depends on Infrastructure only through Application interfaces; do not instantiate external services in handlers.
3. Add implementation registration only through Infrastructure composition.
4. Use MediatR for API and Worker use-case dispatch.
5. Apply strict TDD: define expected behavior with focused failing tests before production behavior changes, then run focused and full tests.
6. Treat `REGLAS_VALIDACION_PROCESOS.md` as the AS-IS behavior authority when prose conflicts with executable code.

## Existing documentation

- `CLAUDE.md`: project overview, commands, architecture and operational rules.
- `REGLAS_VALIDACION_PROCESOS.md`: AS-IS SECOP validation, persistence, scoring and alert behavior.
- `Observaciones_SECOP.md`: procurement modality and business interpretation notes.
- `PLAN_IMPLEMENTACION_BUSCADOR_SECOP.md`: historical implementation plan.

## Workspace state at initialization

The workspace contains pre-existing modified and untracked implementation/test/documentation files, including `src/Secop.Api/appsettings.json`. Initialization created only this OpenSpec context and configuration; it did not alter implementation files or create a change proposal. Native status is unresolved, no OpenSpec changes are active, and the next action is `sdd-new`.

## SDD controls

- Execution mode: `auto`.
- Artifact store: `both` (OpenSpec files and Engram persistence).
- Delivery strategy: `ask-on-risk`.
- Review budget: 400 changed lines.
- A future procurement-filtering request was supplied only as session awareness; it remains unproposed and must start through `sdd-new`.
