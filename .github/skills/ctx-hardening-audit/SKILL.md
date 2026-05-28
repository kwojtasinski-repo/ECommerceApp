---
name: ctx-hardening-audit
description: >
  Programmatically verify every row of the ADR-0029 Conformance checklist
  (22 items) and produce a compliance report. Use before merging changes to
  `Dockerfile-context-mode`, `docker-compose.yaml` (context-mode block),
  `docker/adguard/**`, `docker/context-mode/**`, `.env.context-mode.example`,
  or before promoting an upgrade of `CONTEXT_MODE_TAG` / AdGuard.
  Read-only — surfaces gaps, never edits.
argument-hint: "[scope: full | delta | upgrade]"
---

# context-mode hardening audit (ADR-0029)

> **Source of truth**: ADR-0029 Conformance checklist
> ([docs/adr/0029/0029-context-mode-mcp-sandbox.md §Conformance checklist](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md#conformance-checklist)).
> Each numbered audit item below maps 1:1 to one checklist row.

Difference from sibling skills:

| Skill                         | Job                                                                |
| ----------------------------- | ------------------------------------------------------------------ |
| `ctx-sandbox-bootstrap-verify` | Runtime smoke after `docker compose up`. Goes fast, 8 checks.     |
| `ctx-doctor-playbook`         | Diagnose specific failure messages. Reactive.                     |
| **`ctx-hardening-audit`**     | **Coverage** of the 22 ADR-0029 conformance rows. Pre-merge gate. |

Scope:

- `full` — every item (default; ~3 min)
- `delta` — only items whose backing file changed in `git diff origin/main...HEAD`
- `upgrade` — items affected by `CONTEXT_MODE_TAG` or AdGuard image bump
  (items 1, 14, 16)

---

## Audit items

Each item: **how to verify** + **expected** + **fail action**. Failures
produce a row in the final report — never auto-fix.

### 1. Dockerfile builds from pinned git tag

```powershell
Select-String -Path Dockerfile-context-mode -Pattern '^ARG CONTEXT_MODE_TAG=' `
  ; Select-String -Path Dockerfile-context-mode -Pattern 'git clone --depth 1 --branch'
```

Expected: `ARG CONTEXT_MODE_TAG=vX.Y.Z` present AND a `git clone --branch ${CONTEXT_MODE_TAG}` line.
Fail action: PR must not merge with `npm install -g context-mode` or unpinned tag.

### 2. 6 hardening flags on the service

Grep `docker-compose.yaml` `context-mode:` block for: `read_only: true`,
`cap_drop: [ALL]`, `security_opt: ... no-new-privileges:true`, `pids_limit`,
`mem_limit` (or Docker default override), `ipc: none`.

```powershell
$svc = (Get-Content docker-compose.yaml -Raw) -split '(?m)^\s{4}\w+:' `
  | Where-Object { $_ -match 'context-mode:' }
foreach ($flag in 'read_only: true','cap_drop','no-new-privileges:true','pids_limit','mem_limit','ipc: none') {
  if ($svc -notmatch [regex]::Escape($flag)) { "MISSING: $flag" }
}
```

Expected: empty output.
Fail action: report missing flags; merge blocked.

### 3. Uses `ctx-net` and `dns: [adguard]`

Grep compose `context-mode:` block for `networks:` containing `ctx-net`
(or `ecommerceapp_ctx-net`) and `dns:` containing `adguard`. Must NOT
contain `network_mode: host`.

### 4. AdGuard config files present

```powershell
$adg = 'docker/adguard'
foreach ($f in 'AdGuardHome.yaml','community-blocklists.yaml','team-blacklist.txt','team-whitelist.txt','personal-overrides.local.example.txt') {
  if (-not (Test-Path "$adg/$f")) { "MISSING: $adg/$f" }
}
```

### 5. `personal-overrides.local.txt` is in `.gitignore`

```powershell
Select-String -Path .gitignore -Pattern 'personal-overrides\.local\.txt' -Quiet
```

Expected: `True`. Fail action: add to `.gitignore` before merge.

### 6. `network-monitor.cjs` preloaded via `node --require`

Read `docker/context-mode/entrypoint.sh` — must contain
`node --require /app/network-monitor.cjs /app/cli.bundle.mjs`.

### 7. `copilot-instructions.md` §13 append-only

```powershell
git log --oneline -p .github/copilot-instructions.md `
  | Select-String -Pattern '^[-+].*(section|##)\s*1[0-2]\b' -CaseSensitive
```

Expected: no `-` lines touching sections 1–12 after ADR-0029 merge.
Fail action: revert §1–12 edits OR raise a new ADR.

### 8. `.claude/settings.json` deny list

```powershell
$s = Get-Content .claude/settings.json -Raw | ConvertFrom-Json
$deny = $s.permissions.deny
foreach ($pat in 'Bash(sudo *)','Read(.env)','rm -rf /*') {
  if ($deny -notcontains $pat) { "MISSING deny: $pat" }
}
# Secrets pattern is a regex check
if (-not ($deny -match '(secret|token|api[_-]?key|password)')) { "MISSING deny: secrets pattern" }
```

### 9. `ctx_insight` port bound to 127.0.0.1

```powershell
docker port ecommerceapp-context-mode 2>$null `
  | Where-Object { $_ -notmatch '^127\.0\.0\.1:' -and $_ -notmatch '^\[::1\]:' }
```

Expected: empty (every published port is loopback). Fail action: compose
`ports:` must use `"127.0.0.1:9998:9998"` form.

### 10. Hook config uses `docker exec`

```powershell
Get-Content .github/hooks/context-mode.json `
  | Select-String -Pattern '"command"\s*:\s*"docker exec'
```

Expected: at least one match. Fail action: host-installed CLI is forbidden.

### 11. AdGuard gated by monitoring profile

Grep compose `adguard:` block for `profiles:` containing `monitoring`.
Expected: present. Fail action: AdGuard must not start by default.

### 12. `ctx_fetch_and_index` documented as AdGuard-gated

```powershell
Select-String -Path .github/instructions/mcp-routing.instructions.md `
  -Pattern 'ctx_fetch_and_index.*AdGuard|AdGuard.*ctx_fetch_and_index'
```

Expected: at least one match. Fail action: documentation gap.

### 13. Upgrade procedure = single ARG change

Read `Dockerfile-context-mode` — `CONTEXT_MODE_TAG` must appear in exactly
one `ARG` line. Any second occurrence is drift. The compose `image:` line
must reference `${CONTEXT_MODE_TAG:-...}`.

### 14. Docker Desktop licensing caveat documented

```powershell
Select-String -Path docs/adr/0029/0029-context-mode-mcp-sandbox.md `
  -Pattern 'Docker Desktop.*licens|Rancher|Podman'
```

Expected: present. Informational only — no merge block.

### 15. `docker/adguard/README.md` first-boot checklist + admin password rule

```powershell
Select-String -Path docker/adguard/README.md `
  -Pattern '16\+ chars|first.boot|admin password'
```

Expected: at least one match.

### 16. `allowed_clients` enforced

```powershell
$adg = Get-Content docker/adguard/AdGuardHome.yaml -Raw
if ($adg -notmatch 'allowed_clients:\s*\n\s*-\s*127\.0\.0\.1') { "FAIL: allowed_clients missing 127.0.0.1" }
if ($adg -notmatch 'allowed_clients:[\s\S]*?::1') { "FAIL: allowed_clients missing ::1" }

# Runtime check
docker run --rm --network ecommerceapp_ctx-net curlimages/curl:8.10.1 `
  -s -o NUL -w "%{http_code}`n" http://adguard:3000/control/login
# Expect: 403
```

### 17. Monthly version review log present

Look for `docs/reports/context-mode-version-review-YYYY-MM*.md` (any recent
month). Expected: at least one in the last 60 days. Fail action: schedule a
review; mark advisory.

### 18. `.env.context-mode.example` committed with 12 documented knobs

```powershell
$keys = (Get-Content .env.context-mode.example | Where-Object { $_ -match '^\s*[A-Z_]+=' } | ForEach-Object { ($_ -split '=')[0].Trim() })
"Found $($keys.Count) knobs"
# Expected: 12
$expected = @(
  'CONTEXT_MODE_TAG','ADGUARD_TAG','CONTEXT_MODE_MEM_LIMIT','CONTEXT_MODE_PIDS_LIMIT',
  'CONTEXT_MODE_TMPFS_SIZE','CONTEXT_MODE_WORKSPACE','CONTEXT_MODE_INSIGHT_PORT',
  'CONTEXT_MODE_EXTERNAL_MCP_NUDGE_EVERY','CONTEXT_MODE_FETCH_STRICT',
  'ADGUARD_DNS_UPSTREAM','ADGUARD_REFRESH_INTERVAL','ADGUARD_QUERY_LOG_ENABLED'
)
foreach ($k in $expected) { if ($keys -notcontains $k) { "MISSING: $k" } }
```

Adjust the expected list if ADR-0029 grows the documented knob set.

### 19. `.env.context-mode` in `.gitignore`

```powershell
Select-String -Path .gitignore -Pattern '^\.env\.context-mode$' -Quiet
```

### 20. Compose uses `${VAR:-default}` interpolation everywhere context-mode references env

```powershell
Select-String -Path docker-compose.yaml -Pattern '\$\{[A-Z_]+\}(?!:)' `
  | Where-Object { $_.Line -notmatch '#' }
```

Expected: empty (no naked `${VAR}` without default). Fail action: any line
without a default is a future broken-bootstrap waiting to happen.

### 21. Default `CONTEXT_MODE_FETCH_STRICT=1` in example file

```powershell
Select-String -Path .env.context-mode.example -Pattern '^CONTEXT_MODE_FETCH_STRICT=1' -Quiet
```

### 22. PR / personal-note discipline for relaxed flags

Grep recent PR titles / `.github/context/agent-decisions.md` for any
`CONTEXT_MODE_FETCH_STRICT=0` reference. If found, confirm a documented
reason is co-located. Advisory only.

---

## Report shape

```
ctx-hardening-audit (<scope>) — <PASS|FAIL>
 22 items checked, <N> failing

FAIL items:
  #<id> <short title> — <observed> (see ADR-0029 row "<row text>")
  ...

ADVISORY:
  #17 Monthly version review — last log <date or none>
  #22 Relaxed-flag discipline — <ok|N PRs flagged>

CONTEXT_MODE_TAG: vX.Y.Z (compose) / vX.Y.Z (Dockerfile) — <match|MISMATCH>
ADGUARD image:    <tag>
Date: <UTC ISO timestamp>
```

If any item FAILS, do not merge the PR until either the gap is closed or
ADR-0029 is amended to drop / soften that conformance row. Hand control
back to the human; never auto-amend ADR-0029 from this skill.
