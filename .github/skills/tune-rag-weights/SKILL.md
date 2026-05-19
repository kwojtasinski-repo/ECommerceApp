---
name: tune-rag-weights
description: >
  Tune the ranking weight multipliers in tools/rag/config.yaml.
  Use when a file consistently ranks too low or too high in query results
  despite good semantic similarity. Weight changes are query-time only —
  no re-indexing required.
argument-hint: "[file-path-or-glob] [up|down|value]"
---

# Tune RAG Ranking Weights

Adjust the `ranking.weights` table in `tools/rag/config.yaml` to boost or suppress
specific files in query results without re-embedding anything.

---

## How weights work

```
final_score = raw_similarity_score × weight_multiplier
```

- Raw score: cosine similarity [0.0 – 1.0] from Qdrant
- Weight: fnmatch glob matched against the file's `rel_path` (first match wins)
- Default if no pattern matches: **1.00**
- Weights **do not require re-indexing** — they are applied at query time

## Current baseline (do not delete these — they are deliberate)

| Pattern | Weight | Rationale |
|---------|--------|-----------|
| `.github/context/known-issues.md` | 1.25 | Bug-fix gate — always consult first |
| `.github/context/agent-decisions.md` | 1.20 | Correction history — high signal |
| `.github/context/project-state.md` | 1.15 | BC block status — critical |
| `docs/adr/*/amendments/**` | 1.20 | Amendments override main ADR content |
| `docs/adr/*/example-implementation/**` | 1.10 | Code examples are high value |
| `docs/adr/*/[0-9]*-*.md` | 1.00 | Main ADR file — baseline |
| `docs/adr/*/README.md` | 0.95 | Router pages — mostly links, lower value |
| `.github/context/future-skills.md` | 0.80 | Forward-looking, low urgency |

---

## Step-by-step: identify which file needs tuning

### 1 — Run a failing query to see current scores

```python
# In the MCP chat or via CLI:
query_docs("your query here", top_k=5)
```

Look at:
- The expected file: is it missing from top-5? What is its score?
- Files that ranked above it: are they correct? What is their score?

### 2 — Decide the adjustment

| Symptom | Action |
|---------|--------|
| Right file at #3-5 instead of #1 | Boost it: raise weight 1.00 → 1.15 |
| Wrong file always wins despite good query | Suppress wrong file: lower its weight 1.00 → 0.85 |
| Right file never appears at all | Check glossary first — may be a language gap, not a weight gap |
| Right file at #1 but score < 0.30 | Score threshold issue — see `diagnose-rag` skill |

**Weight range guide:**

| Weight | Effect |
|--------|--------|
| 1.30+ | Very strong boost — reserve for "always check this first" files |
| 1.15–1.25 | High priority — important context files |
| 1.00–1.10 | Normal — ADR main files, architecture docs |
| 0.90–0.99 | Mild suppression — router pages, index files |
| 0.70–0.89 | Strong suppression — meta-docs, forward-looking docs |
| < 0.70 | Aggressive suppression — use only if file is hurting every query |

### 3 — Edit `tools/rag/config.yaml`

```yaml
ranking:
  weights:
    # Add new rule BEFORE the catch-all patterns for the same folder
    - { pattern: "docs/adr/0027/**",  weight: 1.15 }   # RAG ADR — high relevance for rag queries

    # ... existing entries ...
```

**Order matters — first match wins.**
Put more specific patterns (full path, filename) before broader globs (folder/**).

### 4 — Test immediately (no re-index needed)

Re-run the same query via MCP:

```
query_docs("your query here", top_k=5)
```

Confirm the target file moved to the expected position.

### 5 — Run the full eval benchmark to check for regressions

```powershell
cd tools/rag
.venv\Scripts\python eval.py
```

Check that previously passing queries still pass. If a query regressed, the weight
change over-boosted something — roll back or narrow the glob.

---

## Common patterns

### Boost a specific ADR because it covers a hot topic

```yaml
- { pattern: "docs/adr/0016/**",  weight: 1.15 }   # Coupons — queried frequently
```

### Suppress router README files more aggressively

```yaml
- { pattern: "docs/adr/*/README.md",  weight: 0.85 }
```

### Boost all `.github/context/` files equally

```yaml
- { pattern: ".github/context/*.md",  weight: 1.15 }
```

### Suppress docs that are excluded from ingest but leaked in somehow

```yaml
- { pattern: "docs/reports/**",  weight: 0.50 }
```

---

## What NOT to do

- **Do not set weight > 1.40** — it will dominate every query regardless of semantic fit
- **Do not add a weight for every file** — only tune files with proven query regression
- **Do not use weight to fix language gaps** — that is the glossary's job (see `expand-rag-glossary`)
- **Do not forget to check .NET server** — `tools/rag-dotnet/config.yaml` is a separate copy;
  keep weights in sync between the two configs
