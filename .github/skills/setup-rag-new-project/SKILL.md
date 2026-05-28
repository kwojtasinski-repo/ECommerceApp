---
name: setup-rag-new-project
description: >
  Bring up RAG (Qdrant + Python HTTP server + .NET HTTP server) for a brand-new
  project. Covers `rag-config.yaml` template, `metadata-rules.yaml` template, embedder
  model choice (default MiniLM 384-dim), `docker-compose.yaml` stanza, first ingest,
  smoke test. Assumes a fresh git repository with no prior RAG setup.
argument-hint: "<project-name> [--embedder=<model>] [--language=python|dotnet|both]"
---

# setup-rag-new-project — bring up RAG for a NEW project

End-to-end bootstrap for a brand-new project that has no RAG setup yet. Produces a
working RAG pipeline: documents under `docs/` and `.github/context/` get embedded into
Qdrant, queryable via `query_docs` / `read_docs` over MCP.

> **KNOWN GAP — ADR-0028 Amendment 004**: per-collection config persistence is NOT yet
> implemented (Phase 3 P3-1..P3-8 in `docs/roadmap/rag-remote-multitenant.md`). Until
> shipped, every project on the same RAG server shares the mounted `rag-config.yaml`
> weights and `multilingual-glossary.yaml`. A NEW project running on its OWN Docker
> Compose stack (the default path of this skill) is unaffected — the limitation only
> bites if you try to host multiple projects on a single shared RAG server. Track the
> fix at [Phase 3 in the roadmap](../../../docs/roadmap/rag-remote-multitenant.md).

---

## When to use

- Brand-new project, no `tools/rag/` directory, no `docker-compose.yaml` RAG service.
- Existing project being split out of a monorepo into its own RAG stack.
- Migrating from a hand-rolled vector store to the standard Qdrant + dual-server setup.

## When NOT to use

- Existing project, just want to re-ingest after content changes — run
  `python tools/rag/ingest.py` directly; no setup needed.
- Existing project, want to add a new file type to ingest — use
  `.github/skills/generate-rag-rules/SKILL.md` to edit `metadata-rules.yaml`.
- Project uses ONLY context-mode (no RAG) — skip this skill;
  `.github/skills/setup-context-mode-new-project/SKILL.md` (E2) is enough.

---

## Steps

### 1. Copy the canonical templates

This repo's `tools/rag/` is the reference implementation. Copy:

```sh
mkdir -p tools/rag tools/rag-dotnet
cp -r <ecommerceapp-checkout>/tools/rag/rag-config.yaml tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/metadata-rules.yaml tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/multilingual-glossary.yaml tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/queries.yaml tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/ingest.py tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/server.py tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/query.py tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag/compare_queries.py tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag-dotnet/. tools/rag-dotnet/
```

(For projects that won't host their own copy of the .NET tooling, skip the
`tools/rag-dotnet/` directory and use `--language=python` from now on.)

### 2. Tune `rag-config.yaml`

Open `tools/rag/rag-config.yaml`. The two project-specific fields are
`embedder.model` and `ranking.weights`. Leave everything else at defaults.

```yaml
embedder:
  model: sentence-transformers/all-MiniLM-L6-v2   # 384-dim, fast, default
  dim: 384
# Alternatives:
#   sentence-transformers/all-mpnet-base-v2       # 768-dim, ~3x slower, +10% MRR
#   intfloat/e5-large-v2                          # 1024-dim, multilingual
```

The shipped `ranking.weights` block is reasonable for any docs-heavy repository. Tune
only if a parity audit shows specific files ranking too low — use
`.github/skills/tune-rag-weights/SKILL.md`.

### 3. Tune `metadata-rules.yaml`

This file maps file paths → `doc_kind` (used by topic filters at query time). Default
shipped rules cover `docs/adr/`, `docs/architecture/`, `docs/roadmap/`,
`.github/context/`, etc. For a NEW project, audit your folder layout:

