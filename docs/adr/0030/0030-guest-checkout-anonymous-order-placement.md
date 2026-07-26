# ADR-0030: Guest Checkout — Anonymous Order Placement via Session-Scoped Shopper Identity

## Status
Proposed

## Date
2026-07-26

## Context

**Problem 1 — No anonymous order placement exists.** `CartController` and `CheckoutController`
(`ECommerceApp.API/Controllers/Presale/`) carry a class-level `[Authorize]`, and the mutating
actions (`AddToCart`, `Initiate`, `Confirm`) additionally require
`[Authorize(Policy = ApiPolicies.TrustedApiUser)]`. `BaseController.GetUserId()` reads
`ClaimTypes.NameIdentifier` and throws `ArgumentNullException` if the claim is absent — there is
no fallback for an unauthenticated caller. The Web MVC checkout
(`ECommerceApp.Web/Areas/Presale/Controllers/CheckoutController.cs`) explicitly redirects an
anonymous visitor's `AddToCart` to `/Account/Login`. This is a deliberate current constraint, not
an oversight, but it blocks a common e-commerce requirement: placing an order without creating an
account.

**Problem 2 — `Order.CustomerId` must stay a required, positive `int`.** `Order.Create`
(`ECommerceApp.Domain/Sales/Orders/Order.cs`) throws `DomainException("CustomerId must be
positive.")` for any value `<= 0`. A nullable-`CustomerId` design (common in some guest-checkout
implementations, e.g. treating the customer's email as the sole identifying key) was considered
and explicitly rejected — see Alternatives. This ADR does not touch `Order`'s invariants at all.

**Problem 3 — naive guest-identity designs don't fit this codebase's style.** Two common patterns
were considered and rejected during design discussion:
- A boolean `IsGuest` flag on a shared customer/user entity. This leads to behavior branching
  scattered through application code (`if (customer.IsGuest) ...`) instead of the entity's type
  or its relationships carrying the meaning — an anemic-model smell.
- Creating a real, login-capable `ApplicationUser` (ASP.NET Identity) for every guest, purely so
  the existing `[Authorize]`/JWT pipeline keeps working unmodified. This manufactures a security
  surface (a "throwaway account" with no password, requiring careful lockout handling) for
  something that is not actually an authentication concept.

**Problem 4 — reconciling a guest's later account with their past guest orders must not leak
data.** A synchronous "we found previous orders for this e-mail" response at registration time is
a user-enumeration oracle: an attacker can script registration attempts against a list of e-mail
addresses and observe which ones return a hit. Any account-linking flow must not reveal a match
in-band.

**Key existing capabilities that materially reduce the size of this change** (discovered during
investigation, not assumed up front):

1. `PresaleUserId` (`ECommerceApp.Domain/Presale/Checkout/PresaleUserId.cs`) is a bare
   `TypedId<string>` wrapper. Nothing about `CartLine`, `SoftReservation`, `CartService`, or
   `SoftReservationService` requires this string to originate from a JWT claim — it is populated
   from `ClaimTypes.NameIdentifier` today purely by caller convention
   (`CheckoutController.cs:30,52`: `new PresaleUserId(GetUserId())`). The entire cart/soft-reservation
   flow is already decoupled from ASP.NET Identity.
2. `IOrderService.PlaceOrderFromPresaleAsync` (ADR-0012 §13, the **live** checkout path) builds
   `OrderCustomer` directly from the caller-supplied `CheckoutCustomer` payload and calls
   `Order.Create(dto.CustomerId, ...)` **without** calling `ICustomerExistenceChecker` or
   `IOrderCustomerResolver`. Those verifications only run on the legacy `CartItemIds` path
   (`PlaceOrderAsync`), which co-exists by design (ADR-0012 §13) but is not what the presale
   checkout UI calls. `CheckoutController.Confirm`'s own comment already states: *"No server-side
   customer lookup is performed."* — the domain model does not need to change to accept a guest's
   `CustomerId`; only how that `CustomerId` is produced needs to change.
3. `UserProfile` (`ECommerceApp.Domain/AccountProfile/UserProfile.cs`) already supports a
   **one-to-many** relationship from `UserId` (string) to `UserProfile` rows. ADR-0005 §6
   deliberately removed the unique index on `UserProfileConfiguration.UserId`:
   *"One `ApplicationUser` may own multiple `UserProfile` rows; the unique constraint was
   intentionally omitted to support that scenario."* There is no FK from `UserProfiles.UserId` to
   `AspNetUsers.Id` (per-BC `DbContext`s, ADR-0013 — no cross-schema FK). Registration
   (`RegisterModel.OnPostAsync`) does not create a `UserProfile` at all — profile creation is
   already a separate, manual step today. In other words: **`UserProfile.UserId` is already just
   an opaque string key, not a verified Identity reference.** A guest checkout can reuse this
   exact mechanism by populating `UserId` with a guest session token instead of an
   `AspNetUsers.Id`.
4. `IAccountProfileClient` (`ECommerceApp.Application/Presale/Checkout/Contracts/`) is the
   existing Presale → AccountProfile ACL adapter (implemented by `AccountProfileClientAdapter`),
   today used only to prefill the checkout form (`GetProfileAsync`). It is the natural place to
   add the guest-provisioning method (Decision §3).

Because of (2) and (3), this ADR requires **no changes to the `Order` aggregate, no nullable
columns, and no new tables** — it reuses `UserProfile` exactly as it already behaves, and adds one
new small domain operation (`ReassignOwner`) plus a session-identity resolution step ahead of the
existing checkout call.

## Decision

### 1. Guest shopper identity — cookie-carried `PresaleUserId`, never an `ApplicationUser`

`BaseController` gains `GetOrCreateShopperId()`, used only by `CartController` and
`CheckoutController` in place of the current `new PresaleUserId(GetUserId())`:

```csharp
// ECommerceApp.API/Controllers/BaseController.cs
protected PresaleUserId GetOrCreateShopperId()
{
    if (User.Identity?.IsAuthenticated == true)
        return new PresaleUserId(GetUserId());

    var existing = Request.Cookies[GuestSession.CookieName];
    if (!string.IsNullOrEmpty(existing))
        return new PresaleUserId(existing);

    var token = GuestSession.NewToken(); // cryptographically random, prefixed (see §7)
    Response.Cookies.Append(GuestSession.CookieName, token, GuestSession.CookieOptions);
    return new PresaleUserId(token);
}
```

No new authentication scheme, no guest JWT. `PresaleUserId` remains, as it is today, an opaque
string the domain never interprets — it is a session-scoped shopping identity, not a credential.
Everything downstream of it (`CartService`, `SoftReservationService`, `CheckoutService`) is
**unchanged**, per Context point (1).

### 2. Authorization — `[AllowAnonymous]` replaces `TrustedApiUser` on cart/checkout endpoints

`CartController` and `CheckoutController` actions (`AddToCart`, `Initiate`, `Confirm`, and the
read actions) move from `[Authorize]` / `[Authorize(Policy = ApiPolicies.TrustedApiUser)]` to
`[AllowAnonymous]`. Trust is no longer "does a JWT exist" — it is "does the caller have a valid
shopper identity," resolved explicitly by `GetOrCreateShopperId()` rather than by a declarative
policy. This is intentionally explicit code instead of a custom `IAuthorizationHandler` — see
Alternatives.

`ApiPolicies.TrustedApiUser` is **not removed or weakened** — it continues to gate whatever
higher-trust actions used it before (e.g. any endpoint not part of the guest-eligible checkout
surface). Guest checkout does not need `api:purchase` or role claims because trust here is scoped
per-session (the cookie), not per-account.

### 3. Resolving `CustomerId` for a guest order — reuse `UserProfile`, not a new type

`ConfirmCheckoutRequest.CustomerId` becomes optional. When absent (guest flow), `CheckoutController`
resolves it via the existing Presale → AccountProfile ACL:

```csharp
// ECommerceApp.Application/Presale/Checkout/Contracts/IAccountProfileClient.cs
public interface IAccountProfileClient
{
    Task<CheckoutProfileVm> GetProfileAsync(string userId, CancellationToken ct = default);

    // New: idempotent per PresaleUserId — returns the same UserProfileId on repeated calls
    // within the same guest session (e.g. Initiate retried, or Confirm resubmitted).
    Task<int> EnsureGuestCustomerAsync(string userId, CheckoutCustomer customer, CancellationToken ct = default);
}
```

`AccountProfileClientAdapter.EnsureGuestCustomerAsync` delegates to a new
`IUserProfileService.GetOrCreateForGuestAsync(string userId, ...)`, which:

1. Calls `IUserProfileRepository.GetByUserIdAsync(userId)` (already exists). If found, returns its
   `UserProfileId.Value` unchanged — a guest resubmitting `Confirm`, or calling `Initiate` again,
   does not create duplicate profiles.
2. Otherwise calls `UserProfile.Create(userId: guestToken, firstName, lastName, ..., email, phoneNumber)`
   — the **same factory method** used for registered profiles, with the guest session token in the
   `UserId` slot instead of an `AspNetUsers.Id` — and persists it via `IUserProfileRepository.AddAsync`.

The resulting `UserProfileId.Value` is passed as `Order.CustomerId`, satisfying the existing
`> 0` invariant with zero changes to `Order` or `OrderCustomer`. For the authenticated flow,
nothing changes: the frontend continues to supply `CustomerId` exactly as it does today.

### 4. No stored "is this a guest" flag — it is a derived fact, checked only where needed

Nothing in `UserProfile` or `Order` records guest-ness. Where it matters (e.g. an admin view, or
the linking flow in §6), it is computed on demand:

```csharp
bool isUnclaimed = await _userManager.FindByIdAsync(profile.UserId) is null;
```

This keeps `UserProfile` exactly the type it already is — the "guest" character of a row is a
statement about the *absence* of a matching `ApplicationUser`, not a property of `UserProfile`
itself.

### 5. In-place promotion — "create an account" at end of checkout

`UserProfile` gains one new domain method:

```csharp
// ECommerceApp.Domain/AccountProfile/UserProfile.cs
public void ReassignOwner(string newUserId)
{
    if (string.IsNullOrWhiteSpace(newUserId))
        throw new ArgumentException("UserId is required.", nameof(newUserId));
    UserId = newUserId;
}
```

When a guest opts in ("create an account with these details" checkbox on the order-confirmation
screen), a new `IGuestPromotionService.PromoteAsync(int profileId, string password)`:

1. Creates the `ApplicationUser` via `UserManager.CreateAsync` (email = `UserProfile.Email.Value`).
2. Calls `UserProfile.ReassignOwner(applicationUser.Id)` and persists via
   `IUserProfileRepository.UpdateAsync`.

`Order.CustomerId` is untouched — it already points at `UserProfile.Id`, which does not change.
**No order is rewritten.** This directly answers the "do we rewrite what the guest already did"
question raised in design discussion: no, because the guest's `UserProfile` row *is* the same row
that becomes the registered customer's profile — promotion is a single field update, not a copy.

### 6. Deferred fallback — linking orders when the account is created in a *different* session

Out of scope for v1 (see roadmap), documented here because it shapes §3–§5 so it can be added
without further domain changes. If a person guest-checks-out, closes the browser (loses the
cookie), and registers a real account later with the same e-mail:

- Registration (`RegisterModel.OnPostAsync`) returns an **identical response regardless of
  whether a match exists** — this closes the enumeration channel from Problem 4.
- A handler on successful registration queries `UserProfile`s by `Email` where
  `IsUnclaimed(profile)` (§4) is true. `Email` has no unique constraint (confirmed in
  `UserProfileConfiguration`), so **all** matches are candidates, not just one.
- If any match exists, a **separate email** is sent containing a signed, single-use, expiring
  token. Nothing is shown in the registration response or UI.
- Only clicking that link (proof of mailbox ownership) triggers `ReassignOwner` for each matched
  profile. A scan of the database by e-mail therefore produces no observable signal to the
  attacker — the match is never surfaced synchronously.

### 7. Guest session cookie

- Value: cryptographically random ≥128-bit token, prefixed (e.g. `gst_<token>`) so it can never
  collide with an `AspNetUsers.Id` (GUID) by construction — belt-and-suspenders for the `IsUnclaimed`
  check in §4, which otherwise relies on a negative lookup.
- Attributes: `HttpOnly`, `Secure`, `SameSite=Lax`.
- Lifetime: bounded to the checkout window — same order of magnitude as
  `PresaleOptions.SoftReservationTtl` (15 min) plus a short confirmation grace period, **not**
  a long-lived persistent cookie. Losing the cookie loses the *cart*, not any placed order.
- The cookie is never treated as a credential and never accepted by `[Authorize]` — it is read
  exclusively by `GetOrCreateShopperId()` for the guest-eligible Presale endpoints.

### 8. Abuse surface is deliberately narrow

No `ApplicationUser` is ever created for browsing/cart/soft-reservation activity — only a
`UserProfile` row, and only once, at `Confirm` time. There is no "fake account creation" attack
surface in the Identity sense (no password, no login capability, no session token tied to it).
The residual risk is the same as any public form that triggers an email send: someone can submit
another person's real address at checkout, causing them to receive an unwanted order-confirmation
email. This is mitigated by standard rate limiting on `POST /api/checkout/confirm` (and on guest
cookie minting) per IP/session — a general public-endpoint hardening concern, not something
specific to this feature, and not re-implemented here (see migration-plan prerequisite).

## Consequences

### Positive
- **Zero changes to `Order` invariants.** `CustomerId` stays a required positive `int`; no
  nullable columns, no new discriminators on the aggregate.
- **No new domain type or table.** Guest and registered customers are both plain `UserProfile`
  rows; the only difference is whether `UserId` currently resolves to an `ApplicationUser`, which
  is never stored, only computed.
- **No anemic flag.** There is no `IsGuest` property anywhere; "guest-ness" is derived, and no
  code branches on it except the two places that legitimately need it (§4, §6).
- **In-place promotion, not migration.** Creating an account from a completed guest checkout is a
  one-field update (`UserId`) on the existing `UserProfile` row. Past orders need no rewriting
  because `Order.CustomerId` never changes.
- **Small blast radius.** The cart/soft-reservation pipeline (ADR-0012) is reused unmodified; the
  only new code is shopper-identity resolution (§1), an authorization change (§2), one new ACL
  method (§3), and one new domain method (§5).
- **No new guest-specific authentication surface.** No guest JWT, no second auth scheme — the
  `[Authorize]`/JWT Bearer pipeline continues to mean exactly one thing: a real account.

### Negative
- **Orphaned `UserProfile` rows accumulate** for guests who never complete promotion (§5) and are
  never claimed (§6). Requires a retention/cleanup job (see Risks).
- **`Email` has no unique constraint**, so the linking flow (§6) may match multiple `UserProfile`
  rows for one address over time (e.g. several guest checkouts before ever registering). All
  matches must be reassigned, not just the first.
- **Guest cart lifetime is now cookie-bound.** Clearing cookies mid-session loses the cart (same
  behavior as most e-commerce sites; acceptable, but a UX regression relative to a logged-in
  user's server-persisted cart, which survives across devices).
- `CheckoutCustomer`/`ConfirmCheckoutRequest` gains a conditional-required field
  (`CustomerId` becomes optional, validity depends on `[AllowAnonymous]` vs authenticated caller) —
  slightly weakens the request contract's self-description; needs explicit validation and a clear
  error message when a guest omits required contact fields.

### Risks and mitigations
- **Risk**: guest session token guessing or fixation.
  **Mitigation**: high-entropy random token (§7), `HttpOnly`/`Secure`/`SameSite=Lax`. Logging into
  a real account mid-session does not reuse this cookie — the authenticated flow's identity comes
  from the JWT, independent of the guest cookie, so there is no session-fixation path from guest
  to authenticated.
- **Risk**: bulk fake checkouts spamming order-confirmation e-mails to arbitrary addresses.
  **Mitigation**: rate limiting on `POST /api/checkout/confirm` and guest-cookie issuance per
  IP/session (prerequisite — confirm what rate-limiting infrastructure already exists before
  implementation; not assumed present today).
- **Risk**: unclaimed guest `UserProfile` rows grow unbounded (storage + eventual GDPR/retention
  concern).
  **Mitigation**: a scheduled cleanup job (TimeManagement BC, same pattern as
  `SoftReservationExpiredJob`) purging `UserProfile` rows where `IsUnclaimed` is true, no
  associated `Order`, and `CreatedAt` exceeds a retention threshold (e.g. 90 days) — TBD in
  migration plan. Rows with at least one `Order` are retained regardless of claim status, since
  order records themselves have their own retention requirements.
- **Risk**: the §6 linking e-mail itself becomes a spam vector if triggered on every registration
  regardless of match, since sending is conditional on a match — but the *response* must not be.
  **Mitigation**: the conditional email send happens out-of-band (background handler), the
  registration HTTP response is identical either way, and the confirmation UI wording is
  reviewed to avoid any indirect signal (e.g. no "we've sent additional information" text that
  only appears on a match).

## Alternatives considered

- **Nullable `Order.CustomerId`.** Common in other systems (e.g. general DDD guidance suggests
  nullable `CustomerId` + email as the unique key for guests). Rejected per explicit preference:
  it would ripple `null`-handling through every consumer of `CustomerId` (reporting, admin views,
  the legacy `CustomerExistenceChecker` path) for a benefit (avoiding one `UserProfile` row per
  guest) that does not outweigh the cost, given `UserProfile` already supports the reuse in §3
  at no schema cost.
- **`IsGuest` boolean flag on `UserProfile` (or a shared `User`/`Customer` entity).** Rejected —
  anemic-model risk: behavior would branch on the flag throughout application code instead of
  being expressed through relationships. Guest-ness is fully derivable from whether `UserId`
  resolves to an `ApplicationUser` (§4), so storing it would be redundant, mutable state that can
  drift from the truth.
- **Separate `GuestProfile`/`GuestCustomer` aggregate + table, promoted into `UserProfile` on
  registration.** Considered seriously (this was the initial direction of the design discussion)
  and rejected once it became clear `UserProfile` already tolerates a one-to-many, FK-less
  `UserId` relationship (ADR-0005 §6) and already accepts inline contact data without profile
  verification on the live checkout path (ADR-0012 §13). A second table would duplicate the exact
  same field set (name, address, contact info) and require a cross-table "promote" migration step
  that `ReassignOwner` (§5) achieves in a single field update on one row.
- **Minting a guest JWT via a dedicated `/guest-session` endpoint, reusing the JWT Bearer scheme.**
  Considered during design discussion. Rejected in favor of a plain opaque cookie plus explicit
  `GetOrCreateShopperId()` (§1): stretches JWT semantics (issuer/audience/expiry validation) for
  something that is not an authentication credential, and would mean `[Authorize]` no longer
  reliably means "this is a real account" — a distinction worth preserving for anything
  security-sensitive added later (e.g. order history, saved payment methods).
- **Custom `IAuthorizationHandler`/policy for "authenticated or valid guest cookie."** Rejected in
  favor of `[AllowAnonymous]` + explicit resolution in `GetOrCreateShopperId()` (§2). This
  codebase does not otherwise use custom authorization handlers; an explicit, readable guard in
  one method is more consistent with its existing style of explicit ACL/adapter interfaces over
  framework-declarative mechanisms, and keeps the security-relevant branch visible at the call
  site instead of hidden in policy configuration.
- **Synchronous "we found previous orders for this e-mail, link them?" prompt at registration
  (Magento's "Guest to Customer" pattern).** Rejected — user-enumeration oracle (Problem 4).
  Replaced by the always-identical response + out-of-band emailed link in §6.

## References

- Related ADRs:
  - [ADR-0005 — AccountProfile BC: UserProfile Aggregate Design](../0005/0005-accountprofile-bc-userprofile-aggregate-design.md) (`UserProfile.UserId` one-to-many, no unique index — §6 basis for guest reuse)
  - [ADR-0012 — Presale/Checkout BC Design](../0012/0012-presale-checkout-bc-design.md) (`PresaleUserId`, `SoftReservation`, `CheckoutService`, `IAccountProfileClient`, `PlaceOrderFromPresaleAsync` — all reused unmodified)
  - [ADR-0013 — Per-BC DbContext Interfaces](../0013/0013-per-bc-dbcontext-interfaces.md) (no cross-schema FK — why `UserProfile.UserId` can safely hold a non-Identity token)
  - [ADR-0014 — Sales/Orders BC Design](../0014/0014-sales-orders-bc-design.md) (`Order.CustomerId` invariant, `OrderCustomer`)
  - [ADR-0025 — API Tiered Access: Trusted Purchase Policy](../0025/0025-api-tiered-access-trusted-purchase-policy.md) (`ApiPolicies.TrustedApiUser`, unchanged by this ADR; guest endpoints move to `[AllowAnonymous]` instead of relaxing this policy)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../../architecture/bounded-context-map.md)
- Roadmap:
  - [`docs/roadmap/guest-checkout.md`](../../roadmap/guest-checkout.md)
