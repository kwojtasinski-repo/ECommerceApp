# RAG parity audit — Python (:3002) vs .NET (:3001)

Generated: 2026-05-28 18:05 UTC  
Queries: 26 (5 specific, 3 generic, 6 Sprint-1, 4 multilingual)

Source script: `tools/rag/compare_queries.py`. Run from host, not inside `rag-tools` container (script targets host loopback `localhost:3001/3002`).

## Summary

- Total queries: **26**
- Top-1 path match: **10**
- Top-1 mismatch: **16**
- Queries with errors: **0**
- Files only in Python top-5 (sum across queries): **38**
- Files only in .NET top-5 (sum across queries): **30**
- Avg |score delta| at top-1: **0.065**

## Top-1 mismatches

| Tag | Question | Python top-1 | .NET top-1 |
|---|---|---|---|
| `Q1-spec` | What is the maximum number of coupons per order and where is it configured? | `docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md` (0.679) | `docs/adr/0016/README.md` (0.562) |
| `Q5-spec` | What bounded contexts are currently blocked or in progress in the BC migration? | `.github/context/project-state.md` (0.648) | `docs/adr/0019/0019-identity-iam-bc-design.md` (0.577) |
| `G1-gen` | How is dependency injection wired across the application? | `docs/adr/0027/0027-rag-pipeline-design.md` (0.494) | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.434) |
| `G2-gen` | What architecture style does the project follow? | `docs/adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md` (0.535) | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` (0.514) |
| `G3-gen` | Where are validation rules defined for incoming DTOs? | `docs/adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` (0.675) | `docs/adr/0014/amendments/a4-operator-notifications.md` (0.520) |
| `S1-rag` | How does the RAG pipeline ingest, chunk and rank documents? | `.github/context/agent-decisions.md` (0.611) | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.439) |
| `S1-mt` | How is multitenant isolation enforced in the remote RAG deployment? | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.499) | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` (0.431) |
| `S1-boot` | What is the bootstrap flow of the context-mode container at startup? | `docs/README.md` (0.625) | `docs/getting-started-context-mode.md` (0.620) |
| `S1-cache` | What is the L3 auto-cache hook and how does it persist RAG responses? | `.github/context/agent-decisions.md` (0.762) | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.660) |
| `ML-pl-ref` | Jak są obsługiwane refresh tokeny w IAM? | `.github/context/project-state.md` (0.662) | `docs/adr/0010/amendments/a1-retry-observability-configuration.md` (0.494) |
| `QP-0027-chunk` | ADR-0027 RAG chunking strategy max tokens overlap heading boundaries decision | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` (0.686) | `docs/adr/0027/0027-rag-pipeline-design.md` (0.678) |
| `QP-0016-coup` | ADR-0016 coupon maximum per order limit five ten ceiling decision | `docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md` (0.746) | `docs/adr/0016/README.md` (0.628) |
| `QP-0019-curr` | ADR-0019 NBP exchange rate currency conversion decision API integration | `docs/adr/0014/amendments/a3-integration-flow-decisions.md` (0.668) | `docs/adr/0008/0008-supporting-currencies-bc-design.md` (0.671) |
| `QP-which-rag` | Which ADR specifically defines the RAG architecture and embedder model choice? | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.699) | `docs/adr/0028/tech-details-dotnet.md` (0.615) |
| `QP-which-mt` | Which ADR specifically governs remote multitenant RAG ingest and per-collection storage? | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` (0.635) | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` (0.572) |
| `QP-cross-bc` | What architectural decision record covers cross-bounded-context messaging and event publishing? | `.github/context/agent-decisions.md` (0.545) | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` (0.475) |

## Per-query detail

### `Q1-spec` — What is the maximum number of coupons per order and where is it configured?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md` | 0.679 | `docs/adr/0016/README.md` | 0.562 |
| 2 | `docs/adr/0016/README.md` | 0.642 | `docs/adr/0016/README.md` | 0.453 |
| 3 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.584 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.448 |
| 4 | `docs/adr/0016/README.md` | 0.489 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.419 |
| 5 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.489 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.408 |

### `Q2-spec` — How does the order placement saga handle compensation when payment fails?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.624 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.658 |
| 2 | `docs/adr/0026/README.md` | 0.583 | `docs/adr/0026/README.md` | 0.596 |
| 3 | `docs/adr/0014/README.md` | 0.520 | `docs/adr/0026/README.md` | 0.585 |
| 4 | `docs/adr/0017/0017-sales-fulfillment-bc-design.md` | 0.505 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.579 |
| 5 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.501 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.552 |

