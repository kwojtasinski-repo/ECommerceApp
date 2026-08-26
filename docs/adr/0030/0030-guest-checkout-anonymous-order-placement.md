# ADR-0030: Guest Checkout - Anonymous Order Placement

## Status
Accepted; closed initiative. Phases 1-9 are implemented and independently validated.

## Date
2026-07-26 (original) - revised 2026-08-16

## Context

The MVC storefront required an authenticated Identity user before a shopper could complete checkout. The `Order` aggregate requires a positive `CustomerId`, while the existing `UserProfile.UserId` relationship supports opaque string ownership without a cross-schema Identity foreign key. The decision is how to support an anonymous storefront session without weakening order invariants, creating throwaway accounts, or leaking another shopper's data.

This initiative is deliberately limited to `ECommerceApp.Web`. `ECommerceApp.API` remains a separate authenticated surface and is out of scope. There is no outbound email implementation, so account linking and order recovery use persisted verification codes surfaced through an administrator-only interim view until real delivery is wired.

## Architectural Decisions

### 1. Session-scoped guest identity

Guest identity is a cryptographically random, prefixed cookie consumed by the dedicated `IShopperIdentityResolver` used by `CheckoutController`. It is represented in checkout operations as `PresaleUserId`. The cookie is `HttpOnly`, `Secure`, `SameSite=Lax`, and bounded to the checkout window. It is not a JWT, an Identity credential, or a general controller capability.

### 2. Preserve the existing order model

Guest checkout provisions a `UserProfile` using the existing `UserProfile.Create` factory and passes that profile's id as `Order.CustomerId`. `Order.CustomerId` remains a required positive `int`; no nullable order or payment columns and no guest-only aggregate are introduced.

Guest-ness is derived from whether `UserProfile.UserId` resolves to an Identity user. No `IsGuest` flag is stored. Promotion creates the real `ApplicationUser` and calls `UserProfile.ReassignOwner`; the profile id and `Order.CustomerId` remain unchanged.

### 3. Reuse narrow ACLs and a generic verification primitive

Presale uses `IAccountProfileClient.EnsureGuestCustomerAsync` to provision the profile through the AccountProfile boundary. `VerificationCode`, owned by Supporting/Verification, is a generic, single-use primitive with a purpose and opaque subject key. `GuestAccountLink` reassigns all matching unclaimed profiles after a later registration; `GuestOrderAccess` unlocks exactly one order. The two flows intentionally have different breadth. Registration responses remain identical whether a match exists, preventing an email-enumeration oracle.

### 4. Unified order access for one order

Placed orders receive an opaque, random `OrderAccessToken`. A `GuestAccess` authentication scheme is signed in for one order only. The class-level default policy recognizes either the registered Identity scheme or `GuestAccess`; resource ownership is then checked explicitly through the `OrderAccess` policy and its authorization handler.

The unified lookup flow is `GET /Presale/Checkout/Order/{id}`, followed by the rate-limited `RequestOrderAccess` and `ConfirmOrderAccess` actions when recovery is needed. The order id is not treated as the secret. The verification code sent to the address on file is the recovery secret, and a recovered guest session can access only the one order named by its `GuestAccess` claim.

The `GuestAccess` cookie has a sliding 30-day idle timeout. Its backing `OrderAccessToken` is revalidated at most once per five minutes, so revocation by deleting the backing row does not require a database lookup on every request.

### 5. Explicit Web surface and abuse controls

Only the intended checkout actions are anonymous. Cart and checkout operations are keyed by the resolved `PresaleUserId`; order and payment operations use the resource-based `OrderAccess` authorization check. Registered users retain access to their own orders, and maintenance roles retain their existing operational access.

`RequestOrderAccess` uses simultaneous fixed-window limits of 10 requests per 10 minutes per client IP and 5 requests per 15 minutes per `OrderId`, returning `429` with `Retry-After` when exceeded. The API surface is unchanged.

## Consequences

Positive consequences:

- Anonymous checkout does not alter `Order` or `Payment` invariants.
- No throwaway `ApplicationUser` is created for browsing, cart, or checkout activity.
- Promotion is an in-place ownership change with no order rewrite.
- Account linking and order recovery share one verification primitive while retaining different scopes.
- Centralized resource authorization prevents guest-only and authenticated-user ownership checks from drifting apart.

Costs and residual risks:

- Unclaimed profiles can accumulate and require the scheduled cleanup job, which must never delete a profile referenced by an order.
- Duplicate email values are allowed; account linking consequently has deliberate multi-profile breadth.
- Guest carts remain bounded by the session cookie and checkout reservation window.
- Until outbound email exists, administrators must relay pending verification links or codes.
- Anonymous order lookup requires ongoing rate-limit configuration and monitoring.

## Alternatives Considered

- Nullable `Order.CustomerId`: rejected because it would spread null handling through every order consumer for no benefit; `UserProfile` already supplies a stable positive id.
- An `IsGuest` flag: rejected because guest-ness is derived and a flag would scatter behavioral branching through the model.
- A separate guest profile aggregate: rejected because the existing `UserProfile` relationship already supports opaque session ownership and in-place promotion.
- Guest identity on `BaseController`: rejected because it would grant a narrow capability to every controller; the dedicated resolver keeps ownership explicit.
- Guest JWT or passwordless login: rejected or deferred because guest order viewing is not a general authentication credential and privileged account login needs a separate security decision.
- Email-only order access or encoded sequential ids: rejected because either permits enumeration or provides weaker secrecy than a genuine random token plus verification code.

## References and Links

- [ADR-0005 - AccountProfile UserProfile aggregate design](../0005/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0012 - Presale/Checkout BC design](../0012/0012-presale-checkout-bc-design.md)
- [ADR-0013 - Per-BC DbContext interfaces](../0013/0013-per-bc-dbcontext-interfaces.md)
- [ADR-0014 - Sales/Orders BC design](../0014/0014-sales-orders-bc-design.md)
- [ADR-0025 - API tiered access](../0025/0025-api-tiered-access-trusted-purchase-policy.md)
- [Bounded context map](../../architecture/bounded-context-map.md)
- [Guest checkout roadmap](../../roadmap/guest-checkout.md)
- [Conformance checklist](checklist.md)
- [Migration plan](migration-plan.md)
- [Verified implementation details](example-implementation/guest-checkout-implementation.md)
