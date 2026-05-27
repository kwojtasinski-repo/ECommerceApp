# context-mode — implementation details

> Companion document to [`context-mode-integration.md`](./context-mode-integration.md)
> Contains the full configuration for every file. Copy as-is unless noted otherwise.

---

## Introduction

### Why context-mode

GitHub Copilot is rolling out request usage billing. Every tool invocation
(run_in_terminal, read_file, grep_search, fetch URL) pushes raw data into the
context window — which directly inflates the number of requests billed.

Examples of raw output without the integration:

- `dotnet test` (30 suites) → 6 KB in context vs 337 B after integration
- Playwright snapshot → 56 KB vs 299 B
- Analysis of 47 files → 700 KB vs 3.6 KB
- A full working session → 315 KB vs 5.4 KB (98% reduction)

### Goal

Reduce context usage without changing how we work. Copilot still issues
commands — the context-mode sandbox executes them and returns only the result
(stdout), never the raw data.

### What we get

- `ctx_execute("shell", "dotnet test")` → 337 B instead of 6 KB
- `ctx_batch_execute(["dotnet build", "dotnet test"])` → one call instead of two
- Session continuity: after compaction the model resumes the last task without re-asking
- RAG MCP keeps working as before (project docs, ADRs, architecture)
- Monitoring: `.ctx-network-alerts.log` + AdGuard query log

### What we worry about and how we prevent it

**Data exfiltration via the MCP server**
The context-mode MCP sees tool arguments (code passed to ctx_execute, file
paths, content of ctx_execute_file). With network access it could ship those
arguments off the machine.

Mitigation: a custom `ctx-net` bridge + AdGuard as a DNS firewall — every
DNS resolution goes through our lists (community + team). Hardening flags
(`read_only`, `cap_drop:[ALL]`, `no-new-privileges`, `pids_limit`,
`mem_limit`, `ipc:none`) shrink the blast radius. A Node.js monitoring
hook logs every connection attempt to `.ctx-network-alerts.log` (a
secondary signal independent of DNS — it catches plain-IP connections too).

**Uncontrolled upgrades**
`npm install -g context-mode` always pulls the latest version. Breaking
changes do happen (172 releases so far).

Mitigation: git clone with a pinned tag in the Dockerfile. The whole team
runs the same version. Upgrade = one commit + review.

**Conflict with the existing Copilot configuration**
The project has an extensive `.github/copilot-instructions.md`, agents, ADR
routing. Overwriting that file would destroy the configuration.

Mitigation: append-only (section 13 at the end). Existing sections 1–12 stay
untouched.

**Differences between runtimes (Docker Desktop, Rancher, Podman)**
`internal: true` Docker networks have different edge cases across runtimes,
but a custom `ctx-net` bridge with DNS-only egress behaves identically
everywhere.

Mitigation: we use a plain bridge `ctx-net` (not `internal: true`) with
enforced `dns: [adguard]` — cross-runtime compatible.

### Risks and consequences

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| context-mode attempts an outbound connection | Possible | High | AdGuard DNS firewall + hardening flags; hook + alert log audit trail |
| AdGuard blocks a domain that is actually needed | Possible | Low | `team-whitelist.txt` has higher priority than community lists |
| Breaking change after upgrade | Low (pinned version) | Medium | Intentional upgrade rolled by the whole team |
| Hooks interfere with RAG MCP | Low | Low | `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50` |
| copilot-instructions.md collision | Very low | High | Append only; section 13 sits at the lowest priority |
| Podman rootless quirks | Possible | Low | Local-only docker→podman swap (not committed) |
| Docker Desktop license >250 employees | Certain at a 300+ company | Low | Rancher Desktop / Podman — free |

---

## Phase 1 — Docker sandbox

### `docker/context-mode/network-monitor.js`

```js
/**
 * Network monitoring hook for context-mode.
 * Overrides net.Socket.connect BEFORE the MCP server starts.
 * Every connection attempt is written to stderr (docker logs) and to
 * the alert file if the target is outside the private network range.
 * Works without kernel capabilities — plain Node.js, cross-runtime.
 */
'use strict';

const net = require('net');
const fs  = require('fs');

const ALERT_LOG = '/workspace/.ctx-network-alerts.log';

// IP ranges treated as internal (safe)
const INTERNAL = [
  /^127\./,
  /^::1$/,
  /^localhost$/i,
  /^0\.0\.0\.0$/,
  /^10\./,
  /^172\.(1[6-9]|2\d|3[01])\./,
  /^192\.168\./,
  /^fd[0-9a-f]{2}:/i,   // IPv6 ULA fc00::/7
];

function classify(host) {
  return INTERNAL.some(r => r.test(String(host))) ? 'INFO' : 'SUSPICIOUS';
}

const _connect = net.Socket.prototype.connect;

net.Socket.prototype.connect = function (options, ...rest) {
  const host = typeof options === 'object'
    ? (options.host || options.hostname || 'unknown')
    : String(options);
  const port = typeof options === 'object' ? options.port : rest[0];
  const level = classify(host);
  const ts    = new Date().toISOString();
  const line  = `[NET-MONITOR] [${level}] ${ts} → ${host}:${port}`;

  process.stderr.write(line + '\n');

  if (level === 'SUSPICIOUS') {
    try { fs.appendFileSync(ALERT_LOG, line + '\n'); } catch (_) { /* workspace unmounted */ }
  }

  return _connect.call(this, options, ...rest);
};
```

### `docker/context-mode/entrypoint.sh`

```bash
#!/bin/sh
# Wrapper that runs context-mode with the monitoring hook preloaded.
# `node --require` loads network-monitor.js BEFORE any MCP server code.
exec node \
  --require /app/network-monitor.js \
  /app/start.mjs \
  "$@"
```

### `Dockerfile-context-mode`

