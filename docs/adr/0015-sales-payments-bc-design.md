# ADR-0015: Sales/Payments BC — Payment Aggregate Design

## Status
Proposed

## Date
2026-03-09

## Context

The legacy `PaymentHandler` tightly couples the Payments and Orders BCs. Two methods expose
the coupling directly:

```csharp
// Application/Services/Payments/PaymentHandler.cs — current state
public void CreatePayment(int orderId)
{
    var order = _orderService.GetOrderById(orderId);  // cross-BC synchronous read
    var payment = new Payment { OrderId = orderId, ... };
    _paymentRepo.AddPayment(payment);

    order.IsPaid = false;                             // mutates Order from Payments BC
    order.PaymentId = payment.Id;
    _orderService.UpdateOrderPayment(order);          // cross-BC synchronous write
}

public void HandlePaymentChangesOnOrder(Payment payment, Order order)
{
    order.IsPaid = payment.IsPaid;                    // mutates Order directly
    order.Payment = null;
    _orderService.UpdateOrder(order);                 // cross-BC synchronous write
}
```

Three critical gaps remain after the Sales/Orders BC was implemented (ADR-0014):

**Gap 1 — Payment not initialized at order placement.**
`OrderService.PlaceOrderAsync` publishes `OrderPlaced` but nothing subscribes to create a
`Payment` entity. The Inventory BC subscribes to `OrderPlaced` to create a `Reservation`, but
the Payments domain has no parallel handler. Any order placed in the new BC has no associated
payment record until the legacy `PaymentHandler.CreatePayment()` is called from the old
controller — which will break after the atomic switch removes the legacy path.

**Gap 2 — No payment timeout in the Orders or Payments BC.**
`OrderPlaced.ExpiresAt` is set at `DateTime.UtcNow.AddDays(3)` (the payment window). The
Inventory BC already schedules `PaymentWindowTimeoutJob` at this time to release the stock
reservation (ADR-0011). However, neither the Orders BC nor the Payments BC schedules a timeout
to expire the `Payment` entity or cancel the `Order` when the window closes unpaid.

**Gap 3 — No path from `PaymentConfirmed` to `Order.MarkAsPaid`.**
`PaymentConfirmed` is already published by legacy code and consumed by the Inventory BC
(`PaymentConfirmedHandler` → `Reservation.Confirm()`). The Orders BC has `Order.MarkAsPaid(int paymentId)`
but no handler subscribing to `PaymentConfirmed` to call it. `PaymentConfirmed` also lacks
`int PaymentId`, which `Order.MarkAsPaid` requires as its argument.

**Gap 4 — No `Order.Cancel()` and no `OrderCancelled` publisher in the new Orders BC.**
The `OrderCancelled` integration message exists and is consumed by Inventory. But there is no
`Order.Cancel()` domain method and no code path that publishes `OrderCancelled` from the new
Orders BC.

The result-based error handling pattern (`PlaceOrderResult`, `OrderOperationResult`) and
the parallel-change strategy established in ADR-0014 are maintained unchanged.

## Decision

We will build the Sales/Payments bounded context as a parallel implementation per the
Parallel Change strategy (ADR-0002). The Payments BC owns payment lifecycle (Pending →
Confirmed / Expired) and schedules the TimeManagement payment timeout. The Orders BC is
extended with `Cancel()` and the two inbound message handlers it was missing.

### 1. Strongly-typed IDs

`PaymentId(int)` — sealed record extending `TypedId<int>` (ADR-0006). `int` ID to align with
the existing legacy `Payment.Id` MSSQL identity column.

`PaymentOrderId(int)` — Payments-local typed wrapper for the Orders order PK. Same struct
pattern as `ReservationOrderId` in Inventory and `PresaleProductId` in Presale.

Both live in `Domain/Sales/Payments/`.

### 2. `Payment` aggregate — state machine

```csharp
// Domain/Sales/Payments/Payment.cs
public class Payment
{
    public PaymentId Id { get; private set; }
    public PaymentOrderId OrderId { get; private set; }       // FK to Orders; no navigation property
    public decimal TotalAmount { get; private set; }          // captured from OrderPlaced.TotalAmount
    public int CurrencyId { get; private set; }               // captured from OrderPlaced.CurrencyId
    public PaymentStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }           // = OrderPlaced.ExpiresAt — single source of truth
    public DateTime? ConfirmedAt { get; private set; }
    public string? TransactionRef { get; private set; }       // external gateway reference; null for cash/legacy
    public byte[] RowVersion { get; private set; } = default!;

    private Payment() { }

    public static Payment Create(
        PaymentOrderId orderId, decimal totalAmount, int currencyId, DateTime expiresAt)
    {
        if (orderId is null || orderId.Value <= 0)
            throw new DomainException("OrderId must be positive.");
        if (totalAmount < 0)
            throw new DomainException("TotalAmount cannot be negative.");
        if (currencyId <= 0)
            throw new DomainException("CurrencyId must be positive.");

        return new Payment
        {
            OrderId = orderId,
            TotalAmount = totalAmount,
            CurrencyId = currencyId,
            Status = PaymentStatus.Pending,
            ExpiresAt = expiresAt
        };
    }

    // Called when gateway confirms payment receipt
    public PaymentConfirmedEvent Confirm(string? transactionRef = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot confirm payment — current status is '{Status}'.");

        Status = PaymentStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        TransactionRef = transactionRef;
        return new PaymentConfirmedEvent(Id.Value, OrderId.Value, DateTime.UtcNow);
    }

    // Called by PaymentWindowExpiredJob at ExpiresAt if still Pending
    public PaymentExpiredEvent Expire()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException($"Cannot expire payment — current status is '{Status}'.");

        Status = PaymentStatus.Expired;
        return new PaymentExpiredEvent(Id.Value, OrderId.Value, DateTime.UtcNow);
    }

    // Reverts a confirmed payment (triggered by Refunds BC or operator action)
    public RefundIssuedEvent IssueRefund(int productId, int quantity)
    {
        if (Status != PaymentStatus.Confirmed)
            throw new DomainException($"Cannot refund payment — current status is '{Status}'.");
        if (quantity <= 0)
            throw new DomainException("Refund quantity must be positive.");

        Status = PaymentStatus.Refunded;
        return new RefundIssuedEvent(Id.Value, OrderId.Value, productId, quantity, DateTime.UtcNow);
    }
}

public enum PaymentStatus
{
    Pending,    // payment initialized — awaiting customer payment
    Confirmed,  // gateway confirmed — stock reservation upgraded
    Expired,    // payment window closed without payment
    Refunded    // confirmed payment reversed
}
```

