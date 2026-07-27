## Plan: Guest customer provisioning — resolve `Order.CustomerId` for a guest via `UserProfile` reuse, unlock `Confirm`

### Scope
- **BC(s)**: AccountProfile (Application layer addition), Presale/Checkout (ACL + API)
- **Governing ADR(s)**: ADR-0030 §3 (`docs/adr/0030/0030-guest-checkout-anonymous-order-placement.md`)
- **Risk**: medium — touches the live order-placement path (`CheckoutController.Confirm`)
- **Behavioral change**: yes — anonymous callers can now complete a full checkout; `ConfirmCheckoutRequest.CustomerId` becomes optional

### Precondition
- Phase 1 must be **PASS** (`01-phase-guest-shopper-identity-validation.md`) — `GetOrCreateShopperId()` must exist and be in use by `CheckoutController` before this phase starts.

### Files to add
- None (extends existing interfaces/classes)

### Files to modify
- `ECommerceApp.Application/AccountProfile/Services/IUserProfileService.cs` (confirm exact path/namespace at implementation time) — add `Task<int> GetOrCreateForGuestAsync(string userId, string firstName, string lastName, bool isCompany, string nip, string companyName, string email, string phoneNumber, CancellationToken ct = default)`
- The corresponding `UserProfileService` implementation — `GetByUserIdAsync` first (existing repo method), return its `UserProfileId.Value` if found; else `UserProfile.Create(...)` + `IUserProfileRepository.AddAsync`, publish `UserProfileCreated` exactly as the existing manual-creation path does (do not skip domain events)
- `ECommerceApp.Application/Presale/Checkout/Contracts/IAccountProfileClient.cs` — add `Task<int> EnsureGuestCustomerAsync(string userId, CheckoutCustomer customer, CancellationToken ct = default)`
- `ECommerceApp.Infrastructure/Presale/Checkout/Adapters/AccountProfileClientAdapter.cs` — implement `EnsureGuestCustomerAsync`, mapping `CheckoutCustomer` fields to the service call
- `ECommerceApp.API/Controllers/Presale/CheckoutController.cs` — `Confirm`: `[Authorize(Policy = ApiPolicies.TrustedApiUser)]` → `[AllowAnonymous]`; branch: authenticated → require `request.CustomerId` (unchanged behavior, `BadRequest` if missing); guest (via `GetOrCreateShopperId()`'s non-authenticated branch) → resolve `customerId` via `IAccountProfileClient.EnsureGuestCustomerAsync`, **ignore any client-supplied `CustomerId`**
- `ConfirmCheckoutRequest` record — `CustomerId` becomes `int?`

### Files NOT to touch
- `ECommerceApp.Domain/Sales/Orders/Order.cs` — `CustomerId` invariant (`> 0`) stays exactly as-is
- `ECommerceApp.Domain/AccountProfile/UserProfile.cs` — no new method in this phase (`ReassignOwner` is Phase 3)
- `CheckoutService`, `IOrderClient`, `OrderService.PlaceOrderFromPresaleAsync` — unchanged; they already accept whatever valid `int customerId` they're given

### Tests required (mandatory — behavioral change = yes)
- Unit: `ECommerceApp.UnitTests/AccountProfile/UserProfileServiceTests.cs` — `GetOrCreateForGuestAsync_NoExistingProfile_CreatesNewProfile`, `GetOrCreateForGuestAsync_ExistingProfileForUserId_ReturnsExistingIdWithoutDuplicating`
- Unit: `ECommerceApp.UnitTests/Presale/Checkout/AccountProfileClientAdapterTests.cs` — `EnsureGuestCustomerAsync` maps fields and delegates correctly
- Integration: `ECommerceApp.IntegrationTests/Presale/Checkout/GuestCheckoutIntegrationTests.cs` (new) —
  - Full anonymous flow: `POST /api/cart` → `POST /api/checkout/initiate` → `POST /api/checkout/confirm` (no `Authorization` header, no `CustomerId` in body) → 200 with `orderId`
  - Assert the resulting `Order.CustomerId` resolves to a real `UserProfile` row
  - Assert **no** `ApplicationUser` row was created as a side effect
  - Resubmitting `Confirm` for the same guest cookie after a failure does not create a second `UserProfile` (idempotency)
  - Authenticated flow regression: existing authenticated `Confirm` test(s) still pass unmodified — `CustomerId` still required and honored exactly as before

### Steps (atomic, ordered)
1. Add `GetOrCreateForGuestAsync` to `IUserProfileService` + implementation.
2. Add `EnsureGuestCustomerAsync` to `IAccountProfileClient` + `AccountProfileClientAdapter` implementation.
3. Build. Fix compile errors before touching the controller.
4. Change `ConfirmCheckoutRequest.CustomerId` to `int?`.
5. Update `CheckoutController.Confirm`: `[AllowAnonymous]`, add the authenticated/guest branch for resolving `customerId`.
6. Build.
7. Write and run unit tests (service + adapter).
8. Write and run integration tests (full guest flow + regression on authenticated flow).

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET (part of UnitTests) — confirm no new cross-BC navigation property was introduced by the ACL addition

### Risks / open questions
- **Risk**: an authenticated request that omits `CustomerId` (now `int?`) could previously fail model binding; now it binds fine but must still be rejected by application logic. → Mitigation: explicit `BadRequest` check for the authenticated branch, covered by a regression test asserting the exact same error behavior as before this phase for a malformed authenticated request.
- **Risk**: `EnsureGuestCustomerAsync` racing itself (e.g. `Initiate` retried concurrently with `Confirm` for the same guest token) could create two `UserProfile` rows despite the `GetByUserIdAsync`-first check (read-then-write race). → **Open question, needs human input**: decide whether to accept this narrow race for v1 (matches the "Accept the race" precedent in ADR-0012 EC-001) or add a unique constraint / retry-on-conflict. Recommend accepting for v1 and documenting, consistent with existing repo precedent, unless the human wants stronger guarantees now.
- **Risk**: `CheckoutCustomer` guest-submitted data is not verified against `ICustomerExistenceChecker` (by design, matching the already-live authenticated path) — a guest could submit any name/address. → Not a new risk introduced by this phase (already true for authenticated `PlaceOrderFromPresaleAsync` per ADR-0012 §13); explicitly out of scope here.

### Rollback plan
- Revert `CheckoutController.cs` (`Confirm` back to `[Authorize(Policy = ApiPolicies.TrustedApiUser)]`, `ConfirmCheckoutRequest.CustomerId` back to non-nullable `int`), revert `IAccountProfileClient`/`AccountProfileClientAdapter`/`IUserProfileService` additions. Any `UserProfile` rows created for guests during a rolled-back deployment window are harmless leftover data (same shape as any other profile) — no migration needed to remove them, though the Phase 4 cleanup job will eventually purge unclaimed ones.