```dockerfile
# ─────────────────────────────────────────────────────────────────
# Stage 1: Build context-mode from source (pinned tag, auditable)
# ─────────────────────────────────────────────────────────────────
FROM node:22-alpine AS builder

# Build deps for native modules (better-sqlite3 fallback when node:sqlite is unavailable)
RUN apk add --no-cache git python3 make g++

# Clone exactly this tag — change it here on upgrade, nowhere else.
# v1.0.147+ required for CONTEXT_MODE_DIR support; bump to latest stable on rebuild.
ARG CONTEXT_MODE_TAG=v1.0.148
RUN git clone --depth 1 --branch ${CONTEXT_MODE_TAG} \
    https://github.com/mksglu/context-mode.git /build

WORKDIR /build
# NOTE: do NOT pass --ignore-scripts — better-sqlite3 needs its postinstall to compile.
RUN npm ci --production

# ─────────────────────────────────────────────────────────────────
# Stage 2: Minimal runtime image
# ─────────────────────────────────────────────────────────────────
FROM node:22-alpine

# Non-root user — security hardening
RUN addgroup -S ctxmode && adduser -S ctxmode -G ctxmode

# Copy the built application
COPY --from=builder /build /app
COPY docker/context-mode/network-monitor.js /app/network-monitor.js
COPY docker/context-mode/entrypoint.sh     /entrypoint.sh

RUN chmod +x /entrypoint.sh \
 && mkdir -p /home/ctxmode/.context-mode \
 && chown -R ctxmode:ctxmode /app /home/ctxmode/.context-mode

USER ctxmode
WORKDIR /workspace

ENTRYPOINT ["/entrypoint.sh"]
```

> **Upgrade**: change `CONTEXT_MODE_TAG` in the Dockerfile → `docker compose build context-mode` → `docker compose up -d context-mode`.

---

## Phase 1+5 — docker-compose.yaml (delta)

> The compose snippet reads developer-tunable values from `.env.context-mode`
> (gitignored). Defaults below in `${VAR:-default}` syntax are used when the
> file is absent. See **§Configurable parameters (env knobs)** below.

Append to the end of the `services:` section and extend the `volumes:` and `networks:` sections:

```yaml
  # ── Context-Mode MCP sandbox (always-on) ──────────────────────────────────────
  context-mode:
    build:
      context: .
      dockerfile: Dockerfile-context-mode
      args:
        CONTEXT_MODE_TAG: "${CONTEXT_MODE_TAG:-v1.0.148}"
    image: ecommerceapp/context-mode:${CONTEXT_MODE_TAG:-v1.0.148}
    container_name: ecommerceapp-context-mode
    env_file:
      - path: .env.context-mode      # gitignored per-developer overrides
        required: false              # OK if file missing — defaults below win
    stdin_open: true
    tty: false
    restart: unless-stopped
    user: "1000:1000"                              # non-root
    read_only: true                                # → root FS immutable
    tmpfs:
      - /tmp:rw,size=${CONTEXT_MODE_TMPFS_SIZE:-64m},mode=1777  # the only writable scratch
    cap_drop: [ALL]                                # no Linux capabilities
    security_opt:
      - no-new-privileges:true                     # no setuid escalation
    pids_limit: ${CONTEXT_MODE_PIDS_LIMIT:-100}    # limit fork bombs
    mem_limit: ${CONTEXT_MODE_MEM_LIMIT:-1g}       # memory cap
    cpus: ${CONTEXT_MODE_CPUS:-2}                  # CPU quota
    ipc: none                                      # zero shared memory with the host
    volumes:
      - .:/workspace                               # access to project files (R/W)
      - context-mode-data:/home/ctxmode/.context-mode  # SQLite session DB
    environment:
      CTX_FETCH_STRICT: "${CONTEXT_MODE_FETCH_STRICT:-1}"   # default 1 = block loopback+RFC1918
      CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY: "${CONTEXT_MODE_NUDGE_EVERY:-50}"
      CONTEXT_MODE_DIR: "/home/ctxmode/.context-mode"  # explicit storage root (v1.0.147+)
    ports:
      - "127.0.0.1:${CONTEXT_MODE_INSIGHT_PORT:-9998}:8765"  # ctx_insight web UI — localhost only
                                                   # NOTE: verify internal port via `ctx doctor` after first start;
                                                   # 8765 is the assumed default. Never bind to 0.0.0.0.
    networks:
      - ctx-net                                    # custom bridge, DNS via AdGuard
    dns:
      - adguard                                    # ONLY via AdGuard (no upstream fallback)

  # ── AdGuard Home — DNS firewall (profile: monitoring) ─────────────────────────
  # Start with: docker compose --profile monitoring up -d adguard
  # UI: http://127.0.0.1:3000  (setup wizard on first visit — set a STRONG password)
  adguard:
    image: adguard/adguardhome:${ADGUARD_TAG:-v0.107.50}
    container_name: ecommerceapp-adguard
    profiles: [monitoring]
    restart: unless-stopped
    ports:
      - "127.0.0.1:${ADGUARD_UI_PORT:-3000}:3000"   # web UI bound to localhost only
    volumes:
      - adguard-work:/opt/adguardhome/work
      - ./docker/adguard:/opt/adguardhome/conf:ro    # our configs (read-only mount)
    networks:
      ctx-net:
        aliases: [adguard]                          # context-mode resolves "adguard" → IP
```

In the `volumes:` section (at the bottom of the file) add:

```yaml
  context-mode-data:    # SQLite session DB for context-mode
  adguard-work:         # AdGuard query log + cached lists
```

In the `networks:` section (create it if missing) add:

```yaml
networks:
  ctx-net:
    driver: bridge
    # We do NOT use `internal: true` — AdGuard MUST have outbound access
    # to fetch lists and resolve upstream DNS for allowed domains.
```

---

## Configurable parameters (env knobs)