```sh
find docs .github -type f -name '*.md' | sed -E 's|/[^/]+\.md$||' | sort -u
```

For every directory NOT covered by an existing rule, add an entry:

```yaml
rules:
  - match: "docs/adr/**/*.md"
    doc_kind: adr
    weight_multiplier: 1.5
  - match: "docs/architecture/**/*.md"
    doc_kind: architecture
    weight_multiplier: 1.3
  # ... add per-project entries below ...
  - match: "docs/<project-area>/**/*.md"
    doc_kind: <project-area>
    weight_multiplier: 1.0
```

Skipping this step → every uncovered chunk gets `doc_kind=other` and zero topic boost
→ specific queries return generic chunks.

### 4. Pick `multilingual-glossary.yaml` scope

Default ECommerceApp glossary has Polish + German terms (the project's user languages).
If your project doesn't use those, **delete the entries** rather than translate —
spurious expansion injects irrelevant tokens.

If your project's primary language is English-only, the simplest thing is:

```yaml
glossary: {}
```

`expand-rag-glossary` skill handles adding new entries later.

### 5. Write the `docker-compose.yaml` stanza

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.11.0
    ports:
      - "6333:6333"
      - "6334:6334"
    volumes:
      - ./data/<project>/qdrant:/qdrant/storage

  rag-python-http:
    profiles: ["rag-python-http"]
    build:
      context: .
      dockerfile: tools/rag/Dockerfile
    ports:
      - "3002:3002"
    environment:
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs
      RAG_CONFIG_PATH: /app/rag-config.yaml
      METADATA_RULES_PATH: /app/metadata-rules.yaml
      GLOSSARY_PATH: /app/multilingual-glossary.yaml
    volumes:
      - ./tools/rag/rag-config.yaml:/app/rag-config.yaml:ro
      - ./tools/rag/metadata-rules.yaml:/app/metadata-rules.yaml:ro
      - ./tools/rag/multilingual-glossary.yaml:/app/multilingual-glossary.yaml:ro
    depends_on: [qdrant]

  rag-dotnet-http:
    profiles: ["rag-dotnet-http"]
    build:
      context: .
      dockerfile: tools/rag-dotnet/Dockerfile
    ports:
      - "3001:3001"
    environment:
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs_dotnet
    volumes:
      - ./tools/rag-dotnet/rag-config.yaml:/app/rag-config.yaml:ro
      - ./tools/rag-dotnet/metadata-rules.yaml:/app/metadata-rules.yaml:ro
      - ./tools/rag-dotnet/multilingual-glossary.yaml:/app/multilingual-glossary.yaml:ro
    depends_on: [qdrant]
```

Both servers can run side-by-side on different ports + different collections; this is
the production pattern in ECommerceApp.

### 6. First ingest

```sh
docker compose up -d qdrant
python tools/rag/ingest.py
```

The script:
- creates the collection at `embedder.dim` if missing
- chunks every file matching `metadata-rules.yaml`
- embeds and inserts with payload (`doc_kind`, `breadcrumb`, `start_line`, `end_line`)
- writes `.rag/chunk-manifest.json` for incremental future runs

Expected output: `Indexed: <N> chunks. Skipped (unchanged): <M>. Errors: 0`.

### 7. Start the HTTP servers

```sh
docker compose --profile rag-python-http up -d --build rag-python-http
docker compose --profile rag-dotnet-http up -d --build rag-dotnet-http
```

For .NET-only projects: drop the python service. For Python-only: drop the dotnet
service.

### 8. Smoke test

```sh
curl -s -X POST http://localhost:3002/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call",
       "params":{"name":"query_docs","arguments":{"question":"what is this project about"}}}' \
  | jq '.result.content[0].text' | head -50
