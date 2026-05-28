---
name: ctx-sandbox-bootstrap-verify
description: >
  Verify a freshly bootstrapped context-mode container is healthy, hardened,
  and routes egress through AdGuard. Run after every `docker compose up` of
  the context-mode service, after any Dockerfile/compose edit, and after
  bumping `CONTEXT_MODE_TAG` or the AdGuard image. Each check returns
  pass/fail with a fix link.
argument-hint: "[scope: quick | full | post-upgrade]"
---

# Verify context-mode sandbox bootstrap

> **Read first**: ADR-0029 Conformance checklist
> ([docs/adr/0029/0029-context-mode-mcp-sandbox.md](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md))
> defines the 22 hardening items. This skill is the runnable counterpart —
> each section below maps to one or more checklist rows.

Scopes:

| Scope          | Runs                                      | Use when                                   |
| -------------- | ----------------------------------------- | ------------------------------------------ |
| `quick`        | §1 + §2 only (~30s)                       | Smoke test after `docker compose up`       |
| `full`         | §1 – §8 (~2 min)                          | First bootstrap, post-PR, weekly review    |
| `post-upgrade` | §1 + §3 + §6 + §7 + §8                    | After bumping `CONTEXT_MODE_TAG` / AdGuard |

If you need the formal compliance report against all 22 ADR-0029 items,
load `.github/skills/ctx-hardening-audit/SKILL.md` instead — this skill is
runtime smoke; that one is checklist coverage.

---

## § 1 — Container is up and non-root

```powershell
docker ps --filter name=ecommerceapp-context-mode --format "{{.Status}}`t{{.Image}}"
# Expect: "Up X minutes" + image "ecommerceapp/context-mode:vX.Y.Z"

docker exec ecommerceapp-context-mode id
# Expect: uid=100(ctxmode) gid=101(ctxmode) groups=101(ctxmode)
# FAIL if uid=0 — see Dockerfile-context-mode stage 2 (USER ctxmode line missing)
```

Fail → check `Dockerfile-context-mode` `USER ctxmode` directive is present and
the build cache picked it up (`docker compose build --no-cache context-mode`).

---

## § 2 — All 6 hardening flags applied

```powershell
docker inspect ecommerceapp-context-mode --format '{{json .HostConfig}}' `
  | ConvertFrom-Json `
  | Select-Object ReadonlyRootfs,CapDrop,SecurityOpt,PidsLimit,Memory,IpcMode
```

Expected:

| Field          | Expected value                    |
| -------------- | --------------------------------- |
| `ReadonlyRootfs` | `True`                          |
| `CapDrop`      | `[ALL]`                           |
| `SecurityOpt`  | contains `no-new-privileges:true` |
| `PidsLimit`    | non-zero (default 100)            |
| `Memory`       | non-zero (default 512MiB)         |
| `IpcMode`      | `none`                            |

Any FAIL → diff `docker-compose.yaml` `context-mode:` block against ADR-0029
Conformance row "all 6 hardening flags". Missing fields usually come from
a merge that dropped the YAML keys.

---

## § 3 — Network isolation: only `ctx-net` + AdGuard DNS

```powershell
docker inspect ecommerceapp-context-mode --format '{{json .NetworkSettings.Networks}}' `
  | ConvertFrom-Json
```

- Must contain `ctx-net` (or `ecommerceapp_ctx-net`) and **only** that network.
- Must NOT contain `bridge`, `host`, or any project-default network.

```powershell
docker exec ecommerceapp-context-mode cat /etc/resolv.conf
# Expect first 'nameserver' line points to adguard's IP on ctx-net (not 8.8.8.8, not 1.1.1.1)
```

Fail → `dns:` block in compose missing or `network_mode: host` accidentally
re-introduced. See ADR-0029 §"Network isolation".

---

## § 4 — Network monitor is preloaded

```powershell
docker exec ecommerceapp-context-mode ls -la /app/network-monitor.cjs
# Expect: file exists, owned ctxmode:ctxmode, readable

docker exec ecommerceapp-context-mode cat /entrypoint.sh
# Expect: 'node --require /app/network-monitor.cjs /app/cli.bundle.mjs "$@"'
```

Fail → `docker/context-mode/network-monitor.cjs` not copied, or entrypoint
edited to drop `--require`. Network monitoring is silent — without it you
lose the egress audit log.

---

## § 5 — FTS5 session store is writable

```powershell
docker exec ecommerceapp-context-mode sh -c `
  'touch /home/ctxmode/.context-mode/.write-test && rm /home/ctxmode/.context-mode/.write-test && echo OK'
# Expect: OK
```

Fail → named volume `context-mode-data` missing or owned by root. Recreate:
`docker compose down context-mode ; docker volume rm ecommerceapp_context-mode-data ; docker compose up -d context-mode`.

---

## § 6 — `ctx_doctor` is green

Call from VS Code Copilot Chat (the MCP server must be enabled):

```
ctx_doctor()
```

Expected lines:

- `Runtimes: 2/11` (javascript + shell — only those two are installed; any
  other language returns "not available" by design; see
  [mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md))
- `FTS5: enabled`
- `Workspace mount: <CONTEXT_MODE_WORKSPACE or /workspace>`
- No `ERROR` / `WARN` lines

If `ctx_doctor` errors out → load `.github/skills/ctx-doctor-playbook/SKILL.md`
for the message → cause → fix map.

---

## § 7 — Ports bound to localhost only (no `0.0.0.0`)

```powershell
docker port ecommerceapp-context-mode
# Expect: every line starts with '127.0.0.1:' — never '0.0.0.0:'
```

Fail → `ports:` block in compose uses `"9998:9998"` instead of
`"127.0.0.1:9998:9998"`. ADR-0029 row "`ctx_insight` web UI port is bound
to 127.0.0.1 only".

---

## § 8 — AdGuard `allowed_clients` enforced

From any host shell:

```powershell
# Direct (host loopback) — should succeed
Invoke-RestMethod http://localhost:3000/control/status -ErrorAction Stop | Out-Null
"Host loopback OK"

# From inside ctx-net (should be BLOCKED)
docker run --rm --network ecommerceapp_ctx-net curlimages/curl:8.10.1 `
  -s -o /dev/null -w "%{http_code}`n" http://adguard:3000/control/login
# Expect: 403 (forbidden — allowed_clients filters by client IP)
```

Fail → `allowed_clients` missing or set to `[]` in
`docker/adguard/AdGuardHome.yaml`. ADR-0029 row "AdGuard `allowed_clients:
[127.0.0.1, ::1]`".

---

## Summary report shape

When you finish, post one block back to the user:

```
ctx-sandbox-bootstrap-verify (<scope>) — <PASS|FAIL>
 §1 non-root            : PASS
 §2 hardening flags     : PASS (6/6)
 §3 network isolation   : PASS
 §4 network monitor     : PASS
 §5 FTS5 writable       : PASS
 §6 ctx_doctor          : PASS
 §7 ports loopback-only : PASS
 §8 AdGuard ACL         : PASS
Container: ecommerceapp/context-mode:v1.0.151
AdGuard: <image:tag>
Next: <none | re-run after fix | run ctx-hardening-audit for full ADR-0029 coverage>
```

If any FAIL: list the failing § number, the observed value, and the fix link
from this skill. Do NOT auto-edit compose / Dockerfile — leave the fix to
the human or @copilot-setup-maintainer.