context-mode's upstream exposes only **three** environment variables. Anything
else (TTL cache window, throttling thresholds, chunk sizes) is hardcoded — to
change behavior per-call, pass parameters like `ttl: <ms>` or `force: true`
directly to the MCP tool.

We expose a small, deliberately minimal set of knobs via `.env.context-mode`
(gitignored, per-developer overrides) so each developer can tune resource limits
and pinned versions without editing `docker-compose.yaml`. The committed
`.env.context-mode.example` is the source of truth for defaults and team review.

### Upstream env vars (passed into the container, see [README §Security](https://github.com/mksglu/context-mode#security))

| Variable | Default | Range | Purpose |
|---|---|---|---|
| `CONTEXT_MODE_DIR` | `/home/ctxmode/.context-mode` | absolute path | Storage root for sessions + content. Hardcoded in compose. |
| `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY` | `50` | 1–100 | Re-injects "wrap in ctx_execute" hint every N MCP calls. Lower = noisier, higher = quieter. Aliased to `CONTEXT_MODE_NUDGE_EVERY` in `.env.context-mode`. |
| `CTX_FETCH_STRICT` | `1` | `0` / `1` | When `1`, blocks loopback + RFC1918 + ULA in addition to always-blocked IMDS/multicast. Aliased to `CONTEXT_MODE_FETCH_STRICT` in `.env.context-mode`. **Set to `0` only when testing a local dev server as MCP fetch target; document the deviation in your PR description.** |

### Our compose-level knobs (`.env.context-mode`)

| Variable | Default | Why a knob |
|---|---|---|
| `CONTEXT_MODE_TAG` | `v1.0.148` | Upgrade is one-line, no Dockerfile edit. |
| `ADGUARD_TAG` | `v0.107.50` | Same for AdGuard. |
| `CONTEXT_MODE_MEM_LIMIT` | `1g` | Beefy machines can raise (e.g. `2g`) for faster `ctx_index`. |
| `CONTEXT_MODE_PIDS_LIMIT` | `100` | Raise if you hit fork-bomb guard on large monorepo scans. |
| `CONTEXT_MODE_CPUS` | `2` | CPU quota (cores). Raise on machines with spare cores. |
| `CONTEXT_MODE_TMPFS_SIZE` | `64m` | `/tmp` scratch size. Raise if `ctx_index` runs out of temp space. |
| `CONTEXT_MODE_INSIGHT_PORT` | `9998` | Host port for `ctx_insight` UI. Change if `9998` is taken locally. |
| `CONTEXT_MODE_NUDGE_EVERY` | `50` | See upstream table above. |
| `CONTEXT_MODE_FETCH_STRICT` | `1` | See upstream table above. |
| `ADGUARD_UI_PORT` | `3000` | Host port for AdGuard UI. Change if `3000` is taken (often by frontend dev servers). |
| `ADGUARD_DNS_UPSTREAM` | `https://dns.cloudflare-dns.com/dns-query` | Corporate networks can swap to internal resolver; read by `AdGuardHome.yaml` template (substitute on container start). |
| `ADGUARD_FILTERS_UPDATE_INTERVAL` | `168` (hours = 7 days) | Community-list refresh cadence. Lower (`24`) on fast-moving security teams; raise (`720`) for stable environments. |

### `.env.context-mode.example` (committed)

```bash
# .env.context-mode.example — copy to .env.context-mode, edit, never commit your version
CONTEXT_MODE_TAG=v1.0.148
ADGUARD_TAG=v0.107.50

# Container resources (raise if your machine is beefy)
CONTEXT_MODE_MEM_LIMIT=1g
CONTEXT_MODE_PIDS_LIMIT=100
CONTEXT_MODE_CPUS=2
CONTEXT_MODE_TMPFS_SIZE=64m

# Insight web UI host port
CONTEXT_MODE_INSIGHT_PORT=9998

# Routing nudges (1-100). Higher = quieter. Default upstream = 10.
CONTEXT_MODE_NUDGE_EVERY=50

# Network fetch hardening. 1 = block loopback+RFC1918; 0 or unset = allow local dev servers
CONTEXT_MODE_FETCH_STRICT=1

# AdGuard
ADGUARD_UI_PORT=3000
ADGUARD_DNS_UPSTREAM=https://dns.cloudflare-dns.com/dns-query
ADGUARD_FILTERS_UPDATE_INTERVAL=168
```

**What is NOT a knob (and why)**:

- `cap_drop`, `read_only`, `no-new-privileges`, `ipc: none`, `user: 1000:1000` —
  hardening flags from ADR-0029. Changing them requires an ADR amendment.
- AdGuard `allowed_clients` — hardcoded in `AdGuardHome.yaml` (only the `context-mode`
  container IP may query). Loosening it requires an ADR amendment.
- TTL cache window / throttling thresholds / chunk sizes — hardcoded upstream,
  not env-controllable. Override per-call via tool parameters (`ttl: <ms>`,
  `force: true`, `concurrency: 1-8`, `contentType: 'code'|'prose'`).

**Safety note on `CONTEXT_MODE_FETCH_STRICT`**: this is the ONE knob where the default
is a security boundary, not a comfort knob. If a developer sets it to `0` locally,
their sandbox can reach the host loopback and the LAN — that’s how the
`ECommerceApp.API` / SQL Server containers become reachable from inside the MCP
sandbox. Acceptable for a one-off local fetch target test; never commit a script
or task that does this. Periodically grep `.env.context-mode` files during onboarding
review or pair sessions.

---

## Phase 2 — `.vscode/mcp.json`

> If the file already exists (RAG configuration), just add the `context-mode` key to the existing `servers` object.

```json
{
  "servers": {
    "context-mode": {
      "command": "docker",
      "args": [
        "exec",
        "-i",
        "ecommerceapp-context-mode",
        "node",
        "--require", "/app/network-monitor.js",
        "/app/start.mjs"
      ]
    }
  }
}
```

