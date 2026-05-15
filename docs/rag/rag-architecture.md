# RAG Architecture — Multi-Project Design

> Status: **Design updated — incremental ingest, MCP startup auto-check, `/rag-sync` agent, eval pipeline added (May 2026).**  
> Covers: system-level infrastructure, per-repo manifest, MCP wiring, weight system, staleness mitigation, eval pipeline, `/rag-sync` agent, future architecture paths, and impact on `.github/` instruction files.  
> See `context-cost-analysis.md` § 9 for the credit-cost impact of this architecture.

---

## 1. Problem statement

The original RAG setup (v1) had three structural problems:

| Problem                   | Root cause                                                                                                                                                                                                                              |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Stale index**           | `ingest.py` must be run manually after every doc change; Qdrant has no watch mechanism. The files that change most often (agent-decisions.md, known-issues.md, project-state.md) are exactly the ones the agent needs to be current on. |
| **Multi-project scaling** | All config was hardcoded per-project. Adding ParkingLot (or any new repo) means duplicating scripts, Qdrant collection setup, MCP server config, and weight tables.                                                                     |
| **Instruction overhead**  | `docs-index.instructions.md` (1,652 tokens for ParkingLot alone) routes the agent to `read_file(ADR)` which loads entire documents (avg 4,192 tokens) that persist and accumulate across every session turn.                            |

---

## 2. Design principles

1. **One file per repo** — a single `.copilot-rag.yaml` manifest at the repo root contains everything project-specific.
2. **Shared scripts, project-isolated collections** — Python scripts live at a system path, installed once. Each project uses a separate Qdrant collection named after the project.
3. **Defaults in code, overrides in manifest** — weight tables and chunker parameters have sensible defaults in the shared Python code. The manifest carries only what differs.
4. **RAG is primary, `read_file` is the exception** — instruction files tell the agent to `query_docs()` first and only fall back to `read_file` when a chunk is demonstrably insufficient.
5. **Maintenance is automated at session start, explicit for deep changes** — the MCP server auto-checks for changed files on every startup and re-indexes incrementally (2–3 s for typical session changes). A `/rag-sync` agent handles on-demand deep maintenance: Qdrant health check, incremental ingest, eval validation, and eval question coverage check. No mandatory post-edit agent calls.

---

## 3. Infrastructure layer

> **Current state (ECommerceApp v1):** Python scripts live in `tools/rag/` inside this repo. The layout below is the **target state** for a shared multi-repo installation. Extraction to a dedicated `rag-tools` repository is a future phase — see § 14.

```
# Qdrant — single Docker container, shared by all projects
docker run -d --name qdrant -p 6333:6333 qdrant/qdrant

# Shared Python scripts — one installation, any project points to it
C:/tools/rag/
  ingest.py          # --manifest <path> --root <path>
  mcp_server.py      # --manifest <path>  (stdio, started by VS Code)
  tune_weights.py    # --manifest <path>  (reads Qdrant stats + git log, writes manifest)
  chunker.py         # shared — no per-project changes
  common.py          # shared — loads manifest, resolves weights
  requirements.txt
```

**Qdrant** is the only persistent Docker dependency. The MCP server is a local Python process (stdio mode) started by VS Code per workspace — not a container.

---

## 4. Per-repo layer — the `.copilot-rag.yaml` manifest

One file, committed to the repo root. Everything project-specific lives here.

### Full schema

```yaml
project:
  name: ecommerceapp # used as default collection name
  collection: ecommerceapp_docs # Qdrant collection name (explicit override optional)

ingest:
  include:
    - "docs/**/*.md"
    - ".github/**/*.md"
  exclude:
    - "**/*.local.md" # gitignored local files
    - "docs/rag/**" # RAG meta-docs (not project knowledge)
    - "docs/reports/**" # transient session reports

chunker:
  # Optional — all fields have defaults in common.py
  max_tokens: 800
  overlap_tokens: 80

vector_store:
  url: http://localhost:6333 # override in .copilot-rag.local.yaml for non-standard setups

# Weight overrides — only entries that differ from DEFAULT_WEIGHTS in common.py
# Defaults cover: known-issues.md (1.25), agent-decisions.md (1.20), project-state.md (1.15),
# amendments (1.20), example-implementation (1.10), main ADR (1.00), README (0.95),
# architecture (0.90), patterns (0.85), skills (0.70), checklist (0.40), migration-plan (0.30)
weights:
  "docs/adr/*/checklist.md": 0.40 # example: same as default, shown for clarity
  "docs/adr/*/migration-plan.md": 0.30
  "docs/rag/**": 0.20
```