### `Q3-spec` — What are the API purchase limits for trusted vs regular users?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.725 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.721 |
| 2 | `docs/adr/0025/README.md` | 0.681 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.720 |
| 3 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.672 | `docs/adr/0025/README.md` | 0.652 |
| 4 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.616 | `docs/adr/0025/0025-api-tiered-access-trusted-purchase-policy.md` | 0.635 |
| 5 | `docs/adr/0025/README.md` | 0.600 | `docs/adr/0025/README.md` | 0.618 |

### `Q4-spec` — What are the known issues with FluentAssertions or the .NET 8 upgrade?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/known-issues.md` | 0.649 | `.github/context/known-issues.md` | 0.597 |
| 2 | `docs/adr/0028/amendments/0028-001-implementation-deviations.md` | 0.583 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.555 |
| 3 | `docs/adr/0010/amendments/a1-retry-observability-configuration.md` | 0.544 | `docs/adr/0010/amendments/a1-retry-observability-configuration.md` | 0.523 |
| 4 | `.github/context/known-issues.md` | 0.529 | `docs/adr/0028/tech-details-dotnet.md` | 0.500 |
| 5 | `.github/context/known-issues.md` | 0.509 | `docs/adr/0028/amendments/0028-001-implementation-deviations.md` | 0.485 |

### `Q5-spec` — What bounded contexts are currently blocked or in progress in the BC migration?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/project-state.md` | 0.648 | `docs/adr/0019/0019-identity-iam-bc-design.md` | 0.577 |
| 2 | `docs/adr/0002/README.md` | 0.639 | `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md` | 0.555 |
| 3 | `.github/context/project-state.md` | 0.624 | `docs/adr/0024/0024-controller-routing-strategy.md` | 0.551 |
| 4 | `.github/context/repo-index.md` | 0.565 | `docs/adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md` | 0.542 |
| 5 | `docs/adr/0019/0019-identity-iam-bc-design.md` | 0.550 | `docs/adr/0004/0004-module-taxonomy-and-bounded-context-grouping.md` | 0.536 |

### `G1-gen` — How is dependency injection wired across the application?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0027/0027-rag-pipeline-design.md` | 0.494 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.434 |
| 2 | `.github/context/repo-index.md` | 0.479 | `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md` | 0.410 |
| 3 | `docs/adr/0027/0027-rag-pipeline-design.md` | 0.476 | `.github/context/repo-index.md` | 0.389 |
| 4 | `.github/context/repo-index.md` | 0.460 | `docs/adr/0028/tech-details-python.md` | 0.388 |
| 5 | `.github/context/repo-index.md` | 0.441 | `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md` | 0.380 |

### `G2-gen` — What architecture style does the project follow?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0003/0003-feature-folder-organization-for-new-bounded-context-code.md` | 0.535 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.514 |
| 2 | `docs/adr/0028/amendments/0028-003-transport-aware-tools.md` | 0.527 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.486 |
| 3 | `docs/adr/0013/0013-per-bc-dbcontext-interfaces.md` | 0.475 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.470 |
| 4 | `docs/adr/0014/0014-sales-orders-bc-design.md` | 0.462 | `docs/adr/0001/README.md` | 0.460 |
| 5 | `docs/adr/0010/0010-in-memory-message-broker-for-cross-bc-communication.md` | 0.458 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.439 |

### `G3-gen` — Where are validation rules defined for incoming DTOs?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0006/0006-typedid-and-value-objects-as-shared-domain-primitives.md` | 0.675 | `docs/adr/0014/amendments/a4-operator-notifications.md` | 0.520 |
| 2 | `docs/adr/0012/0012-presale-checkout-bc-design.md` | 0.560 | `docs/adr/0028/tech-details-dotnet.md` | 0.456 |
| 3 | `docs/adr/0014/amendments/a4-operator-notifications.md` | 0.555 | `docs/adr/0011/amendments/a1-fulfillment-message-consumption.md` | 0.452 |
| 4 | `.github/context/agent-decisions.md` | 0.531 | `docs/adr/0018/0018-supporting-communication-bc-design.md` | 0.446 |
| 5 | `.github/context/agent-decisions.md` | 0.524 | `docs/adr/0028/amendments/0028-001-implementation-deviations.md` | 0.437 |

### `S1-saga` — How is the order placement saga orchestrated and where are compensations defined?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.560 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.640 |
| 2 | `docs/adr/0014/amendments/a3-integration-flow-decisions.md` | 0.529 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.573 |
| 3 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.521 | `docs/adr/0026/README.md` | 0.570 |
| 4 | `docs/adr/0026/README.md` | 0.485 | `docs/adr/0026/0026-order-lifecycle-saga.md` | 0.531 |
| 5 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.459 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.522 |

