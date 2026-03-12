# Roadmap: Identity/IAM BC — Atomic Switch

> ADR: [ADR-0019](../adr/0019-identity-iam-bc-design.md) — Identity/IAM BC Design and Atomic Switch Plan
> Status: 🟡 In progress — Domain ✅ Application ✅ Infrastructure ✅ `InitIamSchema` migration ✅
> **Coordinate with**: Sales/Orders atomic switch (`orders-atomic-switch.md`) — step 3 below

---

## What is already done

| Layer | Status |
|---|---|
| Domain — `ApplicationUser` in `Domain/Identity/IAM/` | ✅ Done |
| Application — `IAuthenticationService`, `AuthenticationService`, `IUserManagementService`, `UserManagementService`, `IJwtManager`, DTOs, ViewModels, DI | ✅ Done |
| Infrastructure — `IamDbContext` (schema `iam`), `JwtManager`, `SignInManagerInternal`, `UserManagerInternal`, `UserContext`, `IamFeatureOptions` (`UseIamStore`), Google OAuth wiring | ✅ Done |
| `InitIamSchema` migration — `Infrastructure/Identity/IAM/Migrations/20260222222309_InitIamSchema.cs` | ✅ Done — **pending production sign-off** |
| Unit tests — `UserManagementServiceTests`, `AuthenticationServiceTests` | ✅ Done |

---

## Gate condition

`InitIamSchema` migration must be approved and applied per [migration policy](../../.github/instructions/migration-policy.md)
before the flag is flipped. The migration is already generated — approval is the only blocker.

---

## Pending steps

### Step 1 — `InitIamSchema` migration approval

| Action |
|---|
| Submit `Infrastructure/Identity/IAM/Migrations/20260222222309_InitIamSchema.cs` for production sign-off |
| Confirm `iam.*` schema tables are created in the target environment |

### Step 2 — Integration tests

| File | Coverage |
|---|---|
| `IntegrationTests/Identity/IAM/AuthenticationServiceTests.cs` | Sign-in happy path; wrong password → `SignInFailed`; JWT round-trip (issue → validate → claims correct) |
| `IntegrationTests/Identity/IAM/UserManagementServiceTests.cs` | Create user; get user; update user; delete user; assign roles; `GetRolesAsync` |
| All existing integration tests | Must still pass — especially `LoginControllerTests` and `UserServiceTests` |

### Step 3 — Migrate `LoginController`

| File | Action |
|---|---|
| `Web/Areas/Identity/Pages/Account/Login.cshtml.cs` | Replace injection of legacy `IAuthenticationService` (`Application.Services.Authentication`) with `Application.Identity.IAM.Services.IAuthenticationService` |
| `API/Controllers/LoginController.cs` | Same replacement — use new `IAuthenticationService.SignInAsync` returning `SignInResponseDto` with JWT |

> Note: `Web/Areas/Identity/Pages/Account/Login.cshtml.cs` uses Razor Pages — verify `OnPostAsync` still passes model state correctly after service swap.

### Step 4 — Migrate `UserManagementController`

| File | Action |
|---|---|
| `Web/Controllers/UserManagementController.cs` | Replace injection of `IUserService` (`Application.Services.Users`) with `IUserManagementService` (`Application.Identity.IAM.Services`) |
| Verify all action methods | `Index` → `GetUsersAsync`; `AddUser` → `AddUserAsync`; `EditUser` → `UpdateUserAsync`; `DeleteUser` → `DeleteUserAsync`; `GetUserRoles` / `AddRoles` → `GetRolesAsync` / `AddRolesToUserAsync` |

### Step 5 — Flip `UseIamStore: true`

| File | Action |
|---|---|
| `ECommerceApp.Web/appsettings.json` | Set `"UseIamStore": true` |
| `ECommerceApp.Web/appsettings.Development.json` | Set `"UseIamStore": true` |
| `ECommerceApp.API/appsettings.json` (if exists) | Set `"UseIamStore": true` |
| `ECommerceApp.API/appsettings.Development.json` | Set `"UseIamStore": true` |

### Step 6 — Remove `Order.User` navigation property

> Coordinate with Orders atomic switch — this step is simplest to fold into the same PR.

| File | Action |
|---|---|
| `Domain/Model/Order.cs` | Replace `ApplicationUser User { get; set; }` with `string UserId { get; set; }` (plain string — the legacy model is removed at end of Orders switch anyway) |
| `Infrastructure/Database/Configurations/OrderConfiguration.cs` | Remove `HasOne(o => o.User)` / `WithMany()` EF config line |
| Any EF query loading `Order.User` | Replace with `string UserId` reads — no navigation join needed |

### Step 7 — Remove legacy IAM files

| File | Action |
|---|---|
| `Application/Services/Authentication/IAuthenticationService.cs` | Delete |
| `Application/Services/Authentication/AuthenticationService.cs` | Delete |
| `Application/Services/Users/IUserService.cs` | Delete |
| `Application/Services/Users/UserService.cs` | Delete |
| `Domain/Model/ApplicationUser.cs` | Delete |
| `Application/DependencyInjection.cs` | Remove legacy `IAuthenticationService`, `IUserService` registrations |

### Step 8 — Remove `UseIamStore` feature flag

| File | Action |
|---|---|
| `Infrastructure/Identity/IAM/Auth/IamFeatureOptions.cs` | Delete (or mark obsolete — delete preferred) |
| `Infrastructure/Identity/IAM/Auth/Extensions.cs` | Remove conditional branch on `UseIamStore`; register IAM services unconditionally |
| `appsettings*.json` | Remove `UseIamStore` key |

### Step 9 — Verification

| Action |
|---|
| `dotnet build` — green; no references to `IUserService`, `Application.Services.Authentication.IAuthenticationService`, or `Domain.Model.ApplicationUser` remain |
| `dotnet test` — full test suite green |
| Update `bounded-context-map.md` — move Identity/IAM to Completed BCs |

---

## Coordinate with

- **Sales/Orders atomic switch** (`orders-atomic-switch.md`) — step 6 above (`Order.User` nav prop removal) is simplest to do in the same PR as the Orders switch, since `Domain/Model/Order.cs` is removed there anyway.

---

## Acceptance criteria

- [ ] `InitIamSchema` migration approved and applied
- [ ] `LoginController` (Web Razor Pages + API) injects `Application.Identity.IAM.Services.IAuthenticationService`
- [ ] `UserManagementController` injects `Application.Identity.IAM.Services.IUserManagementService`
- [ ] `UseIamStore` flag set to `true` in all environment config files
- [ ] `Application/Services/Authentication/` directory removed
- [ ] `Application/Services/Users/` directory removed
- [ ] `Domain/Model/ApplicationUser.cs` deleted
- [ ] `Domain/Model/Order.cs` has `string UserId` — no `ApplicationUser User` navigation property
- [ ] `UseIamStore` feature flag and conditional DI branches removed from `Infrastructure/Identity/IAM/Auth/Extensions.cs`
- [ ] `dotnet build` green — no remaining references to legacy IAM types
- [ ] Full test suite green
- [ ] `bounded-context-map.md` updated

---

*Last reviewed: 2026-03-12 · ADR: [ADR-0019](../adr/0019-identity-iam-bc-design.md)*
