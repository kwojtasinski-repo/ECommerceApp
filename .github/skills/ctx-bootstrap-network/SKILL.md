---
name: ctx-bootstrap-network
description: >
  Define the AdGuard DNS allowlist for a NEW project's context-mode sandbox.
  Decide which domains are essential (Qdrant, embedder downloads, GitHub for ADR refs),
  which are forbidden (telemetry, unscoped package registries). Produces a project-scoped
  allowlist YAML and the reload procedure. Use BEFORE the first `docker compose up`
  on a fresh project that will run context-mode.
argument-hint: "<project-name> <domain-allowlist-yaml-path>"
---

# ctx-bootstrap-network — DNS allowlist for a new project's context-mode sandbox

ADR-0029 mandates that the context-mode sandbox reach the network ONLY through an AdGuard
Home resolver that NXDOMAINs every domain not on an explicit allowlist. This skill defines
the allowlist for a NEW project — what to allow, what to forbid, and how to reload AdGuard
without bouncing the sandbox container.

**Cross-platform note**: code blocks use POSIX `sh` for portability. Equivalent PowerShell
calls are shown only where syntax meaningfully differs.

---

## When to use

- Standing up context-mode for the first time on a NEW git repository.
- Adding a NEW external dependency (CDN, registry) that context-mode must reach.
- Audit reveals that `ctx_fetch_and_index` is hitting a domain not in the allowlist.

## When NOT to use

- The project already has a working AdGuard allowlist — use `.github/skills/setup-adguard-policy/SKILL.md` (E3) to extend it.
- The change is to ALLOW a single one-off URL — pass it directly to `ctx_fetch_and_index`
  with the explicit URL; do NOT widen the allowlist unless the domain is recurring.
- The project does not use context-mode (e.g. RAG-only setup) — DNS firewall is not needed.

---

## Required allowlist categories

Every new project's allowlist MUST cover these categories. Missing any one of them will
silently break a downstream tool with a confusing "DNS resolution failed" symptom.

| Category | Examples | Why |
|---|---|---|
| **Vector store** | `qdrant`, `<project>-qdrant.<domain>` | RAG read/write |
| **Embedder model CDN** | `huggingface.co`, `cdn-lfs.huggingface.co`, `objects.githubusercontent.com` | First-run model download |
| **Source code refs** | `github.com`, `raw.githubusercontent.com`, `api.github.com` | `ctx_fetch_and_index` of ADRs / READMEs |
| **NPM / runtime deps** | `registry.npmjs.org` (ONLY if installing inside sandbox) | Optional — see "Forbidden by default" below |
| **Project-specific APIs** | e.g. `api.nbp.pl` for ECommerceApp's currency feed | Domain-specific data sources |
| **Localhost resolver fallback** | `127.0.0.1`, `localhost` | Internal Docker DNS |

## Forbidden by default

These domains MUST stay blocked unless an explicit, documented business need exists:

- Telemetry sinks: `*.segment.io`, `*.amplitude.com`, `*.mixpanel.com`, `*.posthog.com`
- LLM provider direct endpoints: `api.openai.com`, `api.anthropic.com`, `api.cohere.com`
  (context-mode does NOT call LLMs directly — it returns text to the agent host)
- Unscoped CDNs: never allow `*.cloudfront.net`, `*.akamaiedge.net` as wildcards — list
  specific subdomains instead
- Container registries from inside the sandbox: `*.docker.io`, `ghcr.io` (the sandbox
  must not pull its own images at runtime)

---

## Steps

### 1. Inventory existing dependencies

Before writing the allowlist, scan the new project for every external host name that
will be hit at runtime. Use `ctx_execute("sh", ...)` to keep it sandboxed:

```sh
grep -rEho 'https?://[a-zA-Z0-9.-]+' . \
  --include='*.json' --include='*.yaml' --include='*.yml' \
  --include='*.md' --include='*.cs' --include='*.py' --include='*.ts' --include='*.js' \
  | sed -E 's|https?://([^/]+).*|\1|' \
  | sort -u
```

PowerShell equivalent (note: case-insensitive `-Pattern`, no `--include`):

```pwsh
Get-ChildItem -Recurse -Include *.json,*.yaml,*.yml,*.md,*.cs,*.py,*.ts,*.js |
  Select-String -Pattern 'https?://[a-zA-Z0-9.-]+' -AllMatches |
  ForEach-Object { $_.Matches.Value } |
  ForEach-Object { ([Uri]$_).Host } |
  Sort-Object -Unique
```

Cross-reference each host against the table above. Anything not in an allowed category
gets reviewed manually — DO NOT auto-allow.

### 2. Write the allowlist YAML

The canonical shape is `docker/adguard/<project>-allowlist.yaml`:

