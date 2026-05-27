# Context-mode + AdGuard — Getting Started

> **Audience:** developer who has just cloned the repo and never used these tools before.
> **Goal:** running `ctx_fetch_and_index`, `ctx_execute`, etc. from VS Code Copilot Chat in under 5 minutes, with the DNS firewall protecting your machine.
> **What you get at the end:** a sandboxed Node.js sandbox (context-mode) whose every outbound DNS query is filtered through AdGuard Home (community blocklists + team allowlist).

---

## 0. Prerequisites (one-time, per machine)

| Tool | Minimum version | Check command | Where to get it |
|------|-----------------|---------------|-----------------|
| Windows PowerShell 5.1 (built-in) **or** PowerShell 7+ | 5.1 | `$PSVersionTable.PSVersion` | already on Windows; PS7 from <https://aka.ms/powershell> |
| Docker Desktop (Linux containers mode) | 4.20+ | `docker --version` | <https://www.docker.com/products/docker-desktop/> |
| Visual Studio Code | 1.95+ | `code --version` | <https://code.visualstudio.com/> |
| GitHub Copilot Chat extension | latest | VS Code Extensions tab | install from VS Code Marketplace |

After installing Docker Desktop, **start it and wait until the whale icon in the tray is solid** (engine running). The script below will fail with `error during connect` if the engine is not ready.

> No need to install Node.js, AdGuard, bcrypt tools, etc. — everything runs inside containers.

---

## 1. Clone the repo and enter the folder

```powershell
git clone https://github.com/<org>/ECommerceApp.git
cd ECommerceApp
```

---

## 2. Run the bootstrap script (the only command you need)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/context-mode-bootstrap.ps1
```

### What it does (you do not need to do any of this by hand)

| Step | Action | Why |
|------|--------|-----|
| 1 | Creates a Docker named volume `ecommerceapp_context-mode-data` and `chown`s it to UID 1000 | the sandbox runs as a non-root user; without the chown its SQLite session DB would be read-only |
| 2 | Generates `docker/adguard/AdGuardHome.yaml` with a **bcrypt-hashed admin password** (random 24-char default — printed once; or pass `-AdGuardPassword '...'`) | replaces AdGuard's browser-based first-run wizard — that wizard is the #1 footgun (see [KI-014](../../.github/context/known-issues.md)) |
| 3 | Builds the `context-mode:v1.0.151` image | `Dockerfile-context-mode` + locked package list |
| 4 | (Re)creates `ecommerceapp-adguard` and `ecommerceapp-context-mode` containers via `docker compose up -d --force-recreate` | applies any new healthcheck / hardening flags |
| 5 | Waits up to 30 s for AdGuard's `:53` DNS listener to come up | DNS without the listener = SERVFAIL on every fetch |
| 6 | Runs three gate checks: AdGuardHome.yaml present, `:53` listener active, sandbox can resolve `raw.githubusercontent.com` through AdGuard | confirms the firewall path actually works end-to-end |
| 7 | Prints `context-mode` healthcheck status | should be `healthy` within ~15 s |

### Expected output (happy path)

```
OK  Volume 'ecommerceapp_context-mode-data' ready.
!   Generated AdGuard password (printed once - store in your password manager):
  <random 24-character password>
OK  AdGuardHome.yaml written (user='admin', bcrypt hash applied).
OK  Image built.
OK  Containers up.
OK  DNS :53 listener active.
OK  G.1 AdGuardHome.yaml present
OK  G.2 :53 listener present
OK  G.3 sandbox can resolve raw.githubusercontent.com
context-mode healthcheck status: healthy

Bootstrap complete.
```

> ⚠️ **Copy the generated password into your password manager NOW.** It is shown only once. If you lose it, re-run the bootstrap with `-AdGuardPassword 'YourNewPass!' -ForceRegenerateAdGuard` to rotate it.

### Common parameters

```powershell
# Set your own password
powershell -File scripts/context-mode-bootstrap.ps1 -AdGuardPassword 'MyStrongPass123!'

# Rotate password / regenerate config
powershell -File scripts/context-mode-bootstrap.ps1 -ForceRegenerateAdGuard

