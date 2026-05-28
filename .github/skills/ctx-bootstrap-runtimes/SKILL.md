---
name: ctx-bootstrap-runtimes
description: >
  Verify, enable, and add language runtimes to a project's context-mode sandbox.
  The shipped sandbox ships only `javascript` (Node) and `shell` (POSIX sh) — adding
  Python, .NET, Go, Ruby, etc. requires editing `Dockerfile-context-mode` and rebuilding.
  Covers the schema-vs-shipped trap, the `/app/runtimes/` install path, and
  `ctx_doctor` verification.
argument-hint: "<language> [--install]"
---

# ctx-bootstrap-runtimes — verify & install sandbox runtimes

context-mode's `ctx_execute(lang, code)` accepts an enum of 11 languages
(`javascript`, `typescript`, `shell`, `python`, `csharp`, `ruby`, `go`, `rust`, `php`,
`perl`, `R`, `elixir`) but the shipped Dockerfile installs **only 2** by default
(`javascript`, `shell`). Calling any other language returns a runtime-not-available
error, NOT a 404 — the schema accepts it, the runtime refuses it.

This skill teaches the agent to (1) verify what's actually shipped, (2) add a missing
runtime via `Dockerfile-context-mode`, (3) confirm with `ctx_doctor`.

---

## When to use

- A task needs `ctx_execute("python", ...)` and you don't know if Python is shipped.
- New project bootstrap — pick which runtimes to include in the sandbox image up front.
- `ctx_execute` returns `<Language> not available. Install <pkg> via …` — this skill
  handles the install.

## When NOT to use

- You only need `javascript` or `shell` — they're always shipped, no install needed.
- You want to install a CLI binary (not a language runtime) — that's a normal Dockerfile
  edit, no special pattern.
- You're considering installing a runtime AT QUERY TIME via `ctx_execute("shell",
  "apt-get install python3")` — NEVER DO THIS. The sandbox is read-only at runtime;
  package installs MUST go through the Dockerfile + image rebuild.

---

## Steps

### 1. Verify what's currently shipped

The authoritative check is `ctx_doctor`. Call it from the agent:

```
ctx_doctor()
```

Look for a `Runtimes: <n>/11` line. If `<n> == 2`, only the shipped defaults are
installed. Look for the per-language detail table — each unshipped language shows the
install command needed.

CLI fallback (when `ctx_doctor` is unavailable):

```sh
docker exec -i <project>-context-mode sh -c \
  'for cmd in node sh python3 dotnet ruby go rustc php perl R elixir; do
     command -v $cmd >/dev/null 2>&1 && echo "OK: $cmd" || echo "MISSING: $cmd"
   done'
```

### 2. Pick the runtimes to install

Per ADR-0029 the sandbox should ship the **minimum** runtimes needed for the project's
expected `ctx_execute` workload. Each runtime adds image size (Python +50 MB,
.NET +130 MB, Go +90 MB) and attack surface.

Heuristic:

| Project profile | Runtimes |
|---|---|
| Pure data/text analysis | `javascript`, `shell`, `python` |
| .NET-heavy debug (e.g. ECommerceApp) | `javascript`, `shell`, `csharp` (via dotnet-script) |
| Multi-stack monorepo | add `python` + `go` |
| Frontend-only | the default 2 are enough |

### 3. Edit `Dockerfile-context-mode`

The Dockerfile pattern is: install into `/app/runtimes/<lang>/` and add to `PATH`. This
mirrors the shipped layout for `node` (which is at `/app/runtimes/node/bin/node`).

Example — add Python 3.12:

```Dockerfile
# --- Python runtime ---
RUN apt-get update && apt-get install -y --no-install-recommends \
      python3.12 python3-pip \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/runtimes/python && ln -s /usr/bin/python3.12 /app/runtimes/python/python
ENV PATH="/app/runtimes/python:${PATH}"
```

Example — add C# via `dotnet-script`:

```Dockerfile
# --- .NET 8 + dotnet-script ---
RUN curl -sSL https://dot.net/v1/dotnet-install.sh \
    | bash /dev/stdin --channel 8.0 --install-dir /app/runtimes/dotnet
ENV PATH="/app/runtimes/dotnet:${PATH}"
ENV DOTNET_ROOT=/app/runtimes/dotnet
RUN /app/runtimes/dotnet/dotnet tool install -g dotnet-script --tool-path /app/runtimes/dotnet/tools
ENV PATH="/app/runtimes/dotnet/tools:${PATH}"
```

Example — add Go 1.22:

```Dockerfile
# --- Go ---
RUN curl -sSL https://go.dev/dl/go1.22.linux-amd64.tar.gz \
    | tar -C /app/runtimes -xz \
    && mv /app/runtimes/go /app/runtimes/go-1.22
