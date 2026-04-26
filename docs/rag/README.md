# RAG MVP — local docs retrieval

Local, offline retrieval over `docs/`. Backed by:

- **sentence-transformers** (`all-mpnet-base-v2`, 768 dims, EN) for embeddings
- **Qdrant** (in-memory by default) as the vector store
- **MCP server** exposing 3 tools to VS Code Copilot

> Synthesis is intentionally **disabled** in MVP — the tool returns retrieved chunks; Copilot synthesizes the answer.
> See `tools/rag/config.yaml` → `synthesis.mode` if you want to enable a local LLM later.

---

## TL;DR — first run (Windows / PowerShell)

```powershell
# 1. Create a venv (any Python 3.10+ works)
python -m venv tools\rag\.venv
tools\rag\.venv\Scripts\Activate.ps1

# 2. Install deps
pip install -r tools\rag\requirements.txt

# 3. Build the index (downloads the embedder once, ~80 MB)
python tools\rag\ingest.py

# 4. Try a query from the CLI
python tools\rag\query.py "How does the order placement saga handle compensation?"

# 5. Run the eval suite (recall@5 / recall@8 over 20 questions)
python tools\rag\eval\eval.py
```

After step 3 you'll have:

- `tools/rag/.cache/snapshot.qdrant` ← in-memory snapshot reload file
- `tools/rag/.cache/manifest.json` ← summary of last build (file count, chunk count, model, dim)

The MCP server (`tools/rag/mcp_server.py`) is started automatically by VS Code Copilot via [`.vscode/mcp.json`](../../.vscode/mcp.json) — no PATH setup needed, the entry points directly at the venv executable.

---

## What gets indexed

Source roots and exclusions are declared in [`tools/rag/config.yaml`](../../tools/rag/config.yaml):

| Included       | Excluded                                             |
| -------------- | ---------------------------------------------------- |
| `docs/**/*.md` | `docs/reports/**` (transient session reports)        |
|                | `.github/**` (Copilot config, not project knowledge) |
|                | `tools/**` (scripts, not knowledge)                  |
|                | source code (`*.cs`, `*.cshtml`, ...)                |

`agent-decisions.md` is **deliberately not indexed** — it is a temporal append-only log of agent corrections, not project knowledge.

## Chunking

Heading-aware split with size guard, overlap, and breadcrumb prepending. See `tools/rag/chunker.py`.

```
H1 (doc title)
  └ H2 (section)
      └ H3 (subsection)            ← chunk boundary by default
          paragraph 1
          paragraph 2              ← if section > 800 tokens, split here with 80-token overlap
```

Each chunk's `embed_text` starts with the breadcrumb so semantic similarity captures the section context, e.g.:

```
ADR-0014 — Sales/Orders BC > §3 Order aggregate > Status transitions

The OrderStatus enum allows the following transitions: ...
```

## Ranking weights

Weights multiply the cosine similarity score before final ranking. They live in `config.yaml → ranking.weights`. First matching glob wins.

| Weight | Path pattern                                                    | Why                                          |
| ------ | --------------------------------------------------------------- | -------------------------------------------- |
| 1.20   | `docs/adr/*/amendments/**`                                      | Amendments OVERRIDE original ADR sections    |
| 1.10   | `docs/adr/*/example-implementation/**`                          | Concrete code examples                       |
| 1.00   | `docs/adr/*/[0-9]*-*.md`                                        | Main ADR file                                |
| 0.95   | `docs/adr/*/README.md`                                          | Mostly links — useful but rarely the answer  |
| 0.90   | `docs/architecture/**`                                          | Cross-cutting maps                           |
| 0.85   | `docs/patterns/**`                                              | Code templates                               |
| 0.80   | `docs/reference/**`                                             | Endpoint maps, etc.                          |
| 0.70   | `docs/roadmap/**`                                               | Forward-looking, lower priority for "how" Qs |
| 0.40   | `docs/adr/*/checklist.md`                                       | Indexed but rarely a primary answer          |
| 0.30   | `docs/adr/*/migration-plan.md`                                  | Same — useful only for migration questions   |
| 0.05   | example-implementation files smaller than `stub_byte_threshold` | Stubs effectively buried in ranking          |

## CLI usage

```powershell
# Default top-5
python tools\rag\query.py "How are TypedIds defined?"

# Top-10, JSON output for piping
python tools\rag\query.py "How are TypedIds defined?" --top-k 10 --json

# Filter by bounded context (substring against breadcrumb / doc title)
python tools\rag\query.py "stock adjustment" --bc Inventory
```

Sample output:

