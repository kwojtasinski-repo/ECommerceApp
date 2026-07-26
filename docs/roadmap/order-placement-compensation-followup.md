# Order-placement compensation — follow-up work (F3/F4 deep-dive)

> **Status:** Paused — parked in favor of another in-progress work stream. Workstream 1 (cart restore) and
> workstream 4 (docs correction) are done. **Next up: workstream 2 (Outbox pattern)** — see table below.
> **Origin:** ad-hoc analysis of roadmap items F3 (saga/orchestrator) and F4 (handler chain refactoring),
> see [`README.md`](./README.md#future-architectural-considerations) and [`saga-pattern.md`](./saga-pattern.md).
> **Goal of this doc:** capture enough decisions that work can resume without re-litigating scope.
> Where a decision was made without asking, it is marked explicitly — verify it still holds before
> building on it blindly.

---

## Why this exists

F3 analysis found that ADR-0026 Option A (choreography + compensation) is implemented, but has a real
gap: `Presale.OrderPlacementFailedHandler` only logs a warning — it does not restore the cart. F4
analysis found the flagged handler chain (`OrderPaymentExpiredHandler` → `OrderCancelled` → Inventory +
Coupons) was already fixed in an earlier, undocumented amendment to ADR-0026 (flat fan-out, 2026-04-XX),
and that the only remaining handlers that publish further events (`ShipmentDelivered/Failed/PartiallyDelivered`
in Inventory) are low-risk leaf publishers, not chains — but worth deduplicating anyway as a
future-proofing move, not a bug fix.

Full findings live in the conversation that produced this doc; the actionable parts are captured below.

---

## Agreed scope, in priority order

| # | Workstream | What | Status |
|---|---|---|---|
| **1** | **Cart restore** | Implement `ICartService.RestoreAsync` + wire it into `Presale.OrderPlacementFailedHandler` | **Done** (2026-07-26) — implemented and validated PASS (build, 1008 unit + 221 integration tests green, spec-conformance and code review clean). Pipeline plan/validation files deleted per convention; this doc's spec above is the permanent record. |
| **2** | **Outbox pattern** | At-least-once delivery for `IMessageBroker`; unblocks saga Option B | **NOT STARTED — next up.** Verified in code (2026-07-26): zero `Outbox` hits anywhere in `.cs` files; `InMemoryMessageBroker`/`ModuleClient`/`AsyncMessageDispatcher` are all synchronous or in-process-channel only, no persistence, no retry. See [`saga-pattern.md`](./saga-pattern.md) § "Infrastructure prerequisite: Outbox Pattern" for scope. |
| **3** | **F4 cleanup** | Deduplicate `ShipmentDelivered/Failed/PartiallyDelivered` handlers into shared logic — preparatory, not risk-driven (new requirements are expected to land here later) | Not started. Verified in code (2026-07-26): `ShipmentDeliveredHandler`, `ShipmentFailedHandler`, `ShipmentPartiallyDeliveredHandler` (`ECommerceApp.Application/Inventory/Availability/Handlers/`) are still independently copy-pasted, no shared base class or helper. |
| **4** | **Docs correction** | `README.md` F3/F4 rows + `saga-pattern.md` don't reflect that ADR-0026 Option A and the flat-fan-out amendment already shipped | **Done** (2026-07-26) — `README.md` F4 row and `saga-pattern.md` (status banner, event-chain table, Gap 3, sequencing) updated. Also caught and fixed a factual error introduced by the first pass: `README.md` had claimed `OrderCancelled` was "unused, reserved for a future manual-cancel path" — verified false, it's actively published by `OrderService.CancelOrderAsync` (manual-cancel endpoint) with 4 live handlers; only the auto-expiry chain to it was removed. |

Workstream 1 was required first and is done. Workstream 4 was low-effort and piggybacked alongside.
**Workstream 2 (Outbox) is next** — it's the prerequisite blocking saga Option B and F4's "before Option B"
sequencing in `saga-pattern.md`. Re-confirm priority vs. workstream 3 if circumstances changed since this
note was written.

---

## Concrete trigger conditions for the cart-restore gap (why it's not a rare edge case)

Verified in code, not assumed:

- `AddApplication()` (`ECommerceApp.Application/DependencyInjection.cs:41-47`) registers `IMessageHandler<OrderPlaced>`
  in this order: **Inventory → Presale → Orders(snapshot) → Payments → Communication(notification) → Communication(email)**.
  `.NET`'s `GetServices()` preserves registration order (already relied on elsewhere per roadmap item F5/KI-007).
- `ModuleClient.PublishAsync` (`ECommerceApp.Infrastructure/Messaging/ModuleClient.cs:27-31`) is a plain
  `foreach` with **no try/catch** — the first handler to throw stops the whole fan-out; later handlers never run.
- `Presale.OrderPlacedHandler` (`ECommerceApp.Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs:29-30`)
  clears the cart on its **first line**, before removing soft reservations on the second.

Presale is handler **2 of 6**. Consequence: the cart is gone almost immediately, and the gap fires if
*any* of the remaining four handlers throws afterward — including `Payments.OrderPlacedHandler` (payment
creation failure) or either Communication handler (e.g. an SMTP/notification-provider hiccup). This is
the opposite of what the ADR-0026 narrative assumes (it describes cart-not-cleared as the risk); in the
actual registration order the cart is cleared early and stays gone. Treat this as a realistic, not
theoretical, gap.

---

## Workstream 1 spec — `ICartService.RestoreAsync`

Decisions below were made by inspecting `CartService.cs`, `ICartService.cs`, `ISoftReservationService.cs`,
and existing tests — not guessed. Proceed directly on these unless code has changed since this was written.

**New DTO** (`Presale.Checkout.DTOs`): `record CartRestoreItem(int ProductId, int Quantity)`.

**New interface method:**
```csharp
Task RestoreAsync(PresaleUserId userId, IReadOnlyList<CartRestoreItem> items, CancellationToken ct = default);
```

**Implementation semantics (decided):**
- **Overwrite, not additive.** For each item, `CartLine.Create(userId.Value, item.ProductId, item.Quantity)` +
  `_cartRepo.UpsertAsync(...)` — same primitive `SetCartItemAsync` uses. **Gotcha verified in code:**
  `PresaleUserId` (`ECommerceApp.Domain/Presale/Checkout/PresaleUserId.cs`) only has an implicit conversion
  *from* `string`, not to it — `CartLine.Create` takes a `string`, so this must be `userId.Value`, not
  `userId` directly (passing `userId` won't compile). Do **not** route through
  `AddToCartAsync` (its additive + `MaxQuantityPerOrderLine`-limit logic could silently drop or shrink
  a legitimate restore).
- **No catalog-existence validation.** `GetCartAsync`/`RefreshCacheAsync` already tolerates products
  missing from the catalog (name resolves to `null` in `CartLineVm`) — restore doesn't need to pre-check.
- **Refresh the cache once**, after the loop — not per item (repo has no bulk-upsert; loop
  `UpsertAsync` calls, single `RefreshCacheAsync` at the end).
- **Soft reservations are explicitly out of scope.** `ISoftReservationService` is not touched by this
  workstream. Reasoning: soft reservations are a short-lived checkout-time UI lock (created later in the
  checkout flow, not at add-to-cart time); Inventory's own compensation handler has already released the
  real stock hold, and re-creating a soft reservation here risks re-locking stock unnecessarily. The user
  re-acquires one naturally next time they go through checkout. This is a deliberate cut — revisit only
  if product feedback says otherwise.
- **Known accepted limitation:** if the user manually re-added a product to their cart in the (short)
  window before compensation runs, `RestoreAsync` overwrites that line with the restored quantity rather
  than merging. Not handled — flagged here so it isn't mistaken for an oversight if noticed later.

**Wiring:** `Presale.OrderPlacementFailedHandler` gets `ICartService` injected, replaces the TODO block
with:
```csharp
var items = message.Items.Select(i => new CartRestoreItem(i.ProductId, i.Quantity)).ToList();
await _cartService.RestoreAsync(new PresaleUserId(message.UserId), items, ct);
```
Wrap in try/catch and log-on-failure rather than rethrow — compensation handlers in this codebase are
expected to be best-effort/no-op-safe (see `Payments.OrderPlacementFailedHandler`,
`Inventory.OrderPlacementFailedHandler` for the existing idempotent pattern).

---

## File map for workstream 1 (verified paths — nothing to hunt for on resume)

| File | Change |
|---|---|
| `ECommerceApp.Application/Presale/Checkout/DTOs/CartRestoreItem.cs` | **New.** `public record CartRestoreItem(int ProductId, int Quantity);` — kept Presale-local rather than reusing `Sales.Orders.Messages.OrderPlacedItem`, to avoid leaking a Sales.Orders contract into Presale's public service interface (BC boundary discipline — same reasoning as roadmap item F2). |
| `ECommerceApp.Application/Presale/Checkout/Services/ICartService.cs` | Add `RestoreAsync` method. **Verified only one implementer exists** (`CartService`) — safe interface addition, nothing else to update for compile-correctness. |
| `ECommerceApp.Application/Presale/Checkout/Services/CartService.cs` | Implement `RestoreAsync`, loop over items calling `_cartRepo.UpsertAsync`, then one `RefreshCacheAsync(userId, ct)` at the end (mirror the existing private method, same as `SetCartItemAsync` does per-item today). |
| `ECommerceApp.Domain/Presale/Checkout/ICartLineRepository.cs` | **No change needed.** Confirmed: only single-item `UpsertAsync(CartLine, ct)` exists, no bulk upsert — the per-item loop in `CartService.RestoreAsync` is intentional, not a workaround. |
| `ECommerceApp.Application/Presale/Checkout/Handlers/OrderPlacementFailedHandler.cs` | Inject `ICartService` via constructor, replace the TODO block (lines 18-27 as of this writing) with the `RestoreAsync` call. **No DI registration change needed** — `ICartService` is already registered (`Presale/Checkout/Services/Extensions.cs:22`, `AddScoped<ICartService, CartService>()`), and the handler itself is already registered (`Extensions.cs:27`, `AddScoped<IMessageHandler<OrderPlacementFailed>, OrderPlacementFailedHandler>()`) — DI only needs the constructor to pick up the new dependency automatically. |
| — | **No database/EF changes.** `CartRestoreItem` carries the same shape (`ProductId`, `Quantity`) already persisted by `CartLine` — no new columns, no migration. |

---

## Test plan for workstream 1

Baseline established and green before starting (run again before merging):

| Suite | Filter | Result at time of writing |
|---|---|---|
| Unit — `Presale.Checkout` | `FullyQualifiedName~Presale.Checkout` | 90 passed |
| Integration — CrossBC + Presale.Checkout | `FullyQualifiedName~CrossBC.OrderPlaced\|FullyQualifiedName~Presale.Checkout` | 23 passed |
| Integration — `OrderPlacementFailedFanOutTests` | `FullyQualifiedName~OrderPlacementFailedFanOutTests` | 5 passed |

None of these currently assert cart-restore behavior (the docstring in `OrderPlacementFailedFanOutTests.cs`
lists "Presale BC — logs warning (cart restore deferred)" but there is no test method for it). New coverage
needed:

1. `ECommerceApp.UnitTests/Presale/Checkout/CartServiceTests.cs`: add
   `RestoreAsync_ShouldUpsertEachLineAndRefreshCache`, `RestoreAsync_ProductNotInCatalog_ShouldStillRestoreLine`.
2. `ECommerceApp.UnitTests/Presale/Checkout/OrderPlacementFailedHandlerTests.cs`: assert `RestoreAsync` is
   called (via a new `Mock<ICartService>` in the constructor, matching this file's existing `Mock<ILogger<...>>`
   pattern) with the message's items; assert a throwing `RestoreAsync` is caught and logged, not propagated.
3. `ECommerceApp.IntegrationTests/CrossBC/OrderPlacementFailedFanOutTests.cs`: new test under a
   `// ── Presale BC compensation ──` section (mirrors the existing `// ── Payments BC ──` /
   `// ── Inventory BC ──` section headers in that file) — seed a cart via `GetRequiredService<ICartService>()`,
   publish `OrderPlaced` then `OrderPlacementFailed`, assert the cart lines exist again via
   `ICartService.GetCartAsync`. Also update the class docstring (lines 17-25) — it already claims Presale
   compensation is covered ("logs warning (cart restore deferred)"); once this lands it should say what the
   new test actually verifies.

---

## Not decided yet (raise when resuming, don't assume)

- Exact wording for the updated `LogWarning`/`LogInformation` message in the handler.
- Whether workstream 2 (Outbox) is a shared `messaging.Outbox` table or per-BC — `saga-pattern.md`
  §Infrastructure prerequisite floats both, no decision made.
- Whether workstream 3's dedup extracts a shared method inside Inventory's `Availability` services or a
  small internal helper class — not designed yet, only agreed that it should happen.
