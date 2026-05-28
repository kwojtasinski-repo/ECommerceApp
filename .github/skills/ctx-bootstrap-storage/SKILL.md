---
name: ctx-bootstrap-storage
description: >
  Provision per-project storage for a NEW project's context-mode + RAG stack: a dedicated
  Qdrant collection (for RAG vectors) AND a host-mounted SQLite FTS5 file (for
  context-mode's `ctx_index`/`ctx_search` knowledge base). Covers collection naming,
  volume mounts, env vars, and the per-collection config gap from ADR-0028 Amendment 004.
argument-hint: "<project-name> [--collection-name=<name>] [--sqlite-path=<path>]"
---

# ctx-bootstrap-storage — per-project storage for RAG + context-mode

A new project needs TWO storage surfaces:

1. **Qdrant collection** — vector store for RAG chunks. One per project (ADR-0028 §multi-tenancy).
2. **FTS5 SQLite file** — context-mode's persistent knowledge base for `ctx_index` /
   `ctx_search` (host-mounted so data survives container recreation).

This skill provisions both. It is normally invoked right after
`.github/skills/ctx-bootstrap-network/SKILL.md` (D1) and before
`.github/skills/ctx-bootstrap-runtimes/SKILL.md` (D3).

> **KNOWN GAP — ADR-0028 Amendment 004**: per-collection config persistence is NOT yet
> wired into the .NET or Python servers (Phase 3 in
> `docs/roadmap/rag-remote-multitenant.md`, P3-1..P3-8 not started). Until Phase 3 ships,
> every project on the same RAG server shares the mounted `rag-config.yaml` weights and
> the mounted `multilingual-glossary.yaml`. Naming collections per project (this skill)
> is still correct — it just doesn't yet give true per-project config isolation. Document
> this on the project README so the next reader is not surprised.

---

## When to use

- First-time bootstrap of a NEW project that will use RAG and/or context-mode.
- Splitting an existing multi-project shared collection into per-project collections.
- Restoring storage after a `docker volume prune` accident.

## When NOT to use

- Adding a NEW document to an EXISTING collection — just run `ingest.py`; no provisioning
  needed.
- Renaming an existing collection — Qdrant has no rename; you must dump + re-create
  (separate skill: `.github/skills/rag-collection-rebuild/SKILL.md`).
- Switching embedder dimension — see `rag-collection-rebuild` (drops and rebuilds).

---

## Steps

### 1. Pick a collection name

Convention from ADR-0028: `<project_slug>_docs` (Python server) and
`<project_slug>_docs_dotnet` (.NET server). Lowercase, snake_case, no hyphens (Qdrant
collection names are case-sensitive and conventionally avoid hyphens for HTTP-path
compatibility).

Examples:

- ECommerceApp → `ecommerceapp_docs`, `ecommerceapp_docs_dotnet`
- AcmeApp → `acmeapp_docs`, `acmeapp_docs_dotnet`
- Game prototype → `wraithwood_docs`, `wraithwood_docs_dotnet`

**Never** re-use a collection name across projects — even if the projects are
"obviously different", a shared name silently mixes their chunks. Symptom: RAG returns
chunks from the wrong project, with no error.

### 2. Create the collection (or let `ingest.py` create it)

The Python and .NET ingest scripts create the collection on first run if it doesn't
exist. The dimension defaults to `embedder.dim` from `rag-config.yaml` (384 for
MiniLM, 768 for mpnet).

Explicit creation (rarely needed; useful when pre-creating with custom HNSW params):

```sh
curl -X PUT "http://localhost:6333/collections/<project>_docs" \
  -H 'Content-Type: application/json' \
  -d '{
    "vectors": { "size": 384, "distance": "Cosine" },
    "hnsw_config": { "m": 16, "ef_construct": 200 }
  }'
```

Verify:

```sh
curl -s http://localhost:6333/collections/<project>_docs | jq '.result.status'
# expected: "green"
```

### 3. Provision the SQLite FTS5 file for context-mode

context-mode persists `ctx_index` content into a host-mounted SQLite file with the
FTS5 + trigram extension. The container expects it at `${CONTEXT_MODE_DB_PATH}`
(default `/data/ctx.db`).

Create the host directory and let context-mode initialise the schema on first run:

```sh
mkdir -p ./data/<project>/context-mode
# Touch the file so the bind-mount target exists (Docker fails if missing):
touch ./data/<project>/context-mode/ctx.db
chmod 644 ./data/<project>/context-mode/ctx.db
```

PowerShell:

```pwsh
New-Item -Path ./data/<project>/context-mode -ItemType Directory -Force | Out-Null
New-Item -Path ./data/<project>/context-mode/ctx.db -ItemType File -Force | Out-Null
```

### 4. Wire the mounts into `docker-compose.yaml`

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.11.0
    volumes:
      - ./data/<project>/qdrant:/qdrant/storage
    ports:
      - "6333:6333"
      - "6334:6334"

  context-mode:
    image: <project>-context-mode:latest
    build:
      context: .
      dockerfile: Dockerfile-context-mode
    environment:
      CONTEXT_MODE_DB_PATH: /data/ctx.db
      CONTEXT_MODE_WORKSPACE: /workspace
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs
    volumes:
      - ./data/<project>/context-mode/ctx.db:/data/ctx.db
      - .:/workspace:ro
    depends_on:
      - qdrant
      - adguard
    dns:
      - 127.0.0.1
    network_mode: "service:adguard"
```

**Notes on the mounts**:

- `./data/<project>/qdrant:/qdrant/storage` — Qdrant's persistent data. If you skip
  the bind-mount, the collection is wiped on `docker compose down -v`.
- `./data/<project>/context-mode/ctx.db:/data/ctx.db` — bind-mount the SQLite file
  itself (not the parent directory). Bind-mounting the parent works too but makes
  rotating the DB harder.
- `.:/workspace:ro` — the project tree mounted **read-only** so `ctx_execute_file`
  can read repo files without giving the sandbox write access (ADR-0029 conformance).
- `network_mode: "service:adguard"` — forces all sandbox traffic through AdGuard's
  network namespace.

### 5. Set the workspace env var

context-mode's `ctx_execute_file` resolves relative paths against
`$CONTEXT_MODE_WORKSPACE`. Setting it explicitly avoids the "files not found" trap
documented in mcp-routing.instructions.md:

```yaml
    environment:
      CONTEXT_MODE_WORKSPACE: /workspace
```

In multi-workspace setups, override per service.

### 6. Verify

After `docker compose up -d qdrant context-mode`:

```sh
# Qdrant reachable from sandbox
docker exec -i <project>-context-mode sh -c 'curl -s http://qdrant:6333/healthz'
# expected: "healthz check passed"

# SQLite writable from sandbox
docker exec -i <project>-context-mode sh -c \
  'sqlite3 /data/ctx.db "CREATE TABLE IF NOT EXISTS smoke(x); INSERT INTO smoke VALUES (1); SELECT * FROM smoke;"'
# expected: 1

# Persistence: restart and check the row survives
docker compose restart context-mode
docker exec -i <project>-context-mode sh -c 'sqlite3 /data/ctx.db "SELECT * FROM smoke;"'
# expected: 1 (was preserved across restart)
```

### 7. First ingest

Run the ingest script once to create the Qdrant collection at the right dimension:

```sh
python tools/rag/ingest.py
```

For .NET-only setups, use:

```sh
dotnet run --project tools/rag-dotnet/src/RagTools.Ingest -- --force-full
```

Check the collection point count:

```sh
curl -s http://localhost:6333/collections/<project>_docs | jq '.result.points_count'
```

---

## Common mistakes

- **Re-using a collection name across projects.** Catastrophic — chunks mix silently
  and RAG starts returning the wrong project's docs. ADR-0028 §multi-tenancy explicitly
  forbids this.
- **Not bind-mounting the SQLite file.** On `docker compose down -v` the volume is
  wiped and every cached `ctx_index` entry is lost. Bind-mount the host path.
- **Bind-mounting `.:/workspace` writable.** ADR-0029 mandates read-only. A writable
  mount lets a malicious tool prompt-inject the agent into modifying source code via
  the sandbox.
- **Forgetting `CONTEXT_MODE_WORKSPACE`.** `ctx_execute_file("./README.md")` returns
  silent empty results because `./` resolves relative to the sandbox cwd, not the
  workspace root.
- **Bind-mounting the parent directory instead of the SQLite file.** Works, but
  SQLite WAL/SHM sidecar files end up on the host filesystem, which makes backups
  noisier. Mount the file directly.
- **Forgetting `depends_on: [qdrant, adguard]` on context-mode.** First-up race
  produces a sandbox container that can't reach either dependency; tool calls return
  cryptic "DNS resolution failed" because AdGuard isn't routing yet.

---

## Worked example: provisioning `acmeapp_docs` + `/data/acmeapp/ctx.db`

```sh
mkdir -p ./data/acmeapp/{qdrant,context-mode}
touch ./data/acmeapp/context-mode/ctx.db
```

Compose snippet (the relevant deltas only):

```yaml
  qdrant:
    volumes: [ "./data/acmeapp/qdrant:/qdrant/storage" ]
  context-mode:
    environment:
      CONTEXT_MODE_DB_PATH: /data/ctx.db
      QDRANT_COLLECTION: acmeapp_docs
    volumes:
      - ./data/acmeapp/context-mode/ctx.db:/data/ctx.db
      - .:/workspace:ro
```

Bring up; run `python tools/rag/ingest.py`; verify:

```sh
curl -s http://localhost:6333/collections/acmeapp_docs | jq '.result.points_count'
# expected: > 0 (number of chunks indexed)
```

---

## Related skills / docs

- [.github/skills/ctx-bootstrap-network/SKILL.md](../ctx-bootstrap-network/SKILL.md) — DNS allowlist (D1, run first)
- [.github/skills/ctx-bootstrap-runtimes/SKILL.md](../ctx-bootstrap-runtimes/SKILL.md) — sandbox runtimes (D3, run next)
- [.github/skills/setup-rag-new-project/SKILL.md](../setup-rag-new-project/SKILL.md) — bring up RAG (E1)
- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) — full context-mode (E2)
- [.github/skills/rag-collection-rebuild/SKILL.md](../rag-collection-rebuild/SKILL.md) — drop & rebuild a collection
- [docs/adr/0028/0028-remote-multitenant-rag-ingest.md](../../../docs/adr/0028/0028-remote-multitenant-rag-ingest.md)
- [docs/adr/0028/amendments/0028-004-per-collection-config-gap.md](../../../docs/adr/0028/amendments/0028-004-per-collection-config-gap.md) — gap acknowledgement
- [docs/roadmap/rag-remote-multitenant.md](../../../docs/roadmap/rag-remote-multitenant.md) — Phase 3 P3-1..P3-8 (the fix plan)
