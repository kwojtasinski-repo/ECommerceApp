## Migration plan

**Prerequisite (verify before starting):** confirm what rate-limiting infrastructure already
exists for public API endpoints. Guest checkout removes the "must be authenticated" gate that
today incidentally throttles abuse of `CartController`/`CheckoutController` — this must be
replaced before `[AllowAnonymous]` ships, not after.

1. **Guest session identity.**
   Add `GuestSession` (static helper or small class) in `ECommerceApp.API/Controllers/Presale/`
   or alongside `BaseController`: `NewToken()` (crypto-random, prefixed), `CookieName`,
   `CookieOptions` (`HttpOnly`, `Secure`, `SameSite=Lax`, expiry ≈ `PresaleOptions.SoftReservationTtl`
   + grace). Add `BaseController.GetOrCreateShopperId()` (§1).

2. **Authorization change.**
   Replace `[Authorize]` / `[Authorize(Policy = ApiPolicies.TrustedApiUser)]` with
   `[AllowAnonymous]` on `CartController`'s and `CheckoutController`'s guest-eligible actions.
   Replace `new PresaleUserId(GetUserId())` call sites with `GetOrCreateShopperId()`. Confirm no
   other endpoint relies on `CartController`/`CheckoutController` requiring authentication as a
   side effect (search for callers assuming `[Authorize]` here).

3. **Guest customer provisioning — domain.**
   Add `IUserProfileService.GetOrCreateForGuestAsync(string userId, string firstName, string
   lastName, bool isCompany, string nip, string companyName, string email, string phoneNumber)`
   to `ECommerceApp.Application/AccountProfile/`. Implementation: `GetByUserIdAsync` first: if
   found, return `UserProfileId.Value`; else `UserProfile.Create(...)` + `AddAsync`.

4. **Guest customer provisioning — ACL.**
   Add `Task<int> EnsureGuestCustomerAsync(string userId, CheckoutCustomer customer,
   CancellationToken ct = default)` to `IAccountProfileClient`
   (`Application/Presale/Checkout/Contracts/`). Implement in `AccountProfileClientAdapter`,
   delegating to step 3's service, mapping `CheckoutCustomer` fields.

5. **Wire into checkout confirmation.**
   Make `ConfirmCheckoutRequest.CustomerId` an `int?`. In `CheckoutController.Confirm`: if the
   caller is authenticated, require `request.CustomerId` (unchanged behavior — return
   `BadRequest` if missing). If the caller is a guest (per `GetOrCreateShopperId()` branch), call
   `IAccountProfileClient.EnsureGuestCustomerAsync(userId.Value, request.Customer, ct)` to obtain
   `customerId`, ignoring any client-supplied value. Pass the resolved `customerId` into
   `_checkout.PlaceOrderAsync` exactly as today.

6. **Promotion domain method.**
   Add `UserProfile.ReassignOwner(string newUserId)` (§5) to
   `ECommerceApp.Domain/AccountProfile/UserProfile.cs`.

7. **Promotion service + endpoint.**
   Add `IGuestPromotionService.PromoteAsync(int profileId, string password)` — creates the
   `ApplicationUser` via `UserManager.CreateAsync`, then `UserProfile.ReassignOwner` +
   `IUserProfileRepository.UpdateAsync`. Expose via a checkout-confirmation-screen action (e.g.
   `POST /api/checkout/create-account`), scoped to the same guest session (verify the caller's
   `GetOrCreateShopperId()` token matches the profile being promoted — a guest may only promote
   their own session's profile).

8. **Cleanup job.**
   Add a scheduled task (TimeManagement BC, same registration pattern as
   `SoftReservationExpiredJob`) that purges `UserProfile` rows where `IsUnclaimed` is true, there
   is no associated `Order`, and `CreatedAt` exceeds the agreed retention threshold. Requires
   `UserProfile` to expose a `CreatedAt` timestamp if it does not already, and a repository query
   to find orders by `CustomerId` (or confirm one already exists via the Sales/Orders ACL).

9. **Deferred — account-linking flow (§6, not required for v1 launch, tracked separately).**
   On successful registration, enqueue a background check for unclaimed `UserProfile`s matching
   the new account's email. If found, send a signed single-use linking token via email. Add an
   endpoint (e.g. `GET /api/account/link-guest-orders?token=...`) that validates the token and
   calls `ReassignOwner` for every matched profile. The registration HTTP response must not
   change based on whether a match was found.

10. **Tests.**
    - Unit: `GetOrCreateShopperId()` (authenticated vs. guest vs. returning-guest-with-cookie);
      `GetOrCreateForGuestAsync` idempotency; `ReassignOwner` validation; `EnsureGuestCustomerAsync`
      idempotency.
    - Integration: full guest checkout (`Initiate` → `Confirm`) with no `Authorization` header,
      asserting an `Order` is created with a valid, resolvable `CustomerId` and no
      `ApplicationUser` is created. Promotion flow: guest checkout → `create-account` → verify
      same `UserProfileId`, same `Order.CustomerId`, new `ApplicationUser` can now log in.
    - Security: registration response byte-for-byte identical for an email with and without a
      matching unclaimed profile (regression test against the enumeration risk in Problem 4).
