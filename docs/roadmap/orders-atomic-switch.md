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

### ~~Step 1 — Presale BC gap (OrderPlacedHandler cleanup)~~ ✅ Already done

> Verified 2026-03-22. `Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs` already exists
> and implements `IMessageHandler<OrderPlaced>`. It calls `ICartService.RemoveRangeAsync` (removes only
> ordered items, not full cart clear) and `ISoftReservationService.RemoveCommittedForUserAsync`.
> Unit tests exist at `UnitTests/Presale/Checkout/OrderPlacedHandlerTests.cs`. No action needed.

### ~~Step 2 — Inventory BC gap (PaymentConfirmedHandler update)~~ ✅ Already done

> Verified 2026-03-22. `Application/Inventory/Availability/Handlers/PaymentConfirmedHandler.cs`
> already calls `_stockService.ConfirmHoldsByOrderAsync(message.OrderId, ct)` — the bulk order-level
> operation. The "single-reservation lookup" described in the original roadmap was never implemented
> this way. No action needed.

### Step 3 — Controller migration (Web — Area-based) ✅ Done

> All Area controllers, views, and route registration are complete. See ADR-0024 for full details.
> View reduction decisions (dropped ByCustomer, ByItem, Payment/Edit) are documented in ADR-0024.

| File | Status |
|---|---|
| `Web/Startup.cs` — area route | ✅ Done |
| `Web/Areas/Sales/Controllers/OrdersController.cs` | ✅ Done |
| `Web/Areas/Sales/Controllers/OrderItemsController.cs` | ✅ Done — `ByItem` action to be removed in cleanup PR |
| `Web/Areas/Sales/Controllers/PaymentsController.cs` | ✅ Done |
| `Web/Areas/Presale/Controllers/CheckoutController.cs` | ✅ Done |
| `Web/Areas/Sales/Views/Orders/` | ✅ Done (6 views) |
| `Web/Areas/Sales/Views/OrderItems/` | ✅ Done (2 views) |
| `Web/Areas/Sales/Views/Payments/` | ✅ Done (4 views, 2 stubs pending service methods) |
| `Web/Areas/Presale/Views/Checkout/` | ✅ Done (Cart, PlaceOrder, Summary) |
| `Application/Presale/Checkout/ViewModels/CartLineVm.cs` | ✅ Done — `ProductName` added |
| `Application/Presale/Checkout/Services/CartService.cs` | ✅ Done — enriches cart lines via `IProductService.GetProductSnapshotsByIdsAsync` |
| `Web/Views/Shared/_Layout.cshtml` nav links | ⏱ Deferred — updated in same PR as frontend wiring |

### Step 3a — Checkout/PlaceOrder: profile prefill ✅ Done

> **Decision 2026-03-22**: `customerId` is NOT a manual form input. The `PlaceOrder` view prefills
> customer data from `IUserProfileService.GetDetailsByUserIdAsync(userId)`. User can edit all fields
> before submitting. The form always sends full customer data. `customerId` is a hidden field resolved
> server-side from `UserProfileDetailsVm.Id`.
>
> **Implemented 2026-03-25.**

| File | Status |
|---|---|
| `Web/Areas/Presale/Controllers/CheckoutController.cs` | ✅ `IUserProfileService` injected; `PlaceOrder` GET calls `GetDetailsByUserIdAsync` and passes `UserProfileDetailsVm?` to view |
| `Web/Areas/Presale/Views/Checkout/PlaceOrder.cshtml` | ✅ Typed model `UserProfileDetailsVm?`; `customerId` hidden; all personal + address fields prefilled from profile; first address used if available |

### Step 4 — API replacement (design-gated — external system contract discussion pending)

> ⚠️ **Not yet agreed.** The API exposes order data to external consumers. Before swapping the injected
> service, the team must agree which endpoints are kept, which are changed, and what breaking changes
> are acceptable to external callers. This is a business/integration agreement, not just a code change.
> Implementation proceeds only after that agreement is reached.

| File | Action |
|---|---|
| `API/Controllers/OrderController.cs` | Replace injection of legacy `IOrderService` with `Application.Sales.Orders.Services.IOrderService`. Update action signatures and return types per agreed external contract |
| `API/Controllers/OrderItemController.cs` | Replace injection of legacy `IOrderItemService` with `Application.Sales.Orders.Services.IOrderItemService`. Update action signatures per agreed contract |

### ~~Step 5 — Update legacy handlers~~ ✅ Not needed

> **Decision 2026-03-22**: Under the parallel-change strategy, legacy handlers (`PaymentHandler`,
> `CouponHandler`, `ItemHandler`) are **not modified**. They remain compilable and registered but
> will simply stop being called once the new BC services are the active path. Legacy handlers are
> removed in Step 8 (cleanup), not patched. No code changes required at switch time.

### Step 6 — Activate new services (DI swap in controllers only) ✅ Done

> Verified 2026-03-22. Both `Infrastructure/DependencyInjection.cs` and `Application/DependencyInjection.cs`
> already register all new BC services (`AddOrdersInfrastructure`, `AddOrderServices`, `AddPaymentsInfrastructure`,
> `AddPaymentServices`, `AddPresaleInfrastructure`, `AddPresaleServices`). Web Area controllers are
> already injecting the new services. Legacy services remain registered alongside as a fallback — they
> are no longer called but remain compilable and deployable. Remove only in Step 8 (cleanup).

| File | Status |
|---|---|
| `Infrastructure/DependencyInjection.cs` — `AddOrdersInfrastructure`, `AddPaymentsInfrastructure`, `AddPresaleInfrastructure` | ✅ Registered |
| `Application/DependencyInjection.cs` — `AddOrderServices`, `AddPaymentServices`, `AddPresaleServices` | ✅ Registered |
| `Web/Areas/Sales/Controllers/OrdersController.cs` — injects `Application.Sales.Orders.Services.IOrderService` | ✅ Wired |
| `Web/Areas/Sales/Controllers/PaymentsController.cs` — injects `Application.Sales.Payments.Services.IPaymentService` | ✅ Wired |
| `Web/Areas/Presale/Controllers/CheckoutController.cs` — injects `ICartService` + `ICheckoutService` | ✅ Wired |

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
| `Web/Areas/Sales/Controllers/OrderItemsController.cs` | Remove `ByItem(int id)` action — view was intentionally dropped (see ADR-0024 View Reduction Decisions) |
| `Web/Controllers/OrderController.cs` | Remove legacy controller |
| `Web/Controllers/OrderItemController.cs` | Remove legacy controller |
| `Web/Views/Order/` | Delete all 15 legacy Order views |
| `Web/Views/OrderItem/` | Delete all 3 legacy OrderItem views |

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
- [x] `PaymentHandler` / `CouponHandler` / `ItemHandler` — no changes required; legacy handlers stop being called after switch, removed in Step 8
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

*Last reviewed: 2026-03-25 · ADRs: [ADR-0014](../adr/0014-sales-orders-bc-design.md), [ADR-0024](../adr/0024-controller-routing-strategy.md)*
