# ECommerceApp — Roadmap

> **Living document.** Update when phases are completed or priorities shift.
> For architectural decisions see `docs/adr/`. For BC implementation status see `docs/architecture/bounded-context-map.md`.
> For confirmed bugs not yet fixed see `.github/context/known-issues.md`.

---

## Active roadmaps

| Area | File | ADR | Status |
|---|---|---|---|
| **Sales/Orders — atomic switch** | [`orders-atomic-switch.md`](./orders-atomic-switch.md) | [ADR-0014](../adr/0014-sales-orders-bc-design.md) | 🟡 DB migration approval pending; controller migration + switch not started |
| **Sales/Payments — DB migrations + atomic switch** | [`payments-atomic-switch.md`](./payments-atomic-switch.md) | [ADR-0015](../adr/0015-sales-payments-bc-design.md) | ⬜ Blocked by Orders switch |
| **Presale/Checkout — Slice 2** | [`presale-slice2.md`](./presale-slice2.md) | [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 | ⬜ Blocked by Orders switch |
| **Identity/IAM — atomic switch** | [`iam-atomic-switch.md`](./iam-atomic-switch.md) | [ADR-0019](../adr/0019-identity-iam-bc-design.md) | 🟡 Migration approval pending; coordinate with Orders switch |
| **Frontend error pipeline & JS migration** | [`frontend-pipeline.md`](./frontend-pipeline.md) | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) | ⬜ Phase 1–2 not started |

---

## Dependency order for next BC work

```
Sales/Orders (DB migration + integration tests + atomic switch)
  └─► Presale/Checkout Slice 2
  └─► Sales/Payments (atomic switch)
        └─► Sales/Coupons
        └─► Sales/Fulfillment
              └─► Supporting/Communication
```

Full details and blocking analysis: [`bounded-context-map.md § Next BCs to implement`](../architecture/bounded-context-map.md).

---

## Deferred (scheduled separately)

| Item | Blocked by | ADR / Tracking |
|---|---|---|
| Per-BC DbContext interfaces | 80–100% BC implementations complete | [ADR-0013](../adr/0013-per-bc-dbcontext-interfaces.md) |
| Bootstrap 5 upgrade + `modalService.js` rewrite | Bootstrap upgrade decision | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) §4 |
| IAM refresh token | Separate ADR required | [bounded-context-map.md](../architecture/bounded-context-map.md) |

---

*Last reviewed: 2026-03-12*
