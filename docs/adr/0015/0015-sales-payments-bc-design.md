# ADR-0015: Sales/Payments BC - Payment Aggregate Design

## Status
Accepted

## Date
2026-03-09

## Context

The legacy payment flow coupled Payments and Orders through synchronous reads and writes.
The Sales/Orders design in [ADR-0014](../0014/0014-sales-orders-bc-design.md) requires
payment creation, expiry, and confirmation to cross bounded-context boundaries without direct
aggregate mutation. `OrderPlaced` carries `ExpiresAt`, `TotalAmount`, and `CurrencyId`, so
Payments can initialize from a message snapshot.

The current implementation also includes the placement-compensation path: `PaymentStatus.Cancelled`
and `Payment.Cancel()` are used when placement fails.

## Architectural Decisions

### Ownership and boundaries

- Payments owns `Payment` and its lifecycle; Orders owns order status.
- Cross-BC communication uses `IMessage` contracts, the outbox, and the message broker.
- Payments has no Orders navigation property or synchronous Orders read.
- One payment per order is enforced by the unique payment `OrderId` constraint.
- Payments schedules payment expiry; Inventory retains its direct reservation timeout. Both
  paths are idempotent and may observe the same `ExpiresAt`.

### Payment model

`Payment` stores typed `PaymentId` and `PaymentOrderId`, `UserId`, captured amount and currency,
`ExpiresAt`, confirmation data, and an optimistic-locking row version. `PaymentOrderId` is a
sealed `TypedId<int>` record. Statuses are `Pending`, `Confirmed`, `Expired`, `Refunded`, and
`Cancelled`.

The aggregate exposes `Create` overloads, `Confirm`, `Expire`, `IssueRefund`, `Refund`, and
`Cancel`. Invalid transitions throw `DomainException`. Domain event records are returned by
the transition methods; `PaymentConfirmed` and `PaymentExpired` are integration messages.

### Message flow

1. Payments `OrderPlacedHandler` creates the payment, marks the outbox message processed, and
   schedules `PaymentWindowExpiredJob` with the payment ID and `OrderPlaced.ExpiresAt`.
2. The expiry job no-ops for invalid, missing, or non-pending payments; otherwise it expires
   the aggregate and enqueues `PaymentExpired` in the outbox.
3. `PaymentConfirmed` carries `PaymentId`; Orders applies `Order.ConfirmPayment` while the
   order is `Placed`.
4. `PaymentExpired` causes Orders to apply `Order.ExpirePayment` while the order is `Placed`.
   Inventory and Coupons consume the message independently.
5. `OrderPlacementFailedHandler` calls `Payment.Cancel()` for placement compensation.

### Persistence and registration

Payments has its own `PaymentsDbContext` and `payments` schema. Infrastructure maps typed IDs,
the row version, and the unique `OrderId` constraint, and registers the context, unit of work,
repository, and migrations. Application registers the handlers and scheduled task.

## Consequences

Payments no longer depends on synchronous Orders operations; payment data is a placement
snapshot; timeout ownership matches the payment lifecycle; and retries are protected by the
processed-message guard, transactions, outbox, unique constraint, and terminal-state no-ops.
The intentional cost is constructor-sensitive message changes and overlapping Payments and
Inventory timeout paths. A reconciliation sweep remains the mitigation for an interrupted
schedule.

## Alternatives

- Put the timer in Orders: rejected because expiry belongs to `Payment.ExpiresAt`.
- Remove Inventory's timeout: rejected because direct reservation release is faster when a
  message chain is delayed.
- Read amount and currency synchronously from Orders: rejected because the placement snapshot
  removes cross-BC coupling.
- Add a separate payment-initialized message: rejected because `OrderPlaced` already contains
  all initialization data.
- Migrate refunds completely here: deferred; the Refunds BC migration remains separate.

## References

- [ADR-0002 - Post-Event-Storming Architectural Evolution Strategy](../0002/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0006 - TypedId and Value Objects](../0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0009 - Supporting/TimeManagement BC](../0009/0009-supporting-timemanagement-bc-design.md)
- [ADR-0010 - In-Memory Message Broker](../0010/0010-in-memory-message-broker-for-cross-bc-communication.md)
- [ADR-0011 - Inventory/Availability BC](../0011/0011-inventory-availability-bc-design.md)
- [ADR-0014 - Sales/Orders BC](../0014/0014-sales-orders-bc-design.md)

## Companions

- [Conformance checklist](checklist.md)
- [Migration plan](migration-plan.md)
- [Payment state machine](example-implementation/payment-state-machine.md)
- [Payment window expiry flow](example-implementation/payment-window-expiry-flow.md)
- [Order cancellation flow](example-implementation/order-cancel-flow.md)
