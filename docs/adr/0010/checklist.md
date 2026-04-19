## Conformance checklist

- [ ] `IMessage`, `IMessageBroker`, `IMessageHandler<T>` live in `Application/Messaging/`
- [ ] No BC service directly injects a service from a different BC to trigger cross-BC side effects
- [ ] All cross-BC messages implement `IMessage` and live in the **publishing** BC's `Messages/` folder
- [ ] All `IMessageHandler<T>` implementations live in the **subscribing** BC
- [ ] `InMemoryMessageBroker` is `internal sealed`
- [ ] `BackgroundMessageDispatcher` is registered via `TryAddHostedService`
- [ ] `MessagingOptions` is bound from configuration (not hardcoded)
- [ ] Handler exceptions are caught and logged — never propagate to `BackgroundMessageDispatcher` loop
- [ ] `MessageEnvelope` carries `MessageId`, `RetryCount`, `RetryAfter`, `FailedAt` — `IMessage` stays clean
- [ ] `BackgroundMessageDispatcher` uses non-blocking `Task.Run(Delay + WriteAsync)` re-enqueue on retry
- [ ] `InMemoryMessageRetryMonitor` is `internal sealed` Singleton, keyed by `MessageId`
- [ ] `RetryCount`, `RetryBaseDelaySeconds`, `RetryMaxDelaySeconds` bound from `MessagingOptions` (not hardcoded)
- [ ] `HandlerOverrides` per-type override falls back to global defaults when a field is absent
- [ ] `DeadLettered` entries logged at `Critical` and retained in monitor until explicitly cleared

---
