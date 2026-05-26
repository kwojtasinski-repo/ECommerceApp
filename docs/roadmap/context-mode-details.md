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

RUN apk add --no-cache git

# Clone exactly this tag — change it here on upgrade, nowhere else
ARG CONTEXT_MODE_TAG=v1.0.146
RUN git clone --depth 1 --branch ${CONTEXT_MODE_TAG} \
    https://github.com/mksglu/context-mode.git /build

WORKDIR /build
RUN npm ci --production --ignore-scripts

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

Append to the end of the `services:` section and extend the `volumes:` and `networks:` sections:

```yaml
  # ── Context-Mode MCP sandbox (always-on) ──────────────────────────────────────
  context-mode:
    build:
      context: .
      dockerfile: Dockerfile-context-mode
      args:
        CONTEXT_MODE_TAG: "v1.0.146"
    image: ecommerceapp/context-mode:1.0.146
    container_name: ecommerceapp-context-mode
    stdin_open: true
    tty: false
    restart: unless-stopped
    user: "1000:1000"                              # non-root
    read_only: true                                # → root FS immutable
    tmpfs:
      - /tmp:rw,size=64m,mode=1777                 # the only writable scratch
    cap_drop: [ALL]                                # no Linux capabilities
    security_opt:
      - no-new-privileges:true                     # no setuid escalation
    pids_limit: 100                                # limit fork bombs
    mem_limit: 1g                                  # memory cap
    ipc: none                                      # zero shared memory with the host
    volumes:
      - .:/workspace                               # access to project files (R/W)
      - context-mode-data:/home/ctxmode/.context-mode  # SQLite session DB
    environment:
      CTX_FETCH_STRICT: "1"                        # blocks loopback + RFC1918
      CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY: "50"  # reduces noise on RAG MCP calls
    networks:
      - ctx-net                                    # custom bridge, DNS via AdGuard
    dns:
      - adguard                                    # ONLY via AdGuard (no upstream fallback)

  # ── AdGuard Home — DNS firewall (profile: monitoring) ─────────────────────────
  # Start with: docker compose --profile monitoring up -d adguard
  # UI: http://localhost:3000  (setup wizard on first visit)
  adguard:
    image: adguard/adguardhome:v0.107.50            # pinned version
    container_name: ecommerceapp-adguard
    profiles: [monitoring]
    restart: unless-stopped
    ports:
      - "127.0.0.1:3000:3000"                       # web UI bound to localhost only
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
```

---

## Phase 3 — `.github/hooks/context-mode.json`

> New file. Create the `.github/hooks/` directory if it does not exist.
> **After adding this file, run `@copilot-setup-maintainer` (Workflow 11 + 7).**

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot pretooluse"
      }
    ],
    "PostToolUse": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot posttooluse"
      }
    ],
    "SessionStart": [
      {
        "type": "command",
        "command": "docker exec -i ecommerceapp-context-mode context-mode hook vscode-copilot sessionstart"
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

```markdown
## 13. Context sandbox (context-mode)

context-mode MCP tools are available. They sandbox raw data — protecting the
context window. **A single unrouted call can dump 56 KB into context.**

### Think in code (MANDATORY for data analysis)

Analysis, counting, filtering, comparing, parsing → **write a script** via
`ctx_execute(language, code)` and `console.log()` only the result. DO NOT
read raw data into context. One script replaces 10 tool calls.

```js
// Before: 47 × read_file = 700 KB.  After: 1 × ctx_execute = 3.6 KB.
ctx_execute("javascript", `
  const files = require('fs').readdirSync('src').filter(f => f.endsWith('.cs'));
  files.forEach(f => console.log(f + ': ' + require('fs').readFileSync('src/'+f,'utf8').split('\\n').length + ' lines'));
`);
```

### Tool priorities (when no project rule overrides)

0. **MEMORY**: `ctx_search(sort: "timeline")` — after resume check history before asking the user.
1. **GATHER**: `ctx_batch_execute(commands, queries)` — many commands + searches in ONE call.
2. **FOLLOW-UP**: `ctx_search(queries: ["q1", "q2"])` — many questions in one call.
3. **PROCESSING**: `ctx_execute(language, code)` or `ctx_execute_file(path, language, code)` — sandbox.
4. **WEB**: `ctx_fetch_and_index(url, source)` → `ctx_search(queries)` — raw HTML never enters context.
5. **INDEX**: `ctx_index(content, source)` — store in FTS5 for later retrieval.

### Redirects (REDIRECTED)

| Instead of | Use |
|---|---|
| `run_in_terminal` (output > 20 lines) | `ctx_batch_execute` or `ctx_execute("shell", ...)` |
| `read_file` for **analysis** | `ctx_execute_file(path, language, code)` |
| `grep_search` on large output | `ctx_execute("shell", "grep ...")` inside the sandbox |
| `fetch` / WebFetch | `ctx_fetch_and_index(url)` → `ctx_search` |

> `read_file` is fine when you are editing a file. Use the sandbox only when you are **analysing**.

### Note: two independent session-memory systems

| System | Tool | Purpose |
|---|---|---|
| context-mode session DB | `ctx_search(source: "compaction")` | Tool history, files edited, decisions in this session |
| VS Code session store | `session_store_sql` | VS Code session history, previous conversations |

These systems are **independent** — do not mix them.

### ctx commands

| Command | Action |
|---|---|
| `ctx stats` | Call `ctx_stats`; show full output |
| `ctx doctor` | Call `ctx_doctor`; run the returned shell commands |
| `ctx upgrade` | Call `ctx_upgrade`; run the returned shell commands |
| `ctx purge` | Call `ctx_purge` with `confirm: true`. Warns about wiping the KB |
```

---

## Phase 5 — AdGuard Home (DNS firewall)

AdGuard acts as a DNS firewall for `context-mode`: every DNS query from the
container hits AdGuard; if the domain is on a list (community or team)
AdGuard returns NXDOMAIN — the connection never starts.

### `docker/adguard/AdGuardHome.yaml` (system + per-client policies)

```yaml
# Initial AdGuard config. First visit to http://localhost:3000 — the setup wizard
# sets the admin password. After the wizard, replace the `users` section below.
bind_host: 0.0.0.0
bind_port: 3000

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
