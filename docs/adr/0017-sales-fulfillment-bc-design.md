# ADR-0017: Sales/Fulfillment BC — Slice 1: Refund Aggregate; Slice 2: Shipment (deferred)

## Status
Accepted

## Date
2026-03-12

## Context

The post-payment order lifecycle — refunds and shipment — is spread across the wrong BCs in the
current codebase. Two coupling hotspots drive this ADR:

**Coupling 1 — `RefundService` mutates Orders directly.**
The legacy `RefundService.AddRefund()` calls `_orderService.AddRefundToOrder(orderId, refundId)` and
`_orderService.DeleteRefundFromOrder(id)` synchronously. `DeleteRefund()` calls both methods from
outside the domain boundary. The same cross-BC synchronous call pattern removed from Payments
(`PaymentHandler → IOrderService`) repeats here.

**Coupling 2 — `RefundApproved` message is in the wrong BC.**
`Application/Sales/Payments/Messages/RefundApproved.cs` is owned by the Payments BC. Payments does
not initiate refunds — it _reacts_ to them. The Fulfillment BC owns the refund lifecycle and must
own the messages it publishes. The current single-item message shape (`OrderId`, `ProductId`,
`Quantity`) also does not support multi-item refunds from one event; `Payment.IssueRefund()` would
be called once per item, resetting `PaymentStatus.Refunded` on the first item and throwing on all
subsequent items.

**Coupling 3 — `Inventory.RefundApprovedHandler` hard-codes the legacy message source.**
`ECommerceApp.Application.Inventory.Availability.Handlers.RefundApprovedHandler` subscribes to the
Payments-owned `RefundApproved`. When the message source moves to Fulfillment, the handler
namespace import changes but the subscription logic remains correct — it only needs updating
during the atomic switch.

**Scope:**
`Sales/Fulfillment` in the BC map covers two concerns:
- **Slice 1** (this ADR) — `Refund` aggregate, approval workflow, `RefundApproved` /
  `RefundRejected` messages, cross-BC coordination (Inventory, Payments, Orders).
- **Slice 2** (deferred) — `Shipment` aggregate, shipment tracking, `ShipmentDelivered` message,
  partial fulfillment. Blocked until atomic switch of Orders and Payments BCs is complete.

## Decision

We introduce **Sales/Fulfillment** as a bounded context within the `Sales` group.

### 1. `Refund` aggregate

`Refund` is the aggregate root. It owns the refund lifecycle via `RefundStatus`. `OrderId` and
item references are plain value types — no navigation properties to other BCs.

```csharp
// Domain/Sales/Fulfillment/
public class Refund
{
    public RefundId Id { get; private set; }
    public int OrderId { get; private set; }        // plain int — no nav prop to Order
    public string Reason { get; private set; }
    public bool OnWarranty { get; private set; }
    public RefundStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public IReadOnlyList<RefundItem> Items { get; }

    private Refund() { }

    public static Refund Create(int orderId, string reason, bool onWarranty, IEnumerable<RefundItem> items) { ... }
    public void Approve() { ... }
    public void Reject() { ... }
}

public enum RefundStatus { Requested, Approved, Rejected }
```

`RefundId` follows the `TypedId<int>` pattern (ADR-0006).

### 2. `RefundItem` — owned entity

`RefundItem` is an owned entity — no aggregate root, no typed ID. It holds `ProductId` (plain `int`,
no nav prop to Catalog) and `Quantity` for a refund line. Configured via
`OwnsMany<RefundItem>(r => r.Items)` targeting `fulfillment.RefundItems`.

### 3. `IRefundService` — request, approve, reject

```csharp
// Application/Sales/Fulfillment/Services/
public interface IRefundService
{
    Task<RefundRequestResult> RequestRefundAsync(RequestRefundDto dto, CancellationToken ct = default);
    Task<RefundOperationResult> ApproveRefundAsync(int refundId, CancellationToken ct = default);
    Task<RefundOperationResult> RejectRefundAsync(int refundId, CancellationToken ct = default);
    Task<RefundDetailsVm?> GetRefundAsync(int refundId, CancellationToken ct = default);
    Task<RefundListVm> GetRefundsAsync(int pageSize, int pageNo, string? search, CancellationToken ct = default);
}
```

**`RequestRefundAsync`**: Verifies order exists via `IOrderExistenceChecker`, guards against
duplicate active refunds per order (`IRefundRepository.FindActiveByOrderIdAsync`), creates and
persists the `Refund` aggregate. Returns `RefundRequestResult`.

**`ApproveRefundAsync`**: Loads refund, guards `Status != Requested` → `AlreadyProcessed`, calls
`refund.Approve()`, persists, publishes `RefundApproved` carrying the full items list. Returns
`RefundOperationResult`.

**`RejectRefundAsync`**: Loads refund, guards `Status != Requested` → `AlreadyProcessed`, calls
`refund.Reject()`, persists, publishes `RefundRejected`. Returns `RefundOperationResult`.

`RefundService` is `internal sealed`.

### 4. Result types

```csharp
// Application/Sales/Fulfillment/Results/
public enum RefundRequestResult { Requested, OrderNotFound, RefundAlreadyExists }
public enum RefundOperationResult { Success, RefundNotFound, AlreadyProcessed }
```