```
#1  score=0.892  (raw=0.811 × w=1.10)
     docs/adr/0011/example-implementation/stock-adjustment-algorithm.md:1-42
     ADR-0011 Inventory > Stock adjustment algorithm
     > The reservation lifecycle has four states: Reserved → Committed → Released → Expired ...

#2  score=0.840  (raw=0.840 × w=1.00)
     docs/adr/0011/0011-inventory-availability-bc-design.md:120-180
     ADR-0011 Inventory > §8a Algorithm
     > ...
```

## MCP tools (VS Code Copilot)

Defined in [`tools/rag/mcp_server.py`](../../tools/rag/mcp_server.py). Wired up in [`.vscode/mcp.json`](../../.vscode/mcp.json) (VS Code) and [`.github/copilot/mcp.json`](../../.github/copilot/mcp.json) (Codespaces). Routing rules in [`.github/instructions/rag.instructions.md`](../../.github/instructions/rag.instructions.md).

| Tool                                | Returns                                                                         |
| ----------------------------------- | ------------------------------------------------------------------------------- |
| `query_docs(question, bc?, top_k?)` | JSON list of hits: `rel_path`, `breadcrumb`, `lines`, `score`, `weight`, `text` |
| `get_adr_history(adr_id)`           | JSON: main ADR content + all amendments in order (chronological by filename)    |
| `list_adrs()`                       | JSON table of all ADRs with `id`, `title`, `amendments` count, `examples` count |

## Architecture and query flow

```
OFFLINE  (run once, or after docs change)
─────────────────────────────────────────────────────────────────────
docs/**/*.md ──► chunker.py ──► sentence-transformers ──► Qdrant
(159 files)      (721 chunks)   all-mpnet-base-v2         (in-memory)
                 heading-aware  768-dim vectors                │
                 breadcrumbs    batch_size=32                  ▼
                 overlap=80tok                    .cache/snapshot.qdrant
                                                 .cache/manifest.json


ONLINE  (every VS Code session)
─────────────────────────────────────────────────────────────────────
VS Code reads .vscode/mcp.json
     │
     ▼
spawns: tools/rag/.venv/Scripts/python.exe  tools/rag/mcp_server.py
     │
     ▼
mcp_server.py loads snapshot.qdrant into memory (no re-embedding)
exposes 3 tools over stdio (MCP protocol):
┌──────────────────┬──────────────────────────────────────────────┐
│ list_adrs()      │ returns all 26 ADR ids + amendment counts    │
│ query_docs(q)    │ embed q → cosine search → re-rank → top-k   │
│ get_adr_history  │ returns main ADR + all amendments in order   │
└──────────────────┴──────────────────────────────────────────────┘


QUERY FLOW  (what happens when you ask Copilot something)
─────────────────────────────────────────────────────────────────────
You ask a question in Copilot Agent chat
     │
     ▼
Copilot reads rag.instructions.md  (applyTo: **)
     │
     ├─► FIRST: check docs-index.instructions.md (deterministic router)
     │          "does it point to an obvious file?" ──YES──► read_file()
     │                                                │
     │                                                NO
     │                                                ▼
     └─► THEN: call RAG MCP tool ◄───────────────────┘
               │
               ▼
         query_docs("your question", top_k=5)
               │
               ▼
         mcp_server.py:
           1. embed question  → 768-dim vector
           2. cosine search   → fetch top-20 from Qdrant
           3. re-rank         → score × weight (amendment=1.2, main ADR=1.0, roadmap=0.7…)
           4. truncate        → top-5 chunks returned
               │
               ▼
         Copilot receives: rel_path + line range + score + text excerpt
               │
               ▼
         Copilot calls read_file() on the 1-2 most relevant paths
               │
               ▼
         Answer — grounded in docs, low token cost
```

> **What is NOT in this flow**: no LLM synthesis in `mcp_server.py` (Copilot does synthesis),
> no automatic re-index (run `ingest.py` manually), no Polish query support (EN-only embedder).



`tools/rag/eval/questions.json` holds 45 anchor questions (all 26 ADRs + amendments + cross-cutting + Polish), each with `expect_any: [path_substrings]`. The eval script reports recall@5 / recall@8 plus a list of failures with the actual top-3 hits, so you can adjust chunking, weights, or the embedder.

```powershell
python tools\rag\eval\eval.py
```

Current baseline (`all-mpnet-base-v2`, 45 questions): **recall@5=95.56%, recall@8=100%**, mean rank=1.33.

Acceptance bar: **recall@8 ≥ 95 %**. Below that, tune in this order:

1. Chunking (`max_tokens`, `min_tokens`, `overlap_tokens`)
2. Weights for the document kind that's missing
3. Add metadata filters (`bc` is the easy win)
4. Last resort: swap embedder in `config.yaml` (see Planned improvements)

