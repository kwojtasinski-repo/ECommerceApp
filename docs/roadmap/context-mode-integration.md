# Roadmap: context-mode — MCP sandbox integration

> Status: — Unblocked — RAG stabilisation complete (2026-05-23), ready to implement
> Scope: `docker/context-mode/`, `Dockerfile-context-mode`, `docker-compose.yaml` (delta), `.vscode/`, `.github/hooks/`, `.github/copilot-instructions.md` (append), `.claude/settings.json`, `.env.context-mode.example`
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
| 3.1 | Hooks config (5 hooks: PreToolUse, PostToolUse, UserPromptSubmit, PreCompact, SessionStart) | `.github/hooks/context-mode.json` | 🔲 |
| 3.2 | Hook verification: restart VS Code, new Copilot session | VS Code | 🔲 |
| 3.3 | Session continuity test: force compaction, check resume | Copilot Chat | 🔲 |
| 3.4 | Measurement: `ctx stats` after a working session — verify % reduction | Copilot Chat | 🔲 |

**Phase 3 acceptance criterion**: hooks are active; `ctx stats` shows savings > 0; session restore after compaction works.

---

### Phase 4 — copilot-instructions.md merge

| Step | Description | File | Status |
|---|---|---|---|
| 4.1 | Append section 13 (context-mode routing) | `.github/copilot-instructions.md` | 🔲 |
| 4.1b | Add `.claude/settings.json` with deny/allow rules (sandbox permissions) | `.claude/settings.json` | 🔲 |
| 4.2 | Verification: existing agents still work (ADR query, BC routing) | Copilot Chat | 🔲 |
| 4.3 | Run `@copilot-setup-maintainer` (Workflow 11 + 7) | Copilot Chat | 🔲 |

**Phase 4 acceptance criterion**: project agents (ADR, BC) behave as before; context-mode routing is active alongside them.

---

### Phase 5 — AdGuard Home (DNS firewall, `monitoring` profile)

| Step | Description | File | Status |
|---|---|---|---|
| 5.1 | AdGuard service in docker-compose (profile `monitoring`) + volume | `docker-compose.yaml` | 🔲 |
| 5.2 | AdGuard config (system + per-client policies) | `docker/adguard/AdGuardHome.yaml` | 🔲 |
| 5.3 | Community blocklists (3 lists, auto-update every 7 days) | `docker/adguard/community-blocklists.yaml` | 🔲 |
| 5.4 | Shared team blacklist (commit + PR) | `docker/adguard/team-blacklist.txt` | 🔲 |
| 5.5 | Shared team whitelist (commit + PR) | `docker/adguard/team-whitelist.txt` | 🔲 |
| 5.6 | Personal overrides example (gitignored) | `docker/adguard/personal-overrides.local.example.txt` | 🔲 |
| 5.7 | `.gitignore`: `personal-overrides.local.txt` | `.gitignore` | 🔲 |
| 5.8 | **MANDATORY**: First-boot wizard — set strong admin password (16+ chars), copy bcrypt hash to repo config | `docker/adguard/AdGuardHome.yaml` | 🔲 |
| 5.9 | Restrict web UI to host loopback only: `allowed_clients: [127.0.0.1, ::1]` + `auth_attempts: 5` + `block_auth_min: 15` | `docker/adguard/AdGuardHome.yaml` | 🔲 |
| 5.10 | Operational notes (first-boot hardening + monthly review checklist) | `docker/adguard/README.md` | 🔲 |
| 5.11 | Verification: from `ctx-net` container, `curl http://adguard:3000/control/login` returns 403; DNS `:53/udp` still works | `docker exec` | 🔲 |
| 5.12 | Verification: AdGuard UI at `http://127.0.0.1:3000` — lists active, `context-mode` registered as a client with the strict policy | browser | 🔲 |

**Phase 5 acceptance criterion**: AdGuard reachable on :3000, 3 community lists active (~240k rules), `context-mode` resolves through AdGuard (`docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com` returns an answer; an unknown suspicious domain — NXDOMAIN), and the PR workflow on `team-blacklist.txt` has been exercised at least once.

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
```

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
| `.claude/settings.json` | New | 4 | Read by context-mode on VS Code Copilot too — enforces deny/allow |
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
