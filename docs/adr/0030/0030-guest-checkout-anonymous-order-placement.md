# ADR-0030: Guest Checkout — Anonymous Order Placement via Session-Scoped Shopper Identity

## Status
Accepted

## Date
2026-07-26 (original) — revised 2026-08-14

## Revision note (2026-08-14)
The original draft (§1–§8 below, mostly intact) scoped this change to `ECommerceApp.API`. Design
review before implementation started changed four things, reflected throughout this revision:

1. **Scope moves entirely to `ECommerceApp.Web` (the MVC storefront). `ECommerceApp.API` is not
   touched.** Guest checkout is a storefront capability, not a public REST surface. See §2.
2. **No `BaseController` helper.** Guest identity resolution is a dedicated, explicitly-injected
   service (`IShopperIdentityResolver`), not a method inherited by every controller in the project.
   See §1.
3. **§6 ("deferred fallback") is no longer deferred.** It is in scope for v1, redesigned around a
   new shared primitive (`VerificationCode`, §9) and an interim admin-operated substitute for real
   email (§10), because no outbound-email infrastructure exists yet in this codebase.
4. **A new concern not in the original draft: returning to view/pay an order after the guest
   session cookie is gone.** See §11 (order access & recovery).

Session isolation (§12) is promoted from an implicit property of the design to an explicit,
tested requirement.

## Revision note (2026-08-16)

Phase 8 (the session-isolation regression suite this ADR's §12 called for) found, while validating
against the as-shipped code, that the `[AllowAnonymous]` + per-action manual
`_orderAccessClient.HasAccessAsync(...)` check pattern from §11 had let a real gap through
undetected: `PaymentsController`'s POST payment-confirmation action was missed entirely (only its
GET got the anonymous override), so a guest could reach the payment form but never actually submit
it. The same audit also surfaced a pre-existing, ADR-0030-unrelated IDOR — `PaymentService.ConfirmAsync`
never checked payment ownership at all, so any authenticated user could already confirm an
arbitrary `PaymentId`. Both are symptoms of the same structural cause: authorization logic
duplicated ad hoc, per action, instead of centralized in one place.

This revision replaces §11's raw `OrderAccessCookie` + scattered manual checks, and closes the
authenticated-user IDOR in the same stroke, with:

1. **A second, narrow-purpose cookie authentication scheme** (`GuestAccess`, alongside
   `Identity.Application`) that the guest is silently signed into — not a real `ApplicationUser`
   row, so the existing "no `ApplicationUser` is ever created for a guest" invariant (§3, Phase 1/2,
   independently validated) is untouched. Pre-order, nothing changes — `IShopperIdentityResolver`'s
   `PresaleUserId`/`GuestSession` cookie (§1, §1a) remains exactly as implemented; it is scoped to
   "the guest's current in-progress operation" (cart, soft reservation, the `PlaceOrder` form
   itself), and there is no `OrderId` to narrow to yet. The `GuestAccess` sign-in only happens once
   an order exists to scope it to — at `PlaceOrder` POST success, or at successful recovery
   verification (below) — and its claim always names **exactly one `OrderId`**, never the guest's
   whole `UserProfile`/order history, even though the same `PresaleUserId` could in principle place
   more than one order. This preserves §11's original single-order blast-radius limit; only the
   delivery mechanism changes.
2. **One shared, resource-based authorization check** (`OrderAccess` policy name, backing an
   `IAuthorizationHandler`) replacing every `[AllowAnonymous]` in
   `CheckoutController`/`PaymentsController`/`OrdersController`. **The attribute-level guard is bare
   `[Authorize]`, not `[Authorize(Policy = "OrderAccess")]`** — this is a deliberate, non-negotiable
   distinction, not a simplification: a declarative policy attribute is evaluated before the action
   body runs, so it cannot know which `Order`/`Payment` the request is even about. `[Authorize]`
   alone (with both `Identity.Application` and `GuestAccess` listed as accepted schemes) only
   establishes "some recognized principal is present"; each action then fetches its resource and
   calls `IAuthorizationService.AuthorizeAsync(User, resource, "OrderAccess")` explicitly —
   ASP.NET Core's standard resource-based authorization pattern. The handler behind that policy name
   checks ownership the same way regardless of which scheme authenticated the caller: the resource's
   owner id (`Order`/`Payment`'s backing `UserProfile.UserId`) must equal `GetUserId()` (or the
   caller holds a `MaintenanceRole`); if the caller authenticated via `GuestAccess`, the resource's
   `OrderId` must also match the sign-in's `OrderAccessOrderId` claim. Because a `GuestAccess`-signed-
   in guest now always has a `ClaimTypes.NameIdentifier` claim, `GetUserId()` needs no anonymous/
   authenticated branching anywhere it's called — the same handler that correctly gates the guest
   path also closes the pre-existing authenticated-user IDOR, since ownership is checked
   unconditionally instead of only in the actions that happened to remember to add it by hand.
