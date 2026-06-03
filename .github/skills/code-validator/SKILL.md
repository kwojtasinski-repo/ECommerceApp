---
name: code-validator
description: >
  Fast pre-commit validator for ECommerceApp (.NET). Checks BLOCKS MERGE violations only —
  no full review ceremony. Use before committing to catch critical violations early,
  or after changes to a specific file. Lighter and faster than the full @code-reviewer.
argument-hint: "[optional file path or BC name]"
---

# Code Validator

Fast pre-commit check — **BLOCKS MERGE** issues only.
Lighter and faster than `@code-reviewer`. No RAG, no ADR loading — context file only.

---

## Step 1 — Load context

Load `.github/context/anti-patterns-critical.context.md`.
This is the ONLY file you need for this check.

Do NOT load `dotnet.instructions.md`, ADR files, or instruction files.

---

## Step 2 — Identify changed files

Use the `changes` tool to identify changed files, or inspect the specified file if one was provided.

---

## Step 3 — Scan for BLOCKS MERGE violations

For each changed file, check:

**Architecture**
- Business logic in a controller (controllers must be thin — service/handler call only)
- `Infrastructure` namespace referenced from `Application` project
- EF entities or `DbContext` used in `Application`, `Web`, or `API` layer
- Direct cross-BC service-to-service calls without `IMessageBroker`
- `ApplicationUser` navigation property on a domain model (must use `string UserId`)

**Exception handling**
- Raw `try/catch` in a controller without `MapExceptionAsRouteValues()` helper
- `return BadRequest(ex.Message)` in any controller action

**Domain model**
- Public setter on a behavioral aggregate (`Order`, `Payment`, `Refund`, `OrderItem`)
- Raw `int`, `Guid`, or `string` as entity ID in domain models (must use `TypedId<T>`)
- External state mutation — state changes outside a named domain method on the aggregate

**Services & repositories**
- `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on async code
- `IQueryable<T>` returned to the service layer from a repository
- `DbContext` or `IQueryable` exposed outside Infrastructure

**DI & registration**
- Service registered directly in `Startup.cs` or `Program.cs` instead of layer `DependencyInjection.cs`

**Security**
- `HttpContext.User` or `User.Claims` accessed directly in a controller (use `GetUserId()` / `GetUserRole()` from `BaseController`)
- Hardcoded role strings (use `ManagingRole`, `MaintenanceRole` constants)
- Hardcoded credentials, connection strings, or secrets in source files

**Testing**
- `Assert.*` used instead of `FluentAssertions` (unit) or `Shouldly` (integration)
- Manual `IHttpContextAccessor` mocking in integration tests (use `SetHttpContextUserId()` from `BaseTest<T>`)

**Frontend**
- Global JavaScript function (must be registered as a `require.js` module)

---

## Step 4 — Report

If blockers found:

```
❌ [rule short name] — `Path/To/File.cs:L42`
Fix: [one-line fix]
```

If clean:

```
✅ No blockers found — safe to commit.
```

**Never report advisory/P2/P3 warnings.** Those belong in a full `@code-reviewer` session.
Keep the report concise — one line per blocker, file path + line number included.
