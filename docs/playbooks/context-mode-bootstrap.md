# Playbook — context-mode bootstrap for a new project

> **Audience**: an engineer (or agent) standing up the context-mode sandbox on a
> brand-new repository for the first time. Walks through D1 + D2 + D3 + E2 + E3 + E5
> in sequence with verification checkpoints between each step.
>
> **Estimated time**: 60–90 minutes for a first-timer on a clean Docker host.
> **Result**: a working sandbox with DNS firewall, persistent FTS5 storage, runtime
> image, MCP client registration, and the L3 RAG auto-cache hook.

---

## Pre-flight

Before starting, confirm:

- [ ] Docker Desktop (or Docker Engine + Compose v2) installed and running.
- [ ] Local ports 53/udp+tcp, 3000, 6333, 6334 are free.
- [ ] At least 4 GB free disk for image layers + Qdrant data.
- [ ] An ECommerceApp checkout (or equivalent reference repo) reachable from the new
      project's working directory — most steps copy templates from it.
- [ ] The MCP client you intend to use (VS Code Copilot, GitHub Copilot Web, Visual
      Studio 17.14+) installed.

If any prerequisite is missing, fix it before proceeding — partial bootstrap leaves
half-configured state that is harder to debug than a clean restart.

---

## Stage 0 — Project skeleton

```sh
mkdir -p <project-root>/{docker/{adguard/{conf,work,filters},context-mode},data/<project>/{qdrant,context-mode},.github/hooks,.vscode}
cd <project-root>
git init
touch data/<project>/context-mode/ctx.db
```

PowerShell:

```pwsh
New-Item -ItemType Directory -Force `
  docker/adguard/conf, docker/adguard/work, docker/adguard/filters, `
  docker/context-mode, data/<project>/qdrant, data/<project>/context-mode, `
  .github/hooks, .vscode | Out-Null
Set-Location <project-root>
git init
New-Item -ItemType File -Force data/<project>/context-mode/ctx.db | Out-Null
```

**Verification**:

```sh
find . -type d -maxdepth 4 | sort
test -f data/<project>/context-mode/ctx.db && echo "ctx.db touched OK"
```

---

## Stage 1 — Network policy (D1 + E3)

### 1a. Inventory external domains the project will hit

Per [.github/skills/ctx-bootstrap-network/SKILL.md](../../.github/skills/ctx-bootstrap-network/SKILL.md) step 1:

```sh
grep -rEho 'https?://[a-zA-Z0-9.-]+' . \
  --include='*.json' --include='*.yaml' --include='*.md' --include='*.cs' --include='*.py' \
  2>/dev/null \
  | sed -E 's|https?://([^/]+).*|\1|' | sort -u
```

On a brand-new project this likely returns nothing — that's fine. Plan for the
**minimum** set: `qdrant`, `huggingface.co`, `cdn-lfs.huggingface.co`,
`objects.githubusercontent.com`, `github.com`, `raw.githubusercontent.com`,
`api.github.com`.

### 1b. Drop the canonical AdGuard config (E3 steps 2–4)

`docker/adguard/conf/AdGuardHome.yaml`:

```yaml
schema_version: 28
users:
  - name: admin
    password: "$2a$10$REPLACE.WITH.YOUR.OWN.BCRYPT.HASH"
