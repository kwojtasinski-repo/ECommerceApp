# ECommerceApp — Roadmap

> **Living document.** Update when phases are completed or priorities shift.
> For architectural decisions see `docs/adr/`. For BC implementation status see `docs/architecture/bounded-context-map.md`.
> For confirmed bugs not yet fixed see `.github/context/known-issues.md`.

---

## Active roadmaps

| Area | File | ADR | Status |
|---|---|---|---|
| **Sales/Orders — atomic switch** | [`orders-atomic-switch.md`](./orders-atomic-switch.md) | [ADR-0014](../adr/0014-sales-orders-bc-design.md) | ✅ Switch live — all acceptance criteria met; Step 8 (legacy cleanup) deferred |
| **Sales/Payments — DB migrations + atomic switch** | [`payments-atomic-switch.md`](./payments-atomic-switch.md) | [ADR-0015](../adr/0015-sales-payments-bc-design.md) | ✅ Switch live — all acceptance criteria met; Step 5/6 (legacy cleanup) deferred |
| **Sales/Coupons — Slice 1 atomic switch** | — (see ADR) | [ADR-0016](../adr/0016-sales-coupons-bc-design.md) | ✅ Switch live — `CouponController` migrated to `Areas/Sales`, legacy UI controllers deleted, nav updated. Legacy service DI retained (Step 8). |
| **Presale/Checkout — Slice 2** | [`presale-slice2.md`](./presale-slice2.md) | [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 | ✅ Switch live — implementation complete, integration tests ✅, EC-001 decision ✅ |
| **Identity/IAM — atomic switch** | [`iam-atomic-switch.md`](./iam-atomic-switch.md) | [ADR-0019](../adr/0019-identity-iam-bc-design.md) | ✅ Switch complete — `Context` → `DbContext`, `Domain.Model.ApplicationUser` deleted, legacy controllers/services/repos deleted |
| **Identity/IAM — refresh token** | [`iam-refresh-token.md`](./iam-refresh-token.md) | ADR-0019 amendment pending | 🔵 Planned — design settled, not started |
| **Frontend error pipeline & JS migration** | [`frontend-pipeline.md`](./frontend-pipeline.md) | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) | ✅ Phase 1–4 complete |
| **Storefront — `/offers` public browsing** | [`storefront-offers.md`](./storefront-offers.md) | — | ✅ §1 routing + §2 home page done — §3 category strip optional |
| **Chunked image upload** | [`chunked-upload.md`](./chunked-upload.md) | — | 🔵 Planned — server-driven design agreed, v1 spike not started |

> **Routing strategy for all controller migrations**: [ADR-0024](../adr/0024-controller-routing-strategy.md) — Web uses ASP.NET Core Areas (new parallel routes), API uses in-place swap. Applies to Orders, Payments, and all subsequent BC controller switches.

---

## Dependency order for next BC work

> Two parallel tracks. Implementation proceeds on all BCs simultaneously (Parallel Change).
> Atomic switches are sequential and deferred until ~80–95% of backend implementations are complete.

**Implementation track** — build in parallel, no ordering constraint:
```
Sales/Coupons Slice 1   ─► can start now
Sales/Fulfillment Slice 1 ─► can start now
Presale/Checkout Slice 2 ✅ Switch live
```

**Switch track** — sequential, executed after backend implementations reach 80–95%:
```
Sales/Orders (DB migration + atomic switch) ✅ Switch live
  └─► Sales/Payments (DB migration + atomic switch) ✅ Switch live
  └─► Presale/Checkout Slice 2 ✅ Switch live
        └─► Sales/Coupons (atomic switch) ✅ Switch live
        └─► Sales/Fulfillment (atomic switch) ✅ Switch live
              └─► Supporting/Communication — ✅ unblocked
```

Full details and blocking analysis: [`bounded-context-map.md § Next BCs to implement`](../architecture/bounded-context-map.md).

---

## Design amendments — completed ✅

> All P0–P3 design amendments have been implemented and verified (all unit, integration, and architecture tests green).
> These are documented as ADR amendments.

