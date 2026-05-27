# Roadmap: context-mode — MCP sandbox integration

> Status: — Unblocked — RAG stabilisation complete (2026-05-23), ready to implement
> Scope: `docker/context-mode/`, `Dockerfile-context-mode`, `docker-compose.yaml` (delta), `.vscode/`, `.github/hooks/`, `.github/copilot-instructions.md` (append), `.env.context-mode.example` (`.claude/settings.json` originally listed; now skipped — see Phase 4 note)
> Companion detail plan: [`context-mode-details.md`](./context-mode-details.md)

---

## Why we are doing this

GitHub Copilot is rolling out request usage billing. Every tool invocation
dumps raw output into the context window — which translates directly into
the number of requests billed against the model.

| Tool | Raw output | In-context after integration | Reduction |
|---|---|---|---|
| Playwright snapshot | 56 KB | 299 B | 99% |
| GitHub issues (20) | 59 KB | 1.1 KB | 98% |
| Test logs (30 suites) | 6 KB | 337 B | 95% |
| Repo research (subagent) | 986 KB | 62 KB | 94% |
| **Whole session** | **315 KB** | **5.4 KB** | **98%** |

Goal: extend a useful working session from ~30 min to ~3 hours without losing context.

---

## Assumptions

| # | Assumption | Rationale |
|---|---|---|
| A1 | Build from source (git clone, pinned tag), not `npm install -g` | Full control over what runs; auditable code |
| A2 | Docker — no host-side install for the team | `docker compose up -d context-mode` is the only operation |
| A3 | Network: custom `ctx-net` bridge + DNS through AdGuard | Allows controlled use of `ctx_fetch_and_index` against a whitelist; provides a query log; cross-runtime (Docker Desktop, Rancher, Podman) |
| A4 | Node.js network monitoring hook | Logs connection attempts (even when AdGuard permitted them); secondary signal independent of DNS |
| A5 | Alert log: `/workspace/.ctx-network-alerts.log` | Cross-runtime; visible in VS Code |
| A6 | VS Code Problem Matcher task | Alerts surface in the Problems panel; cross-runtime |
| A7 | Container logs: VS Code Docker extension + `docker logs` | No dedicated log viewer container (Dozzle, Portainer) — minimise the number of services |
| A8 | Hooks via `docker exec` (not a global CLI) | The team does not install anything locally |
| A9 | copilot-instructions.md: append-only section 13 | Existing agent configuration left untouched |
| A10 | RAG MCP stays active alongside context-mode | Each service serves a different purpose; no conflict |
| A11 | Version pinned in the Dockerfile (v1.0.148) | Controlled upgrade via a single commit; v1.0.147+ enables CONTEXT_MODE_DIR |
| A12 | Non-root user inside the container | Security hardening |
| A13 | Hardening flags: `read_only`, `cap_drop: [ALL]`, `no-new-privileges`, `pids_limit`, `mem_limit`, `ipc: none` | Defense-in-depth on top of network isolation |
| A14 | AdGuard — community lists auto-update every 7 days | `StevenBlack`, `AdGuard SDN`, `EasyPrivacy`. ~240k rules, zero team effort |
| A15 | Shared `team-blacklist.txt` / `team-whitelist.txt` (PR review) + `personal-overrides.local.txt` (gitignored) | Team-wide policy in git, local experiments without conflict |
| A16 | Docker Desktop licensing caveat (company >250 employees = paid) | Rancher Desktop / Podman are free alternatives. Repo uses the `docker` CLI; a local swap is not committed. Qdrant Apache-2.0 has no company-size restriction |
| A17 | CI = Azure DevOps (GitHub Actions unavailable) | Phase 6 auto-triage (when implemented) must use Azure Pipelines + `az repos pr create` instead of `gh pr create`. A local trigger via Windows Task Scheduler stays available as a CI-less option |

---

## What we worry about — risks and mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| context-mode exfiltrates data over the network | Low | High | AdGuard DNS firewall (whitelist) + hardening flags + network-monitor.js hook (audit trail) |
| AdGuard blocks a domain that is actually needed | Possible | Low | UI quick-fix + PR to `team-whitelist.txt` (higher priority than community lists) |
| Community blocklist auto-update introduces a false positive | Possible | Low | 7-day interval (not 24h) gives time to react; `team-whitelist.txt` is an immediate hot-fix |
| Breaking change in a new version | Low | Medium | Pinned version in the Dockerfile; intentional upgrade by the whole team |
| Hooks interfere with RAG MCP (`mcp__*` calls) | Low | Low | `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50` |
| copilot-instructions.md conflict | Very low | High | Append only; new section 13; existing configuration unchanged |
| Differences between Docker Desktop / Rancher / Podman | Possible | Low | `ctx-net` bridge behaves identically; `docker`→`podman` is a local swap |
| AdGuard misconfiguration blocks maintenance | Low | Medium | The AdGuard UI stays reachable even with filtering enabled; emergency: `docker compose stop adguard` |
| SQLite session DB does not persist | None | High | Named volume `context-mode-data` |
| Non-root user has no access to workspace | Possible | Medium | Volume mount with correct permissions; verify after the first run |
| Docker Desktop license for a company >250 employees | Certain | Low | Rancher Desktop / Podman are free and compatible; installing Docker Engine on Linux is also free |

---

## Implementation phases

### Phase 1 — Docker sandbox (foundation) + hardening + ctx-net bridge