Expected business outcomes are returned as result values. `DomainException` from aggregate methods
propagates as `BusinessException` via `ExceptionMiddleware`.

### 5. Integration messages (Fulfillment → other BCs)

`RefundApproved` is **moved** from `Application/Sales/Payments/Messages/` to
`Application/Sales/Fulfillment/Messages/`. Its shape is improved to carry a list of items and the
`RefundId` for downstream traceability:

```csharp
// Application/Sales/Fulfillment/Messages/
public record RefundApproved(
    int RefundId,
    int OrderId,
    IReadOnlyList<RefundApprovedItem> Items,
    DateTime OccurredAt) : IMessage;

public record RefundApprovedItem(int ProductId, int Quantity);

public record RefundRejected(
    int RefundId,
    int OrderId,
    DateTime OccurredAt) : IMessage;
```

`RefundRejected` has no downstream subscribers in Slice 1. It is published for future use by a
`Supporting/Communication` BC (notification to customer). The old `RefundApproved` in
`Application/Sales/Payments/Messages/` is removed during the atomic switch.

### 6. Cross-BC coordination

Fulfillment publishes; three BCs react. Same one-way message pattern as Coupons (ADR-0016) and
Payments (ADR-0015).

- **`InventoryRefundApprovedHandler`** (`Application/Inventory/Availability/Handlers/`) — existing handler;
  atomic switch updates `using` from `Sales.Payments.Messages` → `Sales.Fulfillment.Messages` and iterates
  `message.Items` instead of reading a single `ProductId`/`Quantity`.
- **`PaymentRefundApprovedHandler`** (new — `Application/Sales/Payments/Handlers/`) — calls
  `IPaymentService.ProcessRefundAsync(message.OrderId, message.RefundId, ct)`. Registered in Payments DI.
- **`OrderRefundApprovedHandler`** (new — `Application/Sales/Orders/Handlers/`) — calls
  `IOrderService.AddRefundAsync(message.OrderId, message.RefundId, ct)`. No `Order.Status` change in
  Slice 1. Registered in Orders DI.

### 7. Payments BC extension — `IPaymentService.ProcessRefundAsync` (ADR-0015 extension)

`IPaymentService` gains one new method:

```csharp
Task<PaymentOperationResult> ProcessRefundAsync(int orderId, int refundId, CancellationToken ct = default);
```

`Payment` gains `Refund(int refundId)` — guards `Status == Confirmed`, transitions to
`PaymentStatus.Refunded`, returns a `PaymentRefundedEvent` (new domain event in
`Domain/Sales/Payments/Events/`). `PaymentService.ProcessRefundAsync` loads payment by `orderId`,
calls `payment.Refund(refundId)`, and persists. The old `Payment.IssueRefund(productId, quantity)`
is kept during the parallel-change window and removed at the atomic switch.

### 8. `IOrderExistenceChecker` ACL

Same pattern as Coupons BC (ADR-0016 §6). Fulfillment defines its own interface:

```csharp
// Application/Sales/Fulfillment/Contracts/
public interface IOrderExistenceChecker
{
    Task<bool> ExistsAsync(int orderId, CancellationToken ct = default);
}
```

Infrastructure adapter wraps `IOrderService.GetOrderDetailsAsync(orderId, ct)`.

Note: Each BC that needs this contract defines its own copy of `IOrderExistenceChecker` in its own
`Contracts/` folder (per interface-segregation principle). They are structurally identical but
independently versioned — this is intentional.

### 9. DB schema (`fulfillment.*`) and `FulfillmentDbContext`

```
fulfillment.Refunds
  Id           int            PK IDENTITY
  OrderId      int            NOT NULL                 (no FK to sales.Orders — cross-BC)
  Reason       nvarchar(1000) NOT NULL
  OnWarranty   bit            NOT NULL
  Status       nvarchar(20)   NOT NULL  ('Requested' / 'Approved' / 'Rejected')
  RequestedAt  datetime2      NOT NULL
  ProcessedAt  datetime2      NULL

fulfillment.RefundItems
  Id        int  PK IDENTITY
  RefundId  int  NOT NULL  FK → fulfillment.Refunds(Id)  ON DELETE CASCADE
  ProductId int  NOT NULL                              (no FK to Catalog — cross-BC)
  Quantity  int  NOT NULL
```

No FK from `Refunds.OrderId` to `sales.Orders` — cross-BC boundary (IDs only).
No FK from `RefundItems.ProductId` to Catalog — cross-BC boundary (IDs only).

```csharp
internal sealed class FulfillmentDbContext : DbContext
{
    public DbSet<Refund> Refunds { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("fulfillment");
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FulfillmentDbContext).Assembly,
            t => t.Namespace?.Contains("Sales.Fulfillment") == true);
    }
}
```

`RefundItem` is configured via `OwnsMany` inside `RefundConfiguration`.

### 10. Folder structure

