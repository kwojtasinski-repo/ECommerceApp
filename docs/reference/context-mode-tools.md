# Context-mode operational tools — reference

> **Audience**: maintainers of the context-mode + AdGuard sandbox stack (per [ADR-0029](../adr/0029/0029-context-mode-mcp-sandbox.md)). Living spec for the two scripts that own the lifecycle.
> **Scope**: WHAT each tool owns, what it deliberately does NOT touch, the full flag surface, idempotency contract, and when to use each. Kept in sync with `scripts/context-mode-bootstrap.{ps1,sh}` and `scripts/adguard/domain-policy.{ps1,sh}`.
> **Out of scope**: end-user onboarding (see [getting-started-context-mode.md](../getting-started-context-mode.md)) and operational runbook (see [docker/adguard/README.md](../../docker/adguard/README.md)).

---

## Tool inventory

| Tool | Purpose | Owns | Frequency | Risk |
|---|---|---|---|---|
| `scripts/context-mode-bootstrap.{ps1,sh}` | One-shot stack bootstrap — replaces the AdGuard first-run wizard | admin user, bcrypt password, `AdGuardHome.yaml`, container build + recreate, named volume | Once per machine (rarely re-run) | High (destructive on `-ForceRegenerateAdGuard`) |
| `scripts/adguard/domain-policy.{ps1,sh}` | Day-to-day filter content management | `team-blacklist.txt`, `team-whitelist.txt`, container restart | Frequent (every block/allowlist change) | Low (file edit + 5 s restart) |

**Separation of concerns** is the core invariant. The two tools have zero functional overlap. Mixing them risks accidental container recreate when the user just wanted to add a rule.

---

## 1. `scripts/context-mode-bootstrap.{ps1,sh}`

### Purpose

Idempotent one-shot bootstrap that brings a fresh clone to a fully running, password-protected, AdGuard-gated context-mode stack — replacing the manual `http://127.0.0.1:3000` wizard.

### What it owns

| Artefact | Action |
|---|---|
| Named docker volume `context-mode-session-data` | Create + chown for SQLite session DB |
| `docker/adguard/AdGuardHome.yaml` | Generate from `.yaml.template` with bcrypt password hash. **Skipped** if file exists unless `-ForceRegenerateAdGuard` |
| AdGuard admin user / password | Set via template substitution (`${ADMIN_USER}`, `${PASSWORD_HASH}`). Bcrypt hash computed via `httpd:alpine htpasswd -nbBC 10` |
| `context-mode` image | Build via `Dockerfile-context-mode` if missing (skipped with `-SkipBuild`) |
| `adguard` + `context-mode` containers | Recreate so they pick up new config + healthchecks |

### What it deliberately does NOT touch

- `docker/adguard/team-blacklist.txt` / `team-whitelist.txt` — filter content (owned by `domain-policy`)
- `docker/adguard/personal-overrides.local.example.txt` or any `*.local.txt` — per-developer files
- `AdGuardHome.yaml` `filters:` / `whitelist_filters:` sections beyond what the template defines (community blocklist URLs + team file references)
- DNS upstreams, query log retention, statistics config
- `.vscode/`, MCP panel settings, RAG configuration

### Flags (PowerShell — bash parity)

| Flag | Type | Default | Effect |
|---|---|---|---|
| `-AdGuardUser` | string | `admin` | Admin username for AdGuard UI + `/control/*` API |
| `-AdGuardPassword` | string | _generated 24-char_ | Plaintext password. If omitted, script generates one and prints to terminal. Bcrypted before write |
| `-ForceRegenerateAdGuard` | switch | off | Overwrite existing `AdGuardHome.yaml`. **Destructive**: invalidates any UI sessions, requires re-login |
| `-SkipBuild` | switch | off | Skip `docker build` of `context-mode`. Useful when iterating on hooks without image changes |

### Idempotency contract

