# BC migration — historical track record

> Historical record of how Bounded Context implementation and switch tracks were sequenced during the parallel-change migration (2024–2026).
> **All BCs are now ✅ Switch live.** For the current state see [`bounded-context-map.md`](../architecture/bounded-context-map.md).
> For the active roadmap (context-mode, future architectural items) see [`README.md`](./README.md).

---

## Why this file exists

During the migration, `docs/roadmap/README.md` carried two ASCII diagrams answering "what BC can I start now?" and "in which order do we cut over?". With every BC now switched to production, those diagrams stopped guiding new work and started misleading new readers (and agents) into thinking implementation track items were still open.

They are preserved here because the sequencing — which BC blocked which, what order the cut-overs happened in — is genuinely interesting for understanding how the codebase reached its current shape.

---

## Implementation track (historical)

Two parallel tracks ran during the migration. Implementation proceeded on all BCs simultaneously (Parallel Change); atomic switches were sequential and deferred until ~80–95% of backend implementations were complete.

```
Sales/Coupons Slice 1   ─► can start now
Sales/Fulfillment Slice 1 ─► can start now
Presale/Checkout Slice 2 ✅ Switch live
```

All three landed — see the per-BC rows in [`bounded-context-map.md`](../architecture/bounded-context-map.md).

---

## Switch track (historical)

Sequential cut-overs, executed after backend implementations reached 80–95%:

```
Sales/Orders (DB migration + atomic switch) ✅ Switch live
  └─► Sales/Payments (DB migration + atomic switch) ✅ Switch live
  └─► Presale/Checkout Slice 2 ✅ Switch live
        └─► Sales/Coupons (atomic switch) ✅ Switch live
        └─► Sales/Fulfillment (atomic switch) ✅ Switch live
              └─► Supporting/Communication — ✅ unblocked
```

Supporting/Communication BC remains unblocked but unscheduled — there is no active roadmap entry for it as of this writing.

---

## See also

- [`README.md`](./README.md) — active roadmap (context-mode, future architectural items F1–F5)
- [`../architecture/bounded-context-map.md`](../architecture/bounded-context-map.md) — current per-BC state (source of truth)
- [`../adr/`](../adr/) — architectural decisions referenced by each BC row above
