# ADR-0018: Supporting/Communication BC

**Status**: Accepted
**BC**: Supporting/Communication

## What this decision covers
`INotificationService` stub, `IOrderUserResolver`, and the 7 notification handlers
for order/payment/refund lifecycle events.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0018-supporting-communication-bc-design.md | Full design: notification handlers, INotificationService, DI wiring | Adding a new notification handler |

## Key rules
- `LoggingNotificationService` is the current implementation (stub)
- All handlers are in `Application/Supporting/Communication/Handlers/`

## Related ADRs
- ADR-0010 (message broker) — handlers subscribe via IMessageHandler<T>