> **Podman**: replace `"docker"` with `"podman"` locally. Do not commit the change.
> **Rancher Desktop (containerd)**: replace `"docker"` with `"nerdctl"` locally.

---

## Phase 2 — `.vscode/tasks.json` (delta)

Append to the `tasks` array:

```json
{
  "label": "Context-Mode: Start",
  "type": "shell",
  "command": "docker compose up -d context-mode",
  "detail": "Start the context-mode MCP sandbox (ctx-net, monitoring on).",
  "group": "build",
  "presentation": { "reveal": "silent", "panel": "shared" }
},
{
  "label": "Context-Mode: Stop",
  "type": "shell",
  "command": "docker compose stop context-mode",
  "detail": "Stop context-mode.",
  "presentation": { "reveal": "silent", "panel": "shared" }
},
{
  "label": "Context-Mode: Network Alerts",
  "type": "shell",
  "command": "if (Test-Path .ctx-network-alerts.log) { Get-Content .ctx-network-alerts.log -Wait -Tail 50 } else { Write-Host 'No alerts — file does not exist (good sign).' }",
  "detail": "Tail the network alert log from context-mode. Empty = no suspicious connections.",
  "presentation": { "reveal": "always", "panel": "dedicated" }
},
{
  "label": "Context-Mode: Start + AdGuard",
  "type": "shell",
  "command": "docker compose --profile monitoring up -d context-mode adguard ; Start-Sleep 2 ; Start-Process 'http://localhost:3000'",
  "detail": "Start context-mode + AdGuard DNS firewall. Opens http://localhost:3000.",
  "presentation": { "reveal": "silent", "panel": "shared" }
}
```

---

## Phase 2 — `.gitignore` (delta)

Append (for example at the end of the temporary-files section):

```gitignore
# context-mode network alerts log (auto-generated, do not commit)
.ctx-network-alerts.log

# AdGuard personal overrides (per-developer, not shared)
docker/adguard/personal-overrides.local.txt

# context-mode per-developer env overrides (do not commit your version)
.env.context-mode
```

---

## Phase 3 — `.github/hooks/context-mode.json`

> New file. Create the `.github/hooks/` directory if it does not exist.
> **After adding this file, run `@copilot-setup-maintainer` (Workflow 11 + 7).**
>
> All 5 hooks are required for full session continuity (capture + snapshot + restore).
> Omitting `PreCompact` means the model loses working state on every compaction.
>
> **Container CLI path (2026-05-27 correction)**: the shipped image has no `context-mode` wrapper on PATH (`/app/bin/` contains only `statusline.mjs`). Invoke the bundle directly with `node /app/cli.bundle.mjs hook vscode-copilot <event>`. The configuration below already uses the corrected form.

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot pretooluse"
      }
    ],
    "PostToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot posttooluse"
      }
    ],
    "UserPromptSubmit": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot userpromptsubmit"
      }
    ],
    "PreCompact": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot precompact"
      }
    ],
    "SessionStart": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode node /app/cli.bundle.mjs hook vscode-copilot sessionstart"
      }
    ]
  }
}
```

> **Podman**: replace `docker exec` with `podman exec` locally.

---

## Phase 4 — `.github/copilot-instructions.md` (append)

> **Append at the very end** of the existing file. DO NOT modify sections 1–12.
> Section 13 has lower priority than project rules (ADR, BC, agents).
> **After modifying, run `@copilot-setup-maintainer` (Workflow 11 + 7).**
>
> The block below is the **upstream canonical routing file** from
> [`configs/vscode-copilot/copilot-instructions.md`](https://github.com/mksglu/context-mode/blob/main/configs/vscode-copilot/copilot-instructions.md)
> with a small project addendum at the end about RAG MCP coexistence.

```markdown
## 13. Context sandbox (context-mode) — MANDATORY routing rules

context-mode MCP tools available. Rules protect context window from flooding.
One unrouted command dumps 56 KB into context.

### Think in Code — MANDATORY

Analyze/count/filter/compare/search/parse/transform data: write code via
`ctx_execute(language, code)`, `console.log()` only the answer. Do NOT read
raw data into context. PROGRAM the analysis, not COMPUTE it. Pure JavaScript
— Node.js built-ins only (`fs`, `path`, `child_process`). `try/catch`,
handle `null`/`undefined`. One script replaces ten tool calls.

### BLOCKED — do NOT attempt

- **curl / wget**: terminal `curl`/`wget` intercepted and blocked. Use `ctx_fetch_and_index(url, source)` or `ctx_execute("javascript", "const r = await fetch(...)")`.
- **Inline HTTP**: `fetch('http`, `requests.get(`, `requests.post(`, `http.get(`, `http.request(` intercepted. Use `ctx_execute(language, code)` — only stdout enters context.
- **WebFetch / fetch**: use `ctx_fetch_and_index(url, source)` then `ctx_search(queries)`.

### REDIRECTED — use sandbox

- **Terminal / run_in_terminal (>20 lines output)**: terminal ONLY for `git`, `mkdir`, `rm`, `mv`, `cd`, `ls`, `npm install`, `pip install`. Otherwise: `ctx_batch_execute(commands, queries)` or `ctx_execute("shell", code)`.
- **read_file (for analysis)**: reading to edit → `read_file` correct. Reading to analyze/explore/summarize → `ctx_execute_file(path, language, code)`.
- **grep / search (large results)**: use `ctx_execute("shell", "grep ...")` in sandbox.

### Tool selection

