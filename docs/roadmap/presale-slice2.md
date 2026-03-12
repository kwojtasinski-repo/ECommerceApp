# Roadmap: Presale/Checkout — Slice 2

> ADR: [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 (formal amendment — not a separate ADR)
> Status: ⬜ Not started
> **Blocked by**: Sales/Orders BC atomic switch

---

## Gate condition
Slice 2 cannot start until `Sales/Orders` BC atomic switch is complete and
`IOrderService.PlaceOrderFromPresaleAsync` is available. See [`project-state.md`](../../.github/context/project-state.md).

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
| `Application/Presale/Checkout/Services/CheckoutService.cs` | Implement full §12 flow: load reservations → stock gate (`IStockClient.TryReserveAsync`) → place order (`IOrderService.PlaceOrderFromPresaleAsync`) → remove reservations → clear cart |
| `API/Controllers/Presale/StorefrontController.cs` (or new `CheckoutController`) | Add POST endpoint calling `GetPriceChangesAsync` for confirmation view; POST endpoint calling `PlaceOrderAsync` on confirmation |
| `ECommerceApp.UnitTests/Presale/Checkout/` | Add `CheckoutServiceTests`, `SoftReservationServiceGetAllTests`, `GetPriceChangesTests` |

---

## Checkout coordination flow (ADR-0012 §12)

```
PlaceOrderAsync(userId, customerId, currencyId)
  1. GetAllForUserAsync(userId) → empty? → NoSoftReservations
  2. For each reservation: IStockClient.TryReserveAsync → false? → StockUnavailable(productId)
  3. Build PlaceOrderFromPresaleDto (UnitPrice from SoftReservation — no ICatalogClient call)
     IOrderService.PlaceOrderFromPresaleAsync → failure? → OrderFailed(reason)
     [soft reservations NOT removed on failure — expire via SoftReservationExpiredJob]
  4. ISoftReservationService.RemoveAsync per reservation
  5. ICartService.ClearAsync(userId)
  6. Return Success(orderId)
```

---

## Acceptance criteria

- [ ] `ICheckoutService.PlaceOrderAsync` returns `NoSoftReservations` when no active reservations exist
- [ ] `ICheckoutService.PlaceOrderAsync` returns `StockUnavailable(productId)` when `IStockClient.TryReserveAsync` returns `false`
- [ ] On order placement failure, soft reservations are left intact (expire naturally)
- [ ] `SoftReservation.UnitPrice` flows into `PlaceOrderLineDto.UnitPrice` — no fresh `ICatalogClient` call at placement time
- [ ] `IOrderService.PlaceOrderAsync` (legacy `CartItemIds` path) is unchanged after introducing `PlaceOrderFromPresaleAsync`
- [ ] `GetPriceChangesAsync` returns only lines where `LockedPrice != CurrentPrice`
- [ ] Confirmation UI shows price-change warning when `GetPriceChangesAsync` returns non-empty list

---

*Last reviewed: 2026-03-12 · ADR: [ADR-0012 §11–14](../adr/0012-presale-checkout-bc-design.md)*
