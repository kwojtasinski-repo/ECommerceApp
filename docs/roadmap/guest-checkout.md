# Roadmap: Guest Checkout — Anonymous Order Placement

> ADR: [ADR-0030](../adr/0030/0030-guest-checkout-anonymous-order-placement.md) (revised 2026-08-14, 2026-08-16)
> Status: ✅ Accepted (Phases 1–8) — Phase 9 in design/plan, supersedes part of §11/§12's mechanism
> Plan pairs: `.github/plans/01`–`08-phase-*` (all deleted per each phase's PASS cleanup step); `09-phase-*` in progress

---

## Gate condition

None — this reuses the existing live Presale/Checkout (ADR-0012) and AccountProfile (ADR-0005)
mechanisms without changing their contracts. It can start as soon as the ADR is accepted.

**Prerequisite to verify first**: what rate-limiting infrastructure already exists for public
endpoints (ADR-0030 §8). `[AllowAnonymous]` removes the incidental throttling effect of requiring
login. **As of Phase 8, still not implemented anywhere in `ECommerceApp.Web`** — Phase 9 makes this
a hard blocker (not just a note) for its own new order-lookup-by-id path, since that path treats the
order id as non-secret and relies on rate limiting to prevent code-request enumeration.

**Scope note (revised):** everything below lives in `ECommerceApp.Web` (the MVC storefront).
`ECommerceApp.API` is not touched by this roadmap — see ADR-0030 §2.

---

## Phase-to-plan mapping

| Phase | Plan file | ADR section(s) | Status |
|---|---|---|---|
| 01 | `01-phase-guest-shopper-identity-*` | §1, §1a, §2 | Done — independently validated PASS 2026-08-14 |
| 02 | `02-phase-guest-customer-provisioning-*` | §3 | Done — independently validated PASS 2026-08-14 |
| 03 | `03-phase-guest-account-promotion-*` | §5 | Done — independently validated PASS 2026-08-14 |
| 04 | `04-phase-guest-profile-cleanup-*` | Consequences (Negative) | Done — independently validated PASS 2026-08-15 |
| 05 | `05-phase-verification-code-primitive-*` | §9 | Done — independently validated PASS 2026-08-16 |
| 06 | `06-phase-guest-account-linking-*` | §6, §10 | Done — independently validated PASS 2026-08-16 |
| 07 | `07-phase-guest-order-access-recovery-*` | §11 | Done — independently validated PASS 2026-08-16 |
| 08 | `08-phase-guest-checkout-regression-*` | §12 | Done — independently validated PASS 2026-08-16 (found and fixed a Phase 7 production defect: anonymous guests could reach the payment form but not actually submit it) |
| 09 | `09-phase-guest-access-authorization-*` | §11, §12 (2026-08-16 revision) | Not started — plan pending approval |

Ordering/preconditions between phases are stated in each phase file; broadly: 01 → 02 → 03, with
04 startable any time after 02; 05 has no dependency on 01–04; 06 depends on 05 (and 02, for there
to be unclaimed profiles); 07 depends on 05 and 02/03; 08 depends on all of the above being PASS;
09 depends on 08 (PASS) and replaces the mechanism 07/08 shipped for §11/§12, per the ADR's
2026-08-16 revision note.

---

## Flow (ADR-0030, revised)

```
Guest browses / adds to cart (ECommerceApp.Web only)
  IShopperIdentityResolver.Resolve() → no auth cookie, no guest cookie → mint guest cookie
  CartLine / SoftReservation flow — UNCHANGED (ADR-0012)

Guest submits PlaceOrder (POST, no CustomerId in the form for a guest)
  → IAccountProfileClient.EnsureGuestCustomerAsync(shopperToken, customerData)
      → UserProfile exists for this token? return its Id : else UserProfile.Create(...) + AddAsync
  → CheckoutService.PlaceOrderAsync(shopperToken, customerId, ...) — UNCHANGED (ADR-0012)
  → Order.CustomerId = UserProfile.Id — Order/OrderCustomer UNCHANGED (ADR-0014)
  → order-access token minted silently, set as cookie + would-be-emailed URL (§11)

Optional: guest checks "create an account" on confirmation screen
  → IGuestPromotionService.PromoteAsync(profileId, requestingUserId, password)
      → ownership check → ApplicationUser created → UserProfile.ReassignOwner(newUserId)
      → same UserProfileId, same Order.CustomerId — no order rewritten

Later, same email registers a real account in a different session
  → registration succeeds with an identical response either way
  → background check for unclaimed UserProfiles by email
  → if matched: VerificationCode(Purpose=GuestAccountLink) generated, surfaced via admin interim view (§10)
  → redeeming it → ReassignOwner for ALL matched profiles

Later still, guest lost the order-access cookie and wants to view/pay
  → arrives at /Identity/Account/Login?guestOrder={token} (from a past email or the admin view)
  → "kontynuuj jako gość" → code sent to the email on file (not the one typed)
  → VerificationCode(Purpose=GuestOrderAccess, SubjectKey=that one OrderId) generated
  → redeeming it unlocks ONLY that one order (not all orders for the email — see ADR §6 vs §11)
```

---

## Acceptance criteria

- [x] `PlaceOrder` (POST, `ECommerceApp.Web`) succeeds with no prior login, given a valid guest
      session cookie and complete checkout form data (Phase 1/2, independently validated PASS 2026-08-14)
- [x] `ECommerceApp.API/Controllers/Presale/*` is byte-for-byte unchanged across all 8 phases
      (Phase 8, independently confirmed via `git diff --name-only <pre-Phase-1-commit> -- ECommerceApp.API/Controllers/Presale`, zero diff)
- [x] No `ApplicationUser` is created for browsing, cart, or soft-reservation activity — only a
      `UserProfile` row, and only once, at order placement (Phase 1/2, independently validated PASS)
- [x] Resubmitting `PlaceOrder` for the same guest session does not create duplicate `UserProfile`
      rows (Phase 2, independently validated PASS, HTTP-level test)
- [x] `Order.CustomerId`/`Payment.ExpiresAt` invariants are unchanged; no nullable columns on
      `Order` or `Payment` (Phase 1/2, confirmed via `git diff` on `Order.cs`/`Payment.cs`)
- [x] No `IsGuest` (or equivalent) flag exists anywhere in the codebase after this change (true as
      of Phases 1-3; re-verify as Phases 4-8 land)
- [x] "Create an account" after guest checkout results in the same `UserProfileId` and the same
      `Order.CustomerId` as before promotion (Phase 3, independently validated PASS, HTTP-level test)
- [x] Registration response is identical whether or not a matching unclaimed guest profile exists
      (Phase 6, re-confirmed Phase 8: `Register.cshtml.cs` always follows the same success path;
      the guest-profile match happens later, out-of-band, in the deferred `GuestAccountLinkCheckJob`)
- [x] Redeeming a `GuestAccountLink` code reassigns **all** matching profiles; redeeming a
      `GuestOrderAccess` code unlocks **exactly one** order — the asymmetry is intentional, verified
      both directions (Phase 6/7, independently validated PASS; Phase 7's
      `RecoveryCode_ForOrderA_DoesNotGrantAccessToOrderB` covers the order-scoping direction)
- [x] No endpoint accepts a bare order number/id and offers to email a code for it — the only entry
      point to order recovery is a pre-issued, unguessable token already in a URL (Phase 7,
      `RedeemRecovery(string code)` is the only entry point; re-confirmed Phase 8)
- [x] The admin-only interim view (§10) gates on `Administrator` only, not `ManagingRole` (Phase 8,
      independently confirmed directly against `GuestVerificationController.cs`)
- [x] Session-isolation regression test (Phase 8) passes: a guest session cannot read or act on a
      concurrent decoy session's (guest or authenticated) cart/order data — `SessionIsolationTests`,
      both guest-under-test and authenticated-under-test variants, independently validated PASS
- [x] A true anonymous, no-prior-login browser E2E test exists covering cart → guest `PlaceOrder` →
      payment → account promotion, plus the cookie-loss → order-access-recovery path — Phase 8's
      `GuestCheckoutLifecycleTests`/`GuestOrderLifecycleScenario` additions, independently validated
      PASS. `GuestOrderLifecycleTests`/`GuestOrderLifecycleThroughListingTests` remain the separate,
      already-logged-in registered-customer coverage they always were — no longer the only "Guest*"
      tests in the suite.
- [ ] **Rate limiting is NOT in place** on `PlaceOrder` POST, guest-cookie issuance, or
      code-request/redemption endpoints. Grepped `ECommerceApp.Web` for rate-limiting middleware
      (`RateLimit`/`EnableRateLimiting`/`AddRateLimiter`) during Phase 8 validation — zero matches.
      This was flagged as a "Prerequisite to verify first" at the top of this roadmap from the start
      and was never assigned to any of the 8 phases' own scope, so it did not block Phase 8's PASS —
      but it remains a real, unresolved gap in this initiative as shipped. `[AllowAnonymous]` has
      been live since Phase 1; anyone deciding to expose this in a real, publicly-reachable
      environment should treat this as a pre-launch blocker, not a someday item.

---

## Known edge cases

### Guest cookie lost mid-checkout
**Scenario:** guest adds items to cart, clears cookies (or switches browser) before submitting
`PlaceOrder`.
**Impact:** cart and any active `SoftReservation`s become unreachable under the old token — no
orphaned `Order` is created either way, since `Order` is only created at `PlaceOrder` POST success.
**Decision:** accept — matches typical e-commerce guest-cart behavior.

### Same guest checks out multiple times without ever registering
**Scenario:** person guest-checks-out on three separate occasions (cookie expired/cleared between
each), same email each time.
**Impact:** three separate `UserProfile` rows, three separate `Order`s, same `Email` value on all
three (no unique constraint — by design).
**Decision:** accept. The account-linking flow (§6) reassigns **all** matching unclaimed profiles
when the person eventually registers, not just one. The order-recovery flow (§11) is intentionally
narrower — one token, one order — see ADR §6 vs §11 for why these are not the same rule.

### Guest wants to view/pay an order after losing the session cookie
**Scenario:** guest closes the browser after placing an order, returns days later (possibly a
different device) to check status or pay.
**Impact:** without §11, there would be no way back in at all.
**Decision:** order-access token (minted silently at order placement, doubles as cookie + URL
token) plus a `VerificationCode`-gated recovery flow through the existing login page. Scoped to
exactly one order per code — see ADR §11's threat model for why this does not become a mass-scan
surface.

### No real email exists yet
**Scenario:** §6 and §11 both nominally "send an email" containing a link/code.
**Impact:** nothing is actually delivered today (`IEmailSender` is a no-op).
**Decision:** interim admin-only Backoffice view (`Administrator` role) exposes pending codes/links
for manual relay — see ADR §10. Designed so wiring real delivery later replaces only the
generation step's side effect, not the redemption logic.

---

*Last reviewed: 2026-08-14 · ADR: [ADR-0030](../adr/0030/0030-guest-checkout-anonymous-order-placement.md)*
