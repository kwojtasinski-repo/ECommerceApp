# Roadmap: Presale/Checkout — Slice 2

> ADR: [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 (formal amendment — not a separate ADR)
> Status: ✅ Switch live
> ~~**Blocked by**: Sales/Orders BC implementation below 80%~~ — Orders switch is live

---

## Gate condition
Slice 2 requires the `Sales/Orders` BC to be at least 80% implemented and
`IOrderService.PlaceOrderFromPresaleAsync` to be available. See [`project-state.md`](../../.github/context/project-state.md).

---

## Steps (ADR-0012 §11–14)

### Step 11 — `ICheckoutService` + `CheckoutService` + `CheckoutResult`
| File | Action |
|---|---|
| `Application/Presale/Checkout/Results/CheckoutResult.cs` | Add sealed record with factory methods: `Success(orderId)`, `NoSoftReservations()`, `StockUnavailable(productId)`, `OrderFailed(reason)` |
| `Application/Presale/Checkout/Services/ICheckoutService.cs` | Add `Task<CheckoutResult> PlaceOrderAsync(PresaleUserId, int customerId, int currencyId, CancellationToken)` |
| `Application/Presale/Checkout/Services/CheckoutService.cs` | `internal sealed` — implements §12 coordination flow |
| `Application/Presale/Checkout/Services/Extensions.cs` | Register `ICheckoutService → CheckoutService` |

### Step 12 — `GetAllForUserAsync` + `GetPriceChangesAsync` on `ISoftReservationService`
| File | Action |
|---|---|
| `Application/Presale/Checkout/Services/ISoftReservationService.cs` | Add `Task<IReadOnlyList<SoftReservation>> GetAllForUserAsync(PresaleUserId, CancellationToken)` and `Task<IReadOnlyList<SoftReservationPriceChangeVm>> GetPriceChangesAsync(PresaleUserId, CancellationToken)` |
| `Application/Presale/Checkout/ViewModels/SoftReservationPriceChangeVm.cs` | Add `sealed record` with `ProductId`, `LockedPrice`, `CurrentPrice` |
| `Domain/Presale/Checkout/ISoftReservationRepository.cs` | Add `Task<IReadOnlyList<SoftReservation>> GetAllByUserIdAsync(string userId, CancellationToken)` |
| `Infrastructure/Presale/Checkout/Repositories/SoftReservationRepository.cs` | Implement `GetAllByUserIdAsync` |
| `Application/Presale/Checkout/Services/SoftReservationService.cs` | Implement both new service methods |

### Step 13 — `PlaceOrderFromPresaleAsync` on `IOrderService`
| File | Action |
|---|---|
| `Application/Sales/Orders/DTOs/PlaceOrderLineDto.cs` | Add `sealed record PlaceOrderLineDto(int ProductId, int Quantity, decimal UnitPrice)` |
| `Application/Sales/Orders/DTOs/PlaceOrderFromPresaleDto.cs` | Add `sealed record PlaceOrderFromPresaleDto(int CustomerId, int CurrencyId, string UserId, IReadOnlyList<PlaceOrderLineDto> Lines)` |
| `Application/Sales/Orders/Services/IOrderService.cs` | Add `Task<PlaceOrderResult> PlaceOrderFromPresaleAsync(PlaceOrderFromPresaleDto, CancellationToken)` |
| `Application/Sales/Orders/Services/OrderService.cs` | Implement — uses `PlaceOrderLineDto.UnitPrice` directly; bypasses `IOrderProductResolver`; publishes `OrderPlaced` message |

> `PlaceOrderAsync` (legacy `CartItemIds` path) is left unchanged — both methods co-exist.

### Step 14 — `CheckoutService` coordination + API endpoint + unit tests
| File | Action |
|---|---|
| `Application/Presale/Checkout/Contracts/IOrderClient.cs` | Add ACL interface `IOrderClient` + `OrderPlacementResult` + `CheckoutLine` — isolates Presale from Orders BC |
| `Infrastructure/Presale/Checkout/Adapters/OrderClientAdapter.cs` | Adapter: wraps `IOrderService.PlaceOrderFromPresaleAsync`; maps `CheckoutLine[]` → `PlaceOrderFromPresaleDto` |
| `Application/Presale/Checkout/Services/CheckoutService.cs` | Implement §12 flow via `IOrderClient` (no `IStockClient` — stock reservation is reactive via `Inventory.OrderPlacedHandler`) |
| `API/Controllers/V2/CheckoutController.cs` | GET `/api/v2/checkout/price-changes` + POST `/api/v2/checkout/confirm` |
| `ECommerceApp.UnitTests/Presale/Checkout/` | `CheckoutServiceTests` (10 tests), `GetPriceChangesTests` (7 tests) |

---

## Checkout coordination flow (ADR-0012 §12)

```
PlaceOrderAsync(userId, customerId, currencyId)
  1. GetAllForUserAsync(userId) → empty? → NoSoftReservations
  2. Build CheckoutLine list (UnitPrice from SoftReservation — no ICatalogClient call at checkout time)
     IOrderClient.PlaceOrderAsync → failure? → OrderFailed(reason)
     [soft reservations NOT removed on failure — expire via SoftReservationExpiredJob]
     [stock reservation is reactive: OrderPlaced → Inventory.OrderPlacedHandler → StockService.ReserveAsync]
  3. ISoftReservationService.RemoveAsync per reservation
  4. ICartService.ClearAsync(userId)
  5. Return Success(orderId)
```

> **ACL boundary**: `CheckoutService` depends on `IOrderClient` (Presale Contracts), not `IOrderService` directly.
> `OrderClientAdapter` (Infrastructure) bridges to Orders BC — same pattern as `ICatalogClient` / `IStockClient`.
> `IStockClient` is retained in the codebase for `StorefrontQueryService` use but is no longer part of the checkout write path.

---

## Acceptance criteria

- [x] `ICheckoutService.PlaceOrderAsync` returns `NoSoftReservations` when no active reservations exist
- [x] `CheckoutService` uses `IOrderClient` ACL — not direct `IOrderService` injection (same isolation pattern as `ICatalogClient`)
- [x] On order placement failure, soft reservations are left intact (expire naturally)
- [x] `SoftReservation.UnitPrice` flows into `CheckoutLine.UnitPrice` → `PlaceOrderLineDto.UnitPrice` — no fresh `ICatalogClient` call at placement time
- [x] `IOrderService.PlaceOrderAsync` (legacy `CartItemIds` path) is unchanged after introducing `PlaceOrderFromPresaleAsync`
- [x] `GetPriceChangesAsync` returns only lines where `LockedPrice != CurrentPrice`
- [x] Confirmation UI shows price-change warning when `GetPriceChangesAsync` returns non-empty list

---

---

## Known edge cases

### [EC-001] `SoftReservationExpiredJob` fires while `CheckoutService.PlaceOrderAsync` is in progress (TOCTOU race)

**Scenario:**
1. User initiates checkout → `SoftReservation`s created, expiry jobs scheduled via `IDeferredJobScheduler`.
2. User pauses on the price-change confirmation screen.
3. `SoftReservationExpiredJob` fires (TTL reached) → deletes `SoftReservation` rows from DB, evicts from cache, calls `IDeferredJobScheduler.CancelAsync`.
4. User clicks "Confirm Order" *just after* the TTL boundary → calls `CheckoutService.PlaceOrderAsync`.

**Possible outcomes depending on exact timing:**

| Timing | Outcome | Impact |
|---|---|---|
| Job fires **before** `GetAllForUserAsync` | Returns empty list → `NoSoftReservations` | ⚠️ User sees "checkout not initiated" even though they clicked in good faith — must restart checkout |
| Job fires **between** `GetAllForUserAsync` and `RemoveAsync` | Order placed with correct locked prices (in-memory list still valid); `RemoveAsync` is a DB no-op | ✅ Benign — order placed correctly, cart cleared |
| Job fires **after** `RemoveAsync` | Normal happy path | ✅ No issue |

**Key observation:** `SoftReservationExpiredJob` does not clear `CartLine`s — only `SoftReservation` rows.
If `NoSoftReservations` is returned, the user's cart is still intact; they only need to re-initiate checkout (call `HoldAsync` again).

**Mitigation options to consider:**
- **Accept the race** (current approach): `PresaleOptions.SoftReservationTtl` should be generous enough (e.g., 15 min) that this is rare. The `NoSoftReservations` response should be surfaced to the user as "Your reservation expired — please restart checkout", not a generic error.
- **Grace window**: When rendering the confirmation page, call `GetPriceChangesAsync` and check if reservations are still active. Warn the user if TTL is close (e.g., < 60 s remaining).
- **TTL extension on confirmation view**: Extend `SoftReservation.ExpiresAt` by a fixed grace period when the user navigates to the confirmation page. Requires `UpdateExpiryAsync` on `ISoftReservationRepository` and cancelling + rescheduling the `IDeferredJobScheduler` job.

**Decision — Accept the race** ✅: `PresaleOptions.SoftReservationTtl` is 15 min, making the race rare. The `CheckoutController` already surfaces `NoSoftReservations` as a user-friendly message: *"Checkout not initiated. Please initiate checkout first."* — the user restarts checkout from the cart. No code change required.

---

*Last reviewed: 2026-03-26 · ADR: [ADR-0012 §11–14](../adr/0012-presale-checkout-bc-design.md)*