### `S1-rag` — How does the RAG pipeline ingest, chunk and rank documents?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/agent-decisions.md` | 0.611 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.439 |
| 2 | `.github/context/agent-decisions.md` | 0.561 | `docs/adr/0027/0027-rag-pipeline-design.md` | 0.439 |
| 3 | `.github/context/agent-decisions.md` | 0.540 | `.github/context/agent-decisions.md` | 0.382 |
| 4 | `.github/context/agent-decisions.md` | 0.518 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.376 |
| 5 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.514 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.367 |

### `S1-mt` — How is multitenant isolation enforced in the remote RAG deployment?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.499 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.431 |
| 2 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.475 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.429 |
| 3 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.468 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.420 |
| 4 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.462 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.390 |
| 5 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.462 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.383 |

### `S1-ctx` — How does the context-mode sandbox bootstrap and what hardening flags does it apply?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.704 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.672 |
| 2 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.690 | `docs/README.md` | 0.628 |
| 3 | `docs/adr/0029/README.md` | 0.658 | `docs/adr/0029/README.md` | 0.602 |
| 4 | `docs/reference/context-mode-tools.md` | 0.560 | `docs/getting-started-context-mode.md` | 0.589 |
| 5 | `docs/roadmap/context-mode-integration.md` | 0.530 | `docs/adr/0029/README.md` | 0.576 |

### `S1-boot` — What is the bootstrap flow of the context-mode container at startup?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/README.md` | 0.625 | `docs/getting-started-context-mode.md` | 0.620 |
| 2 | `docs/getting-started-context-mode.md` | 0.598 | `docs/README.md` | 0.606 |
| 3 | `docs/reference/context-mode-tools.md` | 0.520 | `docs/reference/context-mode-tools.md` | 0.493 |
| 4 | `docs/reference/context-mode-tools.md` | 0.482 | `docs/getting-started-context-mode.md` | 0.468 |
| 5 | `docs/roadmap/context-mode-details.md` | 0.456 | `docs/getting-started-context-mode.md` | 0.452 |

### `S1-cache` — What is the L3 auto-cache hook and how does it persist RAG responses?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/agent-decisions.md` | 0.762 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.660 |
| 2 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.743 | `.github/context/agent-decisions.md` | 0.635 |
| 3 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.721 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.622 |
| 4 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.709 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.611 |
| 5 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.708 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.576 |

### `ML-pl-ctx` — Jak działa piaskownica context-mode i jakie ma zabezpieczenia?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.706 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.675 |
| 2 | `docs/getting-started-context-mode.md` | 0.700 | `docs/getting-started-context-mode.md` | 0.660 |
| 3 | `docs/adr/0029/README.md` | 0.659 | `docs/README.md` | 0.635 |
| 4 | `docs/reference/context-mode-tools.md` | 0.572 | `docs/getting-started-context-mode.md` | 0.635 |
| 5 | `docs/roadmap/context-mode-integration.md` | 0.523 | `docs/adr/0029/README.md` | 0.612 |

### `ML-pl-ref` — Jak są obsługiwane refresh tokeny w IAM?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/project-state.md` | 0.662 | `docs/adr/0010/amendments/a1-retry-observability-configuration.md` | 0.494 |
| 2 | `docs/adr/0019/0019-identity-iam-bc-design.md` | 0.527 | `docs/adr/0018/0018-supporting-communication-bc-design.md` | 0.486 |
| 3 | `docs/adr/0018/0018-supporting-communication-bc-design.md` | 0.514 | `docs/adr/0018/0018-supporting-communication-bc-design.md` | 0.485 |
| 4 | `docs/adr/0019/README.md` | 0.514 | `docs/adr/0019/README.md` | 0.471 |
| 5 | `docs/adr/0008/0008-supporting-currencies-bc-design.md` | 0.502 | `docs/adr/0019/0019-identity-iam-bc-design.md` | 0.458 |

### `ML-de-ctx` — Wie funktioniert die context-mode Sandbox und welche Härtungsflags hat sie?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.699 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.664 |
| 2 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.696 | `docs/README.md` | 0.633 |
| 3 | `docs/getting-started-context-mode.md` | 0.693 | `docs/getting-started-context-mode.md` | 0.617 |
| 4 | `docs/adr/0029/README.md` | 0.662 | `docs/adr/0029/README.md` | 0.600 |
| 5 | `docs/adr/0029/README.md` | 0.654 | `docs/adr/0029/README.md` | 0.599 |

### `ML-de-ada` — Wie wird AdGuard für die DNS-Allowlist konfiguriert?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.579 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.596 |
| 2 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.545 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.557 |
| 3 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.543 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.523 |
| 4 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.542 | `docs/getting-started-context-mode.md` | 0.521 |
| 5 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.487 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.521 |