```

Expected: top-ranked chunk from your project README or top-level docs, with
`(path, line range)` breadcrumbs.

If the response is empty: `metadata-rules.yaml` likely missed a folder. Check
`docker logs rag-python-http` for `doc_kind=other` warnings.

### 9. Register the MCP server with your client

See [.github/skills/setup-mcp-clients/SKILL.md](../setup-mcp-clients/SKILL.md) (E4) for
VS Code / GitHub Copilot Web / Visual Studio shapes.

---

## Common mistakes

- **Skipping `metadata-rules.yaml` audit.** Default rules cover ECommerceApp's folder
  layout. A new project with `docs/design/`, `docs/api/`, etc. gets every chunk tagged
  `doc_kind=other`. Topic filters then never boost those chunks. Symptom: queries that
  obviously target a design doc return generic chunks.
- **Pointing both servers at the same collection.** Python and .NET use slightly
  different chunking + metadata serialisation; mixed writes cause duplicate or stale
  chunks. Always use `_docs` (Python) and `_docs_dotnet` (.NET) as a pair, or stick to
  one language.
- **Forgetting `qdrant` service in `docker-compose.yaml`.** Both servers default to
  `http://qdrant:6333`. Missing the service → "connection refused" on first query.
- **Hard-coding `embedder.dim` independently of `embedder.model`.** Each
  sentence-transformers model has a fixed dimension; changing one without the other
  breaks Qdrant inserts ("vector dimension mismatch"). Always change both together
  and run `python tools/rag/ingest.py --force-full`.
- **Copying the ECommerceApp glossary verbatim.** Polish/German terms expand
  irrelevant tokens for an English-only project, dropping precision. Trim to your
  project's actual languages.
- **Skipping the smoke test in step 8.** A clean `docker compose up` does not mean RAG
  works — verify with an actual query.

---

## Worked example: bootstrapping "AcmeApp" (English-only, dual-server)

1. `mkdir -p tools/rag tools/rag-dotnet docs/{adr,api,design}` and copy templates.
2. Edit `metadata-rules.yaml`: add rules for `docs/api/**` (`doc_kind=api`) and
   `docs/design/**` (`doc_kind=design`).
3. Empty `multilingual-glossary.yaml` (English-only).
4. Append the `docker-compose.yaml` stanza from step 5.
5. `docker compose up -d qdrant && python tools/rag/ingest.py` → 312 chunks indexed.
6. Start both HTTP servers.
7. Smoke: `query_docs("what's the architecture")` → top chunk from
   `docs/architecture/README.md` (line 1–40). ✅
8. Register in `.vscode/mcp.json` (see E4).

---

## Related skills / docs

- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) — context-mode sandbox (E2)
- [.github/skills/setup-mcp-clients/SKILL.md](../setup-mcp-clients/SKILL.md) — client configuration (E4)
- [.github/skills/setup-auto-cache-hook/SKILL.md](../setup-auto-cache-hook/SKILL.md) — L3 cache wire-up (E5)
- [.github/skills/generate-rag-rules/SKILL.md](../generate-rag-rules/SKILL.md) — extending `metadata-rules.yaml`
- [.github/skills/generate-eval-questions/SKILL.md](../generate-eval-questions/SKILL.md) — building a `queries.yaml`
- [.github/skills/tune-rag-weights/SKILL.md](../tune-rag-weights/SKILL.md) — ranking weight tuning
- [.github/skills/expand-rag-glossary/SKILL.md](../expand-rag-glossary/SKILL.md) — adding glossary terms
- [docs/playbooks/rag-bootstrap.md](../../../docs/playbooks/rag-bootstrap.md) — long-form playbook (P2)
- [docs/rag/rag-architecture.md](../../../docs/rag/rag-architecture.md)
- [docs/adr/0027/0027-rag-pipeline.md](../../../docs/adr/0027/0027-rag-pipeline.md)
- [docs/adr/0028/0028-remote-multitenant-rag-ingest.md](../../../docs/adr/0028/0028-remote-multitenant-rag-ingest.md)
- [docs/adr/0028/amendments/0028-004-per-collection-config-gap.md](../../../docs/adr/0028/amendments/0028-004-per-collection-config-gap.md) — KNOWN GAP