`.copilot-rag.local.yaml` is gitignored and merges on top — used for local Qdrant URL overrides only.

### ParkingLot manifest (minimal)

```yaml
project:
  name: parkinglot
  collection: parkinglot_docs

ingest:
  include:
    - "docs/**/*.md"
    - ".github/**/*.md"
  exclude:
    - "**/*.local.md"

# No weight overrides — defaults apply
```

---

## 5. MCP wiring — `.vscode/mcp.json`

VS Code starts the MCP server via `docker run`. No Python or Qdrant on the host — only Docker is required.

```json
{
	"servers": {
		"ecommerceapp-rag": {
			"type": "stdio",
			"command": "docker",
			"args": [
				"run",
				"--rm",
				"--interactive",
				"--volume",
				"${workspaceFolder}:/workspace",
				"--volume",
				"qdrant_data:/data/qdrant",
				"--env",
				"RAG_WORKSPACE=/workspace",
				"--env",
				"PYTHONUNBUFFERED=1",
				"rag-tools",
				"python",
				"/app/mcp_server.py"
			]
		}
	}
}
```

The MCP server exposes 3 tools — `query_docs`, `get_adr_history`, `list_adrs` — with the collection name read from `config.yaml` baked into the image.

---

## 6. Weight system

### Default weights (in `common.py` — shared, no per-project duplication)

```python
DEFAULT_WEIGHTS = [
    # Context files — highest priority
    { "pattern": ".github/context/known-issues.md",    "weight": 1.25 },
    { "pattern": ".github/context/agent-decisions.md", "weight": 1.20 },
    { "pattern": ".github/context/project-state.md",   "weight": 1.15 },
    { "pattern": ".github/context/repo-index.md",      "weight": 1.00 },
    # ADRs
    { "pattern": "docs/adr/*/amendments/**",           "weight": 1.20 },
    { "pattern": "docs/adr/*/example-implementation/**","weight": 1.10 },
    { "pattern": "docs/adr/*/[0-9]*-*.md",             "weight": 1.00 },
    { "pattern": "docs/adr/*/README.md",               "weight": 0.95 },
    # Architecture and patterns
    { "pattern": "docs/architecture/**",               "weight": 0.90 },
    { "pattern": "docs/patterns/**",                   "weight": 0.85 },
    { "pattern": ".github/instructions/**",            "weight": 0.75 },
    { "pattern": ".github/skills/**",                  "weight": 0.70 },
    { "pattern": "docs/reference/**",                  "weight": 0.70 },
    { "pattern": "docs/roadmap/**",                    "weight": 0.60 },
    # Low priority
    { "pattern": "docs/adr/*/checklist.md",            "weight": 0.40 },
    { "pattern": "docs/adr/*/migration-plan.md",       "weight": 0.30 },
    { "pattern": "docs/rag/**",                        "weight": 0.20 },
    # Fallback
    { "pattern": "**",                                 "weight": 0.90 },
]
```

**Merge rule:** manifest `weights` entries are prepended before defaults. First match wins. This means manifest overrides take precedence without touching the shared code.

---

## 7. RAG maintenance agent

### Where it lives

`.github/prompts/tune-rag-weights.prompt.md` — invoked manually as a Copilot Chat slash command.

### Trigger

**Manual only.** Automatic triggers (file watcher, git hooks, scheduled) all consume tokens continuously. Run this when:

- A new ADR is added and is dominating query results unexpectedly
- A context file has grown significantly (e.g., agent-decisions.md doubled in size)
- Query results feel stale or irrelevant

### Signals the agent uses (all four)

| Signal                   | What it detects                                                          | How                                                         |
| ------------------------ | ------------------------------------------------------------------------ | ----------------------------------------------------------- |
| **Chunk count per file** | Files with 20+ chunks dominate results by volume — may need lower weight | Query Qdrant collection stats                               |
| **Change frequency**     | Files edited often should stay high weight                               | `git log --since="90 days" --format="%s" -- <path>`         |
| **Query hit rate**       | Chunks never returned by `query_docs` → reduce weight or exclude         | Query Qdrant stored scroll + filter by file                 |
| **File size growth**     | Files that grew >30% since last tune                                     | Compare current byte size vs manifest `stub_byte_threshold` |

### Output

