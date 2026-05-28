# Playbook — RAG bootstrap for a new project

> **Audience**: an engineer (or agent) standing up the RAG retrieval pipeline on a
> brand-new repository for the first time. Walks through E1 + E4 + (optional) E5 in
> sequence with verification checkpoints.
>
> **Estimated time**: 45–75 minutes for a first-timer on a clean Docker host.
> **Result**: a working RAG stack — Python + .NET HTTP servers, Qdrant collection,
> ingested corpus, smoke-tested `query_docs`, MCP client registered.

---

## Pre-flight

- [ ] Docker Desktop / Compose v2 installed and running.
- [ ] Local ports 6333, 6334, 3001, 3002 free.
- [ ] Python 3.10+ on the host (for `ingest.py`) — `python --version` ≥ 3.10.
- [ ] At least 2 GB free disk for the embedder model + Qdrant index.
- [ ] An ECommerceApp checkout (or another reference RAG repo) reachable to copy
      templates from.
- [ ] The MCP client you'll use is installed.

---

## Stage 0 — Project skeleton

```sh
mkdir -p <project-root>/{tools/rag,tools/rag-dotnet,data/<project>/qdrant,.vscode,docs,.github/context}
cd <project-root>
git init
```

If the project already has `docs/` and `.github/context/`, leave them — RAG ingests
whatever's there.

---

## Stage 1 — Copy + tune templates (E1 steps 1–4)

### 1a. Copy from the reference repo

```sh
cp <ecommerceapp-checkout>/tools/rag/{rag-config.yaml,metadata-rules.yaml,multilingual-glossary.yaml,queries.yaml,ingest.py,server.py,query.py,compare_queries.py} tools/rag/
cp -r <ecommerceapp-checkout>/tools/rag-dotnet/. tools/rag-dotnet/
```

Drop `tools/rag-dotnet/` if the project is Python-only.

### 1b. Tune `rag-config.yaml`

Edit `tools/rag/rag-config.yaml`. Most fields are reasonable defaults; only the
following need per-project decisions:

| Field | Default | Change when |
|---|---|---|
| `embedder.model` | `sentence-transformers/all-MiniLM-L6-v2` | Multi-language project → use `intfloat/e5-large-v2` (1024-dim) |
| `embedder.dim` | `384` | Must match `model` — change BOTH together |
| `ranking.weights.*` | Inherited from ECommerceApp | Tune later via [`tune-rag-weights`](../../.github/skills/tune-rag-weights/SKILL.md), not now |
| `query.fetch_k` | 30 | Keep — affects re-rank pool size |
| `chunker.max_tokens` | 800 | Keep — covers ADR-sized content well |

### 1c. Audit `metadata-rules.yaml`

Step that NEW projects most often forget. Audit which folders exist:

```sh
find docs .github/context -type d | sort
```

For every directory not covered by an existing rule, add an entry. Example:

```yaml
rules:
  - match: "docs/api/**/*.md"
    doc_kind: api
    weight_multiplier: 1.2
  - match: "docs/design/**/*.md"
    doc_kind: design
    weight_multiplier: 1.0
```

Without this, every uncovered chunk gets `doc_kind=other` → topic filters never boost
it → queries return generic chunks.

### 1d. Trim glossary to project languages

```sh
cat tools/rag/multilingual-glossary.yaml
```

For an English-only project, replace with:

```yaml
glossary: {}
```

For projects with German/Polish UI, keep the relevant sections; remove the others.
Spurious glossary entries inject irrelevant tokens.

### 1e. Seed `queries.yaml`

A NEW project usually has 0 named queries. Don't ship empty —
[`generate-eval-questions`](../../.github/skills/generate-eval-questions/SKILL.md)
generates a starter set from the doc tree. For minimal coverage right now, add
5–10 queries against the project's most important ADRs / READMEs.

---

## Stage 2 — Docker stanza (E1 step 5)

Append to `docker-compose.yaml`:

