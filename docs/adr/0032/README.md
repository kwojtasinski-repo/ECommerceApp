# ADR-0032: Cross-Schema Transactional Outbox via Explicit Shared Connection

**Status**: Accepted — monolith-scoped, revisit trigger defined
**Scope**: All BCs (infrastructure rule) — `CrossContextTransactionScope`, `IOutboxTransaction`, `OutboxWriter`

## What this decision covers
Why outbox writes that cross from a BC's own schema into the centralized `messaging` schema need
an explicitly passed `IOutboxTransaction` instead of an ambient/ThreadLocal transaction, why
`System.Transactions.TransactionScope` is not used, and when this mechanism should be replaced by
per-schema local outbox tables.

## Files in this folder

| File | Purpose | When to read |
|------|---------|---------------|
| 0032-cross-schema-outbox-transaction-sharing.md | Full context, decision, alternatives considered, exit path | Adding a new mutating BC service method that must be transaction-joinable; deciding whether to invest in ambient transaction propagation; planning a BC extraction |

## Key rules
- Any service method that writes business state **and** must be called from inside an
  already-open transaction needs a transaction-accepting overload (`..., IOutboxTransaction transaction, ...`)
  alongside its transaction-owning one — added at the same time, not retrofitted after a crash.
- Never introduce an ambient/scoped-DI "current transaction" accessor as a shortcut around this —
  see Alternatives considered for why it was rejected.
- Never reach for `System.Transactions.TransactionScope` for cross-schema atomicity — MSDTC
  escalation breaks the Testcontainers-based E2E suite in CI.
- Before extracting any BC into a separate service, migrate its outbox writes off the centralized
  `messaging` schema onto a local, per-schema outbox table first (see §Exit path in the main doc).

## Related ADRs
- ADR-0010 (in-memory message broker) — the cross-BC publish/subscribe seam this outbox feeds
- ADR-0011 (Inventory/Availability BC design) — first concrete consumer (`StockService`)
- ADR-0013 (per-BC DbContext interfaces) — companion schema-isolation rule
