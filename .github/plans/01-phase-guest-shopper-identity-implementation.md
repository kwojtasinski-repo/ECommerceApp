## Plan: Guest shopper identity — session-scoped `PresaleUserId` for anonymous cart + checkout initiation

### Scope
- **BC(s)**: Presale/Checkout (API surface only — no Domain/Application changes)
- **Governing ADR(s)**: ADR-0030 §1–2 (`docs/adr/0030/0030-guest-checkout-anonymous-order-placement.md`)
- **Risk**: low
- **Behavioral change**: yes — `CartController` and `CheckoutController.Initiate` become reachable without a JWT

### Files to add
- `ECommerceApp.API/Controllers/Presale/GuestSession.cs` — static helper: `NewToken()` (crypto-random, prefixed `gst_`), `CookieName`, `CookieOptions` (HttpOnly, Secure, SameSite=Lax, expiry ≈ `PresaleOptions.SoftReservationTtl` + grace)

### Files to modify
- `ECommerceApp.API/Controllers/BaseController.cs` — add `protected PresaleUserId GetOrCreateShopperId()`: authenticated → existing `GetUserId()` path; else read guest cookie or mint one via `GuestSession.NewToken()` and `Response.Cookies.Append(...)`
- `ECommerceApp.API/Controllers/Presale/CartController.cs` — remove class-level `[Authorize]` / action-level `[Authorize(Policy = ApiPolicies.TrustedApiUser)]`, add `[AllowAnonymous]`; replace `new PresaleUserId(GetUserId())` with `GetOrCreateShopperId()`
- `ECommerceApp.API/Controllers/Presale/CheckoutController.cs` — same change, **`Initiate` action only**. `Confirm` stays `[Authorize(Policy = ApiPolicies.TrustedApiUser)]` — guest support for `Confirm` is Phase 2 (needs `CustomerId` resolution, not yet built)

### Files NOT to touch (verify untouched after this phase)
- Anything under `ECommerceApp.Domain/Presale/Checkout/` — `PresaleUserId`, `CartLine`, `SoftReservation` need zero changes per ADR-0030 Context point 1
- `ECommerceApp.Application/Presale/Checkout/Services/CartService.cs`, `SoftReservationService.cs`, `CheckoutService.cs`
- `CheckoutController.Confirm` (Phase 2 territory)

### Tests required (mandatory — behavioral change = yes)
- Unit: `ECommerceApp.UnitTests/Api/BaseControllerTests.cs` (new or extended) — `GetOrCreateShopperId_Authenticated_ReturnsClaimUserId`, `GetOrCreateShopperId_NoCookieNoAuth_MintsNewCookieAndReturnsToken`, `GetOrCreateShopperId_ExistingGuestCookie_ReturnsSameToken`
- Integration: `ECommerceApp.IntegrationTests/Presale/Checkout/GuestCartIntegrationTests.cs` (new) — anonymous `POST /api/cart` (add item) succeeds and sets a `Set-Cookie` response header; a second anonymous request reusing that cookie sees the same cart; anonymous `POST /api/checkout/initiate` succeeds and creates `SoftReservation`s scoped to the guest token; anonymous `POST /api/checkout/confirm` still returns 401 (Phase 2 not yet built)

### Steps (atomic, ordered)
1. Add `GuestSession` static helper (token generation + cookie options).
2. Add `BaseController.GetOrCreateShopperId()`.
3. Update `CartController`: `[AllowAnonymous]`, swap identity resolution call sites.
4. Update `CheckoutController.Initiate` only: `[AllowAnonymous]`, swap identity resolution call site. Leave `Confirm` untouched.
5. Build. Fix compile errors.
6. Write and run unit tests for `GetOrCreateShopperId()`.
7. Write and run integration tests for anonymous cart + initiate.
8. Manually verify (or via test) that an authenticated request's behavior is byte-for-byte unchanged — no regression for logged-in users.

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET (part of UnitTests) — confirm no new cross-BC navigation properties introduced

### Risks / open questions
- **Risk**: removing `[Authorize]` from `CartController` might have an undocumented caller relying on it always requiring a JWT (e.g. an admin tool). → Mitigation: grep all consumers of `/api/cart/*` before merging (frontend + any Postman/HttpScenarios files) as part of Step 3.
- **Risk**: no rate limiting exists yet on `POST /api/cart` / `POST /api/checkout/initiate`, and `[AllowAnonymous]` removes the incidental throttling effect of requiring auth. → **Open question, needs human input**: confirm what rate-limiting infrastructure already exists (ADR-0030 migration-plan prerequisite) before this phase ships to production; acceptable to land behind a feature flag / in a lower environment first if no rate limiting exists yet.
- **Risk**: guest cookie `SameSite=Lax` may not survive certain cross-origin SPA setups (if the frontend is on a different origin/port in dev). → Mitigation: verify in the actual dev SPA setup during integration testing; adjust `SameSite` only if proven necessary, never loosen to `None` without `Secure`.

### Rollback plan
- Revert `CartController.cs`, `CheckoutController.cs`, `BaseController.cs` to prior state (re-add `[Authorize]`/`TrustedApiUser`), delete `GuestSession.cs`. No DB/schema changes in this phase, so rollback is a pure code revert with no data migration concerns.
