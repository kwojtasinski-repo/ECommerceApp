# Roadmap: Sales/Orders BC — Atomic Switch

> ADR: [ADR-0014](../adr/0014-sales-orders-bc-design.md) — Sales/Orders BC Design
> Status: 🟡 In progress — Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ Integration tests ✅
> **Blocks**: Sales/Payments atomic switch · Presale/Checkout Slice 2 · Sales/Coupons · Sales/Fulfillment

---

## What is already done

All layers are implemented and tested. The two remaining gate items before the atomic switch:

| Layer | Status |
|---|---|
| Domain — Order, OrderItem, OrderCustomer, OrderEvent, OrderStatus, typed IDs, event payloads | ✅ Done |
| Application — OrderService, OrderItemService, handlers, snapshot job, DTOs, ViewModels, DI | ✅ Done |
| Infrastructure — OrdersDbContext, configurations, repositories, adapters, DI | ✅ Done |
| Unit tests | ✅ Done |
| Integration tests — `OrderServiceTests.cs` (8 tests) | ✅ Done |
| `InitSalesSchema` DB migration (creates `sales.Orders`, `sales.OrderItems`, `sales.OrderEvents`) | ✅ Done — **pending production sign-off** |

---

## Gate conditions

Both DB migrations must be approved per [migration policy](../../.github/instructions/migration-policy.md)
before any controller migration or atomic switch:

1. **`InitSalesSchema`** — already generated; awaiting production sign-off.
2. **Schema update migration** — for `OrderStatus` column + removal of `IsPaid`/`IsDelivered`/`IsCancelled`/`PaymentId`/`RefundId` per §16 design revision. Not yet generated.

---

## Pending steps

### Step 1 — Schema update migration (requires approval)

| File | Action |
|---|---|
| `Infrastructure/Sales/Orders/Migrations/` | `dotnet ef migrations add UpdateOrdersSchema --project Infrastructure --context OrdersDbContext` — adds `Status` column (index), removes legacy flag columns, aligns with §16 design revision |
| Submit migration PR | Wait for sign-off before proceeding |

### Step 2 — Presale BC gap (OrderPlacedHandler cleanup)

| File | Action |
|---|---|
| `Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs` | Add `IMessageHandler<OrderPlaced>` that calls `ICartService.ClearAsync(userId)` + `ISoftReservationService.RemoveAllForUserAsync(userId)` — cleans up cart and soft reservations when an order is placed via the old path |
| `Application/Presale/Checkout/Services/Extensions.cs` | Register the new handler |
| `UnitTests/Presale/Checkout/OrderPlacedHandlerTests.cs` | Unit tests: empty cart case, no reservations case, happy path |

### Step 3 — Inventory BC gap (PaymentConfirmedHandler update)

| File | Action |
|---|---|
| `Application/Inventory/Availability/Handlers/PaymentConfirmedHandler.cs` | Update to call `IStockService.ConfirmReservationsByOrderAsync(orderId)` instead of the current single-reservation lookup pattern |
| `UnitTests/Inventory/Availability/PaymentConfirmedHandlerTests.cs` | Add / update unit tests for the new method signature |

### Step 4 — Controller migration (Web)

| File | Action |
|---|---|
| `Web/Controllers/OrderController.cs` | Replace injection of legacy `IOrderService` (`Application.Services.Orders`) with new `Application.Sales.Orders.Services.IOrderService` |
| `Web/Controllers/OrderItemController.cs` | Replace injection of legacy `IOrderItemService` with new `Application.Sales.Orders.Services.IOrderItemService` |
| Verify all action methods map to new service method signatures | `PlaceOrderAsync`, `GetOrdersForCustomerAsync`, `GetOrderDetailsAsync`, `UpdateOrderAsync`, `FulfillOrderAsync`, `CancelOrderAsync`, `GetOrderItemsAsync`, `AddOrderItemAsync`, `DeleteOrderItemAsync` |

### Step 5 — Controller migration (API)

| File | Action |
|---|---|
| `API/Controllers/OrderController.cs` | Same swap as Web — new `IOrderService` |
| `API/Controllers/OrderItemController.cs` | Same swap — new `IOrderItemService` |

### Step 6 — Update legacy handlers (before DI swap)

| File | Action |
|---|---|
| `Application/Services/Payments/PaymentHandler.cs` | Replace direct `order.IsPaid = true; order.PaymentId = paymentId` with `order.ConfirmPayment(paymentId)` |
| `Application/Services/Coupons/CouponHandler.cs` | Replace direct `order.CouponUsed`/`CalculateCost` chain with `order.AssignCoupon(couponUsedId, discountPercent)` |
| `Application/Services/Items/ItemHandler.cs` | Remove `orderItem.Item.Cost` navigation chain — use `orderItem.UnitCost.Amount` |

### Step 7 — Atomic switch (DI swap)

| File | Action |
|---|---|
| `Application/DependencyInjection.cs` | Remove registrations for legacy `OrderService` (`Application.Services.Orders`) and `OrderItemService` (`Application.Services.Orders`) |
| `Infrastructure/DependencyInjection.cs` | Remove registrations for legacy `OrderRepository` (`Infrastructure.Repositories`) and `OrderItemRepository` (`Infrastructure.Repositories`) |
| `Infrastructure/DependencyInjection.cs` | Confirm new `OrdersDbContext`, `OrderRepository` (Sales.Orders), `OrderItemRepository` (Sales.Orders) are registered |

### Step 8 — Verification

| Action |
|---|
| `dotnet build` — green |
| `dotnet test` — full test suite green, zero regressions |
| Update `bounded-context-map.md` — move Sales/Orders to Completed BCs |
| Open integration tests for remaining service-level assertions (payments, coupon, fulfill flows) |

---

## Coordinate with

- **IAM atomic switch** (`iam-atomic-switch.md`) — `Order.User` nav prop removal (step 6 in ADR-0019) should be done as part of this switch or immediately after. The `Domain/Model/Order.cs` legacy model is removed here.
- **Sales/Payments** (`payments-atomic-switch.md`) — Payments atomic switch is blocked by this switch completing.

---

## Acceptance criteria

- [ ] `sales.Orders` schema matches §16 design revision — `Status` column present, legacy flag columns absent
- [ ] `OrderController` (Web + API) uses `Application.Sales.Orders.Services.IOrderService` — no injection of `Application.Services.Orders.IOrderService`
- [ ] `PaymentHandler.CreatePayment()` calls `order.ConfirmPayment(paymentId)` — no direct `order.IsPaid = true`
- [ ] `CouponHandler.HandleCouponChangesOnOrder()` calls `order.AssignCoupon(...)` — no LoD navigation chain
- [ ] Legacy `OrderService` + `OrderItemService` (`Application/Services/Orders/`) DI registrations removed
- [ ] Legacy `OrderRepository` + `OrderItemRepository` (`Infrastructure/Repositories/`) DI registrations removed
- [ ] `OrderPlacedHandler` (Presale BC) clears cart + soft reservations on `OrderPlaced`
- [ ] `PaymentConfirmedHandler` (Inventory BC) uses `ConfirmReservationsByOrderAsync`
- [ ] Full test suite green after atomic switch
- [ ] `bounded-context-map.md` updated

---

*Last reviewed: 2026-03-12 · ADR: [ADR-0014](../adr/0014-sales-orders-bc-design.md)*
