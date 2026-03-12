# ADR-0018: Supporting/Communication BC — Customer Notification Design

## Status
Proposed

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
- Sends customer notifications (email and/or SMS) in response to those messages
- Has **no domain model** — application-layer only
- **Never publishes** messages back to any core BC — it is a pure consumer

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
| `RefundApproved` | Fulfillment (ADR-0017) | Email: refund approved, items + amounts |
| `RefundRejected` | Fulfillment (ADR-0017) | Email: refund rejected, reason |
| `OrderPlaced` | Orders (ADR-0014) | Email: order confirmation |
| `OrderCancelled` | Orders (ADR-0014) | Email: order cancellation |
| `PaymentConfirmed` | Payments (ADR-0015) | Email: payment received |
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

The concrete notification delivery mechanism (SMTP, SendGrid, Twilio, etc.) is hidden behind
`INotificationService`. The interface surface is deliberately minimal at this stage:

```csharp
public interface INotificationService
{
    Task SendEmailAsync(string recipientUserId, string subject, string body, CancellationToken ct);
}
```

The `recipientUserId` is resolved to an email address via a user query against `IamDbContext`
(read-only, no cross-BC write coupling). Concrete implementation is deferred — a stub logging
implementation is acceptable until delivery infrastructure is in place.

### § 5 Folder structure

```
Application/Supporting/Communication/
  Handlers/         ← one file per IMessageHandler<T>
  Services/         ← INotificationService + impl
  Extensions.cs     ← AddCommunication(IServiceCollection)
```

No `Domain/Supporting/Communication/` folder — there is no domain model.  
No `Infrastructure/Supporting/Communication/` folder initially — if a notification log is
added later, an `Infrastructure` folder and `NotificationLogDbContext` are introduced at that time.

### § 6 Delivery infrastructure (deferred)

Concrete email/SMS delivery, template engine, opt-out preference storage, and retry policy are
implementation details to be decided bottom-up during Slice 1 implementation. This ADR does not
prescribe them.

If a notification audit log is needed, it will get its own `NotificationLogDbContext` with schema
`communication`. That decision requires a migration policy approval per ADR migration policy.

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

## Migration plan

1. Implement `INotificationService` stub (logs to `ILogger` — no real delivery)
2. Implement `IMessageHandler<T>` for each subscribed message — wire via `AddCommunication()` extensions
3. Activate after Fulfillment Slice 1 is switched (provides `RefundApproved` / `RefundRejected`)
4. Replace stub with concrete SMTP adapter when delivery infrastructure is selected
5. Add `CouponExpired` handler after Coupons Slice 2

## Conformance checklist

- [ ] No `Domain/Supporting/Communication/` folder exists (no domain model)
- [ ] All handlers implement `IMessageHandler<T>` — no direct service-to-service calls
- [ ] `INotificationService` is the only external dependency injected into handlers
- [ ] No message publishing from Communication BC handlers — consumer only
- [ ] `Extensions.cs` registers all handlers via `AddCommunication(IServiceCollection)`
- [ ] `recipientUserId` is resolved via read-only IAM query — no write coupling

## References

- [ADR-0002 §2](./0002-post-event-storming-architectural-evolution-strategy.md) — async event-driven direction
- [ADR-0010 — In-Memory Message Broker](./0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0014 — Sales/Orders BC](./0014-sales-orders-bc-design.md) — source of `OrderPlaced`, `OrderCancelled`
- [ADR-0015 — Sales/Payments BC](./0015-sales-payments-bc-design.md) — source of `PaymentConfirmed`, `PaymentExpired`
- [ADR-0016 — Sales/Coupons BC](./0016-sales-coupons-bc-design.md) — future source of `CouponExpired`
- [ADR-0017 — Sales/Fulfillment BC](./0017-sales-fulfillment-bc-design.md) — source of `RefundApproved`, `RefundRejected`
