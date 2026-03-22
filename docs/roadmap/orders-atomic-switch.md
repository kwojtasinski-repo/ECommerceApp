# Roadmap: Sales/Orders BC — Atomic Switch

> ADR: [ADR-0014](../adr/0014-sales-orders-bc-design.md) — Sales/Orders BC Design
> Status: 🟡 In progress — Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ Integration tests ✅ DB migration ✅ approved
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
| `InitSalesSchema` DB migration (creates `sales.Orders`, `sales.OrderItems`, `sales.OrderEvents`) | ✅ Done — **approved** |

---

## Gate conditions

DB migration must be approved per [migration policy](../../.github/instructions/migration-policy.md)
before any controller migration or atomic switch:

1. **`InitSalesSchema`** — ✅ generated and **approved**. Creates `sales.Orders`, `sales.OrderItems`, `sales.OrderEvents` with `Status nvarchar(30)` column and indexes.

> **`UpdateOrdersSchema` is NOT needed.** The new BC uses a separate `sales` schema. The `Status` column
> was included from the start in `InitSalesSchema`. The legacy `dbo.Orders` table (with `IsPaid`,
> `IsDelivered`, `PaymentId`, `RefundId`) remains untouched — it's consumed by legacy services that
> stay active during the parallel-change period.

---

## Pending steps

### ~~Step 1 — Schema update migration~~ ✅ Not needed

> Removed. The `InitSalesSchema` migration already creates the `sales.Orders` table with `Status nvarchar(30)`.
> The new BC uses a completely separate schema (`sales.*`) from the legacy tables (`dbo.*`).
> No column additions or removals are required — the new schema was correct from the start.

### Step 1 — Presale BC gap (OrderPlacedHandler cleanup)

| File | Action |
|---|---|
| `Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs` | Add `IMessageHandler<OrderPlaced>` that calls `ICartService.ClearAsync(userId)` + `ISoftReservationService.RemoveAllForUserAsync(userId)` — cleans up cart and soft reservations when an order is placed via the old path |
| `Application/Presale/Checkout/Services/Extensions.cs` | Register the new handler |
| `UnitTests/Presale/Checkout/OrderPlacedHandlerTests.cs` | Unit tests: empty cart case, no reservations case, happy path |

### Step 2 — Inventory BC gap (PaymentConfirmedHandler update)

| File | Action |
|---|---|
| `Application/Inventory/Availability/Handlers/PaymentConfirmedHandler.cs` | Update to call `IStockService.ConfirmReservationsByOrderAsync(orderId)` instead of the current single-reservation lookup pattern |
| `UnitTests/Inventory/Availability/PaymentConfirmedHandlerTests.cs` | Add / update unit tests for the new method signature |

### Step 3 — Controller migration (Web — Area-based)

> Routing strategy: [ADR-0024](../adr/0024-controller-routing-strategy.md) — new parallel routes via ASP.NET Core Areas.
> Legacy controllers stay active and untouched.

| File | Action |
|---|---|
| `Web/Startup.cs` | Add area route: `{area:exists}/{controller}/{action=Index}/{id?}` before the default route |
| `Web/Areas/Sales/Controllers/OrdersController.cs` | Create `[Area("Sales")]` controller injecting `Application.Sales.Orders.Services.IOrderService`. Actions: `Index`, `MyOrders`, `Details`, `Edit`, `ByCustomer`, `PaidOrders`, `Dispatch`, `Fulfillment` |
| `Web/Areas/Sales/Controllers/OrderItemsController.cs` | Create `[Area("Sales")]` controller injecting `Application.Sales.Orders.Services.IOrderItemService`. Actions: `Index`, `ByItem`, `Details` |
| `Web/Areas/Sales/Views/Orders/` | Create views using new `Application.Sales.Orders.ViewModels.*` |
| `Web/Areas/Sales/Views/OrderItems/` | Create views using new VMs |
| `Web/Areas/Presale/Controllers/CheckoutController.cs` | Create `[Area("Presale")]` controller for cart + place-order flow. Actions: `Cart`, `AddItem`, `PlaceOrder`, `OrderDetails`, `Summary`, `UpdateCartItem`, `DeleteCartItem` |
| `Web/Areas/Presale/Views/Checkout/` | Create views for cart/checkout using new VMs |
| `Web/Views/Shared/_Layout.cshtml` | Update nav links: `/Order/ShowMyCart` → `/Presale/Checkout/Cart`, `/Order/ShowMyOrders` → `/Sales/Orders/MyOrders`, etc. |

### Step 4 — Controller migration (API — in-place swap)

> Routing strategy: [ADR-0024](../adr/0024-controller-routing-strategy.md) — hard in-place replacement,
> same route paths, new service implementations. API breaking changes accepted (internal API).

| File | Action |
|---|---|
| `API/Controllers/OrderController.cs` | Replace injection of legacy `IOrderService` with `Application.Sales.Orders.Services.IOrderService`. Update action signatures and return types (see ADR-0024 breaking changes table) |
| `API/Controllers/OrderItemController.cs` | Replace injection of legacy `IOrderItemService` with `Application.Sales.Orders.Services.IOrderItemService`. Update action signatures |