```
ECommerceApp.Domain/Sales/Fulfillment/
  Refund.cs
  RefundId.cs
  RefundItem.cs
  RefundStatus.cs
  IRefundRepository.cs

ECommerceApp.Application/Sales/Fulfillment/
  Contracts/
    IOrderExistenceChecker.cs
  Services/
    IRefundService.cs
    RefundService.cs               <- internal sealed
    Extensions.cs
  Results/
    RefundRequestResult.cs
    RefundOperationResult.cs
  Messages/
    RefundApproved.cs              <- MOVED from Application/Sales/Payments/Messages/
    RefundApprovedItem.cs          <- new; carries ProductId + Quantity per item
    RefundRejected.cs
  DTOs/
    RequestRefundDto.cs
    RequestRefundItemDto.cs
  ViewModels/
    RefundDetailsVm.cs
    RefundListVm.cs
    RefundVm.cs

ECommerceApp.Application/Sales/Orders/Handlers/
  OrderRefundApprovedHandler.cs    <- IMessageHandler<RefundApproved>; calls IOrderService.AddRefundAsync

ECommerceApp.Application/Sales/Payments/Handlers/
  PaymentRefundApprovedHandler.cs  <- IMessageHandler<RefundApproved>; calls IPaymentService.ProcessRefundAsync

ECommerceApp.Infrastructure/Sales/Fulfillment/
  FulfillmentDbContext.cs
  FulfillmentDbContextFactory.cs
  FulfillmentConstants.cs
  Repositories/
    RefundRepository.cs
  Configurations/
    RefundConfiguration.cs         <- OwnsMany RefundItems → fulfillment.RefundItems
  Adapters/
    OrderExistenceCheckerAdapter.cs
  Extensions.cs
  Migrations/
    (generated)
```

### 11. Slice 2 — Shipment (deferred)

The following are explicitly out of scope for Slice 1. Implementation is gated on Slice 1 being
in production and the Sales/Orders + Sales/Payments atomic switches being complete.

#### 11.1 `Shipment` aggregate

`Shipment` records the physical dispatch of an order. One `Order` can have multiple `Shipment`
records to support partial fulfillment. `Shipment` is the aggregate root.

```csharp
// Domain/Sales/Fulfillment/
public class Shipment
{
    public ShipmentId Id { get; private set; }
    public int OrderId { get; private set; }           // plain int — no nav prop to Order
    public string? TrackingNumber { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }

    private readonly List<ShipmentLine> _lines = new();
    public IReadOnlyList<ShipmentLine> Lines => _lines.AsReadOnly();

    private Shipment() { }

    public static Shipment Create(int orderId, IEnumerable<ShipmentLine> lines)
    {
        if (orderId <= 0) throw new DomainException("OrderId must be positive.");
        var lineList = lines?.ToList() ?? throw new DomainException("Lines are required.");
        if (!lineList.Any()) throw new DomainException("At least one line is required.");
        var s = new Shipment { OrderId = orderId, Status = ShipmentStatus.Pending };
        s._lines.AddRange(lineList);
        return s;
    }

    public void MarkAsInTransit(string trackingNumber)
    {
        if (Status != ShipmentStatus.Pending)
            throw new DomainException($"Shipment '{Id?.Value}' is not in Pending status.");
        if (string.IsNullOrWhiteSpace(trackingNumber))
            throw new DomainException("Tracking number is required.");
        TrackingNumber = trackingNumber;
        Status = ShipmentStatus.InTransit;
        ShippedAt = DateTime.UtcNow;
    }

    public void MarkAsDelivered()
    {
        if (Status != ShipmentStatus.InTransit)
            throw new DomainException($"Shipment '{Id?.Value}' is not in transit.");
        Status = ShipmentStatus.Delivered;
        DeliveredAt = DateTime.UtcNow;
    }

    public void MarkAsFailed()
    {
        if (Status is not (ShipmentStatus.Pending or ShipmentStatus.InTransit))
            throw new DomainException($"Shipment '{Id?.Value}' cannot fail from status '{Status}'.");
        Status = ShipmentStatus.Failed;
    }
}

// Domain/Sales/Fulfillment/
public enum ShipmentStatus { Pending, InTransit, Delivered, Failed }
```

`ShipmentId` follows the `TypedId<int>` pattern (ADR-0006). State transitions:
`Pending → InTransit → Delivered | Failed`.

#### 11.2 `ShipmentLine` — owned entity

`ShipmentLine` records which products and quantities were dispatched in a given shipment.
Persisted in `fulfillment.ShipmentLines` via `OwnsMany`.

```csharp
// Domain/Sales/Fulfillment/
public class ShipmentLine
{
    public int ProductId { get; private set; }    // plain int — no nav prop to Catalog
    public int Quantity { get; private set; }

    private ShipmentLine() { }

    public static ShipmentLine Create(int productId, int quantity)
    {
        if (productId <= 0) throw new DomainException("ProductId must be positive.");
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        return new ShipmentLine { ProductId = productId, Quantity = quantity };
    }
}
```

#### 11.3 `IShipmentService`