| Step | Description | File | Status |
|---|---|---|---|
| 1.1 | Node.js network monitoring hook | `docker/context-mode/network-monitor.js` | 🔲 |
| 1.2 | Entrypoint wrapper | `docker/context-mode/entrypoint.sh` | 🔲 |
| 1.3 | Two-stage Dockerfile (git clone → runtime, non-root, pinned tag) | `Dockerfile-context-mode` | 🔲 |
| 1.3b | Env knobs example file (12 tunables: tags + container resources + ports + nudge + fetch-strict + AdGuard DNS/refresh) committed | `.env.context-mode.example` | 🔲 |
| 1.3c | `.gitignore` entry for `.env.context-mode` (per-developer overrides) | `.gitignore` | 🔲 |
| 1.4 | docker-compose delta: service + hardening flags + `ctx-net` bridge + `dns: [adguard]` + volume | `docker-compose.yaml` | 🔲 |
| 1.5 | Hardening flags: `read_only`, `tmpfs:/tmp`, `cap_drop:[ALL]`, `no-new-privileges`, `pids_limit:100`, `mem_limit:1g`, `ipc:none` | `docker-compose.yaml` | 🔲 |
| 1.6 | Named volume for the SQLite session DB | `docker-compose.yaml` (volumes section) | 🔲 |
| 1.7 | VS Code task: `Context-Mode: Start` | `.vscode/tasks.json` | 🔲 |
| 1.8 | Verification: `docker compose up -d context-mode` | terminal | 🔲 |

**Phase 1 acceptance criterion**: the container starts and does not crash, runs on `ctx-net`, has DNS pointing at `adguard`, and the hardening flags are visible in `docker inspect`.

---

### Phase 2 — MCP connection to VS Code

| Step | Description | File | Status |
|---|---|---|---|
| 2.1 | MCP config (docker exec stdio) | `.vscode/mcp.json` | 🔲 |
| 2.2 | VS Code tasks: `Context-Mode: Stop`, `Context-Mode: Network Alerts` | `.vscode/tasks.json` | 🔲 |
| 2.3 | Verification: `ctx stats` in Copilot Chat | Copilot Chat | 🔲 |
| 2.4 | Sandbox test: `ctx_execute("javascript", "console.log('hello')")` | Copilot Chat | 🔲 |
| 2.5 | Add `.ctx-network-alerts.log` to `.gitignore` | `.gitignore` | 🔲 |
| 2.6 | Verify `ctx_insight` web UI loads at `http://127.0.0.1:9998` (localhost-only) | browser | 🔲 |

**Phase 2 acceptance criterion**: `ctx stats` returns a response; `ctx_execute` works; the alert log is empty (no connection attempts).

---

### Phase 3 — Hooks (routing enforcement)

| Step | Description | File | Status |
|---|---|---|---|
| 3.1 | Hooks config (5 hooks: PreToolUse, PostToolUse, UserPromptSubmit, PreCompact, SessionStart) | `.github/hooks/context-mode.json` | ✅ |
| 3.2 | Hook verification: restart VS Code, new Copilot session | VS Code | 🔲 |
| 3.3 | Session continuity test: force compaction, check resume | Copilot Chat | 🔲 |
| 3.4 | Measurement: `ctx stats` after a working session — verify % reduction | Copilot Chat | 🔲 |

> **Container CLI path note (2026-05-27)**: the roadmap originally specified `context-mode hook vscode-copilot <event>` as the command. There is no `context-mode` wrapper on PATH in the shipped image (`/app/bin/` contains only `statusline.mjs`). The working invocation is `node /app/cli.bundle.mjs hook vscode-copilot <event>` — the file in `.github/hooks/context-mode.json` uses this corrected form. See `agent-decisions.md` 2026-05-27 entry.

**Phase 3 acceptance criterion**: hooks are active; `ctx stats` shows savings > 0; session restore after compaction works.

---

### Phase 4 — copilot-instructions.md merge

| Step | Description | File | Status |
|---|---|---|---|
| 4.1 | Append MCP routing section to copilot-instructions | `.github/copilot-instructions.md` §12 | ✅ Done (different shape) |
| 4.1b | Add `.claude/settings.json` with deny/allow rules | `.claude/settings.json` | ⛔ Skipped (N/A — see note) |
| 4.2 | Verification: existing agents still work (ADR query, BC routing) | Copilot Chat | 🟡 Ongoing |
| 4.3 | Run `@copilot-setup-maintainer` (Workflow 11 + 7) | Copilot Chat | 🔲 Deferred to batch end |

**Note on 4.1**: Realised differently from original spec. Heavy lifting (rules tables, retry sequences, anti-patterns, Invalid-answer directive) lives in [.github/instructions/mcp-routing.instructions.md](../../.github/instructions/mcp-routing.instructions.md) which auto-loads via `applyTo: **`. `copilot-instructions.md §12` keeps only a thin pointer + 5-bullet non-negotiable summary so the routing rules surface on every chat without bloating the always-loaded file.

**Note on 4.1b — why skipped**: `.claude/settings.json` is Claude Code (Anthropic CLI) specific. This repo uses VS Code Copilot Chat only — no `.claude/` directory exists. The functional equivalent of `.claude/settings.json` deny/allow rules is split across three VS Code mechanisms: (1) `chat.tools.terminal.autoApprove` in `.vscode/settings.json` for shell commands; (2) per-agent `tools:` frontmatter in `.github/agents/*.agent.md` for MCP tool allowlists (already used by `@verifier`); (3) `.github/hooks/*.json` PreToolUse handlers shipped in Phase 3 (can return `block`/`modify`). For context-mode's sandbox specifically, filesystem-level deny is already enforced by the docker `/workspace:ro` read-only mount in `docker-compose.yaml`. Adding a Claude-Code-format file would be cargo-cult — re-open this step only if the team adopts Claude Code alongside VS Code Copilot.

**Phase 4 acceptance criterion**: project agents (ADR, BC) behave as before; context-mode routing is active alongside them.

---

### Phase 5 — AdGuard Home (DNS firewall, `monitoring` profile)

