# ADR-0018: Supporting/Communication BC — Customer Notification Design

## Status
Accepted

## Date
2026-03-12

## Context

ECommerceApp currently has no mechanism to notify customers of domain events that directly affect
them (refund outcomes, coupon expiry, order status changes, payment confirmations). Notifications
are mentioned as a functional requirement but have no implementation or architectural home.

Several core BCs produce events that should trigger customer-facing notifications:
- **Fulfillment BC** (ADR-0017): publishes `RefundApproved` and `RefundRejected`
- **Coupons BC** (ADR-0016 §9 Slice 2): will publish `CouponExpired`
- **Orders BC** (ADR-0014): publishes `OrderPlaced` and `OrderCancelled`
- **Payments BC** (ADR-0015): publishes `PaymentConfirmed` and `PaymentExpired`

Notification logic (email composition, SMS routing, template rendering) is pure supporting
infrastructure. It has no domain logic of its own and must not affect the state or lifecycle of
any other BC.

The exact notification rules are not fully known yet — which events trigger which channels
(email vs. SMS), notification templates, opt-out preferences, and retry semantics will be
clarified during implementation. A bottom-up / lean ADR is appropriate here.

**Implementation is blocked by:**
- Fulfillment Slice 1 (ADR-0017) — produces `RefundApproved` / `RefundRejected`
- Coupons Slice 1 (ADR-0016) — produces `CouponExpired` (Slice 2)

## Decision

We will introduce a dedicated **Supporting/Communication BC** as a leaf-node BC that:
- Subscribes to integration messages from the in-memory `IMessageBroker` (ADR-0010)
- Pushes real-time notifications to connected browser clients via **SignalR** in response to those messages
- Has **no domain model** — application-layer only
- **Never publishes** messages back to any core BC — it is a pure consumer

Email and SMS are deliberately excluded from this BC. They are a separate delivery channel with different
infrastructure concerns (SMTP servers, templates, opt-out management, retry) and will be introduced
as a separate supporting BC if and when needed.

### § 1 BC classification

| Property | Value |
|---|---|
| Type | Supporting infrastructure |
| Layer ownership | `Application.Supporting.Communication` |
| Domain model | None |
| Own DbContext | No (optional notification log — deferred) |
| Message role | Consumer only — never publishes to core BCs |

### § 2 Message subscriptions (initial set)

| Message | Source BC | Notification trigger |
|---|---|---|
| `RefundApproved` | Fulfillment (ADR-0017) | Push: refund approved |
| `RefundRejected` | Fulfillment (ADR-0017) | Push: refund rejected |
| `OrderPlaced` | Orders (ADR-0014) | Push: order confirmation |
| `OrderCancelled` | Orders (ADR-0014) | Push: order cancellation |
| `OrderRequiresAttention` | Orders (ADR-0014 §19) | Internal: operator alert (logger only) |
| `PaymentConfirmed` | Payments (ADR-0015) | Push: payment received |
| `PaymentExpired` | Payments (ADR-0015) | Push: payment window expired |
| `CouponExpired` | Coupons (ADR-0016 §9) | Push: coupon expired — deferred to Slice 2 |
| `PaymentExpired` | Payments (ADR-0015) | Email: payment window expired |
| `CouponExpired` | Coupons (ADR-0016 §9) | Email: coupon expired — deferred to Slice 2 |

### § 3 Handler pattern

Each subscribed message gets a dedicated `IMessageHandler<T>` implementation:

```
Application/Supporting/Communication/
  Handlers/
    RefundApprovedNotificationHandler.cs  : IMessageHandler<RefundApproved>
    RefundRejectedNotificationHandler.cs  : IMessageHandler<RefundRejected>
    OrderPlacedNotificationHandler.cs     : IMessageHandler<OrderPlaced>
    OrderCancelledNotificationHandler.cs  : IMessageHandler<OrderCancelled>
    PaymentConfirmedNotificationHandler.cs: IMessageHandler<PaymentConfirmed>
    PaymentExpiredNotificationHandler.cs  : IMessageHandler<PaymentExpired>
  Services/
    INotificationService.cs
    NotificationService.cs
  Extensions.cs
```

### § 4 Notification service contract