```csharp
// Application/Sales/Fulfillment/Services/
public interface IShipmentService
{
    Task<ShipmentOperationResult> CreateShipmentAsync(CreateShipmentDto dto, CancellationToken ct = default);
    Task<ShipmentOperationResult> MarkAsInTransitAsync(int shipmentId, string trackingNumber, CancellationToken ct = default);
    Task<ShipmentOperationResult> MarkAsDeliveredAsync(int shipmentId, CancellationToken ct = default);
    Task<ShipmentOperationResult> MarkAsFailedAsync(int shipmentId, CancellationToken ct = default);
    Task<ShipmentDetailsVm?> GetShipmentAsync(int shipmentId, CancellationToken ct = default);
    Task<ShipmentListVm> GetShipmentsByOrderIdAsync(int orderId, CancellationToken ct = default);
}
```

**`CreateShipmentAsync`**: Verifies order exists via `IOrderExistenceChecker`, creates
`Shipment.Create(dto.OrderId, lines)`, persists via `IShipmentRepository.AddAsync`.

**`MarkAsInTransitAsync`**: Loads shipment, calls `shipment.MarkAsInTransit(trackingNumber)`,
persists, publishes `ShipmentDispatched`.

**`MarkAsDeliveredAsync`**: Loads shipment, calls `shipment.MarkAsDelivered()`, persists,
publishes `ShipmentDelivered`.

**`MarkAsFailedAsync`**: Loads shipment, calls `shipment.MarkAsFailed()`, persists, publishes
`ShipmentFailed`.

`ShipmentService` is `internal sealed`.

#### 11.4 Result types

```csharp
// Application/Sales/Fulfillment/Results/
public enum ShipmentOperationResult { Success, NotFound, OrderNotFound, InvalidStatus }
```

#### 11.5 Integration messages

```csharp
// Application/Sales/Fulfillment/Messages/
public record ShipmentDispatched(
    int ShipmentId, int OrderId, string TrackingNumber, DateTime OccurredAt) : IMessage;

public record ShipmentDelivered(
    int ShipmentId, int OrderId, DateTime OccurredAt) : IMessage;

public record ShipmentPartiallyDelivered(
    int ShipmentId, int OrderId, DateTime OccurredAt) : IMessage;

public record ShipmentFailed(
    int ShipmentId, int OrderId, DateTime OccurredAt) : IMessage;
```

`ShipmentDispatched` and `ShipmentFailed` have no downstream subscribers in Slice 2 iteration 1 —
published for future use by `Supporting/Communication` BC (customer notifications).

#### 11.6 Cross-BC coordination (Orders BC)

**`OrderShipmentDeliveredHandler`** (new):

```csharp
// Application/Sales/Orders/Handlers/
internal sealed class OrderShipmentDeliveredHandler : IMessageHandler<ShipmentDelivered>
{
    private readonly IOrderService _orders;

    public async Task HandleAsync(ShipmentDelivered message, CancellationToken ct = default)
        => await _orders.MarkAsDeliveredAsync(message.OrderId, ct);
}
```

`IOrderService.MarkAsDeliveredAsync` already exists and transitions `Order.Status → Fulfilled`.
The Slice 2 change moves the trigger from a direct controller call to this message handler.

**`OrderShipmentPartiallyDeliveredHandler`** (new):

```csharp
// Application/Sales/Orders/Handlers/
internal sealed class OrderShipmentPartiallyDeliveredHandler : IMessageHandler<ShipmentPartiallyDelivered>
{
    private readonly IOrderService _orders;

    public async Task HandleAsync(ShipmentPartiallyDelivered message, CancellationToken ct = default)
        => await _orders.MarkAsPartiallyFulfilledAsync(message.OrderId, ct);
}
```

`IOrderService.MarkAsPartiallyFulfilledAsync` is a new method — transitions
`Order.Status → PartiallyFulfilled` (status already defined in `OrderStatus` enum).

Both handlers registered in `Application/Sales/Orders/Services/Extensions.cs`.

#### 11.7 `Order.MarkAsRefunded()` — Orders BC aggregate extension

In Slice 1, approving a refund records the event on `Order` but does not change `Order.Status`.
In Slice 2, `Order.MarkAsRefunded(int refundId)` transitions `Order.Status → Refunded` and appends
an `OrderRefunded` event. A corresponding `IOrderService.MarkAsRefundedAsync(orderId, refundId)`
method and updated `OrderRefundApprovedHandler` (calls `MarkAsRefundedAsync` instead of
`AddRefundAsync`) complete the full status lifecycle.

#### 11.8 DB schema additions (`FulfillmentDbContext`)

```
fulfillment.Shipments
  Id              int           PK IDENTITY
  OrderId         int           NOT NULL  (no FK — cross-BC)
  TrackingNumber  nvarchar(100) NULL
  Status          nvarchar(30)  NOT NULL  ('Pending' / 'InTransit' / 'Delivered' / 'Failed')
  ShippedAt       datetime2     NULL
  DeliveredAt     datetime2     NULL

fulfillment.ShipmentLines
  Id          int  PK IDENTITY
  ShipmentId  int  NOT NULL  FK → fulfillment.Shipments(Id)  ON DELETE CASCADE
  ProductId   int  NOT NULL  (no FK — cross-BC)
  Quantity    int  NOT NULL
```

`ShipmentConfiguration` uses `OwnsMany<ShipmentLine>(s => s.Lines)` targeting
`fulfillment.ShipmentLines`. No FK from `ShipmentLines.ProductId` to Catalog — cross-BC.