| Step | Description | File | Status |
|---|---|---|---|
| 5.1 | AdGuard service in docker-compose (profile `monitoring`) + volume | `docker-compose.yaml` | ✅ Done |
| 5.2 | AdGuard config (system + per-client policies) | `docker/adguard/AdGuardHome.yaml` | ✅ Done (template + gitignored runtime yaml) |
| 5.3 | Community blocklists (3 lists, auto-update) | `docker/adguard/community-blocklists.yaml` | ✅ Done (different shape — inline in `AdGuardHome.yaml.template` `filters:` block, not a separate file; `filters_update_interval: 24` hours = daily, more frequent than 7d spec) |
| 5.4 | Shared team blacklist (commit + PR) | `docker/adguard/team-blacklist.txt` | ✅ Done |
| 5.5 | Shared team whitelist (commit + PR) | `docker/adguard/team-whitelist.txt` | ✅ Done (14 prepopulated canonical domains: learn.microsoft, github, nuget, docker hub, etc. — see file header) |
| 5.6 | Personal overrides example (gitignored) | `docker/adguard/personal-overrides.local.example.txt` | ✅ Done — **but see follow-up below** |
| 5.7 | `.gitignore`: `personal-overrides.local.txt` + `AdGuardHome.yaml` | `.gitignore` | ✅ Done (also `AdGuardHome.yaml` because of bcrypt hash) |
| 5.8 | **MANDATORY**: First-boot wizard OR bootstrap script — strong admin password (16+ chars) → bcrypt hash | `docker/adguard/AdGuardHome.yaml` | ✅ Done (bootstrap script generates 24-char password, prints once, computes bcrypt cost=10) |
| 5.9 | Restrict web UI to host loopback | `docker/adguard/AdGuardHome.yaml` + `docker-compose.yaml` | ✅ Done (different shape — UI restriction via docker port mapping `127.0.0.1:3000:3000`, NOT via AdGuard `allowed_clients` which is a DNS-section setting. `auth_attempts: 5` + `block_auth_min: 15` present in template) |
| 5.10 | Operational notes (first-boot hardening + monthly review checklist) | `docker/adguard/README.md` | ✅ Done |
| 5.11 | Verification: from `ctx-net` container, `curl http://adguard:3000/control/...` is auth-gated; DNS `:53/udp` works | `docker exec` | ✅ Done — live verified 2026-05-27: `GET /control/login` returns 405 (Method Not Allowed for GET); `GET /control/status` returns 403 Forbidden without bcrypt cookie. Original spec expected 403 from `/control/login` — corrected here. |
| 5.12 | Verification: AdGuard UI at `http://127.0.0.1:3000` — lists active, `context-mode` registered as a client with the strict policy | browser | 🟡 Manual — user must log in to UI (`admin` / bootstrap-generated password) and visually confirm |

**Phase 5 acceptance criterion**: AdGuard reachable on :3000, 3 community lists active (~240k rules), `context-mode` resolves through AdGuard (`docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com` returns an answer; an unknown suspicious domain — NXDOMAIN), and the PR workflow on `team-blacklist.txt` has been exercised at least once.

**Live verification (2026-05-27)**:
- `nslookup doubleclick.net 172.28.0.2` → `0.0.0.0` (community blocklist active ✅)
- `nslookup learn.microsoft.com 172.28.0.2` → resolved via Akamai CDN (team-whitelist override works ✅)
- `nslookup raw.githubusercontent.com 172.28.0.2` → resolved with IPv4+IPv6 records (G.3 PASS ✅)
- AdGuard live filters: 3 community (AdGuard DNS, AdAway, StevenBlack) + team-blacklist + team-whitelist all `enabled: true` ✅

### Follow-ups / known gaps

| Gap | Severity | Suggested fix |
|---|---|---|
| `personal-overrides.local.txt` is NOT auto-loaded by bootstrap | Medium UX | Bootstrap script should detect file presence and inject filter `id=1003` entry into generated `AdGuardHome.yaml` ("upsert" behaviour: append to team whitelist if personal exists; create as standalone otherwise) — currently requires manual yaml edit per `personal-overrides.local.example.txt` instructions |
| `filters_update_interval: 24h` differs from original spec "every 7 days" | Doc-only | Spec amended — 24h is intentionally more aggressive than 7d soft target; AdGuard limits are 1-168h, and 24h provides faster blocklist refresh for negligible bandwidth (~10MB/day total across 3 lists) |
| Step 5.9 spec said "Restrict via `allowed_clients: [127.0.0.1, ::1]`" — but `allowed_clients` is a DNS-section setting (must remain empty `[]` so `context-mode` can query DNS). UI restriction is achieved via docker port binding `127.0.0.1:3000:3000` + bcrypt password gate | Doc-only | Spec amended above — see "different shape" note on step 5.9 |
| Step 5.11 expected `403` from `/control/login` — reality is `405` (GET not allowed, POST required); `/control/status` returns proper `403` without auth | Doc-only | Spec amended — see "live verified" note on step 5.11 |
| Community lists are inline in template, not a separate `community-blocklists.yaml` | Doc-only | Spec amended — see step 5.3 note. Refactoring to separate YAML would require AdGuard to support `!include` (does not as of v0.107.50) |

---

### Setup-time gate (run BEFORE declaring Phase 5 done)

**One-command setup (recommended)**: `powershell -File scripts/context-mode-bootstrap.ps1` from repo root. The script generates `AdGuardHome.yaml` (skipping the first-run wizard), recreates both containers, waits for the DNS listener, and runs G.1–G.3 automatically. Default password is auto-generated and printed once; pass `-AdGuardPassword '...'` to set your own.

