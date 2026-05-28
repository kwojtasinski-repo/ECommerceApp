---
name: setup-context-mode-new-project
description: >
  Bring up the context-mode sandbox for a NEW project end-to-end. Wires the AdGuard
  DNS firewall, the Qdrant + SQLite storage from D2, the runtime image from D3, the
  MCP client config, and the L3 RAG auto-cache hook. Assumes a fresh git repository.
argument-hint: "<project-name> [--with-auto-cache] [--stdio|--http]"
---

# setup-context-mode-new-project — full context-mode bootstrap for a NEW project

End-to-end bootstrap. Outputs a working sandbox where the agent can call
`ctx_execute`, `ctx_execute_file`, `ctx_fetch_and_index`, `ctx_index`, `ctx_search`
with DNS-firewalled egress and persistent SQLite knowledge base.

> **KNOWN GAP — ADR-0028 Amendment 004**: per-collection RAG config isn't yet stored
> into Qdrant (Phase 3 in `docs/roadmap/rag-remote-multitenant.md`). If this project
> later co-hosts RAG with a sibling project on a shared server, weights and glossary
> won't be isolated until Phase 3 ships. For a standalone project on its own Docker
> Compose stack — the path this skill walks — no observable impact today.

---

## When to use

- Brand-new project: nothing under `docker/context-mode/`, no `.vscode/mcp.json`.
- Existing project, migrating from a non-sandboxed direct-execution agent setup.
- Re-bootstrap after a `docker compose down -v` that wiped sandbox state.

## When NOT to use

- Project just needs RAG, never executes code through the agent → skip and use
  E1 only.
- Adding a single new tool to an existing sandbox → edit
  `docker/context-mode/tools.json` directly; no bootstrap needed.
- Adding a runtime to an existing sandbox → use D3
  (`ctx-bootstrap-runtimes`).

---

## Steps

### 1. Prerequisites

Run the D-skills in order — each one is required by this skill:

1. [`ctx-bootstrap-network`](../ctx-bootstrap-network/SKILL.md) (D1) — AdGuard allowlist
2. [`ctx-bootstrap-storage`](../ctx-bootstrap-storage/SKILL.md) (D2) — Qdrant + SQLite
3. [`ctx-bootstrap-runtimes`](../ctx-bootstrap-runtimes/SKILL.md) (D3) — pick & install runtimes

Verify with `docker compose ps`:

| Service | State |
|---|---|
| `qdrant` | running |
| `adguard` | running |
| `context-mode` | NOT yet — built in this skill |

### 2. Copy the canonical Dockerfile + image entrypoint

From the ECommerceApp checkout:

```sh
cp <ecommerceapp-checkout>/Dockerfile-context-mode .
cp -r <ecommerceapp-checkout>/docker/context-mode/ docker/
```

Rename the image tag in the file header from `ecommerceapp-context-mode` to
`<project>-context-mode`.

### 3. Configure the entrypoint

Open `docker/context-mode/config.json` (or equivalent). Key fields:

```json
{
  "transport": { "type": "stdio" },
  "tools": {
    "ctx_execute": { "enabled": true, "allowedLanguages": ["javascript", "shell", "python"] },
    "ctx_execute_file": { "enabled": true },
    "ctx_fetch_and_index": { "enabled": true, "respectAdGuard": true },
    "ctx_index": { "enabled": true },
    "ctx_search": { "enabled": true },
    "ctx_purge": { "enabled": true, "requireConfirm": true }
  },
  "workspace": "/workspace",
  "dbPath": "/data/ctx.db"
}
```

- Match `allowedLanguages` to what D3 actually installed — do not enable `python`
  here if the runtime isn't shipped, or `ctx_execute` returns confusing errors.
- `respectAdGuard: true` ensures `ctx_fetch_and_index` reads through the resolver
  (defence-in-depth: AdGuard already firewalls, but the tool also checks).
- `requireConfirm: true` on `ctx_purge` blocks accidental wipes.

### 4. Append the `context-mode` service to `docker-compose.yaml`

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

- `stdin_open + tty` are required for stdio MCP transport (`docker exec -i`).
- `network_mode: "service:adguard"` shares the AdGuard network namespace so all
  egress traffic is forced through the DNS firewall.
- The bind-mounts come from D2 — paths must match exactly.

### 5. Build + start

```sh
docker compose build --no-cache context-mode
docker compose up -d context-mode
```

### 6. Verify with `ctx_doctor`

After registering the MCP client (step 7), call:

```
ctx_doctor()
```

Expected output bullets (all green):

- ✅ Workspace mount: `/workspace` readable, NOT writable
- ✅ DB path: `/data/ctx.db` writable
- ✅ Qdrant: reachable at `http://qdrant:6333`, collection `<project>_docs` exists
- ✅ AdGuard: DNS resolver `127.0.0.1` returns NXDOMAIN for `api.openai.com`
- ✅ Runtimes: `<n>/11` shipped (matches D3 install)

If any check fails, see [.github/skills/ctx-doctor-playbook/SKILL.md](../ctx-doctor-playbook/SKILL.md).

### 7. Register the MCP client

#### VS Code (`.vscode/mcp.json`)

```json
{
  "servers": {
    "<project>-context-mode": {
      "type": "stdio",
      "command": "docker",
      "args": ["exec", "-i", "<project>-context-mode", "node", "/app/cli.bundle.mjs", "serve"]
    }
  }
}
```

#### GitHub Copilot Web (`.github/copilot/mcp.json`)

Copilot Web does NOT support stdio. Use HTTP transport — set
`"transport": { "type": "http", "port": 4747 }` in `config.json` and:

