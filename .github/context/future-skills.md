# Skills Roadmap

> **Planning & tracking document** — implemented skills and future plans.
> Skills are task-specific, on-demand code generators stored in `.github/skills/<name>/SKILL.md`.

_Last updated: 2026-03-15_

---

## Implemented — `.github/skills/`

| Skill                      | What it generates                                                                                                                                      |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `/create-unit-test`        | xUnit test class with Moq constructor, `Arrange/Act/Assert` scaffold                                                                                   |
| `/create-dbcontext`        | Per-BC `DbContext` + `Constants` + `DbContextFactory` + `Extensions.cs` DI registration                                                                |
| `/create-ef-configuration` | `IEntityTypeConfiguration<T>` with schema, table name, TypedId conversion, owned types                                                                 |
| `/create-di-extension`     | `internal static class Extensions` with `AddXxxServices()` / `AddXxxInfrastructure()`                                                                  |
| `/create-domain-event`     | 3 modes: **event-only** (`IMessage` record), **handler-only** (`IMessageHandler<T>`), or **both** with DI registration. Handler is opt-in, not forced. |
| `/create-integration-test` | `WebApplicationFactory` setup, test class inheriting `BaseTest<T>`, auth mocking                                                                       |
| `/create-http-scenario`    | `.http` file with auth header, base URL variable, CRUD operations for a V2 controller                                                                  |
| `/create-validator`        | `AbstractValidator<T>` for a DTO/command with standard FluentValidation rules                                                                          |

---

## Planned — build when trigger condition is met

| Skill                      | Trigger condition                                                                                        | Notes                                                                                                                                                    |
| -------------------------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `/create-message-contract` | Cross-BC contract pattern stabilizes (3+ BCs using `Contracts/` folders) **or** user explicitly requests | Must also remind to update `docs/architecture/bounded-context-map.md` and relevant ADR                                                                   |
| `/create-dto-viewmodel`    | AutoMapper is removed → manual mapping via extension methods (`ToDto()`, `ToViewModel()`)                | Very mechanical once the pattern is settled. Will need: `public static XxxDto ToDto(this Xxx entity)`                                                    |
| `/create-cqrs-handler`     | First CQRS-based BC is implemented and the handler shape is finalized                                    | Split into `ICommandHandler<T>` + `IQueryHandler<T>`. Don't build before the pattern exists in code.                                                     |
| `/create-service-repo`     | When a new BC explicitly needs classic service+repository pattern                                        | **Critical**: `disable-model-invocation: true` — Copilot must NEVER auto-suggest this. Only invoke explicitly. This is a design decision, not a default. |

---

## Never — design decisions, not templates

These require too much domain judgment. Copilot should assist but not auto-generate.

| Area                                 | Why not a skill                                                                                                         |
| ------------------------------------ | ----------------------------------------------------------------------------------------------------------------------- |
| **Aggregate creation**               | Not everything is an aggregate. Requires domain modeling. User controls this manually.                                  |
| **Repository interface**             | Query shape depends on aggregate design — too varied across BCs.                                                        |
| **Application-layer pattern choice** | Service/repo, CQRS, state machine — varies per BC. The skill is for the specific pattern once chosen, not for choosing. |

---

## Technical notes for future skill authors

- **Event without handler is safe**: `ModuleClient.PublishAsync` uses `GetService()` — returns null and logs warning if no handler is registered. No crash.
- **Single handler per event**: Current `ModuleClient` uses `GetService()` (singular). If multiple BC subscribers are needed for the same event, must change to `GetServices()` + loop first.
- **Skill format**: `.github/skills/<name>/SKILL.md` with optional `scripts/`, `references/`, `assets/` directories. Keep SKILL.md under 500 lines.
- **Progressive loading**: Skills load in 3 levels — metadata (~100 tokens at startup) → instructions (on activation) → resources (on reference). Design for lazy loading.
- **Source spec**: [agentskills.io/specification](https://agentskills.io/specification)
