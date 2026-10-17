# Availability invalidation scenarios

How soft reservations react when Inventory reports that availability dropped, and which edge
cases are accepted. Code: `AvailabilityReservationChangeSagaDefinition`,
`CheckoutReservationAvailabilityDroppedHandler`, `SoftReservationService.InvalidateExcessForProductAsync`.
The core design document lists the same scenarios and the accepted risk in short form.

## Flow

1. `StockService` publishes `StockAvailabilityChanged(ProductId, AvailableQuantity, OccurredAt)`
   after every quantity-changing operation.
2. The `AvailabilityReservationChange` saga (single `Notify` step, correlation id = `ProductId`)
   enqueues `CheckoutReservationAvailabilityDropped` into the Outbox.
3. `OutboxPollerService` dispatches the row; `CheckoutReservationAvailabilityDroppedHandler` calls
   `InvalidateExcessForProductAsync(productId, availableQuantity)`.
4. The service sums the `Active` reservations of the product. If the sum fits the available
   quantity it returns. Otherwise it deletes reservations in descending `ExpiresAt` order
   (newest first) until the remainder fits. Each deletion cancels the expiry job and evicts the
   cache and user-index entries. `Committed` reservations are never touched.

The handler is **state-based**: it recomputes from the current reservations on every run instead
of applying a delta, which is why most repeat deliveries are harmless.

## Scenarios

| # | Scenario | Outcome | Status |
|---|----------|---------|--------|
| S1 | Availability changes and active holds still fit | Handler returns without deleting anything | Normal, happy path |
| S2 | Availability drops below the sum of active holds | Newest holds are removed until the rest fits; oldest holds survive | Normal, happy path |
| S3 | Active holds exist alongside `Committed` ones | Only `Active` holds are considered and removed | Normal, happy path |
| S4 | Every stock change emits an event, so bursts of events arrive for one product | Each event starts its own saga instance and runs the handler in turn; later runs see the state left by earlier ones | Normal |
| S5 | The same row is delivered twice in sequence | The second run finds the holds already fit and does nothing | Safe, state-based |
| S6 | The hold was already removed by the expiry job or a user cancel before the handler reached it | `DeleteAsync` throws `DbUpdateConcurrencyException`; the message fails, is retried with backoff and the retry recomputes from fresh data | Safe, self-healing |
| S7 | Two pollers deliver the same row concurrently | Same exception path as S6 for the losing run. The atomic outbox lease (`TryAcquireLeaseAsync`, see [ADR-0010](../../0010/outbox-claim-and-inmemory-test-decorator.md)) removes this at the source | Mitigated |
| S8 | Process dies after the handler ran but before `MarkDispatched` | The row is reset as a zombie after the 5-minute lock window and redelivered; the handler finds nothing left to do, or finishes a partially completed loop | Safe, state-based |
| S9 | Stale redelivery: the replayed message carries an old `AvailableQuantity` after availability has risen | Holds that were valid at the current stock level are removed | **Accepted risk** (see below) |

## Risks

### R1 — Stale message replay removes valid holds (accepted)

**Scenario.** Event A reports `available = 2` and the handler starts but the process dies. While
the row waits out the 5-minute lock window, stock rises and event B (`available = 10`) is handled
as a no-op. The zombie row for A is then redelivered and the handler compares the holds with `2`,
removing holds that are valid at `10`. The same mechanism applies to any retry after a failed
attempt, because the message carries the value from the moment of the event.

**What happens.** The affected holds are deleted: expiry job cancelled, row removed, cache and
index entries evicted. The newest holds go first, so these are the customers who have just begun
checkout. Committed holds are not affected. No notification is sent. The customer has to
initiate checkout again, which succeeds while the current stock allows it. Hard stock in
Inventory is never changed by this path, because soft reservations never touch `StockItem`
counters and the definitive reservation happens at order placement.

**Why it is accepted.** It needs a process crash inside one handler run, a stock increase within
the next five minutes, and active holds on that product at the same time. The failure direction is
conservative (holds are released too early, never too late) and the cost is a repeated checkout
step. Detecting it would need a per-product high-water mark of `OccurredAt` (new state and a
migration) or reading availability from `StockSnapshot` instead of the message value, which
depends on snapshot update ordering that has not been verified. Neither is justified by the risk.

**Revisit when** operators report customers losing holds right after a deployment or crash, or
when the snapshot-based variant is confirmed to be ordered reliably (the cheaper fix).

### R2 — Soft reservations are advisory

An over- or under-invalidated hold cannot oversell stock. The definitive check is the Inventory
reservation made when `OrderPlaced` is processed (see ADR-0011 and the order-creation stock
pre-flight check). An under-invalidated hold, caused by a `HoldAsync` landing after the handler's
snapshot, clears on the next `StockAvailabilityChanged` for that product.