### `QP-0027-chunk` — ADR-0027 RAG chunking strategy max tokens overlap heading boundaries decision

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.686 | `docs/adr/0027/0027-rag-pipeline-design.md` | 0.678 |
| 2 | `docs/adr/0027/0027-rag-pipeline-design.md` | 0.682 | `.github/context/known-issues.md` | 0.597 |
| 3 | `.github/context/known-issues.md` | 0.586 | `.github/context/agent-decisions.md` | 0.555 |
| 4 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.582 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.528 |
| 5 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.581 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.502 |

### `QP-0029-sand` — ADR-0029 context-mode sandbox runtime Node JavaScript shell allowlist decision

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.696 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.673 |
| 2 | `docs/getting-started-context-mode.md` | 0.688 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.670 |
| 3 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.679 | `docs/adr/0029/README.md` | 0.622 |
| 4 | `docs/adr/0029/0029-context-mode-mcp-sandbox.md` | 0.676 | `docs/adr/0029/README.md` | 0.592 |
| 5 | `docs/adr/0029/README.md` | 0.674 | `docs/getting-started-context-mode.md` | 0.581 |

### `QP-0016-coup` — ADR-0016 coupon maximum per order limit five ten ceiling decision

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0016/amendments/a1-oversize-guard-and-catalog-name-sync.md` | 0.746 | `docs/adr/0016/README.md` | 0.628 |
| 2 | `docs/adr/0016/README.md` | 0.663 | `.github/context/known-issues.md` | 0.626 |
| 3 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.634 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.590 |
| 4 | `.github/context/known-issues.md` | 0.627 | `docs/adr/0016/README.md` | 0.535 |
| 5 | `docs/adr/0016/0016-sales-coupons-bc-design.md` | 0.565 | `docs/adr/0014/README.md` | 0.534 |

### `QP-0019-curr` — ADR-0019 NBP exchange rate currency conversion decision API integration

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0014/amendments/a3-integration-flow-decisions.md` | 0.668 | `docs/adr/0008/0008-supporting-currencies-bc-design.md` | 0.671 |
| 2 | `docs/adr/0008/README.md` | 0.557 | `docs/adr/0008/README.md` | 0.618 |
| 3 | `docs/adr/0008/README.md` | 0.539 | `docs/adr/0008/README.md` | 0.522 |
| 4 | `docs/adr/0008/README.md` | 0.499 | `docs/adr/0008/README.md` | 0.493 |
| 5 | `docs/adr/0010/README.md` | 0.463 | `docs/adr/0015/example-implementation/payment-state-machine.md` | 0.483 |

### `QP-0028-batch` — ADR-0028 batch manifest ZIP upload pipeline versus direct ingest decision

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.666 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.570 |
| 2 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.584 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.561 |
| 3 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.577 | `docs/adr/0017/example-implementation/shipment-dispatch-flow.md` | 0.551 |
| 4 | `docs/adr/0014/amendments/a4-operator-notifications.md` | 0.564 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.547 |
| 5 | `.github/context/agent-decisions.md` | 0.560 | `docs/adr/0028/amendments/0028-002-batch-manifest-pipeline.md` | 0.528 |

### `QP-which-rag` — Which ADR specifically defines the RAG architecture and embedder model choice?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.699 | `docs/adr/0028/tech-details-dotnet.md` | 0.615 |
| 2 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.648 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.559 |
| 3 | `docs/adr/0028/tech-details-dotnet.md` | 0.644 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.552 |
| 4 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.643 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.547 |
| 5 | `.github/context/agent-decisions.md` | 0.633 | `docs/adr/0001/0001-project-overview-and-technology-stack.md` | 0.534 |

### `QP-which-mt` — Which ADR specifically governs remote multitenant RAG ingest and per-collection storage?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.635 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.572 |
| 2 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.619 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.561 |
| 3 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.617 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.559 |
| 4 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.599 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.529 |
| 5 | `docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md` | 0.590 | `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` | 0.485 |

### `QP-cross-bc` — What architectural decision record covers cross-bounded-context messaging and event publishing?

| # | Python (:3002) | score | .NET (:3001) | score |
|---|---|---|---|---|
| 1 | `.github/context/agent-decisions.md` | 0.545 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.475 |
| 2 | `docs/adr/0014/amendments/a4-operator-notifications.md` | 0.526 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.470 |
| 3 | `.github/context/agent-decisions.md` | 0.519 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.469 |
| 4 | `docs/adr/0020/0020-backoffice-bc-design.md` | 0.495 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.441 |
| 5 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.493 | `docs/adr/0002/0002-post-event-storming-architectural-evolution-strategy.md` | 0.429 |