3. **§11's recovery flow becomes one generic, unified order-lookup path** instead of two separate
   flows (the token-bearing `Summary` URL vs. the `/Identity/Account/Login?guestOrder=` recovery
   page), living in `CheckoutController` (Presale/Checkout — where this identity concept already
   lives, not Sales/Orders or Sales/Payments): `GET /Presale/Checkout/Order/{id}`. The order id in
   the URL is **no longer treated as a secret** — the real secret moves entirely to the
   email-verification code:
   - If the caller already holds a valid `GuestAccess` (or `Identity.Application`) claim naming that
     order (checked via the same `AuthorizeAsync` call as point 2), it's shown immediately — no
     added friction for the common case of viewing the order right after checkout.
   - Otherwise, if the order belongs to a real registered account, redirect to
     `/Identity/Account/Login` (unchanged existing behavior for real accounts).
   - Otherwise (order belongs to an unclaimed guest `UserProfile`): `POST
     /Presale/Checkout/Order/{id}/RequestAccess` (body: email) generates a `VerificationCode`
     (`Purpose = GuestOrderAccess`, §9) sent to the email on file (never the one typed — unchanged
     anti-enumeration posture from §6/§11); `POST /Presale/Checkout/Order/{id}/ConfirmAccess` (body:
     code) redeems it and mints a fresh `GuestAccess` sign-in scoped to that one order.
   - **This changes §11's threat model** and makes the rate-limiting prerequisite already named in
     §8 a **hard blocker**, not an aspirational note, on the `RequestAccess` step specifically, since
     an order id is now guessable/enumerable by design rather than an unguessable pre-issued token.
     Concrete policy (fixed-window, `Microsoft.AspNetCore.RateLimiting`): **10 requests per 10
     minutes per client IP**, and **5 requests per 15 minutes per `OrderId`** (both partitions
     active simultaneously — either limit reached returns `429` with a `Retry-After` header). The
     per-`OrderId` partition exists specifically so one order can't be hammered from many IPs/proxies.
4. `OrderAccessToken`/`IOrderAccessClient` (Phase 7) are not deleted — they remain the underlying
   issuance/persistence mechanism, now used to mint the `GuestAccess` claims principal (via
   `SignInAsync`) at the two points above, instead of being re-validated against the database on
   every subsequent request. `OrderAccessToken` has no expiry today (§11 deliberately ties liveness
   to `Payment.Status`/`ExpiresAt`, not a cookie TTL) and no revocation path beyond deleting the row
   — this revision does not add one. The `GuestAccess` cookie's own expiration mirrors this: no
   fixed absolute expiry: **sliding, 30-day idle timeout** (`options.ExpireTimeSpan =
   TimeSpan.FromDays(30)`, `options.SlidingExpiration = true`), consistent with "not a second timer"
   from §11's original design. Freshness against a deleted `OrderAccessToken` row (e.g. an admin
   revokes access) is enforced via `CookieAuthenticationEvents.OnValidatePrincipal` on the
   `GuestAccess` scheme, re-checking the row exists, **at most once per 5 minutes per principal**
   (ASP.NET Core's own `ValidationInterval` pattern) — not on every request, to avoid reintroducing
   a per-request DB hit.

This does **not** touch Phases 1–3's core mechanism (guest identity resolution, cart, order
placement, `UserProfile`/promotion) or Phase 4's cleanup job — it is scoped to §11 (order access &
recovery) and §12 (session isolation / the authorization mechanism), implemented as Phase 9. See
`docs/roadmap/guest-checkout.md` for the phase-to-plan mapping.

## Context

**Problem 1 — No anonymous order placement exists.** `CheckoutController`
(`ECommerceApp.Web/Areas/Presale/Controllers/CheckoutController.cs`) carries a class-level
`[Authorize]`. Its `AddToCart` action is `[AllowAnonymous]` but immediately redirects an
unauthenticated caller to `/Account/Login` (`CheckoutController.cs:150-155`) rather than letting
the add proceed. `Cart` and `PlaceOrder` (GET) have no `[AllowAnonymous]` at all — an anonymous
request never reaches them; ASP.NET's authorization middleware redirects to login first. This is a
deliberate current constraint, not an oversight, but it blocks a common e-commerce requirement:
placing an order without creating an account.

`ECommerceApp.API/Controllers/Presale/{Cart,Checkout}Controller.cs` carry the same
`[Authorize]`/`[Authorize(Policy = ApiPolicies.TrustedApiUser)]` shape. **This ADR does not change
them.** The API is a separate, independently-authenticated surface (for future SPA/mobile
consumers); anonymous checkout through it is an explicit non-goal — see Decision §2.

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
  the existing `[Authorize]`/cookie-auth pipeline keeps working unmodified. This manufactures a
  security surface (a "throwaway account" with no password, requiring careful lockout handling)
  for something that is not actually an authentication concept.

**Problem 4 — reconciling a guest's later account with their past guest orders must not leak
data.** A synchronous "we found previous orders for this e-mail" response at registration time is
a user-enumeration oracle: an attacker can script registration attempts against a list of e-mail
addresses and observe which ones return a hit. Any account-linking flow must not reveal a match
in-band. The same concern applies to a new surface this revision adds: recovering access to a
placed order after the session cookie is gone (§11) must not become a way to enumerate or mass-scan
order data — see §11's threat model.

