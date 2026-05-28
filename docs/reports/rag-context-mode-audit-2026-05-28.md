# RAG & context-mode — Config + Skills Audit (2026-05-28)

> Inventory of `rag-config.yaml`, `queries.yaml`, `metadata-rules.yaml`, `multilingual-glossary.yaml`,
> plus existing skills for RAG and context-mode. Report-only — no file modifications. Recommendations only.

---

## Part 1 — Configuration file status

| File | Status | Main gaps |
|---|---|---|
| `tools/rag/rag-config.yaml` | ✅ GOOD | Minor: no comment about `.github/hooks/*.mjs`; no decision on whether to index `docs/reports/` |
| `tools/rag/metadata-rules.yaml` | ✅ GOOD | `docs/rag/auto-cache-hook.md` classified as `rag_meta` instead of `rag_guide` (lower ranking than warranted) |
| `tools/rag/queries.yaml` | ⚠️ PARTIAL | **No named queries for ADR-0026, 0027, 0028, 0029** + 6 operational topics missing |
| `tools/rag/multilingual-glossary.yaml` | ⚠️ PARTIAL | No PL/DE for: **context-mode, sandbox, AdGuard, refresh token, IAM, host-side, auto-cache, atomic switch** |

### TOP 10 to add (priority → estimated total: ~23 min)

**P1 — critical gaps in queries.yaml (~5 min)**
1. `adr-0026-saga` — saga / choreography / compensation
2. `adr-0027-rag` — RAG pipeline / embedding model
3. `adr-0028-multitenant` — `top_k: 10` (3 amendments + main)
4. `adr-0029-context-mode` — `top_k: 12` (includes auto-cache hook amendment)

**P2 — operational queries (~5 min)**
5. `context-mode-bootstrap` — bootstrap, AdGuard, DNS, init
6. `rag-caching-strategy` — L1/L2/L3 handoff, `ctx_index`, `ctx_search`

**P3 — multilingual glossary (~3 min)**
7. `context-mode` / `sandbox` (PL: tryb kontekstowy, piaskownica · DE: Kontextmodus, Sandkasten)
8. `refresh-token` (PL: token odświeżania · DE: Aktualisierungstoken)
9. `iam` (PL: zarządzanie tożsamością · DE: Identitäts- u. Zugriffsverwaltung)

**P4 — weight rebalancing (~10 min)**
10. Split `rag_meta` → `rag_meta` (0.70) + `rag_guide` (0.85–0.90) for `auto-cache-hook.md`, `rag-architecture.md`, `SETUP-GUIDE.md` (blocked today by `docs/rag/**` exclude_glob — see Part 5)

---

## Part 2 — Existing RAG skills (6) — cover ~80% of day-to-day work

| Skill | What it does |
|---|---|
| `diagnose-rag` | Triage for MCP startup, tool errors, low scores (7 decision paths) |
| `expand-rag-glossary` | Add PL/DE patterns to glossary (query-time only) |
| `generate-eval-questions` | Auto-generate eval queries for newly indexed docs |
| `generate-rag-rules` | Update `metadata-rules.yaml` + `queries.yaml` after adding folders |
| `rag-with-memory` | Cache RAG results in context-mode FTS5 (L1/L2/L3) |
| `tune-rag-weights` | Adjust `ranking.weights` in `rag-config.yaml` |

---

## Part 3 — Missing RAG skills (10 proposals)

### Operational / infra

| Name | Why | When |
|---|---|---|
| **rag-parity-audit** | Compare Python vs .NET on a test query set; validate `list_adrs`, error envelope, amendment counts | After server upgrade; before stack switch; as a pre-release gate |
| **rag-cli-ingest-fix** | Fix `--remote` CLI in both stacks (per-file → ZIP `/batch`) — tracked blocker in `rag-mcp-anomalies.md` | When local ingest CLI fails |
| **rag-collection-ops** | Rename / archive / merge / clone Qdrant collections; orphan audit | After long dev cycles; multi-tenant; before upgrade |
| **rag-variant-migration** | Switch team between Python ↔ .NET (collections are NOT interchangeable — different tokenizers) | When .NET reaches parity; performance concerns |

### Validation / hardening

| Name | Why | When |
|---|---|---|
| **rag-validation-hardening** | Close 9 gaps from `rag-mcp-anomalies.md` (size caps, path traversal, ZIP bomb, collection-name) | Pre-production; per CVE |
| **rag-tool-endpoint-dev** | Add new MCP tool / endpoint to Python and .NET (parity) | New query-operator pattern |

### Performance

| Name | Why | When |
|---|---|---|
| **rag-performance-tuning** | Profile slow queries, chunker bottlenecks, Qdrant payload filtering | Latency > SLA; collection > 500 MB |
| **rag-embedder-upgrade** | Plan model upgrade (re-embed, eval, rollback) | Annual ML model updates |

### Debug

| Name | Why | When |
|---|---|---|
| **rag-empty-result-deep-dive** | Systematic analysis of empty results despite good match (glossary miss, weight, threshold) | When the 2-step retry from mcp-routing didn't help |

---

## Part 4 — Missing context-mode skills (**CRITICAL GAP: 0 existing**)

ADR-0029 defines a complex sandbox (hardening, hooks, DNS firewall, session persistence), yet **no operational skill exists**. This is the largest risk before rolling out to the full team.

### Onboarding / setup

| Name | Why | When |
|---|---|---|
| **ctx-sandbox-bootstrap-verify** | Post-bootstrap smoke tests: hardening flags, container health, DNS, network monitor hook, session DB | After `context-mode-bootstrap.ps1`; before enabling MCP; every new team member |
| **ctx-adguard-allowlist-onboard** | Safely add a new domain to `team-whitelist.txt` with validation + PR flow | New integration; user reports "AdGuard blocked my fetch" |

### Debug / troubleshooting

| Name | Why | When |
|---|---|---|
| **ctx-doctor-playbook** | Interpret `ctx_doctor()` output → map symptoms to fixes | First MCP call fails; `ctx_search` empty; `ctx_execute` hang |
| **ctx-hook-debugging** | Debug PreToolUse / PostToolUse / PreCompact hooks (redaction, compaction, mcp-nudge) | Hook output odd; session loses data; credential leak |
| **ctx-network-alerts-forensics** | Analyze `.ctx-network-alerts.log` + AdGuard query logs | Security audit; suspicious access; compliance |

---

## Part 5 — Notes from Sprint 1 quick wins

- `docs/rag/**` is currently in `source.exclude_globs` (rag-config.yaml). This blocks the proposed `rag_guide` kind split. Three options for follow-up:
  - **A**: leave as is (docs/rag/** stays human-only documentation)
  - **B**: remove the exclude, add `rag_guide` kind with weight 0.85–0.90, force-full reindex
  - **C**: selective unblock for specific files (e.g., `auto-cache-hook.md`, `mcp-first-routing-migration-playbook.md`) but keep `SETUP-GUIDE.md` excluded
- Quick-win edits to `queries.yaml` and `multilingual-glossary.yaml` are query-time only — no reindex needed.
- HTTP MCP servers (ports 3001 + 3002) must be restarted to see new config; stdio servers reload per VS Code session.

---

_Generated 2026-05-28. Infra status: 6/6 RAG variants ✅, fresh ingest, Qdrant/AdGuard/context-mode healthy._
