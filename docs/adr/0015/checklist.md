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
