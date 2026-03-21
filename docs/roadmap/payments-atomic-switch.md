# Roadmap: Sales/Payments BC — DB Migrations + Atomic Switch

> ADR: [ADR-0015](../adr/0015-sales-payments-bc-design.md) — Sales/Payments BC Design
> Status: 🟡 In progress — Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ Integration tests ✅ DB migration ✅ approved
> **Blocked by**: Sales/Orders atomic switch (`orders-atomic-switch.md`)

---

## What is already done

| Layer | Status |
|---|---|
| Domain — Payment aggregate, PaymentId, PaymentOrderId, PaymentStatus, domain events | ✅ Done |
| Application — OrderPlacedHandler, PaymentWindowExpiredJob, PaymentService, DTOs, ViewModels, DI | ✅ Done |
| Application — Orders BC extensions: OrderPaymentConfirmedHandler, OrderPaymentExpiredHandler, Cancel() | ✅ Done |
| Infrastructure — PaymentsDbContext, PaymentConfiguration, PaymentRepository, DI | ✅ Done |
| Unit tests — PaymentAggregateTests, OrderPlacedHandlerTests, PaymentWindowExpiredJobTests, OrderPaymentConfirmedHandlerTests, OrderPaymentExpiredHandlerTests | ✅ Done |

---

## Gate condition

Sales/Orders atomic switch must be complete before this switch executes — the new
`IOrderService.CancelOrderAsync` and `IOrderService.MarkAsPaidAsync` must be the active
implementations before the legacy `PaymentHandler` is removed.

---

## Pending steps

### Step 1 — DB migrations (require approval)

Two migrations must be submitted and approved together per [migration policy](../../.github/instructions/migration-policy.md):

| Migration | Context | Creates |
|---|---|---|
| `InitPaymentsSchema` | `PaymentsDbContext` | `payments.Payments` table with `RowVersion`, `UNIQUE(OrderId)`, `Status` index |
| `AddOrderCancellationFields` | `OrdersDbContext` | `IsCancelled bit NOT NULL DEFAULT 0`, `CancelledAt datetime2 NULL` on `sales.Orders` |

```
dotnet ef migrations add InitPaymentsSchema --project Infrastructure --context PaymentsDbContext
dotnet ef migrations add AddOrderCancellationFields --project Infrastructure --context OrdersDbContext
```

> Both migrations are non-destructive on existing data. Submit together as a coordinated PR.

### Step 2 — Integration tests

| File | Coverage |
|---|---|
| `IntegrationTests/Sales/Payments/PaymentServiceTests.cs` | `InitializePaymentAsync`, `ConfirmAsync` (Pending → Confirmed), `ExpireAsync` (Pending → Expired), duplicate `OrderId` guard |
| `IntegrationTests/Sales/Payments/OrderPlacedHandlerTests.cs` | Payment created on `OrderPlaced`; `PaymentWindowExpiredJob` scheduled |
| `IntegrationTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs` | `Order.Status` transitions to `PaymentConfirmed` on `PaymentConfirmed` message |
| `IntegrationTests/Sales/Orders/OrderPaymentExpiredHandlerTests.cs` | `Order.Status` transitions to `Cancelled` on `PaymentExpired` message |
| All existing integration tests | Must still pass |

### Step 3 — Atomic switch

| File | Action |
|---|---|
| `Web/Controllers/PaymentController.cs` | Replace legacy `PaymentHandler.CreatePayment()` call with `IPaymentService.ConfirmAsync(dto)` |
| `Web/Controllers/OrderController.cs` | Replace `PaymentHandler.HandlePaymentChangesOnOrder()` call with `IOrderService.MarkAsPaidAsync(orderId)` (already injected after Orders switch) |
| `Application/Services/Payments/PaymentHandler.cs` | Remove `CreatePayment()` and `HandlePaymentChangesOnOrder()` methods (or remove file if no other methods remain) |
| `Application/DependencyInjection.cs` | Remove legacy `IPaymentHandler` / `PaymentHandler` DI registration |
| `Infrastructure/DependencyInjection.cs` | Confirm `PaymentsDbContext`, `IPaymentRepository` (Payments), `PaymentWindowExpiredJob` are registered |

### Step 4 — Retire Inventory `PaymentWindowTimeoutJob` (post-switch)

> This step is deferred until the Payments BC has been live for at least one payment window cycle
> to confirm `PaymentWindowExpiredJob` (Payments) is firing correctly.

| File | Action |
|---|---|
| `Application/Inventory/Availability/Handlers/PaymentWindowTimeoutJob.cs` | Remove — replaced by Payments BC `PaymentWindowExpiredJob` |
| `Application/Inventory/Availability/Services/Extensions.cs` | Remove `PaymentWindowTimeoutJob` registration |
| Unit tests | Remove `PaymentWindowTimeoutJobTests.cs` (Inventory) |

### Step 5 — Verification

| Action |
|---|
| `dotnet build` — green |
| `dotnet test` — full test suite green |
| Update `bounded-context-map.md` — move Sales/Payments to Completed BCs |

---

## Coordinate with

- **Sales/Orders** (`orders-atomic-switch.md`) — must complete first; `IOrderService.MarkAsPaidAsync` and `CancelOrderAsync` must be the active implementations.
- **Sales/Coupons** (ADR-0016) and **Sales/Fulfillment** (ADR-0017) — unblocked after this switch completes.

---

## Acceptance criteria

- [ ] `payments.Payments` table exists with `RowVersion`, `UNIQUE(OrderId)`, `Status` index
- [ ] `IsCancelled` and `CancelledAt` columns exist on `sales.Orders`
- [ ] `PaymentController` (Web) injects `IPaymentService` — no `PaymentHandler.CreatePayment()` call
- [ ] `OrderController` (Web) injects `IOrderService` (Sales.Orders) — no `PaymentHandler.HandlePaymentChangesOnOrder()` call
- [ ] `PaymentHandler.CreatePayment()` and `HandlePaymentChangesOnOrder()` removed
- [ ] `IPaymentHandler` / `PaymentHandler` DI registrations removed from `Application/DependencyInjection.cs`
- [ ] Integration tests for PaymentService, OrderPaymentConfirmedHandler, OrderPaymentExpiredHandler pass
- [ ] Full test suite green
- [ ] `bounded-context-map.md` updated

---

*Last reviewed: 2026-03-12 · ADR: [ADR-0015](../adr/0015-sales-payments-bc-design.md)*
