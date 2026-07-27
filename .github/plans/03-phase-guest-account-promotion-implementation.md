## Plan: In-place guest → registered account promotion — `ReassignOwner`, no order rewriting

### Scope
- **BC(s)**: AccountProfile (Domain + Application), Identity/IAM (consumes `UserManager`), Presale/Checkout (API surface)
- **Governing ADR(s)**: ADR-0030 §5 (`docs/adr/0030/0030-guest-checkout-anonymous-order-placement.md`)
- **Risk**: medium — creates a real `ApplicationUser` and mutates an existing `UserProfile` row; must be scoped so a guest can only promote their own session's profile
- **Behavioral change**: yes — new capability, no existing behavior touched

### Precondition
- Phase 2 must be **PASS** — a guest `UserProfile` must exist (created via `Confirm`) before promotion is meaningful.

### Files to add
- `ECommerceApp.Application/AccountProfile/Services/IGuestPromotionService.cs` — `Task<PromotionResult> PromoteAsync(int profileId, string requestingUserId, string password, CancellationToken ct = default)`
- `ECommerceApp.Application/AccountProfile/Services/GuestPromotionService.cs` — `internal sealed`; owns the ownership check (§ below) + `UserManager.CreateAsync` + `ReassignOwner` + `UpdateAsync`
- `ECommerceApp.Application/AccountProfile/Results/PromotionResult.cs` — result type with factory methods (`Success`, `ProfileNotFound`, `NotOwner`, `IdentityCreationFailed(errors)`)

### Files to modify
- `ECommerceApp.Domain/AccountProfile/UserProfile.cs` — add:
  ```csharp
  public void ReassignOwner(string newUserId)
  {
      if (string.IsNullOrWhiteSpace(newUserId))
          throw new ArgumentException("UserId is required.", nameof(newUserId));
      UserId = newUserId;
  }
  ```
- `ECommerceApp.Application/AccountProfile/DependencyInjection.cs` (or equivalent) — register `IGuestPromotionService` → `GuestPromotionService`
- `ECommerceApp.API/Controllers/Presale/CheckoutController.cs` — add `POST api/checkout/create-account`, `[AllowAnonymous]`, body `{ int ProfileId, string Password }`; resolves `requestingUserId` via `GetOrCreateShopperId()` and passes it to `PromoteAsync` for the ownership check

### Files NOT to touch
- `ECommerceApp.Domain/Sales/Orders/Order.cs` — `CustomerId` must remain provably untouched by this phase; the whole point is that promotion never rewrites an order
- `IUserProfileService.GetOrCreateForGuestAsync` (Phase 2) — unrelated, no change needed

### Critical rule — ownership check (do not skip)
`PromoteAsync` **must** verify that `requestingUserId` (the calling guest session's own token, from `GetOrCreateShopperId()`) matches `UserProfile.UserId` for the given `profileId` **before** doing anything else. Without this check, any anonymous caller could promote *any* guest profile by guessing/enumerating `profileId` values. Return `PromotionResult.NotOwner()` (map to 403, not 404 — avoid leaking whether the ID exists, consistent with ADR-0030's general anti-enumeration stance) if the check fails.

### Tests required (mandatory — behavioral change = yes)
- Unit: `ECommerceApp.UnitTests/AccountProfile/UserProfileTests.cs` — `ReassignOwner_EmptyOrWhitespaceUserId_ThrowsArgumentException`, `ReassignOwner_ValidUserId_UpdatesUserId`
- Unit: `ECommerceApp.UnitTests/AccountProfile/GuestPromotionServiceTests.cs` — `PromoteAsync_RequestingUserIdDoesNotMatchProfileOwner_ReturnsNotOwner` (the critical test), `PromoteAsync_ProfileNotFound_ReturnsProfileNotFound`, `PromoteAsync_Valid_CreatesApplicationUserAndReassignsOwner`, `PromoteAsync_IdentityCreationFails_DoesNotReassignOwner` (no partial state — profile stays a guest if account creation fails)
- Integration: `ECommerceApp.IntegrationTests/Presale/Checkout/GuestPromotionIntegrationTests.cs` (new) —
  - Full flow: guest checkout (Phase 2) → `POST /api/checkout/create-account` with the same guest cookie → 200, new `ApplicationUser` exists, can log in with the given password
  - Assert `UserProfile.Id` (and therefore `Order.CustomerId` on any prior order) is **unchanged** before/after promotion
  - Assert a *different* guest session's cookie attempting to promote the first guest's `profileId` gets 403, and the original profile's `UserId` is unchanged (attack scenario for the ownership check)

### Steps (atomic, ordered)
1. Add `UserProfile.ReassignOwner` + unit tests for it in isolation first (pure domain method, no dependencies).
2. Add `PromotionResult`.
3. Add `IGuestPromotionService` + `GuestPromotionService` implementing the ownership check + `UserManager.CreateAsync` + `ReassignOwner` + `UpdateAsync`, in that order, with no partial commit if `CreateAsync` fails.
4. Register in DI.
5. Build.
6. Add `POST api/checkout/create-account` to `CheckoutController`.
7. Build.
8. Unit tests (domain method, then service with mocked `UserManager`/repository).
9. Integration tests (happy path + the cross-session attack scenario — do not skip the attack scenario test).

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET (part of UnitTests)

### Risks / open questions
- **Risk**: `UserManager.CreateAsync` succeeds but the subsequent `ReassignOwner`/`UpdateAsync` fails (e.g. DB hiccup) → orphaned `ApplicationUser` with no linked profile. → Mitigation: wrap in a transaction if the two operations can share one (verify: `ApplicationUser` lives in the Identity schema, `UserProfile` in `profile` schema — per ADR-0013 there is no cross-schema FK, so a distributed-transaction-style guarantee may not be available). **Open question, needs human input**: confirm whether a `TransactionScope` spanning both DbContexts is acceptable here, or whether a compensating cleanup (delete the `ApplicationUser` if `ReassignOwner` fails) is preferred. Recommend the compensating-action approach for consistency with the rest of the codebase's per-BC DbContext isolation.
- **Risk**: password policy / validation for the new account — reuse whatever `UserManager.CreateAsync` already enforces (ASP.NET Identity's configured `PasswordOptions`); do not invent a separate validator.
- **Risk**: what happens if `profileId` belongs to an **already-registered** profile (not a guest) — e.g. stale/replayed request. → `PromoteAsync` should check `IsUnclaimed` (per ADR-0030 §4: does `UserProfile.UserId` currently resolve to an `ApplicationUser`?) and return a distinct result (e.g. `AlreadyRegistered`) rather than silently overwriting an existing account's ownership.

### Rollback plan
- Delete `IGuestPromotionService`/`GuestPromotionService`/`PromotionResult`, remove the `create-account` endpoint, remove `ReassignOwner` from `UserProfile`. No schema change to roll back. Any `ApplicationUser` rows created during a rolled-back deployment window are orphaned but harmless (no login flow exposes them beyond normal Identity login, which requires the password only the guest who created them would know) — flag for manual cleanup if this occurs.