**Manual verification** — the wizard (or the bootstrap script above) is the single most common footgun; see [KI-014](../../.github/context/known-issues.md). Until these three checks pass on a fresh machine, `ctx_fetch_and_index` returns SERVFAIL for every external URL.

| # | Command | Expected | Failing means |
|---|---|---|---|
| G.1 | `docker exec ecommerceapp-adguard ls /opt/adguardhome/conf` | `AdGuardHome.yaml` present | Run `scripts/context-mode-bootstrap.ps1` or open `http://127.0.0.1:3000` |
| G.2 | `docker exec ecommerceapp-adguard sh -c "netstat -ln \| grep ':53 '"` | One or more `:53` listeners (udp + tcp) | AdGuard never bound — re-run bootstrap or visit wizard "DNS server" page |
| G.3 | `docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com 172.28.0.2` | A record returned, no SERVFAIL | AdGuard up but upstreams misconfigured — Settings → DNS settings → Upstream DNS servers |

> If you wipe the AdGuard volume (`docker volume rm ecommerceapp_adguard-work`), all three checks fail until the wizard is re-run — budget 5 minutes for the bring-up.

---

### Phase 6 (future, optional) — suggestions automation + "new arrived" UI

> **Status: future amendment**, not part of the current ADR-0029. Enable only if the community lists turn out to be insufficient.

Concept:

| Step | Description | File | Status |
|---|---|---|---|
| 6.1 | Cron script reads the AdGuard REST API (`/control/querylog`), groups and deduplicates | `tools/adguard/triage-queries.ps1` | 🔲 (future) |
| 6.2 | Suggestions buffer (pending review) | `docker/adguard/suggestions.json` | 🔲 (future) |
| 6.3 | VS Code Problem Matcher — yellow warnings in the Problems panel when `suggestions.json` gains new entries | `.vscode/tasks.json` (problem matcher) | 🔲 (future) |
| 6.4 | Helper script `accept-suggestion.ps1` — promotes an entry to `team-blacklist.txt` + commit | `tools/adguard/accept-suggestion.ps1` | 🔲 (future) |
| 6.5 | Scheduled Task / Azure DevOps Pipeline invokes triage every 1h or 24h (GitHub Actions unavailable — team CI = Azure DevOps) | OS scheduler | 🔲 (future) |
| 6.6 | DNS exfiltration heuristic: alert on queries with hostname > 50 chars, or > 5 queries/s to the same parent domain, or hex/base64-looking labels | `tools/adguard/exfil-detect.ps1` | 🔲 (future) |

**Why "future" and not now**: 3 community lists with a 7-day auto-update cover ~95% of bad domains. Adding a script + cron + UI is moving parts for a marginal gain. Revisit once real usage shows a gap.

---

## End-to-end verification

After all phases are complete:

```
1. docker compose up -d context-mode
2. VS Code: Copilot Chat → "ctx stats"                          → response with 0 savings
3. Copilot Chat → "ctx_execute javascript console.log(47*6)"    → "282"
4. Copilot Chat → analyse src/ via ctx_execute_file             → result without raw content
5. docker logs ecommerceapp-context-mode | grep SUSPICIOUS      → no matches
6. cat .ctx-network-alerts.log                                  → empty or absent
```

---

## Dependencies and ordering

```
Stable RAG MCP server       ← required BEFORE starting Phase 1
        ↓
Phase 1 (Docker)
        ↓
Phase 2 (MCP connection)
        ↓
Phase 3 (Hooks)             ← can be skipped and revisited; MCP works without hooks (~60% compliance)
        ↓
Phase 4 (instructions merge) ← requires @copilot-setup-maintainer afterwards
        ↓
Phase 5 (AdGuard)           ← optional gating; does not block previous phases
        ↓
Phase 7 (RAG ↔ context-mode wrapper tool, L2) ← optional; depends on Phase 4 docs in place
```

---

### Phase 7 ✅ Done (option C) — L2 wrapper tool: `query_docs_cached`

> **Status: shipped 2026-05-27** — Python + .NET RAG servers. Both expose the L2 wrapper with byte-identical source labels and markdown shape.
> **Design choice**: option C from the architecture discussion — wrapper returns formatted markdown + deterministic `source` label; the agent makes the follow-up `ctx_index` call. No cross-MCP coupling, no file staging, no direct SQLite writes. Minimum complexity for ~2× speedup of the most common handoff.

**What L2 collapses**: the manual 3-step parent-agent flow (`query_docs` → format markdown → `ctx_index`) becomes 1 + 1: one `query_docs_cached` call (returns `{source, markdown, files_count, chunks_count, next_step}`) plus one pass-through `ctx_index(content=markdown, source=source)`. **LIMIT-1 unchanged** — subagent recalls still use inline chunks.