#### 11.9 Folder additions (Slice 2)

```
ECommerceApp.Domain/Sales/Fulfillment/
  Shipment.cs
  ShipmentId.cs
  ShipmentLine.cs
  ShipmentStatus.cs
  IShipmentRepository.cs

ECommerceApp.Application/Sales/Fulfillment/
  Services/
    IShipmentService.cs
    ShipmentService.cs             <- internal sealed
  Results/
    ShipmentOperationResult.cs
  Messages/
    ShipmentDispatched.cs
    ShipmentDelivered.cs
    ShipmentPartiallyDelivered.cs
    ShipmentFailed.cs
  DTOs/
    CreateShipmentDto.cs
    CreateShipmentLineDto.cs
  ViewModels/
    ShipmentDetailsVm.cs
    ShipmentListVm.cs

ECommerceApp.Application/Sales/Orders/Handlers/
  OrderShipmentDeliveredHandler.cs
  OrderShipmentPartiallyDeliveredHandler.cs

ECommerceApp.Infrastructure/Sales/Fulfillment/
  Repositories/
    ShipmentRepository.cs
  Configurations/
    ShipmentConfiguration.cs       <- OwnsMany ShipmentLines → fulfillment.ShipmentLines
```

### 12. Remaining bounded context gaps — full overview

The table below summarises every BC that has outstanding work as of 2026-03-12. Use this as a
cross-reference alongside `docs/architecture/bounded-context-map.md`.

