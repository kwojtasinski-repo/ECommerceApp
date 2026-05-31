---
name: ctx-doctor-playbook
description: >
  Map every known `ctx_doctor()` message and every observed context-mode
  startup / runtime error to its cause and the exact fix command. Use when
  `ctx_doctor` is not green, when a `ctx_*` tool errors out, or when the
  MCP server fails to register in VS Code. Diagnostic-only — never edits
  files automatically.
argument-hint: "[message: <exact substring from ctx_doctor or tool error>]"
---

# context-mode doctor playbook

> Companion to `.github/skills/ctx-sandbox-bootstrap-verify/SKILL.md`.
> That skill verifies bootstrap; this one diagnoses runtime failures.

Always run `ctx_doctor()` first and paste the full output into the
diagnosis. If `ctx_doctor` itself cannot be called, jump to § A.

---

## § A — `ctx_doctor` cannot be called

| Symptom                                                       | Cause                                                                          | Fix                                                                                                                                            |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| MCP server "context-mode" not listed in Copilot Tools panel   | `.vscode/mcp.json` missing entry, or VS Code never reloaded                    | Reload window (`Developer: Reload Window`); confirm `.vscode/mcp.json` has the `ecommerceapp-context-mode` entry                               |
| Listed but red icon, error: `spawn docker ENOENT`             | Docker Desktop / engine not running on PATH for VS Code                        | Start Docker Desktop; confirm `docker ps` works in a *new* PowerShell window; restart VS Code                                                  |
| `Error: container ecommerceapp-context-mode is not running`   | container exited or never started                                              | `docker compose ps context-mode` → if `Exited`, run `docker logs ecommerceapp-context-mode --tail 100` and match against § C                   |
| `OCI runtime exec failed: ... executable file not found`      | entrypoint path inside container changed                                       | `docker exec ecommerceapp-context-mode ls /entrypoint.sh` — if missing, rebuild image (`docker compose build context-mode`)                    |
| Tools register but every call hangs                           | upstream binary requires interactive TTY (stdio mode misconfigured)            | Confirm `.vscode/mcp.json` uses `docker exec -i` (not `-it`); `-t` allocates a TTY and corrupts the MCP framing                                |

---

## § B — `ctx_doctor` runs but reports problems

| Line in `ctx_doctor` output                                   | Meaning                                                                        | Fix                                                                                                                                            |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `Runtimes: 0/11`                                              | No language runtimes detected — Node itself broken or image corrupt            | `docker compose build --no-cache context-mode ; docker compose up -d context-mode`                                                             |
| `Runtimes: 1/11` (only shell)                                 | Node missing from runtime image                                                | Confirm Dockerfile stage 2 uses `FROM node:22-alpine`, not bare `alpine`                                                                       |
| `Runtimes: 2/11` (javascript + shell)                         | **Normal** — this is the shipped image baseline                                | No action. See [mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md) — schema lists 11 langs, only 2 ship installed    |
| `FTS5: disabled` / `FTS5: error: no such module`              | better-sqlite3 was built without FTS5, OR node:sqlite shim active without FTS5 | Rebuild image — Dockerfile must NOT pass `--ignore-scripts` so postinstall compiles better-sqlite3 with FTS5                                   |
| `Workspace mount: <empty>`                                    | bind-mount missing or env var unset                                            | Compose `volumes:` entry `.:${CONTEXT_MODE_WORKSPACE:-/workspace}:ro` must be present; check `.env.context-mode` if you overrode the default   |
| `Workspace mount: /workspace (not readable)`                  | bind-mount points at wrong host path or perms blocked                          | `docker exec ecommerceapp-context-mode ls /workspace` — if empty, fix host path; if "Permission denied", grant Docker Desktop file sharing     |
| `FTS5 path: ... (not writable)`                               | named volume not mounted or owned by root                                      | See ctx-sandbox-bootstrap-verify §5                                                                                                            |
| `AdGuard DNS: unreachable`                                    | `dns:` block missing in compose OR adguard service not on ctx-net              | Confirm `dns: [adguard]` and both services share `ctx-net`; restart context-mode after AdGuard is up                                           |
| `Network monitor: not loaded`                                 | entrypoint missing `--require /app/network-monitor.cjs`                        | See ctx-sandbox-bootstrap-verify §4                                                                                                            |
| `WARN: hooks directory empty`                                 | host hooks not mounted                                                         | If you depend on `.github/hooks/`, add a bind mount; otherwise ignore — hooks are optional                                                     |

