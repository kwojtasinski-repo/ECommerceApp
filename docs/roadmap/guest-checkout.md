# Roadmap: Guest Checkout — Anonymous Order Placement

> ADR: [ADR-0030](../adr/0030/0030-guest-checkout-anonymous-order-placement.md)
> Status: 🔶 Proposed — design only, not started

---

## Gate condition

None — this reuses the existing live Presale/Checkout (ADR-0012) and AccountProfile (ADR-0005)
mechanisms without changing their contracts. It can start as soon as the ADR is accepted.

**Prerequisite to verify first**: what rate-limiting infrastructure already exists for public API
endpoints (see ADR-0030 migration-plan, step 0). `[AllowAnonymous]` removes the incidental
throttling effect of requiring a JWT.

---

## Steps (ADR-0030)

### Step 1 — Guest session identity
| File | Action |
|---|---|
| `ECommerceApp.API/Controllers/BaseController.cs` | Add `GetOrCreateShopperId()`: authenticated → existing `GetUserId()` path; else read/mint a guest cookie (crypto-random, prefixed, `HttpOnly`/`Secure`/`SameSite=Lax`) |

### Step 2 — Authorization change
| File | Action |
|---|---|
| `ECommerceApp.API/Controllers/Presale/CartController.cs` | Guest-eligible actions: `[Authorize]`/`TrustedApiUser` → `[AllowAnonymous]`; use `GetOrCreateShopperId()` |
| `ECommerceApp.API/Controllers/Presale/CheckoutController.cs` | Same change for `Initiate` and `Confirm` |

### Step 3 — Guest customer provisioning (domain + ACL)
| File | Action |
|---|---|
| `ECommerceApp.Application/AccountProfile/.../IUserProfileService.cs` | Add `GetOrCreateForGuestAsync(string userId, ...)` — `GetByUserIdAsync` first, else `UserProfile.Create` + `AddAsync` |
| `ECommerceApp.Application/Presale/Checkout/Contracts/IAccountProfileClient.cs` | Add `EnsureGuestCustomerAsync(string userId, CheckoutCustomer customer, ct)` returning `int` |
| `ECommerceApp.Infrastructure/Presale/Checkout/Adapters/AccountProfileClientAdapter.cs` | Implement, delegating to step 3's service |

### Step 4 — Wire into checkout confirmation
| File | Action |
|---|---|
| `ECommerceApp.API/Controllers/Presale/CheckoutController.cs` | `ConfirmCheckoutRequest.CustomerId` → `int?`; guest branch resolves it via `EnsureGuestCustomerAsync`, ignoring any client-supplied value |

### Step 5 — In-place promotion
| File | Action |
|---|---|
| `ECommerceApp.Domain/AccountProfile/UserProfile.cs` | Add `ReassignOwner(string newUserId)` |
| `IGuestPromotionService.PromoteAsync(int profileId, string password)` (new) | `UserManager.CreateAsync` + `ReassignOwner` + `UpdateAsync` |
| New endpoint, e.g. `POST /api/checkout/create-account` | Scoped to the calling session's own guest profile only |

### Step 6 — Cleanup job
| File | Action |
|---|---|
| TimeManagement BC (pattern: `SoftReservationExpiredJob`) | Purge unclaimed `UserProfile` rows with no `Order`, past retention threshold (TBD, e.g. 90 days) |

### Step 7 — Deferred: account-linking on separate-session registration (§6, not required for v1)
| File | Action |
|---|---|
| Registration handler | On success, background-check unclaimed `UserProfile`s by email; if found, send single-use linking token email. Response is identical regardless of match. |
| New endpoint, e.g. `GET /api/account/link-guest-orders?token=...` | Validate token, `ReassignOwner` for **all** matching profiles |

---

## Flow (ADR-0030)

```
Guest browses / adds to cart
  GetOrCreateShopperId() → no JWT, no cookie → mint guest cookie, PresaleUserId = token
  CartLine / SoftReservation flow — UNCHANGED (ADR-0012)

Guest confirms order (POST /api/checkout/confirm, no Authorization header)
  CustomerId omitted by client
  → IAccountProfileClient.EnsureGuestCustomerAsync(shopperToken, customerData)
      → UserProfile exists for this token? return its Id : else UserProfile.Create(...) + AddAsync
  → CheckoutService.PlaceOrderAsync(shopperToken, customerId, ...) — UNCHANGED (ADR-0012 §12)
  → Order.CustomerId = UserProfile.Id — Order/OrderCustomer UNCHANGED (ADR-0014)

Optional: guest checks "create an account" on confirmation screen
  → IGuestPromotionService.PromoteAsync(profileId, password)
      → ApplicationUser created, UserProfile.ReassignOwner(newUserId)
      → same UserProfileId, same Order.CustomerId — no order rewritten
```

---

## Acceptance criteria

- [ ] `POST /api/checkout/confirm` succeeds with no `Authorization` header, given a valid guest session cookie and complete `CheckoutCustomer` data
- [ ] No `ApplicationUser` is created for browsing, cart, or soft-reservation activity — only at `Confirm` time, and only a `UserProfile` row
- [ ] Resubmitting `Confirm` (or retrying `Initiate`) for the same guest session does not create duplicate `UserProfile` rows
- [ ] `Order.CustomerId` invariant (`> 0`) is unchanged; no nullable columns introduced
- [ ] No `IsGuest` (or equivalent) flag exists anywhere in the codebase after this change
- [ ] "Create an account" after guest checkout results in the same `UserProfileId` and the same `Order.CustomerId` as before promotion
- [ ] Registration response is identical (status, body, timing within normal variance) whether or not a matching unclaimed guest profile exists for that email
- [ ] Rate limiting is confirmed in place on `POST /api/checkout/confirm` and guest-cookie issuance before `[AllowAnonymous]` ships

---

## Known edge cases

### Guest cookie lost mid-checkout
**Scenario:** guest adds items to cart, clears cookies (or switches browser) before confirming.
**Impact:** cart and any active `SoftReservation`s become unreachable under the old token — same
class of behavior as an authenticated user's `SoftReservation` expiring (ADR-0012 EC-001), except
here there is no session to recover; the guest simply starts over. No orphaned `Order` is created
either way, since `Order` is only created at `Confirm`.
**Decision:** accept — matches typical e-commerce guest-cart behavior. Not a data-integrity issue.

### Same guest checks out multiple times without ever registering
**Scenario:** person guest-checks-out on three separate occasions (cookie expired/cleared between
each), same email each time.
**Impact:** three separate `UserProfile` rows, three separate `Order`s, same `Email` value on all
three (no unique constraint — by design, see ADR-0030 Consequences).
**Decision:** accept for v1. The deferred linking flow (Step 7) reassigns **all** matching
unclaimed profiles when the person eventually registers, not just one.

---

*Last reviewed: 2026-07-26 · ADR: [ADR-0030](../adr/0030/0030-guest-checkout-anonymous-order-placement.md)*