**Terminal state diagram:**

```
Pending ──Confirm()──► Confirmed ──IssueRefund()──► Refunded
Pending ──Expire()───► Expired
```

`RowVersion` is mapped with `.IsRowVersion()` — SQL Server auto-manages. The Payments BC
operates at much lower concurrency than Inventory so optimistic locking is a low-risk addition.

### 3. Domain events (aggregate return values)

All domain events are `record` types in past tense, returned from state-transition methods.
They are **not** integration messages — they carry aggregate-internal data only.

```
Domain/Sales/Payments/Events/
  PaymentConfirmedEvent.cs    — returned by Payment.Confirm()
  PaymentExpiredEvent.cs      — returned by Payment.Expire()
  RefundIssuedEvent.cs        — returned by Payment.IssueRefund()
```

### 4. Integration messages

**Existing messages (publisher: Payments BC) — kept unchanged:**

| Message | File | Current fields | Change |
|---|---|---|---|
| `PaymentConfirmed` | `Application/Sales/Payments/Messages/PaymentConfirmed.cs` | `OrderId, Items, OccurredAt` | Add `int PaymentId` — required by `Order.MarkAsPaid(paymentId)` |
| `RefundApproved` | `Application/Sales/Payments/Messages/RefundApproved.cs` | `OrderId, ProductId, Quantity, OccurredAt` | Unchanged |