The concrete notification delivery mechanism is hidden behind `INotificationService`. The interface is
channel-agnostic — it carries a `userId`, an `eventType` discriminator, and a plain-text `message`:

```csharp
public interface INotificationService
{
    Task NotifyAsync(string userId, string eventType, string message, CancellationToken ct = default);
}
```

The Infrastructure layer provides a `SignalRNotificationService` that pushes to the connected browser
client via `IHubContext<NotificationHub>.Clients.User(userId).SendAsync("ReceiveNotification", payload)`.
The Application stub (`LoggingNotificationService`) logs the event without any network call and
is replaced at runtime by the Infrastructure registration (last-registration-wins DI).

The `userId` is carried directly in `OrderPlaced` (no resolver needed for that event); for events
carrying only an `orderId`, `IOrderUserResolver` performs a read-only projection against
`OrdersDbContext` to obtain the `userId`.

### § 5 Folder structure

```
Application/Supporting/Communication/
  Handlers/         ← one file per IMessageHandler<T>
  Services/         ← INotificationService + LoggingNotificationService (stub)
  Contracts/        ← IOrderUserResolver + NullOrderUserResolver
  Extensions.cs     ← AddCommunicationServices(IServiceCollection)

Infrastructure/Supporting/Communication/
  Hubs/
    NotificationHub.cs           ← SignalR Hub (push-only, no client methods)
  Services/
    SignalRNotificationService.cs ← INotificationService impl via IHubContext<NotificationHub>
  HubEndpointExtensions.cs       ← MapCommunicationHubs(IEndpointRouteBuilder)
  Extensions.cs                  ← AddCommunicationInfrastructure: AddSignalR() + registers impl
```

No domain model. No `DbContext`. `IOrderUserResolver` is implemented in the Orders BC's Infrastructure
(`Infrastructure.Sales.Orders.Adapters.OrderUserResolverAdapter`) — the providing BC owns its adapter.

### § 6 Client-side integration

Clients connect to `/hubs/notifications` using the SignalR JavaScript client. They subscribe to
the `"ReceiveNotification"` method which receives `{ EventType, Message }`. Authentication uses
ASP.NET Core Identity cookies (Web) or JWT bearer (API). The hub uses the default
`IUserIdProvider` (maps `ClaimTypes.NameIdentifier` → `userId`), so only the authenticated user
receives their own notifications.

## Consequences

### Positive
- Notifications are isolated — changing templates, channels, or retry logic never touches core BCs
- Leaf-node position (consumer only) eliminates any risk of notification logic affecting order/payment lifecycle
- Handler per message type enables independent evolution of each notification

### Negative
- In-memory `IMessageBroker` does not survive process restart — notifications are lost on crash before delivery
- No retry or dead-letter queue — transient SMTP/SMS failures silently drop notifications

### Risks & mitigations
- **Lost notifications on crash**: mitigated long-term by adding outbox/inbox pattern (ADR-0010 §5) before production use; acceptable during development
- **User email resolution from IAM**: read-only access — no write coupling; acceptable until a dedicated user-info ACL interface is designed

## Alternatives considered

- **Embed notifications in each source BC handler** — rejected because it scatters notification logic across BCs and couples them to delivery infrastructure
- **Synchronous notification in application service** — rejected because SMTP/SMS latency would block the calling service thread and violate the async event-driven direction (ADR-0002 §2)
- **Full outbox + notification log from day one** — rejected because delivery infrastructure is not yet chosen; premature complexity; outbox is an incremental addition once the BC is proven

## References

- [ADR-0002 §2](./0002-post-event-storming-architectural-evolution-strategy.md) — async event-driven direction
- [ADR-0010 — In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0014 — Sales/Orders BC](./0014-sales-orders-bc-design.md) — source of `OrderPlaced`, `OrderCancelled`
- [ADR-0015 — Sales/Payments BC](./0015-sales-payments-bc-design.md) — source of `PaymentConfirmed`, `PaymentExpired`
- [ADR-0016 — Sales/Coupons BC](./0016-sales-coupons-bc-design.md) — future source of `CouponExpired`
- [ADR-0017 — Sales/Fulfillment BC](./0017-sales-fulfillment-bc-design.md) — source of `RefundApproved`, `RefundRejected`