---

## § C — Container exited / boot-time failures

Run `docker logs ecommerceapp-context-mode --tail 200` and match:

| Log message                                                   | Cause                                                                          | Fix                                                                                                                                            |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `Error: Cannot find module '/app/cli.bundle.mjs'`             | Upstream tag changed bundle name OR builder stage failed silently              | Check `Dockerfile-context-mode` `CONTEXT_MODE_TAG`; run `docker run --rm <image> ls /app/*.mjs` to discover the actual entry; update entrypoint |
| `better-sqlite3: NODE_MODULE_VERSION mismatch`                | image built against different Node version than runtime                        | `docker compose build --no-cache context-mode`                                                                                                  |
| `EACCES: permission denied, mkdir '/home/ctxmode/...'`        | volume permissions wrong after host volume reset                               | `docker volume rm ecommerceapp_context-mode-data ; docker compose up -d context-mode`                                                          |
| `EROFS: read-only file system, ... '/tmp/...'`                | tmpfs missing or sized 0                                                       | Compose `tmpfs:` entry must be `/tmp:rw,size=...,mode=1777`                                                                                    |
| `Error: ENETUNREACH ... adguard`                              | AdGuard service not started yet (race on cold boot)                            | Add `depends_on: [adguard]` (already in compose under monitoring profile); start AdGuard first                                                  |
| Repeated restart loop, no other log line                      | Container OOM-killed                                                           | `docker inspect ecommerceapp-context-mode --format '{{.State.OOMKilled}}'` — if True, raise `CONTEXT_MODE_MEM_LIMIT` in `.env.context-mode`    |

---

## § D — Tool calls succeed but behaviour wrong

| Symptom                                                                                                | Cause                                                                              | Fix                                                                                                                                            |
| ------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `ctx_execute("python", ...)` returns "Python not available"                                            | **Working as designed** — only `javascript` and `shell` installed                  | Use `javascript` (Node) or `shell` (POSIX sh); see [mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md) ctx_execute row |
| `ctx_execute_file` returns silent empty output despite file existing                                   | Sandbox cwd is not repo root — relative paths resolve under `/workspace`           | Always pass absolute paths rooted at the workspace mount; discover with `ctx_execute("shell", "echo $CONTEXT_MODE_WORKSPACE")`                 |
| `ctx_execute_file` fails with `ENOENT` and a path like `/workspace/C:\...` or `/workspace//Users/...` | Host absolute path was passed directly into the Linux container                      | Normalize before call from repo-relative: `<relative>` -> `/workspace/<relative>` (or `$CONTEXT_MODE_WORKSPACE/<relative>`), with `/` separators |
| `ctx_fetch_and_index(url)` returns DNS or `NXDOMAIN`                                                   | URL blocked by AdGuard filter (community blocklist or `team-blacklist.txt`)        | Either accept the block (it's working) or add the domain to `docker/adguard/personal-overrides.local.txt`; reload AdGuard task                 |
| `ctx_search` returns nothing despite recent `ctx_index`                                                | Wrong `source` partial match OR index in different session DB                      | Re-check the `source` label; partial match works (`source="rag-cache-"` matches `rag-cache-adr0029-x`)                                         |
| Recall after VS Code restart returns nothing                                                           | session-scoped data lost — `ctx_index` defaults to session scope                   | For cross-session persistence use project knowledge base (default `source=` w/o session prefix); see ADR-0029 §"Session vs project store"      |

---

## § E — When in doubt

1. `docker exec ecommerceapp-context-mode bash scripts/ctx-debug.sh` if the
   script is present (referenced from ADR-0029 monthly review row).
2. `docker logs ecommerceapp-context-mode --tail 500 | docker exec -i ecommerceapp-context-mode tee /tmp/last-boot.log` — capture for the PR.
3. Bisect on `CONTEXT_MODE_TAG`: pin previous known-good tag in
   `Dockerfile-context-mode`, rebuild, re-test. If the old tag works, file
   the issue upstream with the diff.

Never auto-edit compose / Dockerfile from this skill. Report findings and
hand the fix to the human or `@copilot-setup-maintainer`.