The agent **writes directly to `.copilot-rag.yaml`** — no proposal step. Review with `git diff`.  
It only modifies the `weights:` section. It never changes `ingest:`, `project:`, or `vector_store:`.  
After writing: run `python C:/tools/rag/ingest.py --manifest .copilot-rag.yaml --root .` to rebuild index.

---

## 8. Impact on `.github/` instruction files

This is the biggest structural change. The instruction files that currently route the agent to `read_file(ADR)` are replaced or slimmed.

### Files to demote (keep file, remove `applyTo:`)

| File                              | Why                                                                                                                                                                                                                           |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `doc-suggestions.instructions.md` | Existed to remind agent to sync docs after changes. Remove `applyTo: "**"` so it no longer auto-loads every session. Keep the file in `.github/instructions/` as a reference — the content is useful when accessed on demand. |

### Files to slim significantly

| File                                      | Current tokens | Target tokens | What changes                                                                                                                                                                                            |
| ----------------------------------------- | -------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `docs-index.instructions.md` (EComm)      | 469            | ~120          | Remove ADR routing table — replaced by `query_docs()`. Keep only the quick-nav pointer to `rag.instructions.md`.                                                                                        |
| `docs-index.instructions.md` (ParkingLot) | 1,652          | ~120          | Same as above. The 1,652-token routing table is entirely replaced by RAG.                                                                                                                               |
| `pre-edit.instructions.md`                | 956            | ~450          | Remove step "Read ADRs — read ADRs in `docs/adr/` relevant to the area". Replace with: "Use `query_docs()` to find relevant ADR sections." Remove mandatory `@copilot-setup-maintainer` post-edit step. |
| `agent-memory.instructions.md`            | 124            | ~40           | Replace file with 1-line instruction: "Before non-trivial work, run `query_docs('agent decisions corrections')` to load prior corrections."                                                             |
| `copilot-instructions.md` (EComm)         | 1,112          | ~700          | Remove the ADR routing table in § 7 "BC → ADR quick map". Remove maintainer sync rules in § 4. RAG replaces both.                                                                                       |
| `copilot-instructions.md` (ParkingLot)    | 796            | ~450          | Remove skills/ADR routing table. Keep architecture rules and context file pointers.                                                                                                                     |

### Files that grow (intentionally)

| File                  | Current tokens | Target tokens | What changes                                                                                                                                                        |
| --------------------- | -------------- | ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `rag.instructions.md` | 571            | ~750          | Promoted to **primary routing instruction**. Add: which MCP server name to use, when to call `get_adr_history` vs `query_docs`, how to interpret chunk breadcrumbs. |

### Files unchanged

On-demand instruction files (`dotnet.instructions.md`, `api.instructions.md`, `blazor.instructions.md`, etc.) are **unaffected** — they contain code writing guidance, not doc routing. They stay as-is.

---

## 9. Onboarding a new project (5 steps)

```bash
# 1. Create manifest at repo root
#    Copy from ECommerceApp or ParkingLot, change project.name and collection

# 2. Run initial ingest
python C:/tools/rag/ingest.py --manifest .copilot-rag.yaml --root .

# 3. Add MCP server entry to .vscode/mcp.json in the multi-root workspace
#    (one new entry, same pattern as existing ones)

# 4. Add 5-line rag.instructions.md to project's .github/instructions/
#    Tells agent: use <project>-rag MCP server, call query_docs first

# 5. Done — query_docs is live for the new project
```

No Qdrant configuration needed. No new Python dependencies. No Docker image changes.

---

## 10. Staleness mitigation — incremental ingest + MCP startup auto-check

### What "stale" means

The Qdrant collection was built from an older version of your docs. Example:

```
Monday:    Run ingest.py  →  Qdrant indexes agent-decisions.md (10 entries)
Tuesday:   Add 3 new entries to agent-decisions.md
Wednesday: Agent calls query_docs("corrections about Orders BC")
           → returns Monday's chunks → misses Tuesday's corrections
```

### Layer 1 — Incremental ingest (hash-based)

`ingest.py` gains change detection via `tools/rag/.cache/manifest.json` (gitignored):

```json
// manifest.json structure — local only, gitignored
{
	"last_indexed": "2026-05-11T14:32:00Z",
	"files": {
		"docs/adr/0015/0015-orders-bc.md": "sha256:abc123...",
		".github/context/agent-decisions.md": "sha256:def456..."
	}
}
```

On each run:

1. Hash all files matching `ingest.include` globs
2. Compare against manifest — only re-embed **changed/new** files; delete chunks for **removed** files
3. Write updated manifest with new hashes and `last_indexed` timestamp

Result: re-running after 1 file changed takes **2–3 seconds** instead of 30. `--force-full` flag triggers a complete rebuild.

### Layer 2 — MCP server startup auto-check

On `mcp_server.py` initialisation (before serving the first `query_docs` request):

```python
def __init__(self, manifest_path):
    self.manifest = load_manifest(manifest_path)
    changed = detect_changed_files(self.manifest)  # hash comparison
    if changed:
        run_incremental_ingest(changed)            # 2–3 s for typical session changes
    start_serving()
```

Effect: by the time Copilot makes its first `query_docs()` call, the index is already up to date. Zero user action required.

Qdrant-down message when container is unreachable:

```
RAG unavailable: Qdrant not running.
Start with: docker start qdrant
Then run /rag-sync to verify index.
```

### Layer 3 — `/rag-sync` agent (on-demand deep maintenance)

See § 13 for the full agent design. Use when you want to explicitly validate index quality, check eval recall, or review auto-generated question coverage.

### What is NOT stored in Qdrant

Qdrant stores only embeddings (384-dim vectors) and metadata (source path, chunk text, weight). Scripts, configs, and eval questions stay in the repo — they are the tools that work _with_ Qdrant, not data that goes _into_ it.

| Stored in Qdrant                | Stored in repo (version-controlled)                 | Local only (gitignored)            |
| ------------------------------- | --------------------------------------------------- | ---------------------------------- |
| Embeddings + metadata per chunk | `tools/rag/ingest.py`, `mcp_server.py`, `common.py` | `tools/rag/.cache/manifest.json`   |
| Collection: `ecommerceapp_docs` | `.copilot-rag.yaml` (project config)                | `tools/rag/.cache/snapshot.qdrant` |
|                                 | `tools/rag/eval/questions.json` (eval questions)    |                                    |

---

## 11. Implementation checklist

Tasks to execute to complete the v2 architecture:

- [ ] **Refactor `ingest.py`** — add `--manifest` flag, replace hardcoded `config.yaml` path with manifest glob patterns
- [ ] **Refactor `mcp_server.py`** — add `--manifest` flag, read collection name from manifest at startup
- [ ] **Extract `common.py` defaults** — move `DEFAULT_WEIGHTS` list here, implement merge logic for manifest overrides
- [ ] **Create `tune_weights.py`** — reads Qdrant stats + git log, writes `weights:` section to manifest
- [ ] **Move `tools/rag/config.yaml` → `.copilot-rag.yaml`** at repo root, convert to new schema
- [ ] **Create `.copilot-rag.yaml` for ParkingLot**
- [ ] **Update `.vscode/mcp.json`** — add named `parkinglot-rag` server entry with manifest path
- [ ] **Create `.github/prompts/tune-rag-weights.prompt.md`** — the maintenance agent prompt
- [ ] **Implement incremental ingest** — hash-based change detection in `ingest.py`; read/write `tools/rag/.cache/manifest.json`; add `--force-full` flag for complete rebuilds
- [ ] **MCP startup auto-check** — detect changed files at `mcp_server.py` init; run incremental ingest before first request; return Qdrant-down message when unreachable
- [ ] **Create `/rag-sync` agent prompt** — `.github/prompts/rag-sync.prompt.md`; full cycle: Qdrant check → incremental ingest → eval → question coverage review (see § 13)
- [ ] **Create `generate-eval-questions` skill** — `.github/skills/generate-eval-questions/SKILL.md`; extracts template questions from new file headings; appends to `tools/rag/eval/questions.json` with `reviewed: false`
- [ ] **Slim instruction files** per § 8 table above (each file is a separate PR-level change)
- [ ] **Run eval.py baseline** — confirm ≥80% recall before flipping `rag.instructions.md` to RAG-primary

---

## 12. Testing plan (sketch)

```python
# After implementation, run these validation queries manually:
query_docs("what is the BC ownership rule for Orders?")
# Expected: ADR-0014 chunks, NOT ADR-0011 (inventory)

query_docs("agent decisions corrections made recently")
# Expected: agent-decisions.md chunks with 1.20× weight at top

query_docs("known issues current blockers")
# Expected: known-issues.md chunks with 1.25× weight at top

get_adr_history("0014")
# Expected: main ADR-0014 + any amendment files in chronological order

list_adrs()
# Expected: full list of ADR numbers and titles from both workspaces (if multi-root)
```