0. **MEMORY**: `ctx_search(sort: "timeline")` — after resume, check prior context before asking user.
1. **GATHER**: `ctx_batch_execute(commands, queries)` — runs all commands, auto-indexes, returns search. ONE call replaces 30+. Each command: `{label: "header", command: "..."}`.
2. **FOLLOW-UP**: `ctx_search(queries: ["q1", "q2", ...])` — all questions as array, ONE call.
3. **PROCESSING**: `ctx_execute(language, code)` | `ctx_execute_file(path, language, code)` — sandbox, only stdout enters context.
4. **WEB**: `ctx_fetch_and_index(url, source)` then `ctx_search(queries)` — raw HTML never enters context.
5. **INDEX**: `ctx_index(content, source)` — store in FTS5 for later search.

**Parallel I/O batches**: pass `concurrency: 4-8` to `ctx_batch_execute` and `ctx_fetch_and_index` for network/API batches. Keep `concurrency: 1` for CPU-bound work (test, build, lint). GitHub gh: cap at 4.

### Output

Write artifacts to FILES — never inline. Return: file path + 1-line description.
Descriptive source labels for `ctx_search(source: "label")`.

### Session Continuity

Skills, roles, and decisions persist for the entire session. Do not abandon them
as the conversation grows.

### Memory

Session history is persistent and searchable. On resume, search BEFORE asking the user:

| Question | Query |
|---|---|
| What were we working on? | `ctx_search(queries: ["summary"], source: "compaction", sort: "timeline")` |
| What did we decide? | `ctx_search(queries: ["decision"], source: "decision", sort: "timeline")` |
| What NOT to repeat? | `ctx_search(queries: ["rejected"], source: "rejected-approach")` |
| What constraints exist? | `ctx_search(queries: ["constraint"], source: "constraint")` |

DO NOT ask "what were we working on?" — SEARCH FIRST. If search returns 0 results, proceed as a fresh session.

### ctx commands

| Command | Action |
|---|---|
| `ctx stats` | Call `ctx_stats`; display full output verbatim |
| `ctx doctor` | Call `ctx_doctor`; run returned shell command, display as checklist |
| `ctx upgrade` | Call `ctx_upgrade`; run returned shell command, display as checklist |
| `ctx purge` | Call `ctx_purge` with `confirm: true`. Warns before wiping knowledge base |
| `ctx insight` | Call `ctx_insight`; opens the local analytics dashboard at `http://localhost:9998` (localhost-only). 90 metrics across 23 event categories. |

### TTL cache (ctx_fetch_and_index)

Fetched URLs are cached in SQLite for **24h by default**. On cache hit the model gets a hint (~0.3KB) and calls `ctx_search` instead of re-fetching. Override per call:

- `ttl: <milliseconds>` — longer for stable specs, shorter for changelogs.
- `ttl: 0` or `force: true` — bypass cache, refetch always.
- 14-day cleanup runs on startup.

### Progressive throttling (ctx_search)

If the model issues many `ctx_search` calls in a row, results get throttled:

| Calls | Behavior |
|---|---|
| 1–3 | Normal (2 results per query) |
| 4–8 | Reduced (1 result per query) + warning |
| 9+ | **Blocked** — redirected to `ctx_batch_execute` |

**Rule for the model**: batch related queries into a single `ctx_search(queries: ["q1", "q2", "q3"])` call. Don't loop 1-by-1.

### Captured session events (~17 categories)

context-mode captures Files, Tasks, Plans, Rules (CLAUDE.md/AGENTS.md), UserPrompts, Decisions, Git, Errors, Error-Resolutions, Constraints, Blockers, RejectedApproaches, Environment, AgentFindings, IterationLoops, Latency, MCPTools, Subagents, Skills, ExternalRefs, Role, Intent, and Data. Critical (P1) events always persist; lower priorities are dropped first when the 2KB compaction snapshot budget is tight.

### Slash commands (NOT APPLICABLE on VS Code Copilot)

`/context-mode:ctx-*` slash commands are a Claude Code plugin feature. On VS Code Copilot type the bare `ctx <command>` form in chat — the model invokes the MCP tool automatically.

### Troubleshooting

For bug reports run the diagnostic and attach the output:

```powershell
docker exec ecommerceapp-context-mode bash scripts/ctx-debug.sh
```

It collects OS info, runtime versions, sqlite backend, hook validation, FTS5 test, process check, redacted configs, and session DB info into a single pasteable report.

### Project addendum — RAG MCP coexistence

