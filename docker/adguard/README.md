# AdGuard Home — operational README

> **New to this stack?** Start with [docs/getting-started-context-mode.md](../../docs/getting-started-context-mode.md) — it walks you through the one-command bootstrap. This file is for **after** initial setup: rotating credentials, allowlist/blocklist workflow, monthly review.

## First-boot setup (automated — recommended)

```powershell
powershell -File scripts/context-mode-bootstrap.ps1
```

This generates `AdGuardHome.yaml` with a bcrypt admin password and starts everything. No browser wizard needed. Details in [getting-started-context-mode.md](../../docs/getting-started-context-mode.md).

## First-boot hardening (manual fallback only — if the bootstrap script cannot be used)

1. Start container: `docker compose --profile monitoring up -d adguard`
2. Open `http://127.0.0.1:3000` (host-only — never expose publicly).
3. In the wizard set a **strong admin password**: minimum 16 chars, mixed case,
   digits, symbols. Use a password manager. NEVER `admin`/`admin`.
4. Copy the resulting bcrypt hash from `AdGuardHome.yaml` inside the container
   (`docker exec ecommerceapp-adguard cat /opt/adguardhome/conf/AdGuardHome.yaml`)
   into our repo's `docker/adguard/AdGuardHome.yaml` `users:` block.
   (Note: the file is `.gitignore`d — keep it local.)
5. Web UI restriction: in this setup the UI is locked down by **docker port
   binding** `127.0.0.1:3000:3000` (compose file) — the UI is NEVER reachable
   from any non-loopback interface on the host. From other containers on
   `ctx-net` (e.g. `context-mode`) `adguard:3000` IS reachable, but every
   `/control/*` endpoint is bcrypt-password-gated (verified live:
   `GET /control/status` returns `403 Forbidden` without a session cookie).
   **Do NOT** add `allowed_clients: [127.0.0.1, ::1]` to AdGuardHome.yaml —
   that setting controls DNS-section client allowance, not the web UI, and
   setting it would break DNS resolution from `context-mode` (which queries
   from the container subnet, not 127.0.0.1).

## Rotating the admin password

```powershell
powershell -File scripts/context-mode-bootstrap.ps1 -AdGuardPassword 'NewStrongPass!' -ForceRegenerateAdGuard
```

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
