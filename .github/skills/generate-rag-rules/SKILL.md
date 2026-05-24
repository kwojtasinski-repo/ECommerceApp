---
name: generate-rag-rules
description: >
  Update metadata-rules.yaml (document classification) and/or queries.yaml
  (named eval queries) when new documentation folders are added, doc types
  change, or query coverage gaps are identified.
  metadata-rules changes require a full re-index; queries.yaml changes do not.
argument-hint: "[new-folder-path | new-doc-type | coverage-gap-description]"
---

# Generate / Update RAG Metadata Rules and Eval Queries

Two config files control how documents are classified and tested:

| File | Purpose | Re-index required? |
|------|---------|-------------------|
| `tools/rag/metadata-rules.yaml` | Assigns `doc_kind` and `adr_id` to each indexed file | ✅ Yes — force-full |
| `tools/rag/queries.yaml` | Named queries used by `eval.py` and agents | ❌ No |

Both files exist in `tools/rag/` only — the .NET server reads from the same path via the
shared config or `RAG_METADATA_RULES` / `RAG_QUERIES` env overrides.

---

## Part A — metadata-rules.yaml

### When to update

- A new documentation folder was added under `docs/` or `.github/`
- A new file type/section (e.g., `checklist.md`, `migration-plan.md`) was introduced
- Documents are being tagged with the wrong `doc_kind` (e.g., a roadmap file tagged as `adr_main`)
- A new ADR numbering convention was introduced

### How doc_kind_rules work

```yaml
doc_kind_rules:
  - glob: "**/amendments/**"       # most specific first
    kind: "adr_amendment"
  - glob: "docs/adr/**"            # broad catch-all for ADRs
    kind: "adr_main"
  - glob: ".github/context/**"
    kind: "context"
```

- Uses Python `fnmatch` glob syntax (not regex)
- **First matching rule wins** — put specific patterns before broad ones
- `kind` is stored in Qdrant payload and used as the `doc_kind` filter in queries

### Standard kind vocabulary (use these, do not invent new kinds without updating queries.yaml)

| kind | Used for |
|------|---------|
| `adr_main` | Primary ADR document |
| `adr_amendment` | Amendment files under `amendments/` |
| `adr_example` | Code example files under `example-implementation/` |
| `adr_checklist` | `checklist.md` files inside ADR folders |
| `adr_migration_plan` | `migration-plan.md` files inside ADR folders |
| `adr_router` | ADR `README.md` router/index files |
| `context` | `.github/context/*.md` (known-issues, agent-decisions, project-state) |
| `architecture` | `docs/architecture/**` |
| `pattern` | `docs/patterns/**` |
| `reference` | `docs/reference/**` |
| `roadmap` | `docs/roadmap/**` |
| `rag_meta` | `docs/rag/**` — RAG tool docs (excluded from production query results) |
| `instruction` | `.github/instructions/**` |
| `skill` | `.github/skills/**` |
| `prompt` | `.github/prompts/**` |

### Adding a new folder

1. Decide which `kind` applies. Use the table above or create a new kind if none fits.
2. Add the glob **before** the first broad catch-all that would match:

```yaml
  # Add before "docs/adr/**" if the folder is under docs/adr/
  - glob: "docs/adr/**/decision-log/**"
    kind: "adr_decision_log"

  # Add at the end if it is a new top-level folder
  - glob: "docs/runbooks/**"
    kind: "runbook"
```

3. After editing, verify by running ingest with `--dry-run` (if supported) or checking logs.

### adr_id_patterns — when to update

Add a new pattern only if the ADR file naming convention changes. The three built-in
patterns cover:
- `docs/adr/0011/…` (folder style)
- `docs/adr/ADR-0011.md` (prefix style)
- `docs/adr/0011-some-title.md` (numeric prefix style)

```yaml
adr_id_patterns:
  - pattern: "adr/(?P<id>\\d{4})/"     # folder style — matches first
    # ...
```

### Re-index after metadata-rules changes

```powershell
# From repo root — rebuilds ALL documents (kind/adr_id is embedded during ingest)
docker compose --profile rag run --rm rag-tools python ingest.py --force-full
```

Verify: call `query_docs("...", doc_kind="<new_kind>")` — if results are returned with the
new kind, classification is working.

---

## Part B — queries.yaml

### When to update

- A new domain concept was added (new BC, new design pattern, new service)
- A new document kind was introduced — add a coverage query for it
- A query consistently returns wrong results and there is no named query to track it
- `eval.py` reports a query as "no named query covers this file"

### Format

```yaml
named_queries:

  - name: "unique-slug"                # snake_case, used by eval.py as ID
    question: "natural language query" # what a developer would actually ask
    doc_kind: "adr_main"               # optional — restrict to one kind
    adr_id: "0027"                     # optional — restrict to one ADR (4-digit zero-padded)
    top_k: 8                           # optional — override default_top_k
```

### Guidelines for writing good questions

| Do | Don't |
|----|-------|
| Use the exact terms from the target document | Use generic terms (`"what is the main pattern?"`) |
| Include domain symbols (`TypedId`, `KI-008`, `CouponsOptions`) | Paraphrase away from the source vocabulary |
| Match how a developer would actually type the question | Write a test assertion disguised as a question |
| Keep it 5–15 words | Write a paragraph |

### Adding coverage for a new document kind

```yaml
  - name: "runbook-deployment"
    question: "deployment runbook steps and checklist"
    doc_kind: "runbook"
    top_k: 5
```

### Adding coverage for a specific ADR

```yaml
  - name: "rag-multilingual"
    question: "multilingual query expansion glossary Polish German repeat weight"
    doc_kind: "adr_main"
    adr_id: "0027"
    top_k: 3
```

### Testing after changes

```powershell
cd tools/rag
.venv\Scripts\python eval.py          # run all named queries against live index
```

A query passes if the target document appears in top-K results. Failed queries are printed
with their actual top-3 and expected document.

---

## Common mistakes

- **Broad glob placed before specific glob** → wrong `doc_kind` assigned to files; fix by
  reordering (specific first)
- **New folder not excluded from ingest** → RAG meta-docs, reports, transient files indexed
  as real content; add to `source.exclude_globs` in `rag-config.yaml`
- **Skipping force-full re-index after metadata-rules change** → old chunks retain stale
  `doc_kind`; partial ingest will not fix this — force-full is required
- **queries.yaml edited without running eval.py** → query may have a typo or wrong `adr_id`;
  always verify with a live run