```yaml
services:
  qdrant:
    image: qdrant/qdrant:v1.11.0
    container_name: <project>-qdrant
    ports: ["6333:6333", "6334:6334"]
    volumes: ["./data/<project>/qdrant:/qdrant/storage"]

  rag-python-http:
    profiles: ["rag-python-http"]
    container_name: <project>-rag-python-http
    build:
      context: .
      dockerfile: tools/rag/Dockerfile
    ports: ["3002:3002"]
    environment:
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs
    volumes:
      - ./tools/rag/rag-config.yaml:/app/rag-config.yaml:ro
      - ./tools/rag/metadata-rules.yaml:/app/metadata-rules.yaml:ro
      - ./tools/rag/multilingual-glossary.yaml:/app/multilingual-glossary.yaml:ro
    depends_on: [qdrant]

  rag-dotnet-http:
    profiles: ["rag-dotnet-http"]
    container_name: <project>-rag-dotnet-http
    build:
      context: .
      dockerfile: tools/rag-dotnet/Dockerfile
    ports: ["3001:3001"]
    environment:
      QDRANT_URL: http://qdrant:6333
      QDRANT_COLLECTION: <project>_docs_dotnet
    volumes:
      - ./tools/rag-dotnet/rag-config.yaml:/app/rag-config.yaml:ro
      - ./tools/rag-dotnet/metadata-rules.yaml:/app/metadata-rules.yaml:ro
      - ./tools/rag-dotnet/multilingual-glossary.yaml:/app/multilingual-glossary.yaml:ro
    depends_on: [qdrant]
```

**Checkpoint Stage 2**: `docker compose up -d qdrant` succeeds; `curl
http://localhost:6333/healthz` returns `healthz check passed`.

---

## Stage 3 — First ingest

### 3a. Python dependencies

```sh
python -m venv .venv
. .venv/bin/activate    # Windows PowerShell: .venv\Scripts\Activate.ps1
pip install -r tools/rag/requirements.txt
```

First-run delay: pip downloads sentence-transformers (~200 MB).

### 3b. Run ingest

```sh
python tools/rag/ingest.py
```

What happens:
1. Reads `rag-config.yaml`, `metadata-rules.yaml`, `multilingual-glossary.yaml`.
2. Walks the corpus per `metadata-rules.yaml` globs.
3. Chunks each file per `chunker.max_tokens`.
4. Embeds each chunk via the sentence-transformers model.
5. Creates the Qdrant collection at `embedder.dim` if missing.
6. Inserts chunks with payload (`doc_kind`, `breadcrumb`, `start_line`, `end_line`).
7. Writes `.rag/chunk-manifest.json` for incremental future runs.

Expected output:
```
Indexed: 142 chunks. Skipped (unchanged): 0. Errors: 0.
Collection: <project>_docs (dim=384, points=142).
```

If errors > 0, the most common cause is YAML parse error in
`metadata-rules.yaml` — re-check indentation.

### 3c. Mirror to .NET collection (if dual-server)

```sh
dotnet run --project tools/rag-dotnet/src/RagTools.Ingest -- --force-full
```

Creates `<project>_docs_dotnet` from the .NET-side config. The .NET ingest pipeline
is functionally equivalent but writes to its own collection — keeping them separate
prevents write contention and lets you A/B compare retrieval quality.

**Checkpoint Stage 3**: `curl -s http://localhost:6333/collections/<project>_docs |
jq '.result.points_count'` returns > 0.

---

## Stage 4 — Start HTTP servers + smoke

```sh
docker compose --profile rag-python-http up -d --build rag-python-http
docker compose --profile rag-dotnet-http up -d --build rag-dotnet-http
```

Wait for the HTTP ports to open:

```sh
for port in 3001 3002; do
  for i in $(seq 1 20); do
    nc -z localhost $port && echo "Port $port up" && break
    sleep 1
  done
done
```

PowerShell:

```pwsh
3001,3002 | ForEach-Object {
  for ($i=0; $i -lt 20; $i++) {
    if (Test-NetConnection localhost -Port $_ -InformationLevel Quiet -WarningAction SilentlyContinue) {
      "Port $_ up"; break
    }
    Start-Sleep 1
  }
}
```

Smoke test query:

```sh
curl -s -X POST http://localhost:3002/mcp \
  -H 'Content-Type: application/json' \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call",
       "params":{"name":"query_docs",
                 "arguments":{"question":"what is this project","top_k":3}}}' \
  | jq '.result.content[0].text' | head -50
```

Expected: top chunk from your project README or top-level doc with
`(path, line range)` breadcrumb.

If empty: check `docker logs rag-python-http` for `doc_kind=other` warnings —
that's the `metadata-rules.yaml` gap.

---

## Stage 5 — MCP client registration (E4)

`.vscode/mcp.json`:

```jsonc
{
  "servers": {
    "<project>-rag-python":  { "type": "http", "url": "http://localhost:3002" },
    "<project>-rag-dotnet":  { "type": "http", "url": "http://localhost:3001" }
  }
}
```

For Copilot Web (`.github/copilot/mcp.json`), see
[.github/skills/setup-mcp-clients/SKILL.md](../../.github/skills/setup-mcp-clients/SKILL.md) step 3 — requires
a public HTTPS endpoint.

