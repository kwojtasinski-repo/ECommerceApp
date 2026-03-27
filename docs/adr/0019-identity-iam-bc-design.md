# ADR-0019: Identity/IAM BC — Standalone Design and Atomic Switch Plan

## Status
Accepted

## Date
2026-03-12

## Context

Identity and authentication decisions were captured only as a sub-section of ADR-0002 §8 — a
set of non-negotiable BC autonomy rules, not a standalone design document. That sub-section
establishes that `ApplicationUser` must never appear as a navigation property in any other BC's
domain model, but does not describe the IAM BC's own structure, technology choices, atomic switch
steps, or conformance checklist.

### Current state (two parallel implementations)

**Legacy path** (`UseIamStore: false`):
- `IUserService` / `UserService` — `Application/Services/Users/`
- `IAuthenticationService` / `AuthenticationService` — `Application/Services/Authentication/`
- `ApplicationUser` — `Domain/Model/ApplicationUser.cs` (shared domain model — coupling hotspot)
- `Order.User` navigation property — `Domain/Model/Order.cs` (leaks IAM concept into Orders BC)
- `LoginController` and `UserManagementController` use legacy services

**New IAM BC path** (parallel implementation, not yet switched):
- `ApplicationUser` — `Domain/Identity/IAM/ApplicationUser.cs`
- `IamDbContext` — `Infrastructure/Identity/IAM/IamDbContext.cs` (schema `iam`)
- `IUserManagementService` / `UserManagementService` — `Application/Identity/IAM/Services/`
- `IAuthenticationService` / `AuthenticationService` — `Application/Identity/IAM/Services/`
- `IJwtManager` / `JwtManager` — JWT token generation for API controllers
- Google OAuth configured in `Web/Startup.cs`
- `UseIamStore` feature flag in `appsettings.json` — controls which path is active

The atomic switch (flip `UseIamStore: true`, remove legacy, remove `ApplicationUser` nav props
from `Order`) has been deferred pending a formal standalone ADR and integration tests.

### Coupling hotspots being resolved

| Coupling | Location | Resolution |
|---|---|---|
| `ApplicationUser` in `Domain/Model/` | `Domain/Model/ApplicationUser.cs` | Remove after switch — kept only in `Domain/Identity/IAM/` |
| `Order.User` navigation → `ApplicationUser` | `Domain/Model/Order.cs` | Replace with `string UserId` — no nav prop |
| Legacy `IUserService` / `AuthenticationService` | `Application/Services/` | Remove after switch |

## Decision

We will formally adopt the **Identity/IAM BC** as a standalone bounded context using ASP.NET Core
Identity, with JWT for API authentication and cookie + Google OAuth for Web authentication. The
atomic switch plan below is the accepted migration path.

### § 1 BC classification

| Property | Value |
|---|---|
| Type | Infrastructure BC |
| Layer ownership | `Domain.Identity.IAM`, `Application.Identity.IAM`, `Infrastructure.Identity.IAM` |
| Aggregate | `ApplicationUser` (ASP.NET Core Identity `IdentityUser`) |
| Own DbContext | `IamDbContext` — schema `iam` |
| Cross-BC contract | String `UserId` references only — no navigation properties |

### § 2 Technology choices

| Concern | Technology | Rationale |
|---|---|---|
| User store | ASP.NET Core Identity | Already in use; `IdentityUser` covers all current requirements |
| API authentication | JWT (`IJwtManager`) | Stateless; suitable for API consumers |
| Web authentication | Cookie + Google OAuth | Session-based for MVC; Google OAuth already wired |
| Password hashing | ASP.NET Core Identity default | No custom hashing required |
| Role management | ASP.NET Core Identity roles | `Administrator`, `Manager`, `Service`, `User` — see `UserPermissions.Roles` |

### § 3 Service contracts

```csharp
// Application/Identity/IAM/Services/IAuthenticationService.cs
public interface IAuthenticationService
{
    Task<SignInResponseDto> SignInAsync(SignInDto dto);
    Task SignOutAsync();
}

// Application/Identity/IAM/Services/IUserManagementService.cs
public interface IUserManagementService
{
    Task<UserListVm> GetUsersAsync(int pageSize, int pageNumber);
    Task<UserDetailsVm> GetUserDetailsAsync(string userId);
    Task<string> AddUserAsync(CreateUserVm vm);
    Task UpdateUserAsync(UserDetailsVm vm);
    Task DeleteUserAsync(string userId);
    Task<IList<string>> GetRolesAsync(string userId);
    Task AddRolesToUserAsync(string userId, IList<string> roles);
}
```

### § 4 Feature flag

`UseIamStore` in `appsettings.json` (and `appsettings.Development.json`) governs which DI
registrations are active. When `true`, new IAM services are registered; legacy services are
excluded. The flag is removed after the atomic switch is complete.

### § 5 Atomic switch steps

