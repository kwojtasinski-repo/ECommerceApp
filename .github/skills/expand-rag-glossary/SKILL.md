---
name: expand-rag-glossary
description: >
  Add a new language/concept to the multilingual query-expansion glossary.
  Use when queries in Polish or German return wrong documents despite the
  correct English query working fine. No re-indexing required — glossary
  expansion is query-time only.
argument-hint: "[concept or failing query]"
---

# Expand the Multilingual RAG Glossary

The glossary bridges the gap between non-English queries and English documentation.
Before embedding a query, foreign-language words are detected and English synonyms
are appended 3× so the English terms dominate mean pooling (~60–87% English weight).

**Files to update (always both):**
- `tools/rag/multilingual-glossary.yaml` — Python server
- `tools/rag-dotnet/multilingual-glossary.yaml` — .NET server (independent copy)

**No re-index required** — expansion happens at query time.

---

## Step 1 — Confirm it is a language gap, not a weight or index gap

Run the failing query in English first:

```
query_docs("TypedId entity identifier domain primitive", top_k=3)
```

- If English returns the right document → it IS a language gap → proceed with this skill
- If English also fails → the doc may not be indexed, score threshold issue, or weight problem
  → use `diagnose-rag` instead

---

## Step 2 — Find the right English anchor terms

Open the target document (the one that should appear). Identify:
- The **canonical English terms** an English speaker would use to find it
- The **domain symbols** that are language-independent (e.g. `TypedId`, `KI-008`, `ADR-0026`)

Good anchor: `"TypedId entity identifier typed domain primitive"`
Too vague:   `"type domain"` — matches too many documents

---

## Step 3 — Collect the foreign patterns

For each language the team uses (PL / DE), list the words that appear in natural-language
queries for that concept. Use inflected forms if relevant (genitive, plural).

| Language | Word | Meaning |
|----------|------|---------|
| PL | `identyfikator` | identifier |
| PL | `encji` | entity (genitive) |
| PL | `domenowy` | domain (adj) |
| DE | `bezeichner` | identifier/designator |
| DE | `kennung` | ID / identifier |
| DE | `entitäts` | entity's (compound prefix) |

**Rules for patterns:**
- Lowercase only
- One word or compound per line (no phrases — the regex is word-boundary matched)
- No ASCII-only words — the glossary is for non-English patterns only; English passes through unchanged
- Avoid highly generic words (`und`, `der`, `jest`, `i`) that appear in every query
- Prefer domain-specific inflection forms over root forms when a query typically uses the inflected form

---

## Step 4 — Check for overlap with existing entries

Open the glossary and scan existing `patterns:` lists. If a word you want to add already
appears in a different entry, it will fire that entry's `english` anchor — check if that
anchor is still correct, or if you need to move the pattern to the new entry.

---

## Step 5 — Write the new entry

Add the entry at the bottom of the `entries:` list in BOTH files, before the last entry
group (keep the "Testing & Assertions" entry last by convention):

```yaml
  # ── <Concept Group> ─────────────────────────────────────────────────────────
  - english: "<canonical English terms space-separated>"
    patterns:
      - <pl_word>       # PL: <translation>
      - <pl_word2>      # PL: <translation> (inflected form)
      - <de_word>       # DE: <translation>
      - <de_word2>      # DE: <translation> (compound prefix)
```

---

## Step 6 — Test without re-indexing

### Quick smoke test (Python CLI)

```python
# Paste this in a Python REPL or a quick script:
import sys; sys.path.insert(0, "tools/rag")
from query import _expand_query
import yaml

glossary_path = "tools/rag/multilingual-glossary.yaml"
with open(glossary_path, encoding="utf-8") as f:
    raw = yaml.safe_load(f)

entries = [(e["english"], e["patterns"]) for e in raw["entries"]]
test_query = "Entitäts-Bezeichner TypedId Domänenmuster"
print(_expand_query(test_query, entries, repeat=3))
```

Expected output: original query + ` TypedId entity identifier typed domain primitive` repeated 3×.

### Live MCP test

Re-run the failing query via the MCP tool:

```
query_docs("Entitäts-Bezeichner TypedId Domänenmuster", top_k=3)
```

Confirm the target document now appears at #1 with a score > 0.50.

---

## Step 7 — Run the benchmark to check for regressions

```powershell
cd tools/rag
.venv\Scripts\python eval.py
```

The eval suite includes EN / PL / DE queries. Confirm previously passing queries still pass.

---

## What NOT to do

- **Do not add English words to `patterns:`** — the glossary is for non-English only;
  English-only queries already work and must never trigger expansion
- **Do not add single-letter or two-letter words** — they collide with too many contexts
- **Do not skip the .NET copy** — `tools/rag-dotnet/multilingual-glossary.yaml` must be
  updated in the same commit; the two files are independent copies, not symlinks
- **Do not add a new entry when the concept already exists** — extend the existing entry's
  `patterns:` list instead; multiple entries for the same concept produce double expansion
- **Do not use repeat > 3** — it has diminishing returns and can over-suppress the original
  query's semantic signal for mixed-language documents