## Optional: Docker mode

```powershell
docker run -p 6333:6333 qdrant/qdrant
# Then either edit config.yaml (vector_store.mode: docker) or pass --mode docker:
python tools\rag\ingest.py --mode docker
```

Switch to Docker if you want persistence across reboots without re-embedding (which takes ~30 s for this corpus anyway).

## Optional: enable LLM synthesis

Edit `config.yaml`:

```yaml
synthesis:
  mode: local
  ollama_url: http://localhost:11434
  ollama_model: qwen2.5:7b
```

Then `ollama pull qwen2.5:7b`. The MVP query layer does NOT yet call the LLM (intentionally — synthesis is Copilot's job). Wire it up in `query.py` when you actually need standalone answers (e.g. CLI without Copilot, HTTP wrapper for non-VS-Code clients).

## Re-indexing

There is no file watcher. Re-run `python tools\rag\ingest.py` after any meaningful change to `docs/`. Embedding the whole corpus takes ~140 seconds on CPU (all-mpnet-base-v2, 159 files, 721 chunks).

If you forget, you'll see `query_docs` returning stale `text` snippets — `rag.instructions.md` tells Copilot to suggest a re-index in that case.

## Troubleshooting

| Symptom                                                  | Fix                                                                       |
| -------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Snapshot not found at tools/rag/.cache/snapshot.qdrant` | Run `python tools/rag/ingest.py` (you're trying to query before building) |
| Top hit has very low `final_score` (< 0.3)               | Query is too vague or the topic isn't in `docs/`. Try `list_adrs` first.  |
| MCP server not appearing in VS Code                      | Reload window; check Output → "GitHub Copilot Chat" for MCP errors        |
| `recall@8` < 95 %                                        | Most likely chunking issue; try lowering `max_tokens` to 500              |
| `sentence-transformers` download fails behind a proxy    | Set `HTTPS_PROXY` or pre-cache the model under `~/.cache/huggingface/hub` |

## Planned improvements

| #   | Improvement                                | Priority | Notes                                                                                                                                                                                                                                                 |
| --- | ------------------------------------------ | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Incremental delta ingest**               | Medium   | Extend `manifest.json` to store `{rel_path: mtime}` per file. On re-run, skip unchanged files and reload their points from the existing snapshot. Drops re-index time from ~140s to ~5s for a single edited doc.                                     |
| 2   | **CI re-index step** (`workflow_dispatch`) | Medium   | GitHub Actions job that runs `ingest.py` on `docs/**` push (fast once incremental ingest is done); outputs snapshot artifact. Pairs with incremental ingest — without it, CI cost is 140s per push.                                                  |
| 3   | **Multilingual embedder**                  | Low      | `all-mpnet-base-v2` is EN-only. Polish UI labels in ADRs score ~0.25 (noise floor). Drop-in swap: `paraphrase-multilingual-mpnet-base-v2` (768 dims) in `config.yaml → embedder.model` + re-ingest.                                                   |
| 4   | **Persistent Qdrant (Docker)**             | Low      | Current default is in-memory — index reloaded from snapshot on every VS Code session start. `config.yaml` already has `vector_store.mode: docker`. Wire up Docker Compose service for a persistent dashboard and faster startup.                      |
| 5   | **File-watcher auto re-index**             | Low      | `ingest.py --watch` using `watchdog` — auto-rebuild when `docs/` changes during active ADR authoring sessions.                                                                                                                                        |
| 6   | **synthesis.mode: local** wired up         | Deferred | `query.py` intentionally does not call Ollama. Wire `qwen2.5:7b` only for CLI-without-Copilot use cases. Do NOT enable by default — Copilot does synthesis for VS Code users.                                                                        |
| 7   | **Chunk scoring transparency**             | Low      | Add `explain=true` flag to `query_docs` returning per-chunk weight breakdown (embedding score + weight multiplier). Useful for debugging low-recall queries.                                                                                           |

## Layout

```
tools/rag/
├── config.yaml              # all knobs in one place
├── requirements.txt
├── common.py                # config loader, weight resolver, doc-kind detection
├── chunker.py               # heading + size + overlap chunker
├── ingest.py                # build the index (in-memory or Docker)
├── query.py                 # search library + CLI
├── mcp_server.py            # 3-tool MCP server
├── eval/
│   ├── questions.json       # 20 anchor questions
│   └── eval.py              # recall@k report
└── .cache/                  # generated; safe to delete
    ├── snapshot.qdrant
    └── manifest.json

.github/
├── copilot/mcp.json                          # registers the MCP server
└── instructions/rag.instructions.md          # tells Copilot when to use the tools
```