`eval.py` is now a first-class part of the `/rag-sync` agent pipeline — it runs automatically after every agent-triggered incremental ingest. The **80% recall threshold** is the acceptance gate before flipping `rag.instructions.md` to RAG-primary mode. See `tools/rag/eval/questions.json` for the current question set.

---

## 13. `/rag-sync` agent design

### Location

`.github/prompts/rag-sync.prompt.md` — invokable as a Copilot Chat slash command (`/rag-sync`).

### Mode

`mode: agent` — has full tool access (terminal, file read/write).

### Full maintenance cycle

```
/rag-sync
  ↓
1. Check Qdrant is running
   → docker ps | grep qdrant
   → if not running: "Start with: docker start qdrant — then re-run /rag-sync"
  ↓
2. Load manifest.json — report last-indexed timestamp
   → "Last indexed: 2 days ago (2026-05-09T10:14:00Z)"
  ↓
3. Detect changed files since last index
   → compare manifest.json hashes vs current file hashes
   → list changed / new / deleted files
  ↓
4. Run incremental ingest for changed files only
   → python tools/rag/ingest.py --changed-only
   → report: "Re-indexed 3 files in 2.4 s"
  ↓
5. Check eval question coverage
   → compare indexed file paths vs questions.json source paths
   → for uncovered files: delegate to generate-eval-questions skill
   → append auto-generated questions with reviewed: false
   → show unreviewed questions to user for confirmation
  ↓
6. Run eval.py — report recall score
   → "Recall: 23/25 questions passed (92%) ✓"
   → OR: "Recall: 17/25 (68%) — check weights for recently changed files"
  ↓
7. Report summary: files re-indexed, questions added/pending review, recall score
```

### Eval question format

```json
{
	"q": "How is the Refund Policy designed?",
	"expect_any": ["docs/adr/0017/"],
	"auto_generated": true,
	"reviewed": false
}
```

Questions with `reviewed: false` are surfaced by `/rag-sync` for human confirmation. They do not count toward the recall score until confirmed (`reviewed: true`).

### `generate-eval-questions` skill

Location: `.github/skills/generate-eval-questions/SKILL.md`  
Input: file path of a newly indexed document.  
Process: read file → extract first `##` heading → generate template question → return `{q, expect_any}` object.  
Output: written to `tools/rag/eval/questions.json` by the `/rag-sync` agent.

---

## 14. Future architecture paths

### Path A — Shared `rag-tools` repository (target for 4+ developers, 5+ repos)

Extract all Python scripts into a dedicated repository. Each project repo keeps only its manifest and eval questions.

```
rag-tools/              ← separate git repository
  ingest.py
  mcp_server.py
  common.py
  eval/eval.py
  setup.py              ← pip installable

# Per-project repos keep only:
ECommerceApp/.copilot-rag.yaml
ECommerceApp/tools/rag/eval/questions.json
```

Developer onboarding:

```bash
git clone .../rag-tools
pip install -e rag-tools    # puts rag-ingest, rag-mcp-server on PATH
```

**Trigger:** when a second team-shared repo needs RAG. Copy-per-repo causes version drift at 3+ repos with 4+ developers — a bug fix in `ingest.py` must be patched in every copy manually.

### Path B — MSSQL 2025 vector store (when GA)

SQL Server 2025 ships native vector search (`VECTOR` type, `VECTOR_DISTANCE` function). Every .NET developer already has SQL Server installed — no Docker dependency for the vector store.

**Migration steps when MSSQL 2025 is GA:**

1. Change `.copilot-rag.yaml` `vector_store.backend` from `qdrant` to `mssql`
2. Add MSSQL adapter to `common.py` (same interface as Qdrant adapter)
3. Run `python tools/rag/ingest.py --force-full` to populate the SQL Server collection

The incremental ingest logic, manifest schema, MCP tool names, eval pipeline, and `/rag-sync` agent are all **backend-agnostic** — only the adapter in `common.py` changes.

**Why Qdrant now:** SQL Server 2025 is not yet GA (estimated late 2026). SQL Server 2022 has no native vector type. Qdrant is the best available option today.

**Embedding model upgrade:** When switching to MSSQL 2025, consider upgrading from `paraphrase-multilingual-MiniLM-L12-v2` (384 dims) to Ollama `bge-m3` (1024 dims, stronger multilingual). This requires a full rebuild — plan it together with the backend migration.