```json
{
  "mcpServers": {
    "<project>-context-mode": {
      "type": "http",
      "url": "https://<your-public-host>/context-mode"
    }
  }
}
```

(See E4 for full client matrix.)

### 8. Wire the L3 RAG auto-cache hook (optional)

If `--with-auto-cache` (recommended for any project that uses RAG repeatedly),
follow [.github/skills/setup-auto-cache-hook/SKILL.md](../setup-auto-cache-hook/SKILL.md)
(E5).

The hook turns every RAG tool response into an automatic `ctx_index` call, so the
project's `ctx_search` knowledge base grows over time.

### 9. Smoke test the full chain

```
ctx_execute("javascript", "console.log(2+2)")
# → 4

ctx_index("hello", "smoke-test")
ctx_search(["hello"], "smoke-test")
# → 1 result, source=smoke-test

ctx_fetch_and_index("https://raw.githubusercontent.com/<allowed-org>/<repo>/main/README.md", "smoke-ext")
ctx_search(["readme"], "smoke-ext")
# → at least 1 chunk
```

If `ctx_fetch_and_index` returns NXDOMAIN: the URL's host isn't in the AdGuard
allowlist. Re-run D1 to add it.

---

## Common mistakes

- **Skipping AdGuard.** The sandbox container's DNS points at AdGuard. If AdGuard is
  down or never started, `ctx_fetch_and_index` fails with "DNS resolution failed",
  and `ctx_execute("shell", "curl …")` hangs. Always `docker compose up -d adguard`
  first.
- **Using stdio transport in CI.** Stdio only works when the host can run
  `docker exec -i`. CI runners typically can't sustain a long-lived exec session —
  use HTTP transport instead. The MCP client config differs (see E4).
- **Forgetting to mount the FTS5 SQLite file.** Without the bind-mount, every
  `docker compose down` wipes the knowledge base. Looks like ctx_search has
  amnesia on every restart.
- **Enabling Python in `allowedLanguages` without installing it in D3.**
  `ctx_execute("python", …)` then returns "Python not available", which looks like a
  tool bug. Always keep `allowedLanguages` in sync with the actual Dockerfile.
- **Setting `workspace` writable.** ADR-0029 mandates read-only. Symptom: a prompt-
  injection demo could `ctx_execute("shell", "echo bad > /workspace/src/...")` and
  modify source. Always `:ro`.
- **Hard-coding `localhost:6333` in env vars.** Inside the container, Qdrant is at
  `http://qdrant:6333` (service name). Using `localhost` resolves to the container
  itself.
- **Auto-cache hook installed but context-mode not running.** The hook silently
  no-ops — looks like "auto-cache doesn't work", but really context-mode just isn't
  up. Bring context-mode up FIRST, then enable the hook.

---

## Worked example: bootstrapping "AcmeApp" with auto-cache

1. D1 done → AdGuard up, allowlist covers `qdrant`, `huggingface.co`,
   `cdn-lfs.huggingface.co`, `github.com`, `raw.githubusercontent.com`.
2. D2 done → `acmeapp_docs` collection green, `./data/acmeapp/context-mode/ctx.db`
   touched.
3. D3 done → `Runtimes: 3/11` (js, sh, python installed).
4. Copy Dockerfile + `docker/context-mode/config.json` from ECommerceApp.
5. Set `allowedLanguages: [javascript, shell, python]` to match D3.
6. Append the compose service.
7. `docker compose build --no-cache context-mode && docker compose up -d context-mode`.
8. Register in `.vscode/mcp.json` (stdio).
9. `ctx_doctor()` → 5/5 green.
10. E5 → install auto-cache hook.
11. Smoke: `query_docs("anything")` → hook fires → `ctx_search(source="rag-auto-")`
    returns the result back.

---

## Related skills / docs

- [.github/skills/ctx-bootstrap-network/SKILL.md](../ctx-bootstrap-network/SKILL.md) (D1)
- [.github/skills/ctx-bootstrap-storage/SKILL.md](../ctx-bootstrap-storage/SKILL.md) (D2)
- [.github/skills/ctx-bootstrap-runtimes/SKILL.md](../ctx-bootstrap-runtimes/SKILL.md) (D3)
- [.github/skills/setup-adguard-policy/SKILL.md](../setup-adguard-policy/SKILL.md) (E3)
- [.github/skills/setup-mcp-clients/SKILL.md](../setup-mcp-clients/SKILL.md) (E4)
- [.github/skills/setup-auto-cache-hook/SKILL.md](../setup-auto-cache-hook/SKILL.md) (E5)
- [.github/skills/ctx-doctor-playbook/SKILL.md](../ctx-doctor-playbook/SKILL.md) — `ctx_doctor` not green
- [.github/skills/ctx-sandbox-bootstrap-verify/SKILL.md](../ctx-sandbox-bootstrap-verify/SKILL.md) — 8-check smoke
- [.github/skills/ctx-hardening-audit/SKILL.md](../ctx-hardening-audit/SKILL.md) — pre-merge audit
- [docs/playbooks/context-mode-bootstrap.md](../../../docs/playbooks/context-mode-bootstrap.md) (P1)
- [docs/getting-started-context-mode.md](../../../docs/getting-started-context-mode.md)
- [docs/adr/0029/0029-context-mode-mcp-sandbox.md](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md)
- [docs/adr/0028/amendments/0028-004-per-collection-config-gap.md](../../../docs/adr/0028/amendments/0028-004-per-collection-config-gap.md) — KNOWN GAP