```yaml
# AdGuard allowlist — <project-name>
# Generated <date>. Reviewed by <person>.
# Anything not on this list is NXDOMAIN'd by the sandbox resolver.

allowlist:
  vector_store:
    - qdrant
    - <project>-qdrant
  embedder_cdn:
    - huggingface.co
    - cdn-lfs.huggingface.co
    - objects.githubusercontent.com
  source_refs:
    - github.com
    - raw.githubusercontent.com
    - api.github.com
  project_apis:
    - api.nbp.pl              # <-- replace per project
  localhost:
    - 127.0.0.1
    - localhost

# Explicitly forbidden — kept here as documentation; the resolver blocks
# everything not in `allowlist` by default. This block is informational.
forbidden_documented:
  - api.openai.com
  - api.anthropic.com
  - "*.segment.io"
```

### 3. Translate YAML to AdGuard filter rules

AdGuard's filter file uses `||domain^` syntax (block) or `@@||domain^` (allow). Generate
the filter:

```sh
yq -r '.allowlist[] | .[]' docker/adguard/<project>-allowlist.yaml \
  | sed 's|^|@@||domain://|; s|$|^|' \
  > docker/adguard/filters/<project>-allow.txt
```

Append a default-deny line at the end:

```text
||*^$important
```

### 4. Mount the filter into AdGuard

In `docker-compose.yaml`, the AdGuard service must bind-mount the filters directory:

```yaml
services:
  adguard:
    image: adguard/adguardhome:latest
    volumes:
      - ./docker/adguard/filters:/opt/adguardhome/filters:ro
    ports:
      - "53:53/udp"
```

### 5. Reload AdGuard

Filters are reloaded on container restart. The shipped workspace task is the
canonical command:

```sh
docker compose restart adguard
# or, per ECommerceApp convention:
pwsh -NoProfile -File ./scripts/adguard/domain-policy.ps1 reload
```

The `domain-policy.ps1` wrapper exists ONLY in repos that copied it from ECommerceApp.
For a brand-new repo, the bare `docker compose restart adguard` is enough — copy the
wrapper later if you want named subcommands.

### 6. Verify

From inside the sandbox (cross-platform):

```sh
docker exec -i <project>-context-mode sh -c 'nslookup qdrant; nslookup api.openai.com'
```

Expected: `qdrant` resolves to the Docker network IP; `api.openai.com` returns
`NXDOMAIN`. If both resolve, AdGuard is not being used as the resolver — check the
sandbox container's `dns:` setting in `docker-compose.yaml` points at AdGuard.

---

## Common mistakes

- **Forgetting CDN domains for embedder downloads.** Sentence-transformers fetches model
  weights from `cdn-lfs.huggingface.co` on first run. Missing it → first ingest hangs
  forever with no useful error.
- **Allowing `*` instead of specific subdomains.** AdGuard accepts wildcard rules but
  they defeat the purpose of an allowlist — a single compromised npm package can then
  exfiltrate to any subdomain of an allowed parent.
- **Not reloading AdGuard after editing the filter file.** AdGuard reads filters at
  startup. Edits are silent until restart. Symptom: filter changes have no effect even
  though the file on disk is correct.
- **Listing `huggingface.co` but not `cdn-lfs.huggingface.co`.** The HF API serves
  metadata from the bare domain but redirects model file downloads to the CDN.
  Allowing only the parent breaks downloads.
- **Putting the allowlist YAML in `.github/` instead of `docker/adguard/`.** The
  `.github/` tree is for Copilot configuration; the AdGuard filter is operational
  infrastructure and lives alongside its Docker mount source.

---

## Worked example: adding "MyOtherProject" with HF embedder

1. Discovered hosts after step 1: `qdrant`, `myproject-qdrant`, `huggingface.co`,
   `cdn-lfs.huggingface.co`, `github.com`, `api.github.com`, `api.myproject-data.com`.
2. YAML:

   ```yaml
   allowlist:
     vector_store: [qdrant, myproject-qdrant]
     embedder_cdn: [huggingface.co, cdn-lfs.huggingface.co, objects.githubusercontent.com]
     source_refs: [github.com, raw.githubusercontent.com, api.github.com]
     project_apis: [api.myproject-data.com]
     localhost: [127.0.0.1, localhost]
   ```

3. Generated filter file: 9 `@@` rules + `||*^$important`.
4. Restart AdGuard; `nslookup` confirms `cdn-lfs.huggingface.co` resolves and
   `api.openai.com` is NXDOMAIN.
5. First RAG ingest succeeds — embedder pulls model weights cleanly.

---

## Related skills / docs

- [.github/skills/setup-adguard-policy/SKILL.md](../setup-adguard-policy/SKILL.md) — full AdGuard install + lifecycle (E3)
- [.github/skills/ctx-bootstrap-storage/SKILL.md](../ctx-bootstrap-storage/SKILL.md) — provision Qdrant collection + FTS5 SQLite (D2)
- [.github/skills/ctx-bootstrap-runtimes/SKILL.md](../ctx-bootstrap-runtimes/SKILL.md) — verify sandbox runtimes (D3)
- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) — full context-mode bootstrap (E2)
- [docs/playbooks/context-mode-bootstrap.md](../../../docs/playbooks/context-mode-bootstrap.md) — end-to-end playbook (P1)
- [docs/adr/0029/0029-context-mode-mcp-sandbox.md](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md) — sandbox + egress policy ADR
- [docs/getting-started-context-mode.md](../../../docs/getting-started-context-mode.md) — operator guide for this repo
