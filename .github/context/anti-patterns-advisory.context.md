# Anti-Patterns — Advisory (NON-BLOCKING)

> Loaded by: `@code-reviewer` on full reviews. These are suggestions, not blockers.
> For BLOCKS MERGE violations, see `anti-patterns-critical.context.md`.

_Last updated: 2026-06-03_

---

## P2 — Should Fix in This PR

- Missing explicit access modifiers on types or members (classes, properties, methods)
- Missing `CancellationToken` forwarding in async methods that are not controller actions
- Missing `.AsNoTracking()` on read-only EF queries in repositories
- `AddAsync()` used instead of `Add()` for entity insertion (SQL Server does not need async value generation)
- Magic strings or magic numbers instead of named constants
- Service method longer than ~50 lines — suggest extraction
- `var` used for non-obvious types (complex return types, tuples) — prefer explicit type declaration
- Missing `private` or `protected` on fields that should not be public
- `AutoMapper` used in a new BC that follows the static `ToDto()` / `ToViewModel()` extension method pattern (ADR-0008 migration path)

## P3 — Optional

- Missing XML comments on public interfaces or abstract members
- Repeated mock setups across test methods not extracted to a helper or setup method
- Inline null checks where `ArgumentNullException.ThrowIfNull()` would be cleaner (C# 10+)
- Missing structured logging placeholders — string interpolation in log messages instead of `{Named}` placeholders
- `string.Format()` used instead of string interpolation (C# 6+)
- Test method naming inconsistency in the same class (mixing `given_when_should` and `Method_Scenario_Expected` in the same file)
