# ECommerceApp — Roadmap

> **Living document.** Update when phases are completed or priorities shift.
> For architectural decisions see `docs/adr/`. For BC implementation status see `docs/architecture/bounded-context-map.md`.
> For confirmed bugs not yet fixed see `.github/context/known-issues.md`.

---

## Active roadmaps

| Area | File | ADR | Status |
|---|---|---|---|
| **Sales/Orders — atomic switch** | [`orders-atomic-switch.md`](./orders-atomic-switch.md) | [ADR-0014](../adr/0014-sales-orders-bc-design.md) | 🟡 DB migration approval pending; controller migration + switch not started |
| **Sales/Payments — DB migrations + atomic switch** | [`payments-atomic-switch.md`](./payments-atomic-switch.md) | [ADR-0015](../adr/0015-sales-payments-bc-design.md) | ⬜ Atomic switch blocked by Orders switch; implementation ✅ done |
| **Sales/Coupons — Slice 1 implementation** | — (see ADR) | [ADR-0016](../adr/0016-sales-coupons-bc-design.md) | 🟡 Implementation in progress (parallel change); atomic switch blocked by Orders + Payments |
| **Presale/Checkout — Slice 2** | [`presale-slice2.md`](./presale-slice2.md) | [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 | ⛔ Implementation blocked by Orders atomic switch |
| **Identity/IAM — atomic switch** | [`iam-atomic-switch.md`](./iam-atomic-switch.md) | [ADR-0019](../adr/0019-identity-iam-bc-design.md) | 🟡 Migration approval pending; coordinate with Orders switch |
| **Frontend error pipeline & JS migration** | [`frontend-pipeline.md`](./frontend-pipeline.md) | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) | ⬜ Phase 1–2 not started |

---

## Dependency order for next BC work

> Two parallel tracks. Implementation proceeds on all BCs simultaneously (Parallel Change).
> Atomic switches are sequential and deferred until ~80–95% of backend implementations are complete.

**Implementation track** — build in parallel, no ordering constraint (except Presale Slice 2):
```
Sales/Coupons Slice 1   ─► can start now
Sales/Fulfillment Slice 1 ─► can start now
Presale/Checkout Slice 2 ─► blocked until Orders atomic switch is live
```

**Switch track** — sequential, executed after backend implementations reach 80–95%:
```
Sales/Orders (DB migration + atomic switch)
  └─► Sales/Payments (DB migration + atomic switch)
  └─► Presale/Checkout Slice 2 (unblocked)
        └─► Sales/Coupons (atomic switch)
        └─► Sales/Fulfillment (atomic switch)
              └─► Supporting/Communication
```

Full details and blocking analysis: [`bounded-context-map.md § Next BCs to implement`](../architecture/bounded-context-map.md).

---

## Design amendments agreed (implementation pending)

> These are documented as ADR amendments, not separate roadmaps. Priority order reflects
> business impact (P0 = stock integrity risk).

| Priority | Topic | ADR Amendment | What |
|---|---|---|---|
| **P0** | Fulfillment → Inventory parallel fan-out | [ADR-0017 §13](../adr/0017-sales-fulfillment-bc-design.md), [ADR-0011 amendment](../adr/0011-inventory-availability-bc-design.md) | Fix stock leak: 3 Inventory handlers for Fulfillment messages; retire OrderShippedHandler; ShipmentStatus.PartiallyDelivered; enriched messages with Items[] |
| **P1** | Order event payload enrichment | [ADR-0014 §19](../adr/0014-sales-orders-bc-design.md) | ShipmentFailurePayload, enriched PartialFulfilmentPayload (ShipmentId + FailedItems) |
| **P1** | Operator notifications | [ADR-0014 §19](../adr/0014-sales-orders-bc-design.md), [ADR-0018 §2](../adr/0018-supporting-communication-bc-design.md) | OrderRequiresAttention message (no consumer until Communication BC) |
| **P2** | CouponOversizeGuard | [ADR-0016 §10.1](../adr/0016-sales-coupons-bc-design.md) | New constraint rule for fixed-amount coupons exceeding order total; enabled by default |
| **P3** | Catalog → Coupons name sync | [ADR-0016 §10.2](../adr/0016-sales-coupons-bc-design.md) | 3 Catalog messages + 3 Coupons handlers + IScopeTargetRepository for CouponScopeTarget.TargetName sync |

---

## Deferred (scheduled separately)

| Item | Blocked by | ADR / Tracking |
|---|---|---|
| Per-BC DbContext interfaces | 80–100% BC implementations complete | [ADR-0013](../adr/0013-per-bc-dbcontext-interfaces.md) |
| Bootstrap 5 upgrade + `modalService.js` rewrite | Bootstrap upgrade decision | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) §4 |
| IAM refresh token | Separate ADR required | [bounded-context-map.md](../architecture/bounded-context-map.md) |

---

*Last reviewed: 2026-03-12*