| Step | Description | File | Status |
|---|---|---|---|
| 7.1 | Wrapper interface design — params `{question, bc?, top_files?}`; deterministic source derivation rules (ADR id → `rag-cache-adr<NNNN>-<hash8>`; `bc=` set → `rag-cache-<slug(bc)>-<hash8>`; fallback `rag-cache-q-<hash8>`); dedup by overwrite (idempotent). Glossary injection from LIMIT-3 NOT included — kept as future amendment. | inline in `tools/rag/rag_tools.py` docstring | ✅ Done |
| 7.2 | Python implementation — `_tool_query_docs_cached` in [`tools/rag/rag_tools.py`](../../tools/rag/rag_tools.py); registered in [`tools/rag/mcp_server.py`](../../tools/rag/mcp_server.py) `_TOOL_DISPATCH` + `list_tools()` | `tools/rag/mcp_server.py`, `tools/rag/rag_tools.py` | ✅ Done |
| 7.3 | .NET implementation parity in `RagTools.Mcp` — `QueryDocsCached` tool, `QueryDocsCachedFormatter`, `ProjectQueryCached`; Core records gain `EndLine` (`SearchHit`, `DocumentSearchResult`, `QueryHit`) | [`tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Tools/RagTools.cs), [`QueryDocsCachedFormatter.cs`](../../tools/rag-dotnet/src/RagTools.Mcp/Tools/QueryDocsCachedFormatter.cs) | ✅ Done (14 new tests, 492 total) |
| 7.4 | Tool descriptor + JSON schema in `list_tools()` | `tools/rag/mcp_server.py` | ✅ Done |
| 7.5 | Unit tests for `_derive_source_label` + `_format_chunks_to_markdown` | [`tools/rag/tests/test_query_docs_cached.py`](../../tools/rag/tests/test_query_docs_cached.py) | ✅ Done (9 passed) |
| 7.6 | Update [`mcp-routing.instructions.md`](../../.github/instructions/mcp-routing.instructions.md) — L2 as canonical path, L1 as `.NET`-server / timeout fallback | instructions file | ✅ Done |
| 7.7 | Update skill [`rag-with-memory`](../../.github/skills/rag-with-memory/SKILL.md) — L2 first, L1 demoted to fallback | skill file | ✅ Done |
| 7.8 | Append agent-decisions entry for option C decision | agent-decisions log | ✅ Done |

**Phase 7 acceptance results**:

- Single `query_docs_cached(question="...")` call returns ready-to-cache markdown + source label.
- Source labels are deterministic across recalls — verified by `test_source_label_is_deterministic`.
- L1 and L2 cache shape are identical → recalls via `ctx_search(source="rag-cache-")` find both.
- Standard `query_docs` / `read_docs` / `get_history` unchanged — full 293-test Python suite + 492-test .NET suite green.
- `.NET` parity shipped — `top_k` is capped at `RagQueryService.MaxTopK` (20) so chunk-density per file is slightly lower than Python's `max(30, top_files*15)`; label format and markdown shape are identical.

### Phase 7.4 (observation, opportunistic) — measure the `top_k` gap and decide whether to raise `MaxTopK`

> **Status: not started, evaluate-on-demand.** Open question: is the `.NET` `top_k=20` cap actually hurting cache quality vs Python's `top_k=45`? Currently unknown — no telemetry, no A/B comparison. Hold the decision until we have data.

Three cheap probes to gather signal without instrumenting either server:

| Probe | What to measure | Decision threshold to raise `MaxTopK` |
|---|---|---|
| **A. Coverage diff** | Run the same 10 representative questions on both servers. Compare the `files_count` and unique `rel_path` set returned in the `markdown`. | If `.NET` returns ≥ 25% fewer unique files OR misses a file that Python ranks in the top 3, that is real coverage loss → raise to ~45. |
| **B. Recall hit-rate on cached content** | After each `query_docs_cached` call, run 2-3 plausible follow-up questions through `ctx_search(source="rag-cache-...")`. Log: did the relevant span surface in the top 3 `ctx_search` results? | If recall on `.NET` caches is consistently worse than on Python caches (n ≥ 20 pairs), top_k is the likely cause → raise. |
| **C. Markdown size telemetry** | Log `len(markdown)` and `chunks_count` per `query_docs_cached` call (`logger.LogInformation` at the end of the tool method on `.NET`; equivalent in Python). After ~50 calls, compute median + p95 on both servers. | If `.NET` is consistently > 30% smaller on median, the cap is biting and we are paying recall cost → raise. If sizes are close, the postprocessor is already deduplicating effectively → leave it at 20. |

**If raised, what to change**: bump `RagQueryService.MaxTopK` (e.g. 20 → 75). It is a public ceiling on what `query_docs` callers may request; default values for `top_k` parameters do not change, so existing callers are unaffected. No wire-shape change, no migrations.

**Why not just raise it now**: every chunk we pull through the postprocessor pipeline costs time and embedding-comparison work. If 20 is enough in practice, paying for 45 is waste. The compromise is logged here so the answer is "we measured" not "we guessed".

---

### Phase 8 (future, opportunistic) — additional sandbox runtimes

> **Status: not planned, evaluate-on-demand.** Default shipped runtime image bundles only `javascript` (Node) and `shell` (POSIX sh) — `ctx_doctor` → `Runtimes: 2/11`. The schema enum accepts 9 additional langs (`typescript`, `python`, `ruby`, `go`, `rust`, `php`, `perl`, `R`, `elixir`, `csharp`) which currently error at runtime ("X not available. Install ...").

Add a runtime ONLY when a real use case shows up repeatedly. Candidate triggers:

| Runtime | Trigger | Approximate image cost |
|---|---|---|
| `csharp` (`dotnet-script`) | Frequent need for a quick LINQ/built-in-method playground inside the sandbox (no host SDK pollution, no project scaffold) | +~250 MB |
| `python` | Repeated requests for log/CSV analysis, data crunching, or `pandas`/`numpy` workflows | +60-80 MB |
| Others | None currently anticipated for a .NET MVC project | varies |

Implementation when triggered: edit `Dockerfile-context-mode` to install the runtime, rebuild `context-mode` image, restart container, confirm via `ctx_doctor` that the count rises (e.g. `3/11`).

---

### Phase 9 (future, opportunistic) — auto-load `personal-overrides.local.txt`

> **Status: identified follow-up from Phase 5 audit (2026-05-27).** Currently `personal-overrides.local.txt` is gitignored but NOT auto-loaded — user must manually add a filter `id=1003` entry into the (gitignored) `AdGuardHome.yaml`. This is fragile and per-developer friction.

**Acceptance**: `scripts/context-mode-bootstrap.ps1` (and `.sh` counterpart) detect `docker/adguard/personal-overrides.local.txt` and, if present, inject a `filters: id=1003` entry into the generated `AdGuardHome.yaml` automatically. Behaviour is "append" (does NOT replace `team-whitelist` filter id=1002 or `team-blacklist` id=1001 — purely additive). Re-running bootstrap with `-ForceRegenerateAdGuard` re-evaluates personal-overrides presence and rewrites yaml accordingly.

---

### Phase 9 v1 — `domain-policy` CLI (✅ Done 2026-05-27)

> **Shipped**: file-first CLI for team-wide filter management. Scope: `blacklist` + `whitelist` targets only. Personal-overrides intentionally **dropped** (see v2 below).

**Acceptance met**:

- [x] `scripts/adguard/domain-policy.ps1` — PowerShell entry point (~330 lines)
- [x] `scripts/adguard/domain-policy.sh` — bash parity for WSL/Linux contributors
- [x] Subcommands: `status [--verbose]`, `show <target|all> [--tail N] [--grep PATTERN]`, `edit <target>`, `import <target> <localfile>`, `add <target> <rule>`, `reload`, `help`
- [x] Dedup on `add` / `import`: exact text match, case-sensitive, trim whitespace, ignore `#` / `!` comments. No semantic dedup, no cross-file dedup (both intentional — documented in `docker/adguard/README.md`)
- [x] AdGuard rule syntax validation before `add` (rejects malformed input)
- [x] Reload via `docker compose restart adguard` (~5s downtime, no credentials)
- [x] `$EDITOR` fallback chain: `$EDITOR` → `code -w` → `notepad` / `vi`
- [x] WARN-only on committed file edits — prints branch + git/PR template, never auto-stages or pushes
- [x] Host-file operations only — zero `docker exec` for filter edits
- [x] VS Code tasks: `AdGuard: Show all filters`, `AdGuard: Reload filters`
- [x] Documentation: `docker/adguard/README.md` ("Daily management" section) + `docs/getting-started-context-mode.md` daily-life table

**Execution notes**:

- Implementation deliberately avoids touching `AdGuardHome.yaml` `users:` block, DNS upstreams, querylog, or container lifecycle beyond restart — clear separation from `scripts/context-mode-bootstrap.ps1` (user/yaml lifecycle, destructive) vs `domain-policy.ps1` (filter content, safe + frequent). **Full ownership contract for both scripts**: [`docs/reference/context-mode-tools.md`](../reference/context-mode-tools.md).
- Reload chosen over hot-reload because (a) AdGuard v0.107.50 does not reliably hot-reload file-based filters without a kick, (b) 5s downtime is acceptable for the dev sandbox use case, (c) it avoids parsing the bcrypt-protected `/control/filtering/refresh` API and managing session cookies.
- See `.github/context/agent-decisions.md` 2026-05-27 entry for the v2 drop rationale and design discussion summary.

---

### Phase 9 v2 — NOT PLANNED (auto-load `personal-overrides.local.txt`)

> **Decision (2026-05-27): dropped.** Originally deferred from Phase 5.6 / 5.7 audit, then explicitly removed from `domain-policy` v1 scope, then formally dropped after design discussion.

**Why dropped** (rather than deferred):

- **High touch surface for marginal benefit**: required edits to `bootstrap.ps1` (yaml injection), `AdGuardHome.yaml.template` (placeholder for id=1003), and a third CLI target — all to support a per-developer use case that has not actually appeared in practice.
- **Workflow overlap**: any rule a developer needs locally can be added to `team-whitelist.txt` as a one-off PR (the team reviews → permanent) or applied via `domain-policy.ps1 add` and reverted later. The "I need a rule that I do NOT want to share" case is rare in this stack.
- **Risk of mixing concerns**: tying bootstrap (container lifecycle) to filter content (frequent edits) re-introduces the exact coupling Phase 9 v1 was designed to eliminate.

If a real per-developer use case appears, revisit by reopening this section — the `personal-overrides.local.example.txt` placeholder file and `.gitignore` entry remain in place from Phase 5.

---

## L1 ship status & open follow-ups (2026-05-27)

This section captures what shipped with L1 (manual handoff) and what remains pending so the context isn't lost between sessions.

### Shipped (L1 = docs only, no infra)

| Artefact | Path | Purpose |
|---|---|---|
| Skill | [`.github/skills/rag-with-memory/SKILL.md`](../../.github/skills/rag-with-memory/SKILL.md) | Step-by-step walkthrough of the 3-call handoff |
| Routing rule | [`.github/instructions/mcp-routing.instructions.md`](../../.github/instructions/mcp-routing.instructions.md) — section "RAG ↔ context-mode handoff" | Canonical surface-disambiguation table (semantic_search ❌ / memory.* ❌ / ctx_* ✅) |
| Pattern doc integration | [`docs/patterns/context-mode-read-write-split.md`](../patterns/context-mode-read-write-split.md) — section "Integration with RAG" | Cost model + before/after ASCII + break-even table |
| Agent-decisions log entry | [`.github/context/agent-decisions.md`](../../.github/context/agent-decisions.md) — 2026-05-27 entry | POC results + decision rationale + promotion criteria |
| Parametric workspace mount | `docker-compose.yaml` (context-mode service) | `${CONTEXT_MODE_WORKSPACE:-/workspace}` for both volume target and env var, so forks/dev overrides work without doc rewrites |

### Empirical validation (current session)

| Test | Model | Tool-selection accuracy | Notes |
|---|---|---|---|
| Test 1 | Claude (primary agent, this session) | 3/3 | 55% token reduction via `ctx_stats`, 9/9 recall sections correct |
| Test 2 | GPT-5-mini (Explore subagent, no diagnostic probe) | 3/3 *identified* + 0/3 *executed* | Subagent correctly named `get_history`, `ctx_index`, `ctx_search` after reading the skill + routing section, but the MCP tools were not in its tool surface so it fell back to `memory.create` / `memory.view`. Naming convention applied correctly (`rag-cache-adr0029-context-mode`). Did **not** use `semantic_search`. |
| Test 3 | GPT-5-mini (Explore subagent, with explicit `tool_search` probe) | 0/3 (probe also unavailable) | Three explicit `tool_search` calls each returned "not available". Confirms LIMIT-1 as hard surface restriction, not a load-on-demand bootstrap issue. |
| Test 4 | Claude (primary agent, fresh chat window, user-driven) | 3/3 | `get_history(id="0016")` → `ctx_index(source="rag-cache-adr0016-coupons")` → `ctx_search`. `ctx_stats`: 60.8% reduction. **One recall returned zero hits due to Polish query without code identifier — see LIMIT-3.** |

### Known limitations / open follow-ups

#### LIMIT-1 — Subagent MCP tool surface gap (CONFIRMED HARD RESTRICTION)

**Finding (verified by Test 2 + Test 3, 2026-05-27)**: built-in VS Code subagents (tested: `Explore` with GPT-5-mini) have a **hard tool-surface restriction**. They expose neither RAG MCP tools (`mcp_ecommerceapp-*_*`) nor context-mode tools (`ctx_*`), and they **do not even expose `tool_search`** to load deferred tools on demand. Three explicit `tool_search` calls in Test 3 returned zero MCP tools. The skill is read correctly and the right tools are named, but they cannot be invoked.

**This is NOT a `tools:` allowlist issue.** Inspection of `.github/agents/*.md` shows that no custom agent (planner, implementer, code-reviewer, bc-switch, adr-generator, copilot-setup-maintainer) lists MCP tool ids in its `tools:` block, yet their prose freely invokes `query_docs` / `get_history` / `ctx_*` and these calls succeed at runtime in the parent agent context. The restriction is specific to built-in subagent invocation surface.

**Impact (corrected)**: the caller-coordination pattern of "parent caches via `ctx_index`, subagent recalls via `ctx_search`" **does not work** because the subagent cannot `ctx_search`. The handoff cost saving applies only to the parent agent's own recalls, not to delegated subagent work.

**Resolution options** (revised after Test 3):

| Option | Effort | Tradeoff |
|---|---|---|
| A. **Inline-chunks pattern (working today)**: parent does `query_docs`/`get_history`, formats relevant chunks as markdown, passes them in the subagent prompt directly. Subagent reads from prompt, never calls MCP. | Trivial — already shipped in skill | Larger subagent prompt; no cache benefit across multiple subagent invocations. Acceptable for one-shot delegations. |
| B. **Custom MCP-aware agent**: create `.github/agents/explorer-with-mcp.md` (mirroring built-in Explore behavior) and verify empirically whether custom agents inherit the parent's full MCP surface. Requires explicit invocation (`@explorer-with-mcp`). | Small (1 new agent file + empirical test) | Unknown until tested whether custom agents bypass the surface restriction. |
| C. **Wait for L2 (Phase 7)**: `query_docs_cached` wrapper at the RAG server side would solve the parent-recall economy regardless; delegated subagent calls would still need inline-chunks. | None (already roadmapped) | Doesn't help subagent delegations. |

**Recommendation**: **A is shipped** (inline-chunks pattern is now documented in the skill's "Delegating to a subagent" section). **B is the next empirical step** if delegated subagent work becomes a bottleneck. **C remains the long-term parent-side optimization**.

**Tests that established this**:

| Test | Outcome | Lesson |
|---|---|---|
| Test 2 (subagent, no diagnostic) | Subagent correctly identified `get_history`/`ctx_index`/`ctx_search` but fell back to `memory.*` claiming MCP unavailable. | Skill comprehension works; tool availability didn't. Unclear if bootstrap or hard restriction. |
| Test 3 (subagent, with explicit `tool_search` probe) | Subagent reported `tool_search` itself unavailable in 3 separate attempts; zero MCP tools loadable. | **Hard surface restriction confirmed.** Not a bootstrap issue. |

#### LIMIT-2 — Workspace path probing not enforced

**Finding**: docs now reference `$CONTEXT_MODE_WORKSPACE` (default `/workspace`), but no agent rule enforces the one-shot probe at session start. Agents may still default to literal `/workspace` from training data.

**Resolution**: add a single line to the `rag-with-memory` skill and/or context-mode-related agent prelude: *"At session start, if any `ctx_execute_file` call is expected, run `ctx_execute('sh','echo $CONTEXT_MODE_WORKSPACE')` once and cache."* Deferred until first real fork-config break.

#### LIMIT-3 — Multilingual recall gap in FTS5 cache

**Finding (Test 4, 2026-05-27)**: `ctx_search` uses SQLite FTS5 with English Porter stemmer + trigram fallback + RRF. Polish/German recall queries against English-cached content return zero hits unless the query embeds a code identifier (CamelCase class name, option key, ADR id, KI-NNN code). Empirical example:

- Cached ADR-0016 (English content: `Max coupons per order`, `MaxCouponsPerOrder`, `ceiling 10`).
- Query *"jakie są domyślne i maksymalne limity kuponów na zamówienie"* → **zero hits**.
- Query *"where are CouponsOptions configured"* → **top-1 correct**.

This is structurally different from the RAG multilingual gap: RAG bridges PL↔EN via the multilingual-glossary plus embedding similarity. `ctx_search` has neither — it is pure BM25 + trigram, language-agnostic at the token level but with no cross-language semantic bridge.

**Resolution options**:

| Option | Effort | Tradeoff |
|---|---|---|
| A. **Query-time workaround (shipped)**: skill's "Recall query tips (multilingual caveat)" section instructs agents to always include a code identifier or formulate queries in English. | Trivial — already shipped | Relies on agent compliance; weaker models may skip. |
| B. **Index-time bridging (Phase 7 candidate)**: L2 wrapper could inject PL↔EN synonym pairs from `multilingual-glossary.yaml` into the markdown before `ctx_index`, so FTS5 trigram fallback can match either side. | Moderate — ~1 day in the wrapper, requires glossary load + injection logic | Larger cache footprint; only benefits sources passed through the wrapper, not manual `ctx_index` calls. |
| C. **Per-language stemmer (upstream)**: would require context-mode itself to expose multi-language FTS5 tokenizer config. | Unknown — vendor change | Cleanest long-term but out of our control. |

**Recommendation**: **A is sufficient for today** (documented as caveat in skill). **B becomes a Phase 7 design input** — the L2 wrapper should consider glossary injection as an option, but it must not block Phase 7 if the simple wrapper ships first. **C is informational only**.

### What stays for Phase 7 (L2 wrapper)

The Phase 7 plan above (`query_docs_cached`) is unchanged. Promotion trigger remains: another model fails L1 in a real session despite the docs, OR LIMIT-1 resolution proves insufficient.

**Trigger to start Phase 7**: any of —
- Real sessions show repeated manual-handoff mistakes despite L1 docs.
- Second model POC (Claude / o-series) also fails L1 routing.
- Team requests "one-tool RAG" ergonomics.

**Out of scope for Phase 7**:
- Auto-eviction / cache TTL (rely on context-mode's session lifecycle).
- Cross-session cache persistence (use `ctx_index` defaults).
- Modifying `ctx_index` / `ctx_search` themselves (those are upstream context-mode tools).


---

## New files and modifications — registry

| File | Action | Phase | Impact on existing setup |
|---|---|---|---|
| `docker/context-mode/network-monitor.js` | New | 1 | None |
| `docker/context-mode/entrypoint.sh` | New | 1 | None |
| `Dockerfile-context-mode` | New | 1 | None |
| `.env.context-mode.example` | New (12 tunables) | 1 | Source of truth for defaults; copy to gitignored `.env.context-mode` |
| `docker-compose.yaml` | Delta (2 services + `ctx-net` bridge + 2 volumes) | 1/5 | Does not touch existing services |
| `.vscode/mcp.json` | New | 2 | Adds an MCP server; RAG remains |
| `.vscode/tasks.json` | Delta (3 new tasks) | 2 | Existing tasks unchanged |
| `.gitignore` | Delta (3 lines: alert log + personal-overrides + .env.context-mode) | 1/2/5 | None |
| `.github/hooks/context-mode.json` | New | 3 | Requires @copilot-setup-maintainer; 5 hooks total |
| `.github/copilot-instructions.md` | Append (section 13) | 4 | Requires @copilot-setup-maintainer |
| `.claude/settings.json` | ~~New~~ | ~~4~~ | **Skipped** — Claude Code specific; not applicable on VS Code Copilot Chat. See Phase 4 note on step 4.1b. |
| `docker/adguard/AdGuardHome.yaml` | New | 5 | None (gated by `--profile monitoring`) |
| `docker/adguard/community-blocklists.yaml` | New | 5 | None |
| `docker/adguard/team-blacklist.txt` | New | 5 | None |
| `docker/adguard/team-whitelist.txt` | New | 5 | None |
| `docker/adguard/personal-overrides.local.example.txt` | New | 5 | None |
| `docker/adguard/README.md` | New | 5 | Operational notes — first-boot hardening + monthly review checklist |

---

## Licensing (verified)

| Component | License | Company 300+ employees, internal dev use |
|---|---|---|
| context-mode | Elastic License 2.0 | ✅ FREE for internal use; ❌ NOT for hosting as a SaaS for external customers |
| AdGuard Home | GPL-3.0 | ✅ FREE with no company-size limits |
| Qdrant (self-host) | Apache 2.0 | ✅ FREE without restrictions (Qdrant Cloud = separate paid SaaS, not in scope) |
| StevenBlack/hosts | MIT | ✅ FREE |
| AdGuard SDN Filter | CC BY-SA 4.0 | ✅ FREE with attribution |
| EasyPrivacy | CC BY-SA 3.0 | ✅ FREE with attribution |
| Rancher Desktop | Apache 2.0 | ✅ FREE |
| Podman | Apache 2.0 | ✅ FREE |
| Docker Desktop | Subscription | ⚠ PAID for companies >250 employees OR >$10M revenue — workaround: Rancher / Podman / Docker Engine on Linux |

**Conclusion**: the whole stack is free for internal dev tooling at a 300+ employee company, provided Docker Desktop is swapped for Rancher Desktop or Podman.

---

## Multi-runtime notes

| Runtime | Required changes | |
|---|---|---|
| Docker Desktop (Windows) | None — works out of the box | ✅ |
| Rancher Desktop (containerd) | `docker` → `nerdctl` in hooks + tasks; verify `--network none` behaviour | ⚠ Document locally |
| Rancher Desktop (dockerd) | None — identical to Docker Desktop | ✅ |
| Podman (rootless) | `docker` → `podman` in hooks + tasks + mcp.json; `docker-compose` → `podman-compose` | ⚠ Document locally |

> **Rule**: files in the repository use `docker`. Swapping to `podman`/`nerdctl` is a local-only change (not committed) or an env-var override in VS Code tasks.

---

## Upgrade policy

1. Check release notes at [github.com/mksglu/context-mode/releases](https://github.com/mksglu/context-mode/releases)
2. Change the tag in `Dockerfile-context-mode` (one line)
3. `docker compose build context-mode`
4. `docker compose up -d context-mode`
5. `ctx doctor` in Copilot Chat — verification
6. Commit: `chore: bump context-mode to vX.Y.Z`