**Problem 5 — there is no outbound email infrastructure today.** `IEmailSender` (ASP.NET Core
Identity UI's interface, referenced only by `Areas/Identity/Pages/Account/*.cshtml.cs`) has no
custom implementation — it resolves to Identity UI's default no-op sender. Nothing in this
codebase actually delivers an email today. This is planned to change later, but not on this ADR's
timeline. Every flow below that would normally "send an email" instead persists what would have
been sent and exposes it through an admin-only interim view (§10) until real email delivery
exists — designed so swapping in real delivery later requires no redesign, only wiring a real
`IEmailSender`-equivalent where the interim admin view currently reads.

**Key existing capabilities that materially reduce the size of this change** (discovered during
investigation, not assumed up front):

1. `PresaleUserId` (`ECommerceApp.Domain/Presale/Checkout/PresaleUserId.cs`) is a bare
   `TypedId<string>` wrapper. Nothing about `CartLine`, `SoftReservation`, `CartService`, or
   `SoftReservationService` requires this string to originate from a JWT/cookie claim — it is
   populated from `ClaimTypes.NameIdentifier` today purely by caller convention
   (`CheckoutController.cs`: `new PresaleUserId(GetUserId())` at `Cart`, `PlaceOrder` GET,
   `PlaceOrder` POST, `CheckoutStatus`, `CancelCheckout`). The entire cart/soft-reservation flow is
   already decoupled from ASP.NET Identity.
2. `ICheckoutService.PlaceOrderAsync(PresaleUserId userId, int customerId, int currencyId,
   CheckoutCustomer customer, ...)` (the **live** checkout path, called from `CheckoutController`'s
   `PlaceOrder` POST action) builds `OrderCustomer` directly from the caller-supplied
   `CheckoutCustomer` payload and calls `Order.Create(customerId, ...)` **without** calling
   `ICustomerExistenceChecker` or `IOrderCustomerResolver`. The domain model does not need to
   change to accept a guest's `CustomerId`; only how that `CustomerId` is produced needs to change.
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
   add the guest-provisioning method (Decision §3), and it is the pattern this ADR reuses again
   for `VerificationCode` (§9).
5. `Order.CustomerId`'s companion, `Payment`, already has a working, enforced expiry mechanism:
   `OrderService` sets `Payment.ExpiresAt = DateTime.UtcNow.AddDays(3)` at order-placement time
   (`OrderService.cs:91,392`), and `PaymentWindowExpiredJob`
   (`Sales/Payments/Handlers/PaymentWindowExpiredJob.cs`), scheduled by `OrderPlacedHandler` via
   `IDeferredJobScheduler`, automatically expires an unpaid `Payment` at that instant. §11 reuses
   this existing clock instead of inventing a second one.
6. `JobManagementController` (`ECommerceApp.Web/Areas/Jobs/Controllers/`) already gates on
   `[Authorize(Roles = UserPermissions.Roles.Administrator)]` directly — a narrower cut than the
   `ManagingRole`/`MaintenanceRole` groups every other Backoffice controller uses. §10's admin view
   follows this same narrow-role precedent, not the broader groups.

Because of (2) and (3), this ADR requires **no changes to the `Order` aggregate, no nullable
columns** for `Order`/`Payment`, and reuses `UserProfile` exactly as it already behaves. New
storage is limited to: one new generic table for `VerificationCode` (§9) and one new column/table
for the order-access token (§11).

## Decision

### 1. Guest shopper identity — cookie-carried `PresaleUserId`, resolved by a dedicated service

A new `IShopperIdentityResolver` (namespace `ECommerceApp.Web.Areas.Presale.Services` or
equivalent — confirm exact placement at implementation time), **not** a `BaseController` method:

```csharp
public interface IShopperIdentityResolver
{
    PresaleUserId Resolve(HttpContext context);
}

internal sealed class ShopperIdentityResolver : IShopperIdentityResolver
{
    public PresaleUserId Resolve(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            return new PresaleUserId(GetUserId(context));

        var existing = context.Request.Cookies[GuestSession.CookieName];
        if (!string.IsNullOrEmpty(existing))
            return new PresaleUserId(existing);

        var token = GuestSession.NewToken(); // cryptographically random, prefixed (see §1a)
        context.Response.Cookies.Append(GuestSession.CookieName, token, GuestSession.CookieOptions);
        return new PresaleUserId(token);
    }
}
```

Registered `AddScoped<IShopperIdentityResolver, ShopperIdentityResolver>()` and constructor-injected
**only** into `CheckoutController` — no other controller in `ECommerceApp.Web` takes a dependency
on it. This was chosen explicitly over adding a method to `ECommerceApp.Web/Controllers/BaseController.cs`
(inherited today by every Web area controller, including Backoffice, Sales, Inventory — none of
which have any business resolving a shopper identity): a shared base class gives every subclass the
*capability* whether or not it uses it; a narrow, explicitly-injected service gives it only to the
one controller that declared the dependency. There is no existing precedent in this codebase for
narrow, 1-controller logic living on `BaseController` (it currently carries only broadly-used
error-mapping helpers) — the established pattern for feature-specific logic is a dedicated
Application-layer service, which this follows.

#### 1a. Guest session cookie
- Value: cryptographically random ≥128-bit token, prefixed (e.g. `gst_<token>`) so it can never
  collide with an `AspNetUsers.Id` (GUID) by construction — belt-and-suspenders for the
  `IsUnclaimed` check in §5, which otherwise relies on a negative lookup.
- Attributes: `HttpOnly`, `Secure`, `SameSite=Lax`.
- Lifetime: bounded to the checkout window — same order of magnitude as
  `PresaleOptions.SoftReservationTtl` (15 min) plus a short confirmation grace period, **not**
  a long-lived persistent cookie. Losing the cookie loses the *cart*, not any placed order (see §11
  for what happens to an already-placed order).
- Never treated as a credential, never accepted by `[Authorize]` — read exclusively by
  `IShopperIdentityResolver` for `CheckoutController`'s actions.

### 2. Authorization — Web MVC only; `ECommerceApp.API` is explicitly out of scope

`CheckoutController` (`ECommerceApp.Web`) actions change:
- `Cart` (GET), `PlaceOrder` (GET), `PlaceOrder` (POST), `CheckoutStatus`, `CancelCheckout` gain
  `[AllowAnonymous]`; each replaces `new PresaleUserId(GetUserId())` with
  `_shopperIdentityResolver.Resolve(HttpContext)`.
- `AddToCart`'s existing `[AllowAnonymous]` is kept, but the `if (!User.Identity.IsAuthenticated)
  { return RedirectToPage("/Account/Login", ...); }` branch (`CheckoutController.cs:150-155`) is
  **removed** — an anonymous add now resolves a shopper identity and proceeds, it no longer
  redirects to login. This is the one behavioral flip that makes the rest of the flow reachable.

`ECommerceApp.API/Controllers/Presale/{Cart,Checkout}Controller.cs` are **not modified by this
ADR**. They keep `[Authorize]`/`[Authorize(Policy = ApiPolicies.TrustedApiUser)]` exactly as they
are today. Rationale: the API is a distinct, independently-versioned surface intended for future
SPA/mobile/third-party consumers; opening it to anonymous checkout is a separate decision with its
own abuse-surface analysis (rate limiting, quota, API-key-less traffic shaping) that this ADR does
not make. If a future ADR wants API-level guest checkout, it can reuse `IShopperIdentityResolver`'s
design (a parallel implementation reading the same cookie convention) — nothing here precludes it,
but nothing here builds it either.

### 3. Resolving `CustomerId` for a guest order — reuse `UserProfile`, not a new type

`PlaceOrderVm.CustomerId` (`ECommerceApp.Application/Presale/Checkout/ViewModels/PlaceOrderVm.cs`)
becomes `int?`. `CheckoutController.PlaceOrder` (POST) resolves it via the existing Presale →
AccountProfile ACL:

```csharp
// ECommerceApp.Application/Presale/Checkout/Contracts/IAccountProfileClient.cs
public interface IAccountProfileClient
{
    Task<CheckoutProfileVm> GetProfileAsync(string userId, CancellationToken ct = default);

    // New: idempotent per PresaleUserId — returns the same UserProfileId on repeated calls
    // within the same guest session (e.g. PlaceOrder GET revisited, or POST resubmitted).
    Task<int> EnsureGuestCustomerAsync(string userId, CheckoutCustomer customer, CancellationToken ct = default);
}
```

`AccountProfileClientAdapter.EnsureGuestCustomerAsync` delegates to a new
`IUserProfileService.GetOrCreateForGuestAsync(string userId, ...)`, which:

1. Calls `IUserProfileRepository.GetByUserIdAsync(userId)` (already exists). If found, returns its
   `UserProfileId.Value` unchanged.
2. Otherwise calls `UserProfile.Create(userId: guestToken, firstName, lastName, ..., email, phoneNumber)`
   — the **same factory method** used for registered profiles, with the guest session token in the
   `UserId` slot — and persists it via `IUserProfileRepository.AddAsync`. (`UserProfile.Create`
   does not raise a `UserProfileCreated` domain event today — that type exists in
   `Domain/AccountProfile/UserProfileCreated.cs` but is never published anywhere in the current
   codebase. This ADR does not start publishing it; note this explicitly so implementation doesn't
   go looking for an existing call site to mirror.)

The resulting `UserProfileId.Value` is passed as `Order.CustomerId`. For the authenticated flow,
`CustomerId` is still required — `CheckoutController` rejects a missing value with the same
`BadRequest`/model-error behavior as today when the caller is authenticated.

### 4. No stored "is this a guest" flag — it is a derived fact, checked only where needed

```csharp
bool isUnclaimed = await _userManager.FindByIdAsync(profile.UserId) is null;
```

Guest-ness is a statement about the *absence* of a matching `ApplicationUser`, never a stored
property.

### 5. In-place promotion — "create an account" at end of checkout

`UserProfile` gains one new domain method:

```csharp
public void ReassignOwner(string newUserId)
{
    if (string.IsNullOrWhiteSpace(newUserId))
        throw new ArgumentException("UserId is required.", nameof(newUserId));
    UserId = newUserId;
}
```

`IGuestPromotionService.PromoteAsync(int profileId, string requestingUserId, string password)`:
1. Verifies `requestingUserId` (the calling session's own `PresaleUserId`, from
   `IShopperIdentityResolver`) equals `UserProfile.UserId` for `profileId` — **before** anything
   else. Return `NotOwner` (map to 403, not 404 — avoid confirming the id exists) if it fails. Any
   anonymous caller could otherwise promote *any* guest profile by guessing `profileId`.
2. Creates the `ApplicationUser` via `UserManager.CreateAsync`.
3. Calls `UserProfile.ReassignOwner(applicationUser.Id)`, persists via `IUserProfileRepository.UpdateAsync`.

`Order.CustomerId` is untouched — it already points at `UserProfile.Id`, which does not change.
No order is rewritten.

### 6. Guest → registered-account linking (formerly deferred; in scope now)

Scenario: a person guest-checks-out, possibly several times under the same email but different
guest tokens (cookie cleared/expired between visits — see §3's "Same guest checks out multiple
times" edge case, unchanged), then registers a real account later in a **different** session.

- `RegisterModel.OnPostAsync` returns an **identical response regardless of whether a match
  exists** — closes the enumeration channel from Problem 4.
- On success, a background step queries `UserProfile`s by `Email` where `IsUnclaimed` (§4) is
  true. `Email` has no unique constraint, so **all** matches are candidates.
- If any match exists, a `VerificationCode` (§9) is generated with `Purpose = GuestAccountLink`
  and `SubjectKey = email`. Nothing is shown in the registration response or UI. Until real email
  exists, the pending code surfaces only in the admin-only interim view (§10).
- Redeeming the code (link click → enter code, or code entered directly) triggers `ReassignOwner`
  for **every** matched profile — not just one. This is intentionally the opposite scoping rule
  from §11's order-recovery flow: linking an account is a deliberate, one-time "merge my past
  guest activity" action where breadth is the point; recovering access to view one order is a
  frequent, low-stakes action where each code should unlock the least it can.

### 7. Abuse surface is deliberately narrow

No `ApplicationUser` is ever created for browsing/cart/soft-reservation activity — only a
`UserProfile` row, and only once, at order placement. There is no "fake account creation" attack
surface in the Identity sense (no password, no login capability tied to it). The residual risk —
someone submits another person's real email at checkout, causing an unwanted order-confirmation
touch — is bounded further by §11: order-view access is never granted by email alone, only by
already possessing the order's own opaque token (URL) *and* a code sent to the address on file.

## §8. Consequences

### Positive
- Zero changes to `Order`/`Payment` invariants.
- No new domain type or table for guest identity itself — `UserProfile` reused exactly as it
  already behaves.
- No anemic flag anywhere.
- In-place promotion, not migration — one field update, no order rewriting.
- `ECommerceApp.API` untouched — no new abuse surface on the versioned public API.
- `VerificationCode` (§9) is one generic mechanism reused by two features (§6, §11) instead of two
  bespoke token schemes, and is designed so a future passwordless-login feature for real accounts
  could become a third consumer without rework — **not built now**; that is a materially different
  risk class (a code would open a privileged account, not a read-only guest view) and needs its
  own ADR if pursued.

### Negative
- Orphaned `UserProfile` rows accumulate for guests who never complete promotion/linking — needs
  the retention/cleanup job (Phase 4, unchanged by this revision).
- `Email` has no unique constraint, so the linking flow (§6) may reassign several `UserProfile`
  rows at once — by design.
- Guest cart lifetime is cookie-bound (15 min) — same as before this revision.
- The admin-only interim view (§10) is a manual process (an admin relays a link/code by hand)
  until real email delivery exists — accepted as temporary, not hidden as if it were the final
  design.

### Risks and mitigations
- **Guest session token guessing/fixation** — high-entropy random token, `HttpOnly`/`Secure`/
  `SameSite=Lax` (§1a). Logging into a real account mid-session does not reuse this cookie.
- **Bulk fake checkouts** — rate limiting on `PlaceOrder` POST and guest-cookie issuance per
  IP/session (prerequisite — confirm what rate-limiting infrastructure exists before shipping;
  not assumed present today).
- **Unclaimed `UserProfile` rows grow unbounded** — Phase 4 cleanup job (unchanged), guarded by
  "never delete a profile with any `Order`."
- **Order-recovery flow becoming a mass-scan surface** — see §11's threat model; mitigated by
  requiring an unguessable, pre-issued token before any code can even be requested, so there is no
  "enter any order number" entry point to iterate over.
- **Session isolation regression** — see §12; every guest-eligible query is filtered by the
  caller's own resolved `PresaleUserId`/order-access token, never a client-supplied id alone.

## §9. `VerificationCode` — shared primitive for §6 and §11

Owned by a new `Supporting` sub-area (`ECommerceApp.Domain/Supporting/Verification/`,
mirroring `Supporting/TimeManagement`'s existing shape), consumed by `AccountProfile` and
`Presale/Checkout` through their own narrow ACL interfaces — the same cross-BC pattern
`IAccountProfileClient` already establishes; no shared table crosses a `DbContext` boundary
directly, only calls through an adapter.

```csharp
// ECommerceApp.Domain/Supporting/Verification/VerificationCode.cs
public sealed class VerificationCode
{
    public int Id { get; }
    public VerificationPurpose Purpose { get; }   // enum: GuestAccountLink, GuestOrderAccess (extensible)
    public string SubjectKey { get; }              // opaque: email for GuestAccountLink, OrderAccessToken for GuestOrderAccess
    public string Code { get; }                    // cryptographically random, single-use
    public DateTime ExpiresAt { get; }
    public DateTime? ConsumedAt { get; private set; }

    public bool IsValid(DateTime now) => ConsumedAt is null && now < ExpiresAt;
    public void Consume() { /* throws if already consumed/expired */ }
}
```

`SubjectKey` is intentionally opaque to `VerificationCode` itself — it does not know or care
whether it is guarding an email match (§6) or a single order (§11). Each consumer's ACL adapter
is responsible for interpreting its own `SubjectKey` shape and enforcing its own post-verification
scope (§6: all profiles matching the email; §11: exactly the one order the token names — see §11).
`Purpose` exists purely so one code cannot be replayed against the other feature's redemption
endpoint even if `SubjectKey` values ever collided in shape.

Generic enough that a future `IAM`-owned consumer (real passwordless login) could add
`Purpose = AccountLogin` and its own ACL later — **explicitly not built as part of this ADR** (§8).

## §10. Admin-only interim view (substitute for real email)

New `ECommerceApp.Web/Areas/Backoffice/Controllers/GuestVerificationController.cs` (name TBD at
implementation time), `[Authorize(Roles = UserPermissions.Roles.Administrator)]` — **narrower**
than the `ManagingRole` every other Backoffice controller uses (precedent:
`JobManagementController`, Context point 6). Lists pending, unexpired, unconsumed
`VerificationCode`s (both purposes) with the full link an email would have contained, so an admin
can relay it manually. This is explicitly temporary scaffolding: when real email delivery exists,
the generation step gains a real send and this view becomes an operational fallback/audit tool
rather than the primary channel — not thrown away, repurposed.

## §11. Order access & recovery

> **Superseded by the 2026-08-16 revision note above** (Phase 9) — the delivery mechanism (raw
> `OrderAccessCookie` + two separate flows) described below is replaced by the `GuestAccess` scheme
> + one unified order-lookup path. The single-order scoping rule and the "code goes to the email on
> file, never the one typed" rule are unchanged. This section is kept for history/context on what
> Phase 7 originally shipped and why; do not implement against it directly — implement against the
> revision note and Phase 9's plan file.

Three lifecycle states, each with an already-existing or newly-defined clock — no invented TTLs
beyond one:

1. **Cart / pre-order** (`SoftReservation` window, 15 min, unchanged) — losing the guest cookie
   here loses the cart. No order exists yet, nothing to recover.
2. **Order placed, unpaid** — `Payment.ExpiresAt` (existing, `DateTime.UtcNow.AddDays(3)`,
   enforced by `PaymentWindowExpiredJob`, Context point 5) already governs whether the order can
   still be paid. This ADR does not add a second timer for "can I still act on this order" — the
   server checks `Payment.Status`/`Payment.ExpiresAt` directly, not any cookie's own lifetime.
3. **Order placed (paid or not)** — a new **order-access token**, minted silently at `PlaceOrder`
   POST success (no separate action from the guest — they already proved control of the browser by
   completing checkout in it):
   - Cryptographically random, ≥128-bit, one per `Order`/`UserProfile` pair. Stored once, used
     twice: (a) as the value of a cookie set immediately so the confirmation page and any
     same-session return visits just work, and (b) as the opaque path segment in the URL a
     confirmation email would contain (`/Presale/Checkout/Order/{token}` or similar — exact route
     TBD at implementation time). One artifact, not two token schemes.
   - GUID-shaped (or equivalent ≥128-bit random), **not** a hashid/encoded sequential id — this
     codebase's existing convention for this class of secret (§1a's guest cookie, §9's
     `VerificationCode.Code`) is a genuine random token, not a reversible encoding of an integer.
     An encoded id is a weaker security property for no benefit here.

### Recovery when the order-access cookie is lost
Entry point is the **existing** `/Identity/Account/Login` page, not a new parallel page — a
"kontynuuj jako gość" section at the bottom, active only when the URL carries a valid
order-access token (i.e., the guest arrived via the token-bearing link, not by browsing to
`/Identity/Account/Login` directly with nothing in the URL). Flow:

1. Guest lands on `/Identity/Account/Login?guestOrder={token}` (from a past confirmation email, or
   today, from the admin interim view §10).
2. Enters an email in the guest-continuation form. The system does **not** send the code to
   whatever was typed — it sends to the email already on file for the `UserProfile` the token
   resolves to. A mismatch is not distinguished in the response (same anti-enumeration posture as
   §6).
3. A `VerificationCode` is generated, `Purpose = GuestOrderAccess`, `SubjectKey` naming exactly
   that one `OrderId`/`UserProfileId` pair (not the email alone) — redemption unlocks **only** that
   order, never "everything for this email" (deliberately the opposite scoping rule from §6 — see
   §6's rationale).
4. On successful code entry, a fresh order-access cookie is (re)issued for that order and the
   guest is redirected to its summary/payment page.

### Threat model — why this does not become a mass-scan surface
There is no endpoint anywhere that accepts a bare order number/id and offers to email a code for
it. The **only** entry point is a pre-issued, unguessable token already embedded in a URL the
guest received out of band (confirmation email, or today, the admin view). Guessing a valid token
is computationally infeasible at the same entropy class as §1a's guest cookie. Even a leaked or
forwarded URL is insufficient alone — the code step still requires reading the code from the
actual mailbox on file, not the one the visitor types. `PlaceOrder` POST and code-request/redemption
endpoints remain subject to the same per-IP/session rate limiting prerequisite noted in §8.

## §12. Session isolation — explicit requirement, not an implicit property

> **Mechanism updated by the 2026-08-16 revision note above** (Phase 9): the "never a
> client-supplied id used as the sole authority" rule below is now enforced by one shared
> `OrderAccess` authorization policy calling a single `GetUserId()`-based ownership check, rather
> than by each action separately calling `_orderAccessClient.HasAccessAsync(...)`. This is what
> closes the pre-existing authenticated-user IDOR found during Phase 8 validation
> (`PaymentService.ConfirmAsync` had no ownership check at all) — centralizing the check means it
> can no longer be forgotten in one action while present in another.

Every guest-eligible action resolves and filters exclusively by the caller's own identity
(`IShopperIdentityResolver`'s `PresaleUserId` pre-order, the order-access token post-order) — never
by a client-supplied id used as the sole authority, and never by email as an access grant outside
the proven paths in §6/§11. Concretely:
- `CartService`/`SoftReservationService` calls are keyed by the resolved `PresaleUserId` only.
- `EnsureGuestCustomerAsync`/order-summary lookups are keyed by the resolved
  `PresaleUserId`/order-access token only.
- `IGuestPromotionService.PromoteAsync`'s ownership check (§5) is the existing template for this
  rule; it now generalizes to every guest-reachable action, not just promotion.
- The set of Web endpoints reachable without authentication is a **closed, explicit list**
  (`Cart`, `PlaceOrder` GET/POST, `AddToCart`, `CheckoutStatus`, `CancelCheckout`, the order-summary
  view, the order-access recovery flow) — nothing else gains `[AllowAnonymous]` as a side effect of
  this work, including `AccountProfile`/`Identity/Manage` areas, which remain fully
  authentication-gated.

Validated by a regression test (Phase 8) that seeds concurrent decoy sessions (one guest, one
authenticated) and asserts the session under test cannot read or act on either decoy's cart/order
data by any means, including guessing/substituting the decoys' own ids — generalized so the same
test pattern also protects the existing authenticated-user isolation guarantee, not just the new
guest path.

## Alternatives considered

- **Nullable `Order.CustomerId`.** Rejected — would ripple `null`-handling through every consumer
  of `CustomerId` for a benefit that does not outweigh the cost, given `UserProfile` already
  supports the reuse in §3 at no schema cost.
- **`IsGuest` boolean flag.** Rejected — anemic-model risk; guest-ness is fully derivable (§4).
- **Separate `GuestProfile`/`GuestCustomer` aggregate + table.** Rejected — `UserProfile` already
  tolerates the one-to-many, FK-less `UserId` relationship (ADR-0005 §6) at no extra schema cost;
  `ReassignOwner` achieves promotion in one field update.
- **Guest identity resolution on `BaseController`.** Rejected in this revision (was the original
  §1 design) — no precedent in this codebase for narrow, few-consumer logic on a shared base class;
  a dedicated injected service keeps the capability scoped to the controllers that declare it.
- **Hashid/encoded-sequential-id for the order-access token (§11).** Rejected in favor of a genuine
  random token — reversible/weaker for no benefit, and inconsistent with every other secret token
  already in this design.
- **Order-view access granted by email match alone.** Rejected — this was the original naive
  design for §11 and is exactly the "problem" surfaced during design review: matching by email
  without first possessing the order's own token would let anyone probe order data by guessing
  emails. The two-factor shape (token in URL + code to the address on file) closes this.
- **Minting a guest JWT via a dedicated `/guest-session` endpoint.** Rejected — stretches JWT
  semantics for something that is not an authentication credential; `[Authorize]` should keep
  reliably meaning "this is a real account."
- **Custom `IAuthorizationHandler`/policy for "authenticated or valid guest cookie."** Originally
  rejected (2026-08-14) in favor of `[AllowAnonymous]` + explicit resolution in
  `IShopperIdentityResolver`. **Reversed by the 2026-08-16 revision** — the explicit-per-action
  approach's actual failure mode in production (one action's `[AllowAnonymous]` override forgotten,
  a pre-existing ownership check never added at all, found during Phase 8's independent validation)
  is exactly the class of bug a single centralized policy/handler prevents. The "explicit over
  framework-declarative" preference was sound in the abstract but underestimated the cost of
  duplicating the same check across a growing number of actions across three controllers.
- **Synchronous "we found previous orders for this e-mail, link them?" prompt at registration.**
  Rejected — user-enumeration oracle (Problem 4). Replaced by §6's always-identical response +
  out-of-band code.
- **Building real passwordless login for registered accounts now, reusing `VerificationCode`.**
  Considered (it came up naturally once `VerificationCode` was generalized) and explicitly
  deferred — opening a privileged account by code is a different risk class than a read-only guest
  view (needs its own rate-limiting/phishing-resistance/session-elevation analysis) and deserves
  its own ADR, not a rider on guest checkout.

## References

- Related ADRs:
  - [ADR-0005 — AccountProfile BC: UserProfile Aggregate Design](../0005/0005-accountprofile-bc-userprofile-aggregate-design.md)
  - [ADR-0012 — Presale/Checkout BC Design](../0012/0012-presale-checkout-bc-design.md)
  - [ADR-0013 — Per-BC DbContext Interfaces](../0013/0013-per-bc-dbcontext-interfaces.md)
  - [ADR-0014 — Sales/Orders BC Design](../0014/0014-sales-orders-bc-design.md)
  - [ADR-0025 — API Tiered Access: Trusted Purchase Policy](../0025/0025-api-tiered-access-trusted-purchase-policy.md) (unchanged — `ECommerceApp.API` is out of scope for this ADR, §2)
  - [ADR-0009 — Job Management Access Control](../0009/) (`Administrator`-only precedent used by §10)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../../architecture/bounded-context-map.md)
- Roadmap:
  - [`docs/roadmap/guest-checkout.md`](../../roadmap/guest-checkout.md)

## Implementation Status

| Phase | Scope | Status |
|---|---|---|
| 1–3 | Guest shopper identity, guest customer provisioning, account promotion (§1, §1a, §2, §3, §5) | ✅ Done |
| 4 | Guest-profile cleanup job (Consequences, Negative) | ✅ Done |
| 5 | `VerificationCode` shared primitive (§9) | ✅ Done |
| 6 | Guest-to-registered account linking (§6, §10) | ✅ Done |
| 7 | Order access & recovery (§11) | ✅ Done |
| 8 | Session isolation + full regression suite (§12) | ✅ Done |
| 9 | `GuestAccess` auth scheme + unified `OrderAccess` policy, replacing §11's raw cookie + per-action checks (2026-08-16 revision) | ✅ Done |

Phases 1–9 independently validated PASS (2026-08-14 through 2026-08-17), each in a session
separate from the one that implemented it, per this repo's session-continuity convention. Phase 9
was opened immediately after Phase 8's validation surfaced the gap the 2026-08-16 revision note
describes, and closes it: §11's raw `OrderAccessCookie` mechanism is fully removed (only the
underlying `OrderAccessToken` persistence + backing-token generator remain, now consumed via the
`GuestAccess` scheme), replaced by the `GuestAccess` auth scheme + `OrderAccess` policy this
revision note describes.

**As-implemented note (2026-08-17, independent validation)**: implementation matched the plan with
two corrections applied directly during validation, both now fixed and covered by tests:
- `PaymentsController.Create`/`OrdersController.Details`/`CheckoutController.Summary` originally
  called `Forbid()` unconditionally on an `OrderAccess` policy failure; per this section's design,
  only an authenticated-and-wrong-owner `Identity.Application` user should be `Forbid()`den — anyone
  else (anonymous, or a `GuestAccess` guest scoped to a different order) is now routed to the
  unified order-lookup path instead of a dead-end Access Denied page.
- Two authentication-scheme-sensitive checks (`CheckoutController.PlaceOrder`'s
  `User.Identity.IsAuthenticated` real-vs-guest branch, and `CompleteOrderAccessAsync`'s guard
  around minting the `GuestAccess` sign-in) assumed `IsAuthenticated` implied a real registered
  account — true before this phase, false after, since a `GuestAccess` sign-in also sets it. A guest
  placing a *second* order while already `GuestAccess`-signed-in for a first order hit both: they
  were wrongly forced through the "select an existing CustomerId" branch, and even past that, their
  sign-in never got re-scoped to the new order. Both now key off
  `AuthenticationType == IdentityConstants.ApplicationScheme` specifically.

Rate limiting (10 requests/10min per IP + 5 requests/15min per `OrderId`, both active
simultaneously, on `RequestOrderAccess` only) is implemented in `ECommerceApp.Web/Startup.cs` via
`Microsoft.AspNetCore.RateLimiting` and verified at the HTTP level to actually return `429` +
`Retry-After` once exceeded — the rate-limiting gap flagged against Phases 1–8 is closed.
