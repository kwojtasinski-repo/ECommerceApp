# ADR-0030: Guest Checkout — Anonymous Order Placement

**Status**: Proposed
**BC**: Presale/Checkout (primary), AccountProfile (extended)
**Last amended**: —

## What this decision covers
Placing an order without an account: a session-scoped guest shopper identity (cookie-carried
`PresaleUserId`), reuse of the existing `UserProfile` aggregate as the guest's resolvable
`Order.CustomerId`, and in-place promotion to a real account with no order rewriting.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0030-guest-checkout-anonymous-order-placement.md | Core design: shopper identity resolution, authorization change, guest `UserProfile` provisioning, promotion, deferred linking flow | Understanding the guest checkout flow end-to-end |
| checklist.md | Conformance rules | Code review |
| migration-plan.md | Implementation steps | Implementation |

## Key rules
- No `IsGuest` flag anywhere — guest-ness is derived from whether `UserProfile.UserId` resolves
  to an `ApplicationUser`, never stored.
- `Order.CustomerId` stays a required positive `int` — no nullable columns.
- Guest checkout never creates an `ApplicationUser` — only a `UserProfile` row, via the existing
  `UserProfile.Create` factory, with a guest session token in the `UserId` slot.
- Promotion to a real account (`UserProfile.ReassignOwner`) is a single field update on the
  existing row — never a copy, never touches `Order.CustomerId`.
- Guest session identity is a cookie, never a JWT — `[Authorize]`/JWT Bearer continues to mean
  "real account" exclusively.
- Any later account-linking-by-email flow must never reveal a match synchronously (user
  enumeration) — matching happens out-of-band, confirmed only by clicking an emailed token.

## Related ADRs
- ADR-0005 (AccountProfile) — `UserProfile.UserId` one-to-many, no unique index; basis for guest reuse
- ADR-0012 (Presale/Checkout) — `PresaleUserId`, `SoftReservation`, `CheckoutService`, `IAccountProfileClient` all reused unmodified
- ADR-0013 (Per-BC DbContext interfaces) — no cross-schema FK; why a non-Identity token in `UserProfile.UserId` is safe
- ADR-0014 (Sales/Orders) — `Order.CustomerId` invariant untouched
- ADR-0025 (API tiered access) — `TrustedApiUser` policy unchanged; guest endpoints use `[AllowAnonymous]` instead