- Re-running with no flags is **safe**: existing `AdGuardHome.yaml` is preserved, volume is reused, containers are recreated only if compose detects config drift.
- `-ForceRegenerateAdGuard` is the ONLY destructive path. It always regenerates yaml from template with current `-AdGuardUser` / `-AdGuardPassword`.
- Failure midway (e.g. Docker engine down) leaves the system in a consistent partial state — re-run completes.

### When to use

| Situation | Command |
|---|---|
| First-time setup on a fresh clone | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/context-mode-bootstrap.ps1` |
| Forgot password / lost session | `powershell -File scripts/context-mode-bootstrap.ps1 -AdGuardPassword 'New!' -ForceRegenerateAdGuard` |
| Rebuilt hooks / sandbox image | `powershell -File scripts/context-mode-bootstrap.ps1` (idempotent; image rebuild + container recreate) |
| Start existing stack after manual stop | `docker compose --profile monitoring --profile context-mode up -d` |
| Verify MCP handshake from host | `powershell -File scripts/test-mcp-handshake.ps1` |
| Stop the sandbox stack | `docker compose --profile monitoring --profile context-mode down` |
| Daily AdGuard filter edits | **Use `domain-policy` instead** |

### Anti-patterns

- ❌ Running with `-ForceRegenerateAdGuard` to "kick" AdGuard into reloading filters — use `domain-policy reload`.
- ❌ Editing `AdGuardHome.yaml` by hand after bootstrap — next `-ForceRegenerateAdGuard` overwrites edits. Use the template instead.
- ❌ Adding filter URLs or domain rules through bootstrap — those live in `team-blacklist.txt` / `team-whitelist.txt` (managed by `domain-policy`) or in the yaml template's `filters:` section (managed via PR).

---

## 2. `scripts/adguard/domain-policy.{ps1,sh}`

### Purpose

File-first CLI for managing AdGuard team filter rules without touching credentials, bootstrap, or the UI. Optimised for the recurring "block a new C2 domain / allowlist a service the community lists wrongly blocked" workflow.

### What it owns

| Target | File | Filter id | yaml section |
|---|---|---|---|
| `blacklist` | `docker/adguard/team-blacklist.txt` | 1001 | `filters:` |
| `whitelist` | `docker/adguard/team-whitelist.txt` | 1002 | `whitelist_filters:` |

Plus: `docker compose restart adguard` for the reload operation.

### What it deliberately does NOT touch

- `AdGuardHome.yaml` `users:` block (passwords, sessions)
- DNS upstream/bootstrap, query log, statistics settings
- Container lifecycle **beyond** `docker compose restart adguard`
- `personal-overrides.local.example.txt` (per-developer placeholder, not part of v1 — see "Phase 9 v2 NOT PLANNED" in [`docs/roadmap/context-mode-integration.md`](../roadmap/context-mode-integration.md))
- Git (no auto-add, no auto-commit, no auto-push — only prints a reminder template)

### CLI surface

```
INSPECTION
  status [--verbose]                Filter table (id, name, enabled, source, rule count)
  show <target|all> [--tail N] [--grep PATTERN]
                                    Print contents

PRIMARY EDIT (file-first)
  edit <target>                     Open in $EDITOR (fallback: code -w → notepad/vi)
  import <target> <localfile>       Bulk append (dedup) + reload

CONVENIENCE EDIT
  add <target> <rule>               Single rule (dedup, syntax-validated) + reload

CONTROL
  reload                            docker compose restart adguard (~5 s downtime)
  help                              This message

TARGETS
  blacklist     docker/adguard/team-blacklist.txt
  whitelist    docker/adguard/team-whitelist.txt
```

### Dedup contract

Exact text match after trim. Case-sensitive. Skips lines starting with `#` or `!` (comments).

**Intentionally NOT covered**:

| Not deduped | Why |
|---|---|
| `\|\|evil.com^` vs `\|\|www.evil.com^` (semantic overlap) | Different AdGuard rules — match different DNS labels. Naive merge would silently drop coverage. |
| Same domain on `team-blacklist.txt` AND `team-whitelist.txt` | Whitelist intentionally overrides blacklist (AdGuard precedence). Cross-file collision is the legitimate way to temporarily allow a domain you also long-term block. |