This project also runs the `ecommerceapp-rag-*` MCP servers (ADR / docs queries).
Do NOT route `mcp__rag-*` calls through `ctx_execute` — they are already
small, structured responses. The `CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY=50`
env var keeps RAG calls outside the sandbox guidance loop.
```

---

## Phase 4b — `.claude/settings.json` (repo root, permissions)

> New file at `.claude/settings.json`. context-mode reads this file on **all**
> platforms (Claude Code AND VS Code Copilot) and enforces deny/allow rules
> inside the sandbox before any tool runs.
>
> Cheap defense-in-depth: blocks `sudo`, `rm -rf /`, and reading `.env*` even
> if the model is tricked into trying.

```json
{
  "permissions": {
    "deny": [
      "Bash(sudo *)",
      "Bash(rm -rf /*)",
      "Bash(rm -rf ~)",
      "Bash(rm -rf $HOME*)",
      "Bash(curl * | *sh)",
      "Bash(wget * -O- | *sh)",

      "Read(.env)",
      "Read(**/.env)",
      "Read(**/.env.*)",
      "Read(**/*.pem)",
      "Read(**/*.key)",
      "Read(**/*.pfx)",
      "Read(**/id_rsa*)",
      "Read(**/credentials*)",
      "Read(**/secret*)",
      "Read(**/appsettings.*.json)",

      "Edit(.git/**)",
      "Write(.git/**)",
      "Edit(Dockerfile*)",
      "Write(Dockerfile*)",
      "Edit(docker-compose*)",
      "Write(docker-compose*)",
      "Edit(.github/hooks/**)",
      "Write(.github/hooks/**)",
      "Edit(.claude/**)",
      "Write(.claude/**)",
      "Edit(docker/adguard/**)",
      "Write(docker/adguard/**)"
    ],
    "allow": [
      "Bash(git:*)",
      "Bash(npm:*)",
      "Bash(npx:*)",
      "Bash(dotnet:*)",
      "Bash(docker:*)",
      "Bash(python3:*)",
      "Bash(pip:*)"
    ]
  }
}
```

> **Why these denials?** The sandbox mounts the entire repo `.:/workspace` read-write so the agent can edit code. Without these rules a tricked agent could (a) read `.env`/`appsettings.*.json` secrets, (b) modify `.git/config` to insert a malicious credential helper, (c) backdoor the Dockerfile or compose file and wait for a rebuild, (d) edit its own hook config or AdGuard rules to weaken the firewall. `deny` always wins over `allow` per context-mode permission semantics.

---

## Phase 5 — AdGuard Home (DNS firewall)

AdGuard acts as a DNS firewall for `context-mode`: every DNS query from the
container hits AdGuard; if the domain is on a list (community or team)
AdGuard returns NXDOMAIN — the connection never starts.

### `docker/adguard/AdGuardHome.yaml` (system + per-client policies)

```yaml
# Initial AdGuard config. First visit to http://127.0.0.1:3000 — the setup wizard
# sets the admin password. After the wizard, replace the `users` section below.
#
# HARDENING:
#  - Set a STRONG password during the wizard (min 16 chars, mixed). NEVER admin/admin.
#  - `allowed_clients` restricts WHO can talk to the web UI — only the host loopback.
#    Even though context-mode sits on the same docker bridge, it CANNOT log into
#    AdGuard because its source IP is the container subnet, not 127.0.0.1.
#  - Rate limit `/control/login` to defeat bruteforce.
bind_host: 0.0.0.0
bind_port: 3000

# Restrict web UI access: only host loopback (where the port is exposed) can hit it.
allowed_clients:
  - 127.0.0.1
  - ::1

# Bruteforce protection on the login endpoint.
auth_attempts: 5            # max attempts before block
block_auth_min: 15          # minutes locked out after auth_attempts failures

users:
  - name: admin
    password: "$2y$10$REPLACE_AFTER_WIZARD"   # bcrypt hash from wizard

dns:
  bind_hosts: [0.0.0.0]
  port: 53
  upstream_dns:
    - https://dns.cloudflare-dns.com/dns-query   # DoH — encrypted upstream
    - https://dns.google/dns-query
  upstream_dns_file: ""
  bootstrap_dns: [1.1.1.1, 8.8.8.8]

  # Per-client policy: context-mode = strict; the rest (RAG, Qdrant) = permissive
  clients:
    persistent:
      - name: ecommerceapp-context-mode
        ids: [ecommerceapp-context-mode]        # match by docker hostname
        use_global_settings: false
        filtering_enabled: true
        safebrowsing_enabled: true              # Google Safe Browsing (free)
        parental_enabled: false
        safesearch_enabled: false
        # Lists to apply (filter ids from community-blocklists.yaml)
        filters: [1, 2, 3, 1001, 1002]          # community + team-blacklist + team-whitelist

      - name: ecommerceapp-rag
        ids: [ecommerceapp-rag-tools, ecommerceapp-rag-dotnet, ecommerceapp-qdrant]
        use_global_settings: false
        filtering_enabled: false                 # RAG / Qdrant without filtering

  # Global fallback for unknown clients
  filtering_enabled: false
```

### `docker/adguard/README.md` (operational notes — REQUIRED)

```markdown
# AdGuard Home — operational README

## First-boot hardening (MANDATORY)

1. Start container: `docker compose --profile monitoring up -d adguard`
2. Open `http://127.0.0.1:3000` (host-only — never expose publicly).
3. In the wizard set a **strong admin password**: minimum 16 chars, mixed case,
   digits, symbols. Use a password manager. NEVER `admin`/`admin`.
4. Copy the resulting bcrypt hash from `AdGuardHome.yaml` inside the container
   (`docker exec ecommerceapp-adguard cat /opt/adguardhome/conf/AdGuardHome.yaml`)
   into our repo's `docker/adguard/AdGuardHome.yaml` `users:` block, then commit.
5. Verify `allowed_clients: [127.0.0.1, ::1]` is in effect — from a container
   on `ctx-net`, `curl http://adguard:3000/control/login` MUST return 403.

## What context-mode can reach

| Target | Allowed | Why |
|---|---|---|
| `adguard:53/udp` | YES | DNS resolution |
| `adguard:3000/tcp` | NO | Web UI blocked by `allowed_clients` |

## Monthly review checklist (see ADR-0029)

- [ ] context-mode latest stable tag pinned in `Dockerfile-context-mode`?
- [ ] AdGuard Home latest patch pinned in `docker-compose.yaml`?
- [ ] CVE feed checked: GHSA for `better-sqlite3`, `node`, `adguardhome`?
- [ ] `docker exec ecommerceapp-context-mode bash scripts/ctx-debug.sh` healthy?
- [ ] AdGuard query log reviewed for unexpected blocked-domain spikes?
```


### `docker/adguard/community-blocklists.yaml`

```yaml
# Three community lists, auto-update every 7 days.
# Format: AdGuard understands all popular formats (hosts, AdBlock, plain).
# Maintainers add new entries continuously; we fetch weekly snapshots.

filters:
  - id: 1
    enabled: true
    name: "StevenBlack Unified (ads + malware + tracking)"
    url: "https://raw.githubusercontent.com/StevenBlack/hosts/master/hosts"
    # Format: hosts (0.0.0.0 domain). ~140k rules, conservative (no breakage).

  - id: 2
    enabled: true
    name: "AdGuard SDN Filter"
    url: "https://filters.adtidy.org/extension/ublock/filters/3.txt"
    # Format: AdBlock Plus (||domain^). ~50k rules, official AdGuard list.

  - id: 3
    enabled: true
    name: "EasyPrivacy"
    url: "https://easylist.to/easylist/easyprivacy.txt"
    # Format: AdBlock Plus. ~50k rules, focused on tracking.