### Step 5 — Update legacy handlers (before DI swap)

| File | Action |
|---|---|
| `Application/Services/Payments/PaymentHandler.cs` | Replace direct `order.IsPaid = true; order.PaymentId = paymentId` with `order.ConfirmPayment(paymentId)` |
| `Application/Services/Coupons/CouponHandler.cs` | Replace direct `order.CouponUsed`/`CalculateCost` chain with `order.AssignCoupon(couponUsedId, discountPercent)` |
| `Application/Services/Items/ItemHandler.cs` | Remove `orderItem.Item.Cost` navigation chain — use `orderItem.UnitCost.Amount` |

### Step 6 — Activate new services (DI swap in controllers only)

> ⚠️ **Do not remove legacy code yet.** The new code runs in production for at least one full order lifecycle
> before cleanup. Legacy services remain registered as a fallback — they are no longer called but remain
> compilable and deployable. Remove only after confirmed production stability.

| File | Action |
|---|---|
| `Infrastructure/DependencyInjection.cs` | Confirm new `OrdersDbContext`, `OrderRepository` (Sales.Orders), `OrderItemRepository` (Sales.Orders) are registered |
| `Application/DependencyInjection.cs` | New `OrderService` + `OrderItemService` (`Application.Sales.Orders`) must be registered **before** this step |

### Step 7 — Verification

| Action |
|---|
| `dotnet build` — green |
| `dotnet test` — full test suite green, zero regressions |
| Update `bounded-context-map.md` — move Sales/Orders to Active (switch live, cleanup pending) |
| Monitor production for one full payment + fulfillment cycle before cleanup |

### Step 8 — Legacy cleanup (deferred — post-production validation)

> Execute only after new code has been running stably. Coordinate with IAM atomic switch
> (removes `Domain/Model/Order.cs` legacy model) and Payments switch.

| File | Action |
|---|---|
| `Application/DependencyInjection.cs` | Remove registrations for legacy `OrderService` (`Application.Services.Orders`) and `OrderItemService` (`Application.Services.Orders`) |
| `Infrastructure/DependencyInjection.cs` | Remove registrations for legacy `OrderRepository` (`Infrastructure.Repositories`) and `OrderItemRepository` (`Infrastructure.Repositories`) |
| `Application/Services/Orders/` | Delete legacy `OrderService.cs`, `OrderItemService.cs` |
| `Infrastructure/Repositories/` | Delete legacy `OrderRepository.cs`, `OrderItemRepository.cs` |

---

## Coordinate with

- **IAM atomic switch** (`iam-atomic-switch.md`) — `Order.User` nav prop removal (step 6 in ADR-0019) should be done as part of Step 9 (cleanup). The `Domain/Model/Order.cs` legacy model is deleted during cleanup, not at controller switch time.
- **Sales/Payments** (`payments-atomic-switch.md`) — Payments controller switch is unblocked after Step 6 (activation) completes. Payments cleanup is deferred similarly.

---

## Acceptance criteria

### Switch live (Steps 1–7)

- [ ] `sales.Orders` schema correct — `Status` column present (via `InitSalesSchema`); legacy `dbo.Orders` untouched
- [ ] Web: `Areas/Sales/Controllers/OrdersController.cs` uses `Application.Sales.Orders.Services.IOrderService` with `[Area("Sales")]`
- [ ] Web: `Areas/Presale/Controllers/CheckoutController.cs` uses Presale services with `[Area("Presale")]`
- [ ] API: `OrderController` uses `Application.Sales.Orders.Services.IOrderService` — in-place swap
- [ ] `Startup.cs` includes area route `{area:exists}/{controller}/{action=Index}/{id?}`
- [ ] `_Layout.cshtml` nav links point to new Area routes
- [ ] `PaymentHandler.CreatePayment()` calls `order.ConfirmPayment(paymentId)` — no direct `order.IsPaid = true`
- [ ] `CouponHandler.HandleCouponChangesOnOrder()` calls `order.AssignCoupon(...)` — no LoD navigation chain
- [ ] `OrderPlacedHandler` (Presale BC) clears cart + soft reservations on `OrderPlaced`
- [ ] `PaymentConfirmedHandler` (Inventory BC) uses `ConfirmReservationsByOrderAsync`
- [ ] Full test suite green after activation
- [ ] `bounded-context-map.md` updated (switch live)

### Cleanup (Step 8 — deferred)

- [ ] Legacy `OrderService` + `OrderItemService` (`Application/Services/Orders/`) DI registrations removed
- [ ] Legacy `OrderRepository` + `OrderItemRepository` (`Infrastructure/Repositories/`) DI registrations removed
- [ ] `Domain/Model/Order.cs` legacy model deleted (coordinate with IAM switch)
- [ ] Full test suite green after cleanup

---

*Last reviewed: 2026-03-22 · ADRs: [ADR-0014](../adr/0014-sales-orders-bc-design.md), [ADR-0024](../adr/0024-controller-routing-strategy.md)*
