# ADR-0010: In-Memory Message Broker for Cross-BC Communication

**Status**: Accepted — Amended 2026-02-26
**BC**: Shared infrastructure
**Last amended**: 2026-02-26

## What this decision covers
Design of `IMessageBroker` / `ModuleClient` for in-process cross-BC messaging.
Every BC uses this for publishing and subscribing to integration messages.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0010-in-memory-message-broker-for-cross-bc-communication.md | Core design: IMessageBroker, IMessageHandler<T>, ModuleClient, DI setup | Understanding cross-BC communication |
| amendments/a1-retry-observability-configuration.md | Retry policy, structured logging, configuration overrides | Debugging message delivery or tuning retries |
| checklist.md | Handler implementation rules | Code review of new handlers |
| migration-plan.md | Implementation steps (completed) | Historical reference |
| example-implementation/publish-message-example.md | How to publish an integration message from a service | Writing a publisher |
| example-implementation/register-handler-example.md | How to implement IMessageHandler<T> and register it | Writing a subscriber |
| example-implementation/multi-handler-pattern.md | Multiple handlers for one message type | Fan-out scenarios |

## Key rules
- All cross-BC communication goes through `IMessageBroker` — never inject a foreign BC service directly
- Handlers must be idempotent — broker delivers at-least-once
- Amendment A1 adds retry + observability config — use `MessageBrokerOptions` to tune

## Related ADRs
- ADR-0002 (architecture strategy) — BC boundary enforcement
- ADR-0026 (Saga) — compensation flow uses message broker fan-out
- Every BC ADR (0011–0018) — all use this for cross-BC events