filters_update_interval: 168     # hours = 7 days. Use 24 (fresher) or 720 (more stable).
```

### `docker/adguard/team-blacklist.txt`

```
# Team-wide blocklist. Entries have HIGHER priority than community lists.
# AdBlock format: ||domain.com^
# Edit via Pull Request — changes apply to everyone.
#
# Filter id: 1001 (referenced from AdGuardHome.yaml)
#
# Examples (uncomment, commit, PR):
# ||suspicious-tracker.io^
# ||known-malware-c2.example^
```

### `docker/adguard/team-whitelist.txt`

```
# Team-wide allowlist. OVERRIDES community blocklists (highest priority).
# AdBlock format: @@||domain.com^
# Use when a community list mistakenly blocks something legitimate.
#
# Filter id: 1002 (referenced from AdGuardHome.yaml)
#
# Examples (uncomment, commit, PR):
# @@||api.nuget.org^
# @@||raw.githubusercontent.com^
```

### `docker/adguard/personal-overrides.local.example.txt`

```
# Personal overrides — local file, NOT committed (gitignored).
# Copy to `personal-overrides.local.txt` and edit as needed.
#
# AdBlock format (same rules as team-blacklist/whitelist):
# @@||my-experiment.local^
# ||domain-i-dont-want-personally.com^
#
# To have AdGuard load this file, add it as filter id 1003 in
# AdGuardHome.yaml on your local machine (do not commit that change).
```

### What you can add as custom rules — full reference

AdGuard, both in the UI (Filters → Custom filtering rules) and through our
files, accepts:

| Syntax | Action |
|---|---|
| `||domain.com^` | Block `domain.com` + all subdomains |
| `||domain.com^$client=ecommerceapp-context-mode` | Block for a specific client only |
| `@@||domain.com^` | Allow (override blacklist) |
| `*.suspicious.com` | Wildcard subdomain |
| `127.0.0.1 ads.example.com` | Hosts-format override (redirect to localhost) |
| `||ads.com$dnstype=AAAA` | Block IPv6 lookups only |
| `0.0.0.0 telemetry.example.com` | Hosts format |
| `address=/example.com/127.0.0.1` | Dnsmasq-style redirect |

**Comments**: a line starting with `#` is ignored (just like hosts).

### Can we scan ourselves? Yes — AdGuard exposes a REST API