`PaymentConfirmed` requires adding `int PaymentId` (the new `Payment` entity's DB-generated ID).
All existing consumers (`PaymentConfirmedHandler` in Inventory) access `message.OrderId` and
`message.Items` by name — the new field is additive and does not break them.

**New message (publisher: Payments BC):**

```csharp
// Application/Sales/Payments/Messages/PaymentExpired.cs
public record PaymentExpired(
    int PaymentId,
    int OrderId,
    DateTime OccurredAt) : IMessage;
```

Published by `PaymentWindowExpiredJob` after `Payment.Expire()` succeeds.

### 5. `OrderPlaced` message — add `TotalAmount` and `CurrencyId`

```csharp
// Application/Sales/Orders/Messages/OrderPlaced.cs — updated
public record OrderPlaced(
    int OrderId,
    IReadOnlyList<OrderPlacedItem> Items,
    string UserId,
    DateTime ExpiresAt,
    DateTime OccurredAt,
    decimal TotalAmount,   // ← new: Order.Cost at placement time
    int CurrencyId         // ← new: Order.CurrencyId at placement time
) : IMessage;
```

`TotalAmount` allows `OrderPlacedHandler` (Payments BC) to create `Payment` without a
cross-BC read-back to Orders. `CurrencyId` is stored on `Payment` for future multi-currency
payment processing. Both values are available in `OrderService.PlaceOrderAsync` from
`orderWithItems.Cost` and `orderWithItems.CurrencyId` immediately after `CalculateCost()`.

**Impact on existing consumers:**

| Consumer | Change needed |
|---|---|
| `OrderPlacedHandler` (Inventory) | None — accesses `message.Items`, `message.ExpiresAt`, `message.OrderId` by name |
| `OrderPlacedSnapshotHandler` (Orders) | None — accesses `message.OrderId` by name |
| `OrderService.PlaceOrderAsync` | Must pass `TotalAmount = orderWithItems.Cost, CurrencyId = orderWithItems.CurrencyId` |

### 6. `OrderPlacedHandler` — Payments BC, initializes payment + schedules timeout

```csharp
// Application/Sales/Payments/Handlers/OrderPlacedHandler.cs
internal sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
{
    private readonly IPaymentRepository _paymentRepo;
    private readonly IDeferredJobScheduler _scheduler;

    public async Task HandleAsync(OrderPlaced message, CancellationToken ct = default)
    {
        var payment = Payment.Create(
            new PaymentOrderId(message.OrderId),
            message.TotalAmount,
            message.CurrencyId,
            message.ExpiresAt);

        await _paymentRepo.AddAsync(payment, ct);

        await _scheduler.ScheduleAsync(
            PaymentWindowExpiredJob.JobTaskName,
            payment.Id.Value.ToString(),
            message.ExpiresAt,
            ct);
    }
}
```

`Payment.Id` is DB-generated (`IDENTITY`). `AddAsync` persists the entity and populates `Id`
before `ScheduleAsync` is called. `EntityId` encodes only `paymentId` — the job loads the
full `Payment` and `OrderId` at execution time.

### 7. `PaymentWindowExpiredJob` — Payments BC, fires at `ExpiresAt`

```csharp
// Application/Sales/Payments/Handlers/PaymentWindowExpiredJob.cs
internal sealed class PaymentWindowExpiredJob : IScheduledTask
{
    public const string JobTaskName = "PaymentWindowExpiredJob";
    public string TaskName => JobTaskName;

    private readonly IPaymentRepository _paymentRepo;
    private readonly IMessageBroker _broker;

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.EntityId is null || !int.TryParse(context.EntityId, out var paymentId))
        {
            context.ReportFailure($"Invalid EntityId: '{context.EntityId}'.");
            return;
        }

        var payment = await _paymentRepo.GetByIdAsync(paymentId, cancellationToken);
        if (payment is null)
        {
            context.ReportSuccess("No-op: payment not found (already deleted or never created).");
            return;
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            context.ReportSuccess($"No-op: payment {paymentId} is already '{payment.Status}' — not expired.");
            return;
        }

        var @event = payment.Expire();
        await _paymentRepo.UpdateAsync(payment, cancellationToken);

        await _broker.PublishAsync(new PaymentExpired(@event.PaymentId, @event.OrderId, @event.OccurredAt));

        context.ReportSuccess($"Payment {paymentId} expired for order {payment.OrderId.Value}.");
    }
}
```

`EntityId` = `payment.Id.Value.ToString()` — single integer, unlike Inventory's
`PaymentWindowTimeoutJob` which encodes `"{orderId}:{productId}:{quantity}"`.

**Relationship to Inventory's `PaymentWindowTimeoutJob`:**

Both jobs fire at the same `ExpiresAt`. They are independent and operate on different
aggregates:
- Inventory's job: `StockItem.Release(qty)` + DELETE `Reservation` row (fast, direct path)
- Payments BC's job: `Payment.Expire()` → publish `PaymentExpired` → Orders BC cancels → publish `OrderCancelled` → Inventory `OrderCancelledHandler` tries to release (no-op if already released)

The Inventory `OrderCancelledHandler` finding no `Reservation` row is a defined no-op
(idempotent by design per ADR-0011). Keeping both jobs provides belt-and-suspenders reliability:
if Inventory's job fires first, the chain from Payments is a safe no-op. If Payments' job fires
first, Inventory's job is also a safe no-op. No race condition exists because both paths are
idempotent.

### 8. Orders BC extensions — two new inbound handlers

Both handlers live in `Application/Sales/Orders/Handlers/` and are registered in
`Application/Sales/Orders/Services/Extensions.cs`.

#### `OrderPaymentConfirmedHandler` — marks order as paid

```csharp
// Application/Sales/Orders/Handlers/OrderPaymentConfirmedHandler.cs
internal sealed class OrderPaymentConfirmedHandler : IMessageHandler<PaymentConfirmed>
{
    private readonly IOrderRepository _orderRepo;

    public async Task HandleAsync(PaymentConfirmed message, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
        if (order is null)
        {
            return;   // order deleted between placement and payment — no-op
        }

        if (order.IsPaid)
        {
            return;   // idempotent — already marked paid (e.g. handler re-run)
        }

        order.MarkAsPaid(message.PaymentId);
        await _orderRepo.UpdateAsync(order, ct);
    }
}
```

No integration message is published here — `PaymentConfirmed` is the authoritative event.
The `OrderPaid` domain event returned by `MarkAsPaid` is an internal aggregate record; it is
appended to the `sales.OrderEvents` audit log via `AppendEvent` inside `MarkAsPaid`.

#### `OrderPaymentExpiredHandler` — cancels order

```csharp
// Application/Sales/Orders/Handlers/OrderPaymentExpiredHandler.cs
internal sealed class OrderPaymentExpiredHandler : IMessageHandler<PaymentExpired>
{
    private readonly IOrderRepository _orderRepo;
    private readonly IMessageBroker _broker;

    public async Task HandleAsync(PaymentExpired message, CancellationToken ct = default)
    {
        var order = await _orderRepo.GetByIdWithItemsAsync(message.OrderId, ct);
        if (order is null)
        {
            return;   // already deleted
        }

        if (order.IsCancelled || order.IsPaid)
        {
            return;   // idempotent — already in a terminal state
        }

        var items = order.OrderItems
            .Select(i => new OrderCancelledItem(i.ItemId.Value, i.Quantity))
            .ToList();

        order.Cancel();
        await _orderRepo.UpdateAsync(order, ct);

        await _broker.PublishAsync(new OrderCancelled(message.OrderId, items, DateTime.UtcNow));
    }
}
```

`OrderCancelled` is already consumed by Inventory's `OrderCancelledHandler`. Publishing it here
closes the loop: `PaymentExpired` → `OrderCancelled` → Inventory releases reservation (if not
already released by Inventory's own `PaymentWindowTimeoutJob`).

### 9. `Order.Cancel()` — new domain method on Orders aggregate

```csharp
// Domain/Sales/Orders/Order.cs — additions

public bool IsCancelled { get; private set; }
public DateTime? CancelledAt { get; private set; }

public void Cancel()
{
    if (IsCancelled)
        throw new DomainException($"Order '{Id?.Value}' is already cancelled.");
    if (IsPaid)
        throw new DomainException($"Order '{Id?.Value}' cannot be cancelled — already paid.");
    if (IsDelivered)
        throw new DomainException($"Order '{Id?.Value}' cannot be cancelled — already delivered.");

    IsCancelled = true;
    CancelledAt = DateTime.UtcNow;
    AppendEvent(OrderEventType.OrderCancelled);
}
```

`OrderEventType.OrderCancelled` is added to the `OrderEventTypes` static constants class.

`IsCancelled` and `CancelledAt` are mapped as new columns in `sales.Orders` — a **non-additive
migration** (new nullable columns). See §12.

### 10. `IOrderService` — two new methods

```csharp
// Application/Sales/Orders/Services/IOrderService.cs — additions
Task<OrderOperationResult> MarkAsPaidAsync(int orderId, int paymentId, CancellationToken ct = default);
Task<OrderOperationResult> CancelOrderAsync(int orderId, CancellationToken ct = default);
```

`OrderOperationResult` gains a new value: `AlreadyCancelled`.

`MarkAsPaidAsync` delegates to `Order.MarkAsPaid(paymentId)`. This service method is the
callable entry point for any future API surface (e.g., admin override) and for testing — the
handler `OrderPaymentConfirmedHandler` calls the repository directly to avoid an extra DI layer,
but `IOrderService.MarkAsPaidAsync` provides the public contract.

`CancelOrderAsync` delegates to `Order.Cancel()` and publishes `OrderCancelled` via
`IMessageBroker` — it is the general-purpose cancellation path usable by controllers and API
endpoints in addition to the event-driven path from `OrderPaymentExpiredHandler`.

### 11. DB schema (`payments.*`) and own DbContext

```
payments.Payments
  Id               int            PK IDENTITY
  OrderId          int            NOT NULL UNIQUE    ← one payment per order
  TotalAmount      decimal(18,2)  NOT NULL
  CurrencyId       int            NOT NULL
  Status           tinyint        NOT NULL           ← 0=Pending, 1=Confirmed, 2=Expired, 3=Refunded
  ExpiresAt        datetime2      NOT NULL
  ConfirmedAt      datetime2      NULL
  TransactionRef   nvarchar(200)  NULL
  RowVersion       rowversion     NOT NULL
```

`OrderId` has a `UNIQUE` constraint — enforces one `Payment` per `Order` at the DB level. If
`OrderPlacedHandler` fires twice (idempotency failure), the second INSERT fails the constraint
and the infrastructure retry marks the job `DeadLetter`.

```
sales.Orders — new columns (additive migration, see §12)
  IsCancelled      bit            NOT NULL DEFAULT 0
  CancelledAt      datetime2      NULL
```

```csharp
// Infrastructure/Sales/Payments/PaymentsDbContext.cs
internal sealed class PaymentsDbContext : DbContext
{
    public DbSet<Payment> Payments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PaymentsDbContext).Assembly,
            t => t.Namespace?.Contains("Sales.Payments") == true);
    }
}
```

### 12. Migration plan

**Phase 1 — Payments domain layer**
1. `Domain/Sales/Payments/PaymentId.cs` — `sealed record PaymentId(int Value) : TypedId<int>(Value)`
2. `Domain/Sales/Payments/PaymentOrderId.cs` — struct with implicit `int` operator
3. `Domain/Sales/Payments/PaymentStatus.cs` — enum: `Pending`, `Confirmed`, `Expired`, `Refunded`
4. `Domain/Sales/Payments/Events/PaymentConfirmedEvent.cs`, `PaymentExpiredEvent.cs`, `RefundIssuedEvent.cs`
5. `Domain/Sales/Payments/Payment.cs` — `Create`, `Confirm`, `Expire`, `IssueRefund`
6. `Domain/Sales/Payments/IPaymentRepository.cs`

**Phase 2 — Orders aggregate extension**
7. `Domain/Sales/Orders/OrderEventTypes.cs` — add `OrderCancelled` constant
8. `Domain/Sales/Orders/Order.cs` — add `IsCancelled`, `CancelledAt`, `Cancel()` method

**Phase 3 — Integration message updates**
9. `Application/Sales/Orders/Messages/OrderPlaced.cs` — add `decimal TotalAmount`, `int CurrencyId`
10. `Application/Sales/Orders/Services/OrderService.cs` — update `PlaceOrderAsync` to pass `TotalAmount`, `CurrencyId`
11. `Application/Sales/Payments/Messages/PaymentConfirmed.cs` — add `int PaymentId`
12. `Application/Sales/Payments/Messages/PaymentExpired.cs` — new record

**Phase 4 — Application layer (Payments BC)**
13. `Application/Sales/Payments/Handlers/OrderPlacedHandler.cs`
14. `Application/Sales/Payments/Handlers/PaymentWindowExpiredJob.cs`
15. `Application/Sales/Payments/Services/IPaymentService.cs`, `PaymentService.cs`
16. DTOs: `InitializePaymentDto`, `ConfirmPaymentDto` (if manual confirmation is needed via controller)
17. ViewModels: `PaymentVm`, `PaymentDetailsVm`, `PaymentListVm`
18. `Application/Sales/Payments/Services/Extensions.cs` — registers services + handlers + job

**Phase 5 — Application layer (Orders BC extensions)**
19. `Application/Sales/Orders/Services/IOrderService.cs` — add `MarkAsPaidAsync`, `CancelOrderAsync`
20. `Application/Sales/Orders/Services/OrderService.cs` — implement both methods
21. `Application/Sales/Orders/Results/OrderOperationResult.cs` — add `AlreadyCancelled`
22. `Application/Sales/Orders/Handlers/OrderPaymentConfirmedHandler.cs`
23. `Application/Sales/Orders/Handlers/OrderPaymentExpiredHandler.cs`
24. `Application/Sales/Orders/Services/Extensions.cs` — register two new handlers

**Phase 6 — Infrastructure layer (Payments BC)**
25. `Infrastructure/Sales/Payments/PaymentsConstants.cs`, `PaymentsDbContext.cs`, `PaymentsDbContextFactory.cs`
26. `Infrastructure/Sales/Payments/Configurations/PaymentConfiguration.cs`
    — `PaymentId` identity conversion, `PaymentOrderId` conversion, `RowVersion → IsRowVersion()`, `UNIQUE` on `OrderId`
27. `Infrastructure/Sales/Payments/Repositories/PaymentRepository.cs`
28. `Infrastructure/Sales/Payments/Extensions.cs` — registers `AddDbContext<PaymentsDbContext>`, `IDbContextMigrator`, `IPaymentRepository`
29. Register `PaymentWindowExpiredJob` in TimeManagement DI alongside `PaymentWindowTimeoutJob` (Inventory) and `SoftReservationExpiredJob` (Presale)

**Phase 7 — Infrastructure layer (Orders BC schema extension)**
30. `Infrastructure/Sales/Orders/Configurations/OrderConfiguration.cs` — add `IsCancelled` and `CancelledAt` mapping
31. Generate migration for `sales.Orders` extension (new nullable columns) — **separate migration from Orders initial `InitSalesSchema`** per migration policy

**Phase 8 — Unit tests**
32. `UnitTests/Sales/Payments/PaymentAggregateTests.cs` — all state transitions and guards
33. `UnitTests/Sales/Payments/OrderPlacedHandlerTests.cs` — payment created + job scheduled
34. `UnitTests/Sales/Payments/PaymentWindowExpiredJobTests.cs` — null guard, non-Pending guard, happy path
35. `UnitTests/Sales/Orders/OrderAggregateTests.cs` — add `Cancel()` tests
36. `UnitTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs`
37. `UnitTests/Sales/Orders/OrderPaymentExpiredHandlerTests.cs`

**Phase 9 — DB migrations (require approval per migration policy)**
38. `dotnet ef migrations add InitPaymentsSchema --project Infrastructure --context PaymentsDbContext`
    Creates `payments.Payments`.
39. `dotnet ef migrations add AddOrderCancellationFields --project Infrastructure --context OrdersDbContext`
    Adds `IsCancelled bit NOT NULL DEFAULT 0` and `CancelledAt datetime2 NULL` to `sales.Orders`.
40. Submit both migrations for approval — do not apply to production without sign-off.

**Phase 10 — Integration tests and atomic switch**
41. Integration tests for `PaymentService`, `OrderPaymentConfirmedHandler`, `OrderPaymentExpiredHandler`
42. Atomic switch: remove `PaymentHandler.CreatePayment()` + `HandlePaymentChangesOnOrder()` calls from legacy controllers; update `PaymentController` and `OrderController` to use new `IPaymentService` / `IOrderService` APIs
43. Update `bounded-context-map.md` — move Sales/Payments to Completed BCs

## Consequences

### Positive

- **Payment initialization decoupled** — `OrderPlacedHandler` (Payments BC) creates the
  `Payment` entity from the `OrderPlaced` message; no cross-BC synchronous call from Orders.
- **Payment timeout owned by the right BC** — Payments BC schedules `PaymentWindowExpiredJob`
  because the payment window is a payment lifecycle concept, not an order concept. This keeps
  the Orders BC free of `IDeferredJobScheduler` dependency.
- **Order cancellation via events** — `PaymentExpired` → `OrderCancelled` → Inventory release
  follows the established message chain pattern. Every step is idempotent.
- **Belt-and-suspenders stock release** — Inventory's existing `PaymentWindowTimeoutJob` still
  fires at `ExpiresAt` and releases the reservation directly. The new Payments chain provides a
  second path; both are idempotent and neither interferes with the other.
- **`Order.MarkAsPaid` finally wired** — `OrderPaymentConfirmedHandler` completes the missing
  link between the `PaymentConfirmed` integration event and the Orders aggregate state machine.
- **`Order.Cancel()` closes the domain model** — the Orders aggregate can now represent its own
  cancellation state without external mutation.
- **`PaymentConfirmed` carries `PaymentId`** — `Order.MarkAsPaid(paymentId)` has its required
  argument; Inventory's `PaymentConfirmedHandler` is unaffected (additive change).
- **`OrderPlaced` carries `TotalAmount` and `CurrencyId`** — Payments BC does not need a
  cross-BC read to know how much to charge; future analytics can use this field directly.

### Negative

- `PaymentConfirmed` record gains `int PaymentId` — **breaking constructor change** in the
  publisher. Only `PaymentService.ConfirmAsync` constructs it; existing consumers are unaffected.
- `OrderPlaced` record gains `TotalAmount` and `CurrencyId` — **breaking constructor change**
  in `OrderService.PlaceOrderAsync`. Existing consumers unaffected (named field access).
- `sales.Orders` requires a second migration (`AddOrderCancellationFields`) for the
  `IsCancelled` / `CancelledAt` columns. This migration must coordinate with `InitSalesSchema`
  approval timing.
- Two `PaymentWindowTimeoutJob` variants now exist in two different BCs (Inventory and Payments).
  They serve different purposes but share the same conceptual trigger (`ExpiresAt`). The
  duplication must be documented so future maintainers do not remove one thinking it is redundant.

### Risks & mitigations

- **Risk**: `OrderPlacedHandler` (Payments) fails after `AddAsync` but before `ScheduleAsync` —
  payment exists but no expiry job is scheduled.
  **Mitigation**: If the job is never scheduled, the payment stays `Pending` indefinitely. A
  `SnapshotPendingPaymentsJob` (future sweep) can detect payments past `ExpiresAt` and expire
  them. Alternatively, the handler is wrapped in a DB transaction with `ScheduleAsync` inside
  the same scope.
- **Risk**: `PaymentWindowExpiredJob` fires after the customer has already paid (race condition:
  gateway callback arrives just before expiry).
  **Mitigation**: `Payment.Expire()` throws `DomainException` if `Status != Pending`. The job
  catches this and reports success as a no-op — same pattern as Inventory's
  `PaymentWindowTimeoutJob` checking `Status == Confirmed`.
- **Risk**: `PaymentConfirmed` carries `PaymentId` but the legacy `PaymentHandler` does not set
  it correctly before the atomic switch.
  **Mitigation**: The atomic switch PR updates `PaymentHandler.CreatePayment()` to use the new
  `IPaymentService.ConfirmAsync` which publishes the message with the correct `PaymentId`.
- **Risk**: `UNIQUE` constraint on `payments.Payments.OrderId` causes duplicate-key exception
  if `OrderPlacedHandler` is replayed by the messaging infrastructure retry.
  **Mitigation**: The infrastructure dead-letter threshold (default 3 retries) will surface the
  duplicate to `DeadLetter` state. An operator can inspect the `DeadLetter` row and confirm the
  original payment creation succeeded.
- **Risk**: `IsCancelled` + `CancelledAt` migration runs on production before the Orders BC is
  switched — adds nullable columns to a live table.
  **Mitigation**: Adding a `NOT NULL DEFAULT 0` and `NULL` column to an existing table is
  non-destructive in SQL Server. The legacy code writes neither column; after migration the
  columns are default-valued. Safe to run independently of the switch.

## Alternatives considered

- **Orders BC schedules the payment timeout** — rejected. The payment window is a payment
  lifecycle concept (`Payment.ExpiresAt`). Having `OrderService.PlaceOrderAsync` inject
  `IDeferredJobScheduler` and schedule an expiry job would put scheduling responsibility in the
  wrong BC. If the Payments BC is ever extracted to a separate service, the Orders BC would
  still be left holding a scheduler dependency for a payment concern.
- **Remove Inventory's `PaymentWindowTimeoutJob` and rely solely on the Payments chain** —
  rejected. The Inventory job provides a fast, direct stock-release path that does not require
  the three-hop `PaymentExpired → OrderCancelled → OrderCancelledHandler` chain. Under message
  delivery failures, inventory would be locked for longer. Belt-and-suspenders is preferred for
  the stock release path.
- **Single `OrderPaymentExpiredJob` in Orders BC (no Payments BC timer)** — the initial report
  proposal. Rejected in favour of the current design because the payment window belongs to
  `Payment.ExpiresAt`, not to `Order`. Keeping the timer in Payments BC makes the data model
  self-consistent and keeps Orders BC free of scheduler infrastructure.
- **Add a separate `PaymentInitialized` message instead of reusing `OrderPlaced`** — rejected.
  `OrderPlaced` already carries `ExpiresAt`, `TotalAmount`, `CurrencyId`, and `OrderId` — all
  data Payments needs. A separate message would duplicate these fields and add an extra handler
  registration with no additional decoupling benefit.
- **Store only `OrderId` on `Payment`, derive amount via `IOrderService`** — rejected. Cross-BC
  synchronous reads at initialization time introduce coupling and latency. Capturing
  `TotalAmount` and `CurrencyId` in `OrderPlaced` is the established pattern (same as
  `SoftReservation` captures `UnitPrice` at checkout initiation).
- **`IssueRefund` stays on the `Payment` aggregate** vs. moving to a separate `Refunds` BC —
  the legacy `Refund` aggregate exists in `Domain/Model/Refund.cs`. This ADR defers the full
  Refunds BC migration and provides only a minimal `IssueRefund` state transition on `Payment`
  to keep `PaymentStatus` complete. A future ADR will migrate `Refund` as its own BC.

## Conformance checklist

### Payment domain aggregate
- [ ] `Payment` lives under `Domain/Sales/Payments/` with namespace `ECommerceApp.Domain.Sales.Payments`
- [ ] All `Payment` properties use `private set`
- [ ] `Payment` has a `private Payment()` parameterless constructor for EF Core
- [ ] `Payment.Create(PaymentOrderId, decimal totalAmount, int currencyId, DateTime expiresAt)` static factory exists
- [ ] `Payment.Confirm(string? transactionRef)` returns `PaymentConfirmedEvent` domain event
- [ ] `Payment.Expire()` returns `PaymentExpiredEvent` domain event
- [ ] `Payment.IssueRefund(int productId, int quantity)` returns `RefundIssuedEvent` domain event
- [ ] `Payment.Create` throws `DomainException` for `totalAmount < 0` and non-positive IDs
- [ ] `Payment.Confirm` throws `DomainException` if `Status != Pending`
- [ ] `Payment.Expire` throws `DomainException` if `Status != Pending`
- [ ] `Payment.IssueRefund` throws `DomainException` if `Status != Confirmed`
- [ ] `PaymentStatus` enum has exactly four values: `Pending`, `Confirmed`, `Expired`, `Refunded`
- [ ] `PaymentId(int)` is a `sealed record` extending `TypedId<int>(Value)`
- [ ] `PaymentOrderId(int)` is a struct with implicit operator to/from `int`
- [ ] Domain events `PaymentConfirmedEvent`, `PaymentExpiredEvent`, `RefundIssuedEvent` are `record` types in `Domain/Sales/Payments/Events/`
- [ ] `IPaymentRepository` lives in `Domain/Sales/Payments/`
- [ ] No cross-BC navigation properties on `Payment` — `PaymentOrderId` is a typed value wrapper only

### Integration messages
- [ ] `PaymentExpired` record exists in `Application/Sales/Payments/Messages/` with fields: `int PaymentId`, `int OrderId`, `DateTime OccurredAt`; implements `IMessage`
- [ ] `PaymentConfirmed` record has `int PaymentId` added — `OrderId`, `Items`, `OccurredAt` unchanged
- [ ] `OrderPlaced` record has `decimal TotalAmount` and `int CurrencyId` added — all existing fields unchanged
- [ ] `OrderService.PlaceOrderAsync` passes `TotalAmount = orderWithItems.Cost` and `CurrencyId = orderWithItems.CurrencyId` in the `new OrderPlaced(...)` constructor call

### Orders aggregate extensions
- [ ] `Order` has `bool IsCancelled { get; private set; }` property
- [ ] `Order` has `DateTime? CancelledAt { get; private set; }` property
- [ ] `Order.Cancel()` throws `DomainException` if already cancelled, paid, or delivered
- [ ] `Order.Cancel()` sets `IsCancelled = true`, `CancelledAt = DateTime.UtcNow`, appends `OrderEventType.OrderCancelled`
- [ ] `OrderEventType` enum has `OrderCancelled` value — stored as string via `HasConversion<string>()` in `OrderEventConfiguration` (consistent with ADR-0014 implementation)
- [ ] `IOrderService` has `MarkAsPaidAsync(int orderId, int paymentId, CancellationToken ct)` → `Task<OrderOperationResult>`
- [ ] `IOrderService` has `CancelOrderAsync(int orderId, CancellationToken ct)` → `Task<OrderOperationResult>`
- [ ] `OrderOperationResult` enum has `AlreadyCancelled` value

### Payments application handlers and job
- [ ] `OrderPlacedHandler` (Payments BC) is `internal sealed`, implements `IMessageHandler<OrderPlaced>`
- [ ] `OrderPlacedHandler` calls `Payment.Create(...)`, `IPaymentRepository.AddAsync(...)`, then `IDeferredJobScheduler.ScheduleAsync(PaymentWindowExpiredJob.JobTaskName, payment.Id.Value.ToString(), message.ExpiresAt, ct)`
- [ ] `PaymentWindowExpiredJob` declares `public const string JobTaskName = "PaymentWindowExpiredJob"`
- [ ] `PaymentWindowExpiredJob.ExecuteAsync` guards `context.EntityId` for null and parse failure, calling `context.ReportFailure(...)` and returning early
- [ ] `PaymentWindowExpiredJob.ExecuteAsync` is a no-op (`ReportSuccess`) if payment not found or not `Pending`
- [ ] `PaymentWindowExpiredJob.ExecuteAsync` calls `Payment.Expire()` then publishes `PaymentExpired` on happy path
- [ ] `PaymentWindowExpiredJob` is registered in TimeManagement DI alongside `PaymentWindowTimeoutJob` (Inventory) and `SoftReservationExpiredJob` (Presale)

### Orders application handlers
- [ ] `OrderPaymentConfirmedHandler` is `internal sealed`, implements `IMessageHandler<PaymentConfirmed>`
- [ ] `OrderPaymentConfirmedHandler` is a no-op if order not found or already paid
- [ ] `OrderPaymentConfirmedHandler` calls `order.MarkAsPaid(message.PaymentId)` and updates the order
- [ ] `OrderPaymentExpiredHandler` is `internal sealed`, implements `IMessageHandler<PaymentExpired>`
- [ ] `OrderPaymentExpiredHandler` is a no-op if order not found, already cancelled, or already paid
- [ ] `OrderPaymentExpiredHandler` calls `order.Cancel()`, updates the order, then publishes `OrderCancelled`
- [ ] Both handlers are registered in `Application/Sales/Orders/Services/Extensions.cs`

### Infrastructure
- [ ] `PaymentsDbContext` is `internal sealed`, lives under `Infrastructure/Sales/Payments/`
- [ ] `PaymentsDbContext` uses schema `"payments"` via `modelBuilder.HasDefaultSchema(PaymentsConstants.SchemaName)`
- [ ] `PaymentConfiguration` maps `RowVersion` with `.IsRowVersion()` and sets `UNIQUE` constraint on `OrderId`
- [ ] `PaymentConfiguration` maps `PaymentId` with `HasConversion` + `ValueGeneratedOnAdd()`
- [ ] `PaymentConfiguration` maps `PaymentOrderId` with `HasConversion(x => x.Value, v => new PaymentOrderId(v))`
- [ ] `Infrastructure/Sales/Payments/Extensions.cs` registers `AddDbContext<PaymentsDbContext>`, `IDbContextMigrator`, `IPaymentRepository`
- [ ] `Infrastructure/Sales/Orders/Configurations/OrderConfiguration.cs` maps `IsCancelled` and `CancelledAt`
- [ ] `PaymentWindowExpiredJob` is registered as `IScheduledTask` in TimeManagement DI

### Tests
- [ ] `UnitTests/Sales/Payments/PaymentAggregateTests.cs` covers `Create`, `Confirm`, `Expire`, `IssueRefund` and all guard conditions
- [ ] `UnitTests/Sales/Payments/OrderPlacedHandlerTests.cs` covers payment creation + job scheduling; verifies `ScheduleAsync` called with `PaymentWindowExpiredJob.JobTaskName` and correct `EntityId`
- [ ] `UnitTests/Sales/Payments/PaymentWindowExpiredJobTests.cs` covers: null EntityId, non-integer EntityId, payment not found (no-op), already Confirmed (no-op), happy path (Expire + PublishAsync)
- [ ] `UnitTests/Sales/Orders/OrderAggregateTests.cs` covers `Cancel()` and all guard conditions
- [ ] `UnitTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs` covers: order not found (no-op), already paid (no-op), happy path (`MarkAsPaid` called)
- [ ] `UnitTests/Sales/Orders/OrderPaymentExpiredHandlerTests.cs` covers: order not found (no-op), already cancelled (no-op), already paid (no-op), happy path (`Cancel()` + `OrderCancelled` published)

## Implementation Status

| Step | Description | Status |
|------|-------------|--------|
| 1–6 | Payment domain layer (`PaymentId`, `PaymentOrderId`, `PaymentStatus`, events, `Payment` aggregate, `IPaymentRepository`) | ✅ Done |
| 7–8 | Orders aggregate extension (`IsCancelled`, `CancelledAt`, `Cancel()`, `OrderCancelled` event type) | ✅ Done |
| 9–12 | Integration message updates (`OrderPlaced` + `TotalAmount` + `CurrencyId`; `PaymentConfirmed` + `PaymentId`; new `PaymentExpired`) | ✅ Done |
| 13–18 | Application layer — Payments BC (`OrderPlacedHandler`, `PaymentWindowExpiredJob`, `IPaymentService`, `PaymentService`, DTOs, ViewModels, DI) | ✅ Done |
| 19–24 | Application layer — Orders BC extensions (`MarkAsPaidAsync`, `CancelOrderAsync`, `AlreadyCancelled`, two new handlers, DI) | ✅ Done |
| 25–29 | Infrastructure layer — Payments BC (`PaymentsDbContext`, configuration, repository, DI, register job in TimeManagement) | ✅ Done |
| 30–31 | Infrastructure layer — Orders BC schema extension (map `IsCancelled`, `CancelledAt`) | ✅ Done |
| 32–37 | Unit tests (Payment aggregate, two Payments handlers, Order.Cancel, two Orders handlers) | ✅ Done |
| 38–39 | DB migrations (`InitPaymentsSchema`, `AddOrderCancellationFields`) | ⬜ Pending human approval per migration policy |
| 40–43 | Integration tests + atomic switch (remove legacy `PaymentHandler` coupling; update controllers) | ⬜ Not started |

## References

- Related ADRs:
  - [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0006 — TypedId and Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md)
  - [ADR-0009 — Supporting/TimeManagement BC Design](./0009-supporting-timemanagement-bc-design.md) (`IDeferredJobScheduler`, `IScheduledTask`)
  - [ADR-0010 — In-Memory Message Broker for Cross-BC Communication](./0010-in-memory-message-broker-for-cross-bc-communication.md)
  - [ADR-0011 — Inventory/Availability BC Design](./0011-inventory-availability-bc-design.md) (`PaymentWindowTimeoutJob` — direct stock-release path at same `ExpiresAt`)
  - [ADR-0014 — Sales/Orders BC Design](./0014-sales-orders-bc-design.md) (`OrderPlaced`, `OrderCancelled`, `Order.MarkAsPaid`, `Order.Cancel`)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
  - [`.github/instructions/migration-policy.md`](../../.github/instructions/migration-policy.md)