ENV PATH="/app/runtimes/go-1.22/bin:${PATH}"
```

### 4. Update the AdGuard allowlist (if downloading at build time)

Each `curl` from a new domain (e.g. `dot.net`, `go.dev`) MUST be added to the AdGuard
allowlist before the image rebuild, otherwise the build itself fails inside a
network-restricted CI. See
[.github/skills/ctx-bootstrap-network/SKILL.md](../ctx-bootstrap-network/SKILL.md).

If the build runs OUTSIDE the firewalled network (typical: local dev box), this is not
needed — only matters in CI.

### 5. Rebuild the image

```sh
docker compose build --no-cache context-mode
docker compose up -d --force-recreate context-mode
```

`--no-cache` ensures the apt-get / curl steps actually run; Docker will otherwise
re-use a stale layer if the Dockerfile didn't change near the top.

### 6. Verify the new runtime

```
ctx_doctor()
```

Expected: `Runtimes: 3/11` (or higher), with Python row now showing OK.

Smoke test:

```
ctx_execute("python", "print(2+2)")
# expected output: 4
```

If `ctx_execute("python", ...)` still returns "Python not available", the `PATH` env
var didn't propagate. Check with:

```sh
docker exec -i <project>-context-mode sh -c 'echo $PATH; which python'
```

The shipped sandbox sources `PATH` from `/etc/profile.d/`. If your install puts the
runtime in a non-PATH location, add a `*.sh` shim there.

---

## Common mistakes

- **Assuming the schema enum means the runtime is shipped.** It doesn't. The enum is
  what context-mode's tool definition advertises; the actual installed runtimes are a
  subset. Always check `ctx_doctor` before depending on a non-default runtime.
- **`ctx_execute("python", "...")` returns "not available" → treating it as a 404.**
  It's a runtime-missing error from inside the sandbox. The Python install side of
  this skill is the fix; do NOT switch tools or assume context-mode is broken.
- **Installing into `/usr/local/bin/` instead of `/app/runtimes/`.** Works for the
  current image but breaks the layout convention. The next agent running this skill
  will look in `/app/runtimes/` and conclude the runtime is missing.
- **Installing at runtime via `ctx_execute("shell", "apt-get install …")`.** The
  sandbox is read-only at runtime (ADR-0029); apt-get fails. Even if it succeeded, the
  install would be lost on container restart. Always go through the Dockerfile.
- **Forgetting `--no-cache` on `docker compose build`.** Docker reuses a stale layer
  if the Dockerfile diff doesn't touch the lines around the install. Symptom:
  `ctx_doctor` shows the runtime as missing even after a "successful" rebuild.
- **Adding a new runtime without considering image size or attack surface.** Each
  language adds 50–150 MB and a new toolchain to audit. Don't reflexively install all
  11 — install only what the project will actually `ctx_execute`.

---

## Worked example: enable Python in a project that needs `pandas` for `ctx_execute_file`

1. `ctx_doctor()` → `Runtimes: 2/11`, Python missing.
2. Decide which Python — 3.12 (current stable).
3. Edit `Dockerfile-context-mode` — append the Python block from step 3 above.
4. `docker compose build --no-cache context-mode`.
5. `docker compose up -d --force-recreate context-mode`.
6. `ctx_doctor()` → `Runtimes: 3/11`, Python row OK.
7. Smoke: `ctx_execute("python", "import sys; print(sys.version)")` → `3.12.x`.

If `pandas` is needed inside `ctx_execute`, add a `RUN pip install --no-cache-dir pandas`
line right after the Python install in the Dockerfile. Each pip package is part of the
image, not installed at query time.

---

## Common runtime install snippets (copy-paste reference)

| Language | Install block (Debian-based base image) |
|---|---|
| Python 3.12 | `apt-get install -y python3.12 python3-pip` + symlink to `/app/runtimes/python` |
| Ruby 3.3 | `apt-get install -y ruby-full` |
| Go 1.22 | `curl https://go.dev/dl/go1.22.linux-amd64.tar.gz \| tar -xz -C /app/runtimes` |
| Rust stable | `curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs \| sh -s -- -y --no-modify-path --default-toolchain stable` |
| PHP 8.3 | `apt-get install -y php8.3-cli` |
| Perl 5 | `apt-get install -y perl` (usually present already) |
| R 4 | `apt-get install -y r-base-core` |
| Elixir 1.16 | `apt-get install -y elixir` (Debian backports) |
| .NET 8 + dotnet-script | `dotnet-install.sh --channel 8.0` + `dotnet tool install -g dotnet-script` |
| TypeScript | already covered by `javascript` runtime (Node has ts-node) — `ctx_execute("typescript", ...)` works if you `npm i -g ts-node` |

---

## Related skills / docs

- [.github/skills/ctx-bootstrap-network/SKILL.md](../ctx-bootstrap-network/SKILL.md) — allow build-time download domains (D1)
- [.github/skills/ctx-bootstrap-storage/SKILL.md](../ctx-bootstrap-storage/SKILL.md) — Qdrant + SQLite provisioning (D2)
- [.github/skills/ctx-doctor-playbook/SKILL.md](../ctx-doctor-playbook/SKILL.md) — what to do when `ctx_doctor` is not green
- [.github/skills/ctx-sandbox-bootstrap-verify/SKILL.md](../ctx-sandbox-bootstrap-verify/SKILL.md) — 8-check smoke test post-bootstrap
- [.github/instructions/mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md) — the `ctx_doctor` runtime quirk note
- [docs/adr/0029/0029-context-mode-mcp-sandbox.md](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md) — sandbox immutability rule
- `Dockerfile-context-mode` — where install blocks go