Reload MCP servers. In agent chat:

```
list_adrs()
query_docs("how does the project deal with X")
```

**Checkpoint Stage 5**: agent receives a list of ADR IDs (if any exist) and a
non-empty query result.

---

## Stage 6 — Coverage + parity audit (optional but recommended)

### 6a. Coverage gap

```sh
python tools/rag/coverage_dump.py > /tmp/covered.txt
find docs .github/context -name '*.md' | sort > /tmp/corpus.txt
comm -23 /tmp/corpus.txt /tmp/covered.txt | wc -l
```

If gap > 5 high-priority files, run
[.github/skills/rag-eval-coverage/SKILL.md](../../.github/skills/rag-eval-coverage/SKILL.md)
to draft queries.

### 6b. Parity audit (dual-server only)

```sh
python tools/rag/compare_queries.py
```

Writes `docs/reports/rag-parity-audit-<date>.md`. Compare top-1 match counts —
parity > 60% is a healthy starting point. Low parity (< 40%) usually means one
server has a glossary or ranking weight the other doesn't; see
[.github/skills/rag-multilang-test/SKILL.md](../../.github/skills/rag-multilang-test/SKILL.md)
or [.github/skills/tune-rag-weights/SKILL.md](../../.github/skills/tune-rag-weights/SKILL.md).

---

## Stage 7 — Optional: auto-cache hook (E5)

Only if context-mode is also up (see [context-mode-bootstrap.md](context-mode-bootstrap.md)).
The L3 hook removes the need to manually cache RAG results.

```sh
mkdir -p .github/hooks
cp <ecommerceapp-checkout>/.github/hooks/{auto-cache.mjs,auto-cache.probes.mjs,posttooluse-chain.mjs,posttooluse-chain.sh,context-mode.json} .github/hooks/
chmod +x .github/hooks/posttooluse-chain.sh
node .github/hooks/auto-cache.probes.mjs
```

Smoke (in chat):

```
query_docs("anything")
ctx_search(["anything"], "rag-auto-")
```

Expected: the same chunk RAG returned, now in the FTS5 store.

---

## Troubleshooting flowchart

```text
ingest.py: 0 chunks indexed?
├── No matching glob in metadata-rules.yaml → audit + add rules
├── --skip-hash with stale manifest → delete .rag/chunk-manifest.json, retry
└── Empty docs/ folder → ingest needs content; check the corpus

ingest.py: vector dimension mismatch?
└── embedder.model changed but embedder.dim didn't → fix both; run --force-full

query_docs returns empty?
├── doc_kind=other for everything → metadata-rules.yaml gap
├── Glossary expansion injecting wrong tokens → trim glossary to project languages
└── Ranking weights skew → tune-rag-weights skill

HTTP server won't start?
├── Port already in use → lsof -i :3001 (POSIX) / Get-NetTCPConnection -LocalPort 3001 (PS)
├── Image build cached with old config → docker compose build --no-cache rag-*
└── Qdrant not reachable from container → use http://qdrant:6333 (not localhost)

Polish/German query returns wrong doc?
└── See rag-multilang-test skill — expand or trim glossary
```

---

## What to do next

- Run [context-mode-bootstrap.md](context-mode-bootstrap.md) if context-mode isn't
  up yet — auto-cache hook needs both halves.
- Use [`generate-eval-questions`](../../.github/skills/generate-eval-questions/SKILL.md)
  to grow `queries.yaml` past the initial seed.
- Schedule periodic re-ingest: `python tools/rag/ingest.py` after each docs PR.

---

## Reference

- [ADR-0027 — RAG pipeline architecture](../adr/0027/0027-rag-pipeline.md)
- [ADR-0028 — Remote multitenant RAG ingest](../adr/0028/0028-remote-multitenant-rag-ingest.md)
- [ADR-0028 Amendment 4 — Per-collection config gap](../adr/0028/amendments/0028-004-per-collection-config-gap.md) (KNOWN GAP)
- [docs/rag/rag-architecture.md](../rag/rag-architecture.md)
- [.github/skills/setup-rag-new-project/SKILL.md](../../.github/skills/setup-rag-new-project/SKILL.md)
- [.github/skills/setup-mcp-clients/SKILL.md](../../.github/skills/setup-mcp-clients/SKILL.md)
- [.github/skills/setup-auto-cache-hook/SKILL.md](../../.github/skills/setup-auto-cache-hook/SKILL.md)
- [.github/skills/diagnose-rag/SKILL.md](../../.github/skills/diagnose-rag/SKILL.md) — diagnostics when bootstrap goes sideways
