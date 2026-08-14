# ADR-0030: Guest Checkout — Anonymous Order Placement

**Status**: Proposed
**BC**: Presale/Checkout (primary, `ECommerceApp.Web` only), AccountProfile (extended), Supporting
(new `Verification` sub-area)
**Last amended**: 2026-08-14 (see the ADR's Revision note for the full list of what changed since
the 2026-07-26 original draft)

## What this decision covers
Placing an order without an account, entirely within `ECommerceApp.Web` (the MVC storefront —
`ECommerceApp.API` is explicitly untouched): a session-scoped guest shopper identity (cookie-carried
`PresaleUserId`, resolved by a dedicated `IShopperIdentityResolver`, not a `BaseController` method),
reuse of the existing `UserProfile` aggregate as the guest's resolvable `Order.CustomerId`, in-place
promotion to a real account with no order rewriting, a generic `VerificationCode` primitive shared
by two recovery flows (linking a later-registered account to past guest orders, and viewing/paying
an order after the guest session cookie is lost), and an admin-only Backoffice view standing in for
real email until that infrastructure exists.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0030-guest-checkout-anonymous-order-placement.md | Core design: §1–§12, including the 2026-08-14 revision note | Understanding the guest checkout flow end-to-end |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps, mapped to `.github/plans/01`–`08-phase-*` | Implementation |

## Key rules
- **`ECommerceApp.API` is out of scope.** No guest-checkout code touches
  `Controllers/Presale/{Cart,Checkout}Controller.cs` there — verify this stays true at every phase.
- No `IsGuest` flag anywhere — guest-ness is derived from whether `UserProfile.UserId` resolves
  to an `ApplicationUser`, never stored.
- `Order.CustomerId` stays a required positive `int` — no nullable columns on `Order`/`Payment`.
- Guest checkout never creates an `ApplicationUser` — only a `UserProfile` row, via the existing
  `UserProfile.Create` factory, with a guest session token in the `UserId` slot.
- Promotion to a real account (`UserProfile.ReassignOwner`) is a single field update on the
  existing row — never a copy, never touches `Order.CustomerId`.
- Guest session identity is a cookie, never a JWT/auth credential.
- Account-linking-by-email (§6) is **in scope for v1** (not deferred, unlike the original draft) —
  it never reveals a match synchronously; matching happens out-of-band, confirmed only by
  redeeming a `VerificationCode`.
- Order-view recovery (§11) is a **separate, narrower** flow from account linking — one code
  unlocks exactly one order, never all orders for an email. This asymmetry is intentional; do not
  "fix" it to match §6.
- No real email exists yet — §6 and §11 both surface their pending codes/links through an
  admin-only (`Administrator`-role) Backoffice view instead (§10), designed to be replaced by real
  delivery later without redesign.

## Related ADRs
- ADR-0005 (AccountProfile) — `UserProfile.UserId` one-to-many, no unique index; basis for guest reuse
- ADR-0012 (Presale/Checkout) — `PresaleUserId`, `SoftReservation`, `CheckoutService`,
  `IAccountProfileClient` all reused unmodified
- ADR-0013 (Per-BC DbContext interfaces) — no cross-schema FK; why a non-Identity token in
  `UserProfile.UserId` is safe, and why `VerificationCode` is consumed via ACL, not a shared table
- ADR-0014 (Sales/Orders) — `Order.CustomerId` invariant untouched
- ADR-0025 (API tiered access) — unchanged; `ECommerceApp.API` is out of scope for this ADR
- ADR-0009 (Job Management access control) — `Administrator`-only precedent used by §10's admin view