dns:
  bind_hosts: [0.0.0.0]
  port: 53
  upstream_dns: [tls://1.1.1.1, tls://8.8.8.8]
  protection_enabled: true
  filtering_enabled: true
  blocking_mode: nxdomain
filters:
  - enabled: true
    url: file:///opt/adguardhome/filters/<project>-allow.txt
    name: <project>-allow
    id: 1
```

`docker/adguard/filters/<project>-allow.txt`:

```text
! <project> AdGuard allowlist
@@||qdrant^
@@||huggingface.co^
@@||cdn-lfs.huggingface.co^
@@||objects.githubusercontent.com^
@@||github.com^
@@||raw.githubusercontent.com^
@@||api.github.com^
||*^$important
```

### 1c. Add AdGuard to `docker-compose.yaml`

```yaml
services:
  adguard:
    image: adguard/adguardhome:v0.107.50
    container_name: <project>-adguard
    restart: unless-stopped
    ports:
      - "127.0.0.1:53:53/udp"
      - "127.0.0.1:53:53/tcp"
      - "127.0.0.1:3000:3000"
    volumes:
      - ./docker/adguard/conf:/opt/adguardhome/conf
      - ./docker/adguard/work:/opt/adguardhome/work
      - ./docker/adguard/filters:/opt/adguardhome/filters:ro
    cap_add: [NET_ADMIN]
```

### 1d. Bring up + verify

```sh
docker compose up -d adguard
sleep 3
docker logs <project>-adguard 2>&1 | tail -10
```

Look for `[info] dns: starting dns server`. Then:

```sh
nslookup qdrant 127.0.0.1   # → no IP yet, qdrant not started — that's OK
nslookup api.openai.com 127.0.0.1   # → NXDOMAIN
```

**Checkpoint Stage 1**: `api.openai.com` returns NXDOMAIN from `127.0.0.1`. AdGuard
is correctly enforcing default-deny.

---

## Stage 2 — Storage (D2)

### 2a. Qdrant compose service

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.11.0
    container_name: <project>-qdrant
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - ./data/<project>/qdrant:/qdrant/storage
```

### 2b. Bring up + verify

```sh
docker compose up -d qdrant
sleep 5
curl -s http://localhost:6333/healthz   # → "healthz check passed"
```

### 2c. Verify SQLite path (created in Stage 0)

```sh
ls -lah data/<project>/context-mode/ctx.db   # → 0 bytes (will populate on first index)
```

**Checkpoint Stage 2**: Qdrant `/healthz` returns 200, SQLite file exists, mounts
ready.

---

## Stage 3 — Runtimes (D3) + sandbox image

### 3a. Pick runtimes

Default: `javascript` + `shell` (always shipped). Decide which extras (if any) the
project needs. For a docs-heavy repo, defaults are enough; for a project where the
agent will use `ctx_execute("python", ...)`, add Python.

### 3b. Copy Dockerfile + entrypoint

```sh
cp <ecommerceapp-checkout>/Dockerfile-context-mode .
cp -r <ecommerceapp-checkout>/docker/context-mode/* docker/context-mode/
```

Edit `Dockerfile-context-mode` if adding runtimes — see
[.github/skills/ctx-bootstrap-runtimes/SKILL.md](../../.github/skills/ctx-bootstrap-runtimes/SKILL.md) step 3.

### 3c. Edit `docker/context-mode/config.json`

Match `allowedLanguages` to what's actually installed:

```json
{
  "transport": { "type": "stdio" },
  "tools": {
    "ctx_execute":            { "enabled": true, "allowedLanguages": ["javascript", "shell"] },
    "ctx_execute_file":       { "enabled": true },
    "ctx_fetch_and_index":    { "enabled": true, "respectAdGuard": true },
    "ctx_index":              { "enabled": true },
    "ctx_search":             { "enabled": true },
    "ctx_purge":              { "enabled": true, "requireConfirm": true }
  },
  "workspace": "/workspace",
  "dbPath": "/data/ctx.db"
}
```

### 3d. Append the `context-mode` compose service

```yaml
services:
  context-mode:
    image: <project>-context-mode:latest
    build:
      context: .
      dockerfile: Dockerfile-context-mode
    container_name: <project>-context-mode
    environment:
      CONTEXT_MODE_WORKSPACE: /workspace
      CONTEXT_MODE_DB_PATH: /data/ctx.db
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs
    volumes:
      - ./data/<project>/context-mode/ctx.db:/data/ctx.db
      - .:/workspace:ro
      - ./docker/context-mode:/app/config:ro
    depends_on: [qdrant, adguard]
    dns: [127.0.0.1]
    network_mode: "service:adguard"
    stdin_open: true
    tty: true
```

### 3e. Build + bring up

```sh
docker compose build --no-cache context-mode
docker compose up -d context-mode
docker logs <project>-context-mode 2>&1 | tail -20
```

Expected last line: a JSON greeting message or "MCP server listening on stdio".

**Checkpoint Stage 3**: `docker exec -i <project>-context-mode sh -c 'echo $PATH'`
shows runtime directories (`/app/runtimes/node/bin`, etc.). `ctx_doctor` not yet
callable (no MCP client wired) — verify in Stage 5.

---

## Stage 4 — MCP client registration (E4)

For VS Code workspace use:

`.vscode/mcp.json`:

```jsonc
{
  "servers": {
    "<project>-context-mode": {
      "type": "stdio",
      "command": "docker",
      "args": [
        "exec", "-i", "<project>-context-mode",
        "sh", "-lc",
        "workspace=\"$CONTEXT_MODE_WORKSPACE\"; [ -n \"$workspace\" ] || workspace=/workspace; cd \"$workspace\" 2>/dev/null || cd /workspace; exec node --require /app/network-monitor.cjs /app/cli.bundle.mjs"
      ]
    }
  }
}
```

This makes the MCP process start from the live workspace mount automatically instead of expecting every sandbox snippet to hardcode `/workspace`.

Reload VS Code MCP servers (Command Palette → "MCP: Reload servers").

**Checkpoint Stage 4**: agent chat → "what MCP servers are connected?" → expected:
`<project>-context-mode` listed.

---

## Stage 5 — End-to-end verification

### 5a. `ctx_doctor`

In agent chat:

```
ctx_doctor()
```

Expected (all green):

| Check | Expected |
|---|---|
| Workspace mount | `/workspace` readable, NOT writable |
| DB path | `/data/ctx.db` writable |
| Qdrant | reachable at `http://qdrant:6333`, collection `<project>_docs` may not yet exist (created on first ingest) |
| AdGuard | DNS resolver returns NXDOMAIN for `api.openai.com` |
| Runtimes | `<n>/11` matches Stage 3 install |

If any check fails, see [.github/skills/ctx-doctor-playbook/SKILL.md](../../.github/skills/ctx-doctor-playbook/SKILL.md).

### 5b. Index + search round-trip

```
ctx_index("hello playbook", "smoke-test")
ctx_search(["hello"], "smoke-test")
```

Expected: 1 result returned, source = `smoke-test`.

### 5c. External fetch through AdGuard

```
ctx_fetch_and_index(
  "https://raw.githubusercontent.com/<your-org>/<your-repo>/main/README.md",
  "smoke-ext"
)
ctx_search(["readme"], "smoke-ext")
```

Expected: at least 1 chunk. If NXDOMAIN, the URL host isn't in the allowlist — add
it (Stage 1) and reload AdGuard.

### 5d. Forbidden domain check

```
ctx_fetch_and_index("https://api.openai.com/v1/models", "smoke-blocked")
```

Expected: DNS resolution failure — confirms the firewall is actually blocking.

**Checkpoint Stage 5**: all 4 sub-checks pass. Sandbox is operational.

---

## Stage 6 — Auto-cache hook (E5)

Only if the project also has RAG up (E1). Otherwise skip — there's nothing for the
hook to fan-out from.

### 6a. Copy the hook chain

```sh
cp <ecommerceapp-checkout>/.github/hooks/{auto-cache.mjs,auto-cache.probes.mjs,posttooluse-chain.mjs,posttooluse-chain.sh,context-mode.json} .github/hooks/
chmod +x .github/hooks/posttooluse-chain.sh
```

### 6b. Probe

```sh
node .github/hooks/auto-cache.probes.mjs
```

Expected: all probes green.

### 6c. Reload MCP and smoke-test

After reloading MCP servers, in chat:

```
query_docs("anything indexed in your RAG")
ctx_search(["anything"], "rag-auto-")
```

Expected: chunks indexed under `rag-auto-q-<hash>` source label.

If empty: enable debug mode (`AUTO_CACHE_DEBUG=1`) and re-run; the hook stderr log
explains the silent failure.

**Checkpoint Stage 6**: a RAG call followed by `ctx_search(source="rag-auto-")`
returns matching content. Auto-cache wired.

---

## Stage 7 — Harden & commit

### 7a. Pre-merge audit

Run [.github/skills/ctx-hardening-audit/SKILL.md](../../.github/skills/ctx-hardening-audit/SKILL.md)
to confirm all 22 ADR-0029 conformance items pass.

### 7b. Document the project README

Append a short "Context-mode bootstrap" section to `README.md` pointing at this
playbook + the per-skill files used.

### 7c. Commit

```sh
git add docker-compose.yaml docker/ data/<project>/.gitignore .vscode/mcp.json .github/hooks/
git commit -m "chore(sandbox): bootstrap context-mode (D1+D2+D3+E2+E3+E5)"
```

`.gitignore` for `data/<project>/`:

```text
# data/<project>/.gitignore
qdrant/
context-mode/ctx.db
context-mode/ctx.db-wal
context-mode/ctx.db-shm
```

---

## Troubleshooting flowchart

```text
ctx_doctor() red?
├── Workspace mount: writable when should be ro → check compose `:ro` suffix
├── DB path: not writable → check bind-mount source file exists + correct perms
├── Qdrant unreachable → docker logs qdrant; check service name (NOT localhost)
├── AdGuard wrong → see scripts/adguard/domain-policy.ps1 status; restart container
└── Runtimes: 0/11 → image didn't build with --no-cache; rebuild

ctx_search empty after ctx_index?
├── Different source label (case-sensitive) → re-check label spelling
├── ctx.db not persisted (no bind-mount) → fix Stage 2 mount
└── FTS5 schema not initialised → docker exec ... sqlite3 /data/ctx.db ".schema"

ctx_fetch_and_index returns DNS failure?
├── Domain not in allowlist → add to docker/adguard/filters/...; restart adguard
├── AdGuard down → docker compose up -d adguard
└── Container not using AdGuard DNS → check `network_mode: "service:adguard"`

ctx_execute("python", ...) "not available"?
└── Python not shipped in image → see ctx-bootstrap-runtimes (D3) to add

Auto-cache silent (rag-auto-* empty)?
├── context-mode not running → ctx_doctor() first
├── SOURCE_PREFIX customised but agent expects default → match prefixes
├── Hook not registered in client → check .github/hooks/context-mode.json
└── Client doesn't support hooks (Copilot Web) → hook fires only from VS Code-like hosts
```

---

## What to do next

- Run [docs/playbooks/rag-bootstrap.md](rag-bootstrap.md) if RAG isn't up yet — the
  auto-cache hook needs both halves.
- Add `tools/rag/` and ingest the project's docs ([.github/skills/setup-rag-new-project/SKILL.md](../../.github/skills/setup-rag-new-project/SKILL.md)).
- Review [.github/skills/ctx-hardening-audit/SKILL.md](../../.github/skills/ctx-hardening-audit/SKILL.md)
  for the full ADR-0029 conformance checklist.

---

## Reference

- [ADR-0029 — context-mode MCP sandbox](../adr/0029/0029-context-mode-mcp-sandbox.md)
- [ADR-0029 Amendment 1 — host-side RAG auto-cache](../adr/0029/amendments/0029-001-host-side-rag-auto-cache.md)
- [docs/getting-started-context-mode.md](../getting-started-context-mode.md)
- [docs/rag/auto-cache-hook.md](../rag/auto-cache-hook.md)
- All D + E skills under [.github/skills/](../../.github/skills/)