### Rule syntax validation (`add` only)

Accepts: `||domain.com^`, `@@||domain.com^`, plain hostnames, optional `$option=...` modifier. Anything else rejected with exit 2 — prevents typos becoming silent no-ops.

### Reload semantics

`docker compose restart adguard` (~5 s downtime). Chosen over hot-reload because:

1. AdGuard v0.107.50 does not reliably hot-reload file-based filters without a kick.
2. 5 s downtime is acceptable for a dev sandbox.
3. Avoids parsing the bcrypt-protected `/control/filtering/refresh` API + session cookies.

### Idempotency contract

- All subcommands except `reload` are no-ops if the requested edit is already in the target file (dedup hits → "0 added, N already present").
- `reload` is safely re-runnable; AdGuard re-reads files on every restart.
- `edit` skips reload if no file change detected (sha256 before/after).
- Atomic file writes: append-only via `Add-Content` / `>>` — no temp file rename required for single-rule appends; bulk import already preserves pre-existing rules.

### Team workflow integration

After any edit that touches a committed file (`team-*.txt`), the CLI prints a git/PR reminder template with a suggested branch name. **It never auto-stages, commits, or pushes** — git ops stay user-driven so reviewer convention is preserved.

### When to use

| Situation | Command |
|---|---|
| Show current filter state | `./scripts/adguard/domain-policy.ps1 status` |
| Block a single new domain | `./scripts/adguard/domain-policy.ps1 add blacklist '\|\|malware-c2.io^'` |
| Allowlist a single service | `./scripts/adguard/domain-policy.ps1 add whitelist '@@\|\|legitimate-cdn.com^'` |
| Bulk import threat feed | `./scripts/adguard/domain-policy.ps1 import blacklist ./threat-feed.txt` |
| Hand-edit + cleanup | `./scripts/adguard/domain-policy.ps1 edit whitelist` |
| Pick up an out-of-band edit | `./scripts/adguard/domain-policy.ps1 reload` |
| Forgot the admin password | **Use `bootstrap.ps1 -ForceRegenerateAdGuard` instead** |

### Anti-patterns

- ❌ Using `docker exec` or `vi` inside the container to edit filter files — bypasses the dedup logic and risks line-ending corruption. The CLI always edits the HOST file.
- ❌ Calling `docker compose up -d --force-recreate adguard` for a reload — `restart` is enough; `up --force-recreate` resets healthcheck state and can race with other containers.
- ❌ Adding the same domain to both lists "to be safe" — whitelist already wins by AdGuard precedence; redundant entries clutter audit trails.
- ❌ Auto-staging edits via shell aliases — the WARN-only commit reminder is deliberate; reviewer convention is to land filter changes via PR.

---

## Relationship to ADR-0029

ADR-0029 establishes the architecture (sandbox + AdGuard DNS firewall + allowlist precedence). This document is the **operational contract** for the two scripts that maintain that architecture.

| ADR-0029 element | Owning tool |
|---|---|
| AdGuard admin user + bcrypt password | `bootstrap` |
| `AdGuardHome.yaml` lifecycle | `bootstrap` |
| Community blocklist URLs (`id=1,2,3`) | yaml template (PR-reviewed) |
| Team allowlist / blocklist content (`id=1001/1002`) | `domain-policy` |
| Filter reload after content change | `domain-policy reload` |
| Container build + initial start | `bootstrap` |
| Container restart for live reload | `domain-policy reload` |
| Per-developer personal overrides | **NOT PLANNED** (see roadmap Phase 9 v2) |

---

## Update policy for this document

- Any change to either script's flag set, subcommand set, ownership boundary, or idempotency contract MUST update this file in the same commit.
- Test of the contract: after reading this file, a new maintainer should be able to predict the script's behaviour without reading its source.
- Cross-link from ADR-0029 and from `docs/roadmap/context-mode-integration.md` ("Phase 9 v1 ✅ Done" block).