| # | BC | ADR | What's still needed | Blocked by |
|---|---|---|---|---|
| 1 | **Sales/Orders** | [ADR-0014](./0014-sales-orders-bc-design.md) | DB migration approval (`InitSalesSchema`); integration tests; atomic switch (remove legacy `OrderService`) | — |
| 2 | **Sales/Payments** | [ADR-0015](./0015-sales-payments-bc-design.md) | DB migration approval (`InitPaymentsSchema`); `ProcessRefundAsync` extension (§7 this ADR); integration tests; atomic switch (remove legacy `PaymentHandler`, `PaymentService`) | Orders (#1) |
| 3 | **Sales/Coupons — Slice 1** | [ADR-0016](./0016-sales-coupons-bc-design.md) | Full domain + app + infra implementation; DB migration; unit + integration tests; atomic switch (remove legacy `CouponHandler`) | Orders (#1) |
| 4 | **Sales/Coupons — Slice 2** | ADR-0016 §9 | `CouponScope` (per-item), expiry, multi-use, bulk issuance, admin CRUD | Slice 1 in production |
| 5 | **Sales/Fulfillment — Slice 1** | This ADR | Full domain + app + infra implementation; DB migration; `PaymentRefundApprovedHandler` + `OrderRefundApprovedHandler`; update `Inventory.RefundApprovedHandler` import; unit + integration tests; atomic switch (remove legacy `RefundService`) | Orders (#1), Payments (#2) |
| 6 | **Sales/Fulfillment — Slice 2** | ADR-0017 §11 | `Shipment` aggregate, shipment tracking, `ShipmentDelivered` → Orders, partial fulfilment, `Order.MarkAsRefunded()` | Slice 1 in production + Orders/Payments atomic switches |
| 7 | **Presale/Checkout — Slice 2** | [ADR-0012](./0012-presale-checkout-bc-design.md) §11–14 | `ICheckoutService`, `CheckoutService`, `PlaceOrderFromPresaleAsync`, `GetPriceChangesAsync`, API endpoint | Orders (#1) |
| 8 | **Catalog** (atomic switch) | [ADR-0007](./0007-catalog-bc-product-category-tag-aggregate-design.md) | DB migration approval; integration tests; migrate `ItemController` / `ImageController` / `TagController` → new BC; remove legacy `ItemService`, `ImageService`, `TagService` | — |
| 9 | **Currencies** (atomic switch) | [ADR-0008](./0008-supporting-currencies-bc-design.md) | DB migration approval; integration tests; migrate `CurrencyController` (→ async); coordinate with Catalog switch | Catalog switch |
| 10 | **AccountProfile** (atomic switch) | [ADR-0005](./0005-accountprofile-bc-userprofile-aggregate-design.md) | DB migration approval; integration tests; migrate `CustomerController` / `AddressController` / `ContactDetailController`; remove legacy `CustomerService` | — |
| 11 | **Inventory/Availability** (atomic switch) | [ADR-0011](./0011-inventory-availability-bc-design.md) | DB migration approval; data migration (`Items.Quantity → inventory.StockItems`); integration tests; replace `ItemHandler` stock calls with `IMessageBroker.PublishAsync(new OrderPlaced(...))` | Catalog switch |
| 12 | **Presale/Checkout — Slice 1** (atomic switch) | [ADR-0012](./0012-presale-checkout-bc-design.md) | DB migration approval (`InitPresaleSchema`); integration tests | Inventory switch |
| 13 | **TimeManagement** (atomic switch) | [ADR-0009](./0009-supporting-timemanagement-bc-design.md) | DB migration approval (two migrations); integration tests; `CurrencyRateSyncTask` switch | — |
| 14 | **Identity / IAM** (atomic switch) | ADR-0002 §8 | Migrate `LoginController` + `UserManagementController`; flip `UseIamStore: true`; remove old `IUserService` / `AuthenticationService` / `ApplicationUser.cs` | — |
| 15 | **Per-BC DbContext interfaces** | [ADR-0013](./0013-per-bc-dbcontext-interfaces.md) | Implementation — gated on ≥80% BC atomic switches complete | Most atomic switches |
| 16 | **Supporting/Communication** | No ADR yet | Design + ADR required; email/SMS notification for order status changes, refund approvals, coupon expiry | Fulfillment Slice 1 + Coupons Slice 1 |

> Items 8–14 are "switch" tasks for BCs already fully implemented. Items 1–7 and 16 require new
> code. Item 15 is a final clean-up gate.

## Consequences

### Positive
- **`RefundService` coupling eliminated** — Fulfillment BC no longer mutates Orders directly;
  Orders, Payments, and Inventory each react via message handlers.
- **`RefundApproved` owned correctly** — Fulfillment publishes the message; Payments and
  Inventory consume it. The Payments BC is no longer the source of a Fulfillment event.
- **Multi-item refund supported** — `RefundApproved` carries a list; `Inventory.InventoryRefundApprovedHandler`
  loops over items, correctly returning stock for each. `Payment.Refund(refundId)` transitions
  to `Refunded` exactly once per refund regardless of item count.
- **Refund lifecycle is auditable** — `RefundStatus` enum (`Requested / Approved / Rejected`) makes
  every transition explicit.
- **Slice 1 scope is narrow and testable** — no Shipment complexity; the entire Slice 1 can be
  unit- and integration-tested before Slice 2 begins.

### Negative
- `Refund.OrderId` is a plain `int` with no FK to `Orders`. Stale references can exist if an order
  is hard-deleted outside the cancellation flow — same trade-off as `CouponUsed.OrderId`.
- `Order.Status` does not transition to `Refunded` in Slice 1. Downstream display code that relies
  on `Order.Status == Refunded` will not work until Slice 2.
- `Payment.IssueRefund(productId, quantity)` co-exists with the new `Payment.Refund(refundId)` during
  the parallel-change window, adding temporary surface area to the Payments aggregate.

### Risks & mitigations
- **Risk**: `Inventory.RefundApprovedHandler` receives the new list-shape `RefundApproved` before the
  handler code is updated, causing a deserialization mismatch.
  **Mitigation**: The Inventory handler update is a mandatory step of the atomic switch (step 8);
  the old message in `Payments/Messages/` is only removed after the handler is updated.
- **Risk**: `PaymentService.ProcessRefundAsync` is called by the `PaymentRefundApprovedHandler`
  before `Payment.Refund(refundId)` is added to the aggregate.
  **Mitigation**: Both `Payment.Refund()` and `ProcessRefundAsync` are implemented in the same
  Slice 1 step (step 3b). The handler is registered after both are in place.
- **Risk**: Multiple `RefundApproved` subscribers process the message out of order in a future
  async or distributed setup.
  **Mitigation**: Acceptable for the in-memory monolith. The in-memory broker guarantees
  synchronous sequential delivery. Cross-subscriber ordering SLAs are a microservices concern.

## Alternatives considered

- **`RefundService` calls `IOrderService.AddRefundAsync` directly (no messaging)** — rejected. Same
  reasoning as Coupons BC (ADR-0016): direct synchronous cross-BC calls create tight coupling and
  leave state inconsistent on partial failure.
- **Keep `RefundApproved` in the Payments BC** — rejected. Payments does not initiate refunds. The
  message must be owned by the BC that publishes it — Fulfillment.
- **One `RefundApproved` message per item** — rejected. `Payment.Refund()` must be called exactly
  once per refund; publishing N per-item messages would fail for payment (transitions `Refunded` on
  first, throws on subsequent). A single aggregate-level message with an `Items` list is correct.
- **Implement Shipment (Slice 2) immediately** — rejected. Shipment tracking adds a new aggregate,
  a new table, and requires the Orders atomic switch to be complete (so `MarkAsDeliveredAsync` can
  be triggered by message). Slice 1 (Refunds only) delivers immediate value without that dependency.

## Migration plan

**Slice 1 (this ADR):**

1. Create target folder structure: `Domain/Sales/Fulfillment/`, `Application/Sales/Fulfillment/`,
   `Infrastructure/Sales/Fulfillment/`.
2. Create `Domain/Sales/Fulfillment/`: `Refund`, `RefundId`, `RefundItem`, `RefundStatus`,
   `IRefundRepository`.
3. Create `Application/Sales/Fulfillment/`: `IRefundService`, `RefundService` (internal sealed),
   `RefundRequestResult`, `RefundOperationResult`, `RefundApproved` (new shape),
   `RefundApprovedItem`, `RefundRejected`, `IOrderExistenceChecker`, DTOs, ViewModels,
   DI `Extensions.cs`.
   - **Also extend ADR-0015** in parallel:
     - `a)` Add `Payment.Refund(int refundId)` method to `Domain/Sales/Payments/Payment.cs`.
     - `b)` Add `PaymentRefundedEvent` to `Domain/Sales/Payments/Events/`.
     - `c)` Add `ProcessRefundAsync(int orderId, int refundId, CancellationToken ct)` to
       `IPaymentService` and implement in `PaymentService`.
4. Create `Application/Sales/Orders/Handlers/OrderRefundApprovedHandler.cs`. Register in
   `Application/Sales/Orders/Services/Extensions.cs`.
5. Create `Application/Sales/Payments/Handlers/PaymentRefundApprovedHandler.cs`. Register in
   `Application/Sales/Payments/Services/Extensions.cs`.
6. Create `Infrastructure/Sales/Fulfillment/`: `FulfillmentDbContext`, `RefundConfiguration`
   (OwnsMany RefundItems), `RefundRepository`, `OrderExistenceCheckerAdapter`, DI `Extensions.cs`.
7. Register `FulfillmentDbContext` in `Infrastructure/DependencyInjection.cs`.
8. Generate EF migration `InitFulfillmentSchema` targeting `FulfillmentDbContext`.
9. Write unit tests: `RefundAggregateTests`, `RefundServiceTests`, `PaymentRefundApprovedHandlerTests`,
   `OrderRefundApprovedHandlerTests`, `InventoryRefundApprovedHandlerTests` (updated handler).
10. Write integration tests: `RefundServiceIntegrationTests` — `RequestRefundAsync` happy path,
    order not found, duplicate refund; `ApproveRefundAsync` happy path, not found, already processed;
    `RejectRefundAsync` happy path.
11. Atomic switch:
    - Update `Application/Inventory/Availability/Handlers/RefundApprovedHandler.cs`: change `using`
      from `Sales.Payments.Messages` → `Sales.Fulfillment.Messages`; update handler body to
      iterate `message.Items`.
    - Remove the old `Application/Sales/Payments/Messages/RefundApproved.cs`.
    - Remove legacy `RefundService` DI registration and direct `IOrderService` / `IPaymentService`
      calls from legacy `RefundService`.
    - Update controllers / API controllers to use `IRefundService`.

**Slice 2 (deferred — future ADR or ADR-0018 amendment):**

12. Create `Domain/Sales/Fulfillment/`: `Shipment`, `ShipmentId`, `ShipmentLine`,
    `ShipmentStatus`, `IShipmentRepository` (see §11.1–§11.2).
13. Create `Application/Sales/Fulfillment/`: `IShipmentService`, `ShipmentService`
    (internal sealed), `ShipmentOperationResult`, messages (`ShipmentDispatched`,
    `ShipmentDelivered`, `ShipmentPartiallyDelivered`, `ShipmentFailed`),
    `CreateShipmentDto`, `CreateShipmentLineDto`, `ShipmentDetailsVm`, `ShipmentListVm`
    (see §11.3–§11.5).
14. Create `Application/Sales/Orders/Handlers/OrderShipmentDeliveredHandler` and
    `OrderShipmentPartiallyDeliveredHandler` (see §11.6). Register in Orders DI.
    Extend `IOrderService` with `MarkAsPartiallyFulfilledAsync(orderId)`.
    Remove direct controller call to `MarkAsDeliveredAsync` — triggered by `ShipmentDelivered`.
15. Add `Order.MarkAsRefunded(int refundId)` and `IOrderService.MarkAsRefundedAsync(orderId, refundId)`.
    Update `OrderRefundApprovedHandler` to call `MarkAsRefundedAsync` (see §11.7).
16. Create `Infrastructure/Sales/Fulfillment/Repositories/ShipmentRepository` and
    `Configurations/ShipmentConfiguration` (OwnsMany ShipmentLines). EF migration
    `AddShipmentSchema` targeting `FulfillmentDbContext` (see §11.8–§11.9).
17. Write unit tests: `ShipmentAggregateTests`, `ShipmentServiceTests`,
    `OrderShipmentDeliveredHandlerTests`, `OrderShipmentPartiallyDeliveredHandlerTests`.
18. Write integration tests: `ShipmentServiceIntegrationTests` — create, dispatch, deliver,
    partial delivery, fail flows.

## Conformance checklist

- [ ] `Refund` aggregate lives under `Domain/Sales/Fulfillment/` — not `Domain/Model/`
- [ ] `Refund` has private setters and a `private` parameterless constructor for EF Core
- [ ] `RefundId` follows the `TypedId<int>` pattern (ADR-0006)
- [ ] `RefundItem` is an owned entity — no `RefundItemId` typed ID; no separate aggregate root
- [ ] `Refund.OrderId` is a plain `int` — no navigation property to `Order`
- [ ] `RefundItem.ProductId` is a plain `int` — no navigation property to Catalog
- [ ] `RefundService` is `internal sealed`
- [ ] `RefundApproved` lives in `Application/Sales/Fulfillment/Messages/` — NOT in `Payments/Messages/`
- [ ] `RefundApproved` carries `IReadOnlyList<RefundApprovedItem> Items` — not single `ProductId`/`Quantity`
- [ ] `IOrderExistenceChecker` lives in `Application/Sales/Fulfillment/Contracts/`
- [ ] `FulfillmentDbContext` uses schema `"fulfillment"` — DbSet: `Refunds`; `RefundItems` via `OwnsMany`
- [ ] No FK from `fulfillment.Refunds.OrderId` to Orders table — cross-BC boundary
- [ ] No FK from `fulfillment.RefundItems.ProductId` to Catalog table — cross-BC boundary
- [ ] `Payment.Refund(int refundId)` is added to `Domain/Sales/Payments/Payment.cs` before `PaymentRefundApprovedHandler` is registered
- [ ] Old `Application/Sales/Payments/Messages/RefundApproved.cs` is removed at atomic switch step 11
- [ ] Legacy `RefundService` is NOT removed until atomic switch (step 11) is verified green
- [ ] `Shipment` is NOT present in Slice 1 domain or infrastructure

## Implementation Status

| Step | Description | Status |
|------|-------------|--------|
| 1 | Folder structure created | ⬜ Not started |
| 2 | `Domain/Sales/Fulfillment/`: `Refund`, `RefundId`, `RefundItem`, `RefundStatus`, `IRefundRepository` | ⬜ Not started |
| 3 | `Application/Sales/Fulfillment/`: services, results, messages, `IOrderExistenceChecker`, DTOs, ViewModels, DI; + ADR-0015 extension: `Payment.Refund()`, `PaymentRefundedEvent`, `IPaymentService.ProcessRefundAsync` | ⬜ Not started |
| 4 | `Application/Sales/Orders/Handlers/OrderRefundApprovedHandler`; registered in Orders DI | ⬜ Not started |
| 5 | `Application/Sales/Payments/Handlers/PaymentRefundApprovedHandler`; registered in Payments DI | ⬜ Not started |
| 6 | `Infrastructure/Sales/Fulfillment/`: `FulfillmentDbContext`, `RefundConfiguration`, `RefundRepository`, `OrderExistenceCheckerAdapter`, DI | ⬜ Not started |
| 7 | `FulfillmentDbContext` registered in `Infrastructure/DependencyInjection.cs` | ⬜ Not started |
| 8 | EF migration `InitFulfillmentSchema` targeting `FulfillmentDbContext` | ✅ Generated — pending human approval (migration-policy.md) |
| 9 | Unit tests: `RefundAggregateTests`, `RefundServiceTests`, `PaymentRefundApprovedHandlerTests`, `OrderRefundApprovedHandlerTests`, updated `InventoryRefundApprovedHandlerTests` | ✅ Done (33 tests passing; `InventoryRefundApprovedHandlerTests` update deferred to atomic switch) |
| 10 | Integration tests: `RefundServiceIntegrationTests` | ⬜ Not started |
| 11 | Atomic switch: update `Inventory.RefundApprovedHandler`; remove old `RefundApproved` from Payments; migrate controllers → `IRefundService`; remove legacy `RefundService` | ⬜ After integration tests |
| 12–18 | Slice 2 — deferred features (`Shipment` aggregate + `ShipmentLine`, state machine, cross-BC handlers, `Order.MarkAsRefunded()`) — see §11 | ⬜ Future ADR |

## References

- Related ADRs:
  - [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](./0002-post-event-storming-architectural-evolution-strategy.md)
  - [ADR-0003 - Feature-Folder Organization for New Bounded Context Code](./0003-feature-folder-organization-for-new-bounded-context-code.md)
  - [ADR-0004 - Module Taxonomy and Bounded Context Grouping](./0004-module-taxonomy-and-bounded-context-grouping.md) (`Sales/Fulfillment` in `Sales` group)
  - [ADR-0006 - TypedId and Value Objects as Shared Domain Primitives](./0006-typedid-and-value-objects-as-shared-domain-primitives.md) (`RefundId`)
  - [ADR-0010 - In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md) (`RefundApproved` / `RefundRejected` cross-BC messages)
  - [ADR-0011 - Inventory/Availability BC Design](./0011-inventory-availability-bc-design.md) (`InventoryRefundApprovedHandler` updated message import)
  - [ADR-0013 - Per-BC DbContext Interfaces](./0013-per-bc-dbcontext-interfaces.md) (`FulfillmentDbContext` registration pattern)
  - [ADR-0014 - Sales/Orders BC Design](./0014-sales-orders-bc-design.md) (`Order.AssignRefund()` receiver; `IOrderService.AddRefundAsync` existing; `Order.MarkAsRefunded()` Slice 2 extension)
  - [ADR-0015 - Sales/Payments BC Design](./0015-sales-payments-bc-design.md) (`Payment.Refund(refundId)` + `IPaymentService.ProcessRefundAsync` extension)
  - [ADR-0016 - Sales/Coupons BC Design](./0016-sales-coupons-bc-design.md) (`IOrderExistenceChecker` ACL pattern reference)
- Architecture map:
  - [`docs/architecture/bounded-context-map.md`](../architecture/bounded-context-map.md)
- Instruction files:
  - [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md)
  - [`.github/instructions/efcore-instructions.md`](../../.github/instructions/efcore-instructions.md)
  - [`.github/instructions/testing-instructions.md`](../../.github/instructions/testing-instructions.md)