AdGuard ships a full REST API documented in [openapi.yaml](https://github.com/AdguardTeam/AdGuardHome/tree/master/openapi):

| Endpoint | What it returns |
|---|---|
| `GET /control/querylog` | Full history of DNS queries (with filtering) |
| `GET /control/stats` | Aggregates: top domains, top blocked, top clients |
| `POST /control/filtering/add_url` | Add a new filter dynamically |
| `POST /control/filtering/set_rules` | Update custom rules |
| `POST /control/filtering/check_host` | "Would this domain be blocked?" |
| `GET /control/safebrowsing/enabled` | Safe Browsing status |
| `POST /control/dns_config` | Change upstream DNS at runtime |

Custom scanning / automation (example scenarios):

1. **New-domain detection** — cron every 1h: `GET /control/querylog`, find domains never seen before, push to a buffer
2. **Behaviour analysis** — average RPS per domain; sudden spike = signal
3. **Threat intel sync** — daily fetch from OpenPhish/URLhaus, dynamic `add_url`
4. **Audit report** — weekly: top 100 blocked, top 100 allowed, anomalies
5. **Pre-flight check** — before a CI push: `check_host` for every URL in newly added dependencies

**Built-in AdGuard features that already "scan" for us:**

- **Google Safe Browsing** — free, checks every domain against Google's malware DB (hashes, privacy-preserving)
- **Parental Control** — family-friendly filter (optional)
- **Allowed/Blocked services** — pre-built groups (Facebook, TikTok, etc.)
- **DNS rewrites** — redirect any domain to any IP (plays well with local dev DNS)

All of it through the REST API — we can build anything on top.

---

## Phase 6 (future) — suggestions automation + "new arrived" UI

> Status: NOT in the current scope. Plan kept in case community lists turn
> out to be insufficient. Add step by step when the need is real.
> **Note**: team CI is Azure DevOps (GitHub Actions are unavailable). The
> snippets below use `gh pr create` for brevity — replace with
> `az repos pr create` when implementing.

### `tools/adguard/triage-queries.ps1`

```powershell
# Triggered by Windows Task Scheduler / Azure DevOps Pipeline / cron.
# (Team CI = Azure DevOps; GitHub Actions are unavailable.)
# Frequency: every 1h (active observation) or 24h (stable setup).

param(
    [string]$AdGuardUrl = 'http://localhost:3000',
    [string]$AdGuardUser = 'admin',
    [string]$AdGuardPass = $env:ADGUARD_PASS,
    [string]$SuggestionsFile = 'docker/adguard/suggestions.json'
)

$auth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${AdGuardUser}:${AdGuardPass}"))
$headers = @{ Authorization = "Basic $auth" }

# 1. Pull the query log since the last run
$lastRun = if (Test-Path $SuggestionsFile) {
    (Get-Content $SuggestionsFile | ConvertFrom-Json).generated_at
} else { (Get-Date).AddDays(-1).ToString('o') }

$log = Invoke-RestMethod -Uri "$AdGuardUrl/control/querylog?older_than=" -Headers $headers

# 2. Filter: blocked + unknown (not in team-blacklist) + count >= 3
$candidates = $log.data |
    Where-Object { $_.reason -eq 'FilteredBlackList' -and $_.client -eq 'ecommerceapp-context-mode' } |
    Group-Object { $_.question.name } |
    Where-Object { $_.Count -ge 3 } |
    ForEach-Object {
        @{
            domain = $_.Name
            first_seen = ($_.Group | Sort-Object time | Select-Object -First 1).time
            count = $_.Count
            blocked_by = ($_.Group | Select-Object -First 1).rules[0].filter_list_id
            status = 'pending_review'
        }
    }

# 3. Append to suggestions.json
$existing = if (Test-Path $SuggestionsFile) { Get-Content $SuggestionsFile | ConvertFrom-Json } else { @{ suggestions = @() } }
$existing.generated_at = (Get-Date).ToString('o')
$existing.suggestions += $candidates
$existing | ConvertTo-Json -Depth 5 | Set-Content $SuggestionsFile

Write-Host "Found $($candidates.Count) new suggestions → $SuggestionsFile"
```

### `.vscode/tasks.json` — Problem Matcher (yellow warnings)

```jsonc
// Add as one of the tasks. Run it manually or on-save for suggestions.json.
{
  "label": "AdGuard: Show suggestions",
  "type": "shell",
  "command": "if (Test-Path docker/adguard/suggestions.json) { Get-Content docker/adguard/suggestions.json } else { Write-Host '[INFO] No suggestions pending.' }",
  "problemMatcher": [
    {
      "pattern": {
        // Regex catches each pending_review JSON entry and raises a warning (yellow)
        "regexp": "\"domain\":\\s*\"([^\"]+)\".*?\"count\":\\s*(\\d+).*?\"status\":\\s*\"pending_review\"",
        "file": 1,                  // domain goes into "file"
        "message": 2,               // count as the message
        "severity": "warning"       // YELLOW in the Problems panel
      },
      "owner": "adguard-suggestions",
      "fileLocation": ["relative", "${workspaceFolder}"]
    }
  ],
  "presentation": { "reveal": "never" }
}
```

### `tools/adguard/accept-suggestion.ps1`

```powershell
# Promote an entry from suggestions.json to team-blacklist.txt + commit.
param([Parameter(Mandatory)][string]$Domain)

# 1. Append to team-blacklist.txt
Add-Content -Path 'docker/adguard/team-blacklist.txt' -Value "||$Domain^   # accepted $(Get-Date -Format 'yyyy-MM-dd')"

# 2. Mark status=accepted in suggestions.json
$json = Get-Content 'docker/adguard/suggestions.json' | ConvertFrom-Json
($json.suggestions | Where-Object { $_.domain -eq $Domain }) | ForEach-Object { $_.status = 'accepted' }
$json | ConvertTo-Json -Depth 5 | Set-Content 'docker/adguard/suggestions.json'

# 3. Commit
git add docker/adguard/team-blacklist.txt docker/adguard/suggestions.json
git commit -m "chore(adguard): blacklist $Domain (auto-triaged, manual accept)"

Write-Host "Accepted $Domain. Push when ready: git push"
```

### Scheduled Task (Windows)

```powershell
# Register the schedule once (run as admin):
$action = New-ScheduledTaskAction -Execute 'pwsh' -Argument '-File C:\Projekty\ECommerceApp\tools\adguard\triage-queries.ps1'
$trigger = New-ScheduledTaskTrigger -Daily -At 09:00
Register-ScheduledTask -TaskName 'AdGuard Triage' -Action $action -Trigger $trigger
```

---

## Verification per phase

### Phase 1

```powershell
docker compose up -d context-mode
docker ps --filter name=ecommerceapp-context-mode
docker logs ecommerceapp-context-mode
```

Expected: container `Up`, no errors in the logs.

### Phase 2

```
# In Copilot Chat:
ctx stats
```

Expected: a response from context-mode (0 savings at first — normal).

```
# In Copilot Chat:
ctx_execute javascript console.log(6*7)
```

Expected: `42`.

```powershell
# Verify there are no alerts:
Test-Path .ctx-network-alerts.log
```

Expected: `False` or an empty file.

### Phase 3

```
# In Copilot Chat (after restarting VS Code):
ctx stats
```

Expected: `ctx_stats` is called by the SessionStart hook — visible in the logs.

### Phase 4

```
# In Copilot Chat — verify project agents still work:
"Show ADR-0013"
```

Expected: RAG MCP returns the ADR content; context-mode routing does not interfere.

### Phase 5

Open http://localhost:3000 → AdGuard UI reachable; once logged in we see:
- **Filters → DNS blocklists** — 3 community lists `Enabled`, each with a rule count and "Last updated"
- **Clients** — `ecommerceapp-context-mode` registered as a persistent client with filters attached
- **Query Log** — test with `docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com` → green entry; `nslookup evil-tracker.io` → red (blocked)

---

## Upgrading context-mode — procedure

1. Check the [release notes](https://github.com/mksglu/context-mode/releases) — look for breaking changes
2. Change the tag in `Dockerfile-context-mode` (the `ARG CONTEXT_MODE_TAG=...` line)
3. Rebuild: `docker compose build context-mode`
4. Restart: `docker compose up -d context-mode`
5. Verify: `ctx doctor` in Copilot Chat
6. Commit: `chore: bump context-mode to vX.Y.Z`

---

## Multi-runtime — local swap

> Do not commit these changes. Each developer configures their machine.

**Podman:**
- `.vscode/mcp.json`: `"docker"` → `"podman"`
- `.github/hooks/context-mode.json`: `docker exec` → `podman exec`
- VS Code tasks: `docker compose` → `podman-compose`

**Rancher Desktop (containerd/nerdctl):**
- `.vscode/mcp.json`: `"docker"` → `"nerdctl"`
- `.github/hooks/context-mode.json`: `docker exec` → `nerdctl exec`
- VS Code tasks: `docker compose` → `nerdctl compose`

**Rancher Desktop (dockerd):** no changes — identical to Docker Desktop.