1. **DB migration approval** — `InitIamSchema` migration for `IamDbContext` (schema `iam`);  
   submit for review per migration policy.
2. **Integration tests** — cover `IAuthenticationService` (sign-in, sign-out, JWT round-trip) and
   `IUserManagementService` (CRUD, role assignment) against the new IAM path.
3. **Migrate `LoginController`** — replace `IAuthenticationService` (legacy) with
   `Application.Identity.IAM.Services.IAuthenticationService`.
4. **Migrate `UserManagementController`** — replace `IUserService` with `IUserManagementService`.
5. **Flip `UseIamStore: true`** in all environment `appsettings` files.
6. **Remove `Order.User` navigation property** — `Domain/Model/Order.cs`: replace `ApplicationUser User` with `string UserId` (plain string reference only).
7. **Remove legacy files**:
   - `Application/Services/Users/IUserService.cs` + `UserService.cs`
   - `Application/Services/Authentication/IAuthenticationService.cs` + `AuthenticationService.cs`
   - `Domain/Model/ApplicationUser.cs`
8. **Remove `UseIamStore` feature flag** and its conditional DI branches.
9. **Build + full test suite green** — confirm no legacy IAM references remain.

> 🔵 Deferred: IAM refresh token — requires a separate ADR for token rotation and revocation strategy.

### § 6 Cross-BC contract rule

After the switch, every BC that needs to reference a user must store only a plain `string UserId`
(the `IdentityUser.Id`). No `ApplicationUser` navigation properties are permitted outside
`Domain/Identity/IAM/`. Resolution of userId to display name or email is done at the application
layer via a dedicated read query against `IamDbContext` (read-only, no cross-BC write coupling).

## Consequences

### Positive
- `ApplicationUser` is contained within `Domain/Identity/IAM/` — IAM concept no longer leaks into Orders or any other BC
- JWT-based API auth is stateless and portable
- Google OAuth reduces friction for Web sign-in
- Legacy IAM code is fully removable after switch — no dead code paths

### Negative
- Requires DB migration approval before switch can execute
- `Order.User` nav prop removal requires a migration in the Orders BC schema if the new Orders BC DbContext uses it (coordinate with ADR-0014 switch)

### Risks & mitigations
- **Nav prop removal breaks existing EF queries**: mitigated by replacing with `string UserId` before migration; compile-time errors surface all call sites
- **Refresh token gap**: mitigated by deferring to a dedicated ADR — current API usage does not yet require token rotation
- **Google OAuth credential rotation**: mitigated by storing credentials in `appsettings` / secrets — not hardcoded

## Alternatives considered

- **Keep legacy IUserService + AuthenticationService indefinitely** — rejected because they couple IAM concepts into the Application layer shared by all BCs and make BC autonomy (ADR-0002 §8) impossible to enforce
- **IdentityServer / Duende** — rejected because an external auth server adds operational complexity that is not justified at current scale; ASP.NET Core Identity covers all requirements
- **Custom JWT without ASP.NET Core Identity** — rejected because Identity already handles password hashing, role management, and claims — no benefit to replacing it

## Migration plan

See § 5 Atomic switch steps above.

Coordinate with:
- **ADR-0014 (Sales/Orders switch)** — `Order.User` nav prop removal must be coordinated with the Orders BC EF schema; do step 6 as part of the Orders atomic switch or before it.
- **ADR-0013 (per-BC DbContext interfaces)** — `IamDbContext` is the authoritative user store; other BCs may query it via a read-only `IUserQueryContext` interface when needed.

## Conformance checklist

- [ ] `ApplicationUser` class exists only under `Domain/Identity/IAM/` — not in `Domain/Model/`
- [ ] No BC outside Identity/IAM has a navigation property typed `ApplicationUser`
- [ ] `Order.cs` (and all other domain models) reference users via `string UserId` only
- [ ] `IamDbContext` uses EF Core schema `"iam"`
- [ ] `LoginController` and `UserManagementController` inject only `Application.Identity.IAM` interfaces
- [ ] Legacy `IUserService`, `AuthenticationService` (Application/Services/) files deleted after switch
- [ ] `UseIamStore` feature flag removed after switch
- [ ] JWT token issued from `IJwtManager` — no hardcoded signing keys
- [ ] Google OAuth client ID / secret loaded from configuration — not hardcoded

## References

- [ADR-0002 §8 — Bounded context autonomy policy](./0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0013 — Per-BC DbContext Interfaces](./0013-per-bc-dbcontext-interfaces.md)
- [ADR-0014 — Sales/Orders BC](./0014-sales-orders-bc-design.md) — `Order.User` nav prop removal coordination
- `ECommerceApp.Infrastructure/Identity/IAM/IamDbContext.cs`
- `ECommerceApp.Application/Identity/IAM/Services/`
- `ECommerceApp.Infrastructure/Identity/IAM/Auth/IamFeatureOptions.cs` — `UseIamStore` flag
