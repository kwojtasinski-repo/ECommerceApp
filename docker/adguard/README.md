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

## Daily management with the `domain-policy` CLI

Day-to-day filter edits (block a new C2 domain, allowlist a service the
community lists wrongly blocked) go through `scripts/adguard/domain-policy.ps1`
(or `.sh` on WSL/Linux). The CLI edits **host files only** — no `docker exec`,
no credentials, no UI clicks. AdGuard sees the new files via the
`docker/adguard:/opt/adguardhome/conf` bind mount and re-reads them after a
container restart.

```powershell
# Inspect
./scripts/adguard/domain-policy.ps1 status              # filter table
./scripts/adguard/domain-policy.ps1 status --verbose    # + first 5 lines of each file
./scripts/adguard/domain-policy.ps1 show blacklist --tail 20
./scripts/adguard/domain-policy.ps1 show whitelist --grep github

# Edit
./scripts/adguard/domain-policy.ps1 add blacklist '||malware-c2.io^'
./scripts/adguard/domain-policy.ps1 add whitelist '@@||legitimate-cdn.com^'
./scripts/adguard/domain-policy.ps1 import blacklist ./threat-feed.txt
./scripts/adguard/domain-policy.ps1 edit whitelist      # opens in $EDITOR

# Control
./scripts/adguard/domain-policy.ps1 reload              # docker compose restart adguard
./scripts/adguard/domain-policy.ps1 help
```

VS Code tasks: `Tasks: Run Task` → `AdGuard: Show all filters` /
`AdGuard: Reload filters`.

After any edit on `team-blacklist.txt` / `team-whitelist.txt` the CLI prints a
git commit/PR reminder. **Commits are user-driven — the CLI never auto-stages
or pushes.**

### Dedup is intentionally narrow

`add` and `import` skip rules already present in the target file via **exact
text match** (trim whitespace, case-sensitive, comments ignored). Two cases
are **not** covered and that is by design:

| Not deduped | Why |
|---|---|
| `\|\|evil.com^` vs `\|\|www.evil.com^` (semantic overlap) | Different AdGuard rules — they match different DNS labels. A naive merge would silently drop legitimate coverage. |
| Same domain on both `team-blacklist.txt` and `team-whitelist.txt` | Whitelist intentionally overrides blacklist (AdGuard precedence). Cross-file collision is the legitimate way to temporarily allow a domain you also long-term block. |

If you want a semantic audit (e.g. find dominated rules), run it as a separate
review step — the CLI stays predictable.

### What the CLI never touches

- `users:` block in `AdGuardHome.yaml` (passwords, sessions)
- DNS upstream/bootstrap, querylog, statistics settings
- Container lifecycle beyond `docker compose restart adguard`
- Git (no auto-add, no auto-commit, no auto-push)
- `personal-overrides.local.example.txt` (per-developer experiment, not part of v1)

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
