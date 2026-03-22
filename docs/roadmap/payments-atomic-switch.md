# Roadmap: Sales/Payments BC — DB Migrations + Atomic Switch

> ADR: [ADR-0015](../adr/0015-sales-payments-bc-design.md) — Sales/Payments BC Design
> Status: ✅ Switch live — Domain ✅ Application ✅ Infrastructure ✅ Unit tests ✅ Integration tests ✅ DB migration ✅ approved
> **Unblocked by**: Sales/Orders atomic switch ✅ complete

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

| Migration | Context | Creates | Status |
|---|---|---|---|
| `InitPaymentsSchema` | `PaymentsDbContext` | `payments.Payments` table with `RowVersion`, `UNIQUE(OrderId)`, `Status` index | ✅ Done — **approved** |

> ~~`AddOrderCancellationFields`~~ — **Not needed.** The new Orders BC (`sales.Orders`) tracks cancellation
> via the `Status` column (`OrderStatus.Cancelled`) and `OrderEvents` table (with `OccurredAt` timestamps).
> There are no `IsCancelled`/`CancelledAt` columns in the new schema — and the legacy `dbo.Orders` table
> remains untouched during the parallel-change period.

### Step 2 — Integration tests

| File | Coverage |
|---|---|
| `IntegrationTests/Sales/Payments/PaymentServiceTests.cs` | `InitializePaymentAsync`, `ConfirmAsync` (Pending → Confirmed), `ExpireAsync` (Pending → Expired), duplicate `OrderId` guard |
| `IntegrationTests/Sales/Payments/OrderPlacedHandlerTests.cs` | Payment created on `OrderPlaced`; `PaymentWindowExpiredJob` scheduled |
| `IntegrationTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs` | `Order.Status` transitions to `PaymentConfirmed` on `PaymentConfirmed` message |
| `IntegrationTests/Sales/Orders/OrderPaymentExpiredHandlerTests.cs` | `Order.Status` transitions to `Cancelled` on `PaymentExpired` message |
| All existing integration tests | Must still pass |

### Step 3 — Activate new services (controller swap)

> Routing strategy: [ADR-0024](../adr/0024-controller-routing-strategy.md)
> - **Web**: New `Areas/Sales/Controllers/PaymentsController.cs` with `[Area("Sales")]`. Legacy `PaymentController` stays active.
> - **API**: In-place swap on existing payment endpoints.
>
> ⚠️ **Do not remove `PaymentHandler` yet.** Keep it compiled and registered; it is no longer called
> but remains as a fallback until the new code has been validated through at least one payment cycle.

| File | Action |
|---|---|
| `Web/Areas/Sales/Controllers/PaymentsController.cs` | Create `[Area("Sales")]` controller injecting `Application.Sales.Payments.Services.IPaymentService`. Actions: `Index`, `Create`, `Edit`, `Details`, `MyPayments` |
| `Web/Areas/Sales/Views/Payments/` | Create views using new `Application.Sales.Payments.ViewModels.*` |
| `Web/Views/Shared/_Layout.cshtml` | Update Payment nav links: `/Payment/ViewMyPayments` → `/Sales/Payments/MyPayments` |
| `Infrastructure/DependencyInjection.cs` | Confirm `PaymentsDbContext`, `IPaymentRepository` (Payments), `PaymentWindowExpiredJob` are registered |

### Step 4 — Verification

| Action |
|---|
| `dotnet build` — green |
| `dotnet test` — full test suite green |
| Update `bounded-context-map.md` — move Sales/Payments to Active (switch live, cleanup pending) |
| Monitor for one full payment window cycle before cleanup |

### Step 5 — Legacy cleanup (deferred — post-production validation)

> Execute only after `PaymentWindowExpiredJob` (Payments) has been confirmed firing correctly in production.

| File | Action |
|---|---|
| `Application/Services/Payments/PaymentHandler.cs` | Remove `CreatePayment()` and `HandlePaymentChangesOnOrder()` methods (or delete file if empty) |
| `Application/DependencyInjection.cs` | Remove legacy `IPaymentHandler` / `PaymentHandler` DI registration |

### Step 6 — Retire Inventory `PaymentWindowTimeoutJob` (deferred — after Step 5)

> Deferred until Payments BC has been live for at least one payment window cycle.

---

## Coordinate with

- **Sales/Orders** (`orders-atomic-switch.md`) — must complete first; `IOrderService.MarkAsPaidAsync` and `CancelOrderAsync` must be the active implementations.
- **Sales/Coupons** (ADR-0016) and **Sales/Fulfillment** (ADR-0017) — unblocked after this switch completes.

---

## Acceptance criteria

### Switch live (Steps 1–4)

- [x] `payments.Payments` table exists with `RowVersion`, `UNIQUE(OrderId)`, `Status` index
- [x] Web: `Areas/Sales/Controllers/PaymentsController.cs` uses `IPaymentService` with `[Area("Sales")]`
- [x] Web: Legacy `PaymentController` untouched (stays as fallback)
- [x] `_Layout.cshtml` payment nav links point to `/Sales/Payments/*`
- [x] Integration tests for PaymentService, OrderPaymentConfirmedHandler, OrderPaymentExpiredHandler pass
- [x] Full test suite green
- [x] `bounded-context-map.md` updated (switch live)

### Cleanup (Steps 5–6 — deferred)

- [ ] `PaymentHandler.CreatePayment()` and `HandlePaymentChangesOnOrder()` removed
- [ ] `IPaymentHandler` / `PaymentHandler` DI registrations removed from `Application/DependencyInjection.cs`
- [ ] `PaymentWindowTimeoutJob` (Inventory) retired
- [ ] `PaymentWindowTimeoutJobTests.cs` (Inventory) removed
- [ ] Full test suite green after cleanup

---

*Last reviewed: 2026-03-26 · ADRs: [ADR-0015](../adr/0015-sales-payments-bc-design.md), [ADR-0024](../adr/0024-controller-routing-strategy.md)*