| Priority | Topic | ADR Amendment | What | Status |
|---|---|---|---|---|
| **P0** | Fulfillment → Inventory parallel fan-out | [ADR-0017 §13](../adr/0017-sales-fulfillment-bc-design.md), [ADR-0011 amendment](../adr/0011-inventory-availability-bc-design.md) | 3 Inventory handlers (ShipmentDelivered/Failed/PartiallyDelivered); OrderShippedHandler retired; ShipmentStatus.PartiallyDelivered; enriched messages with Items[]; `StockReconciliationRequired` alert on stock operation failures | ✅ Done |
| **P1** | Order event payload enrichment | [ADR-0014 §19](../adr/0014-sales-orders-bc-design.md) | ShipmentFailurePayload, enriched PartialFulfilmentPayload (ShipmentId + FailedItems) | ✅ Done |
| **P1** | Operator notifications | [ADR-0014 §19](../adr/0014-sales-orders-bc-design.md), [ADR-0018 §2](../adr/0018-supporting-communication-bc-design.md) | OrderRequiresAttention message (no consumer until Communication BC) | ✅ Done |
| **P2** | CouponOversizeGuard | [ADR-0016 §10.1](../adr/0016-sales-coupons-bc-design.md) | Constraint rule for fixed-amount coupons exceeding order total; always-on (unconditional injection); per-coupon `BypassOversizeGuard` override | ✅ Done |
| **P3** | Catalog → Coupons name sync | [ADR-0016 §10.2](../adr/0016-sales-coupons-bc-design.md) | 3 Catalog messages (ProductNameChanged, CategoryNameChanged, TagNameChanged) + 3 Coupons handlers + IScopeTargetRepository for CouponScopeTarget.TargetName sync | ✅ Done |

---

## Future architectural considerations

> Items for future discussion and implementation. Not blocked — deferred by choice until
> we have enough production experience and confidence in the current design.

| # | Topic | Rationale | When to act | Tracking |
|---|---|---|---|---|
| **F1** | **ADR slimming — split implementation details** | ADRs are growing large (many exceed 300 lines). Split detailed implementation sections (domain model specs, handler contracts, EF configurations, validation rules) into dedicated `docs/adr/implementation/XXXX-impl.md` files linked from the parent ADR. The ADR retains high-level design decisions; the implementation file captures how the solution works in detail with fine-grained requirements. | When any ADR exceeds ~250 lines, or during the next atomic-switch phase when ADRs are reviewed | `docs/adr/implementation/` directory (to be created) |
| **F2** | **Common location for public event contracts** | Currently each BC defines its own message records under `Application/<BC>/Messages/`. Consider a shared `Application/Messaging/Contracts/` or per-publisher namespace convention so consumers can discover available events without scanning every BC. Trade-off: shared location vs. BC autonomy. | Future design discussion — after all BCs are implemented and cross-BC message count stabilises | — |
| **F3** | **Saga / Orchestrator pattern evaluation** | Some multi-step flows (e.g., PlaceOrder → Payment → Inventory → Fulfillment) are currently choreography-based (event chains). If flows grow more complex or require compensation logic, evaluate saga/orchestrator patterns. Do not refactor prematurely — wait until we confirm the design direction. | When we identify a flow that requires explicit compensation or ordering guarantees beyond what choreography provides | Separate ADR required |
| **F4** | **Event handler chain refactoring** ⚠️ *crucial* | Several handlers publish new events from within an event handler (e.g., `OrderPaymentExpiredHandler` handles `PaymentExpired` → cancels order → publishes `OrderCancelled`). While this works for the current in-memory broker, it creates implicit chains that are hard to trace, test in isolation, and reason about failure/retry semantics. Refactor selected flows to use direct method calls within a single BC or introduce an explicit orchestrator where the chain crosses BC boundaries. | After the current design is validated in production and we have clarity on which chains are problematic. Priority: high — affects debuggability and resilience. | — |
| **F5** | **ModuleClient evolution — universal cross-BC adapter** ⚠️ *important* | `ModuleClient` uses `GetService()` (singular) — only dispatches to the last-registered handler ([KI-007](../../.github/context/known-issues.md)). Fix: change to `GetServices()`. Beyond the bug fix, the `ModuleClient` concept (generic cross-module dispatch via `IServiceProvider`) is architecturally superior to the current per-BC adapter classes (`OrderExistenceCheckerAdapter`, `CatalogClientAdapter`, `StockAvailabilityCheckerAdapter`, etc.). Evolve `ModuleClient` into a universal cross-BC communication mechanism that supports: (1) fire-and-forget events (current), (2) query/request-response semantics (new — replace adapter interfaces), (3) optional async dispatch. This would eliminate the need to write a new adapter class every time one BC needs data from another. | Phase 1 (bug fix): can be done now — change `GetService` → `GetServices` + loop. Phase 2 (query support): after atomic switches are complete and adapter usage patterns stabilise. | [KI-007](../../.github/context/known-issues.md) |

---

## Deferred (scheduled separately)

| Item | Blocked by | ADR / Tracking |
|---|---|---|
| Per-BC DbContext interfaces | 80–100% BC implementations complete | [ADR-0013](../adr/0013-per-bc-dbcontext-interfaces.md) |

---

*Last reviewed: 2026-03-22*