# Fast rerun (image already built)
powershell -File scripts/context-mode-bootstrap.ps1 -SkipBuild
```

---

## 3. Enable the MCP server in VS Code

1. Open the repository in VS Code: `code .`
2. Open Copilot Chat (sidebar icon or `Ctrl+Alt+I`).
3. Click the **MCP** tab at the top of the chat panel.
4. Find `ecommerceapp-context-mode` in the list. Toggle it **on**.
5. VS Code will spawn the MCP client. Within 1–2 seconds it should show as **Started** and list **11 tools** (`ctx_execute`, `ctx_execute_file`, `ctx_index`, `ctx_search`, `ctx_fetch_and_index`, `ctx_batch_execute`, `ctx_stats`, `ctx_doctor`, `ctx_upgrade`, `ctx_purge`, `ctx_insight`).

> The MCP server is just a `node` process started inside the already-running container via `docker exec`. Restarting the MCP from the VS Code UI does **not** restart the container — it only respawns the node process. The DB stays warm.

---

## 4. Smoke test (optional but recommended)

From the repo root:

```powershell
powershell -File scripts/test-mcp-handshake.ps1
```

Expected:

```
raw lines: 2
server: context-mode v1.0.151
tools (11): ctx_execute, ctx_execute_file, ctx_index, ctx_search, ctx_fetch_and_index, ctx_batch_execute, ctx_stats, ctx_doctor, ctx_upgrade, ctx_purge, ctx_insight
```

If you get fewer than 11 tools or a different version, see Troubleshooting.

---

## 5. Try it in Copilot Chat

Ask Copilot something that exercises the sandbox, e.g.:

> *Use ctx_fetch_and_index to grab the README from https://raw.githubusercontent.com/microsoft/vscode/main/README.md, then summarise it.*

The first time Copilot calls `ctx_fetch_and_index`, two things happen:

1. **DNS** for `raw.githubusercontent.com` goes through AdGuard. Because the team allowlist already has `@@||raw.githubusercontent.com^`, it is allowed.
2. **HTTPS connection** opens and the page is downloaded and indexed inside the sandbox's FTS5 database.

If the target domain is on a community blocklist (ads, trackers, malware), AdGuard returns NXDOMAIN and the fetch fails — that is the firewall doing its job.

---

## 6. Daily life

| Situation | Action |
|----------|--------|
| Reboot / Docker restart | Nothing to do — containers have `restart: unless-stopped`. Just open VS Code, MCP reconnects automatically. |
| Bootstrap script printed a password and you closed the terminal | Re-run with `-AdGuardPassword 'NewPass!' -ForceRegenerateAdGuard` |
| Need to allow a new domain | `./scripts/adguard/domain-policy.ps1 add whitelist '@@\|\|example.com^'` (or edit `docker/adguard/team-whitelist.txt` directly). |
| Need to block a domain | `./scripts/adguard/domain-policy.ps1 add blacklist '\|\|evil.com^'` (or edit `docker/adguard/team-blacklist.txt` directly). |
| Bulk-edit, review, or restart AdGuard | See `docker/adguard/README.md` → "Daily management with the `domain-policy` CLI" (`status`, `show`, `edit`, `import`, `reload`). |
| Check what was blocked recently | UI → `http://127.0.0.1:3000` → Query log |
| Stop everything | `docker compose --profile monitoring --profile context-mode down` |
| Start again after stopping | `docker compose --profile monitoring --profile context-mode up -d` (bootstrap not needed once volumes exist) |

---

## 7. Troubleshooting

### "DNS :53 did NOT come up in 30s"

```powershell
docker logs ecommerceapp-adguard --tail 50
```

If you see `couldn't load config: ...`, the YAML template is corrupted — rerun bootstrap with `-ForceRegenerateAdGuard`.

### MCP shows 0 tools or "Failed to start"

```powershell
docker ps | Select-String context-mode
powershell -File scripts/test-mcp-handshake.ps1
```

- Container not running → `docker compose --profile context-mode up -d context-mode`
- Container running but handshake hangs → `docker exec -it ecommerceapp-context-mode node /app/cli.bundle.mjs --help`

### `ctx_fetch_and_index` returns SERVFAIL for every URL

You hit [KI-014](../../.github/context/known-issues.md). Run the gate check:

```powershell
docker exec ecommerceapp-adguard ls /opt/adguardhome/conf
docker exec ecommerceapp-adguard sh -c "netstat -ln | grep ':53 '"
docker exec ecommerceapp-context-mode nslookup raw.githubusercontent.com 172.28.0.2
```

If G.1 is missing `AdGuardHome.yaml`, rerun bootstrap. If G.2 is empty, the AdGuard process did not bind — check its logs. If G.3 returns SERVFAIL but G.1+G.2 pass, the upstream DNS servers are unreachable — visit `http://127.0.0.1:3000` → Settings → DNS settings.

### "Volume bootstrap failed"

You likely do not have Docker Desktop running. Start it, wait for the green whale, retry.

### Need to wipe everything and start fresh

```powershell
docker compose --profile monitoring --profile context-mode down -v
docker volume rm ecommerceapp_context-mode-data ecommerceapp_adguard-work 2>$null
Remove-Item docker/adguard/AdGuardHome.yaml -ErrorAction SilentlyContinue
powershell -File scripts/context-mode-bootstrap.ps1
```

---

## 8. Where to go next

- Full architectural rationale: [ADR-0029 — context-mode MCP sandbox](../adr/0029/0029-context-mode-mcp-sandbox.md)
- Network firewall configuration: [docker/adguard/README.md](../../docker/adguard/README.md)
- Tool routing (when to use which MCP): [.github/instructions/mcp-routing.instructions.md](../../.github/instructions/mcp-routing.instructions.md)
- Roadmap progress / phase tracking: [docs/roadmap/context-mode-integration.md](../roadmap/context-mode-integration.md)
- Known issues affecting setup: [.github/context/known-issues.md](../../.github/context/known-issues.md)
