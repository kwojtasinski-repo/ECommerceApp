---
name: rag-eval-coverage
description: >
  Identify files in the corpus that have NO named eval query covering them, and propose
  new queries. Run before parity audits and after significant doc additions. Outputs a
  gap report (file → has-coverage? → suggested query). Complements
  `generate-eval-questions` (this skill finds gaps; that one writes the questions).
argument-hint: "<glob>"
---

# rag-eval-coverage — find files with no eval-query coverage

`tools/rag/queries.yaml` is the source of truth for evaluated retrieval quality.
`compare_queries.py` runs every named query against both servers and writes a parity
audit. But queries.yaml only covers PART of the corpus — many files have no query
that targets them, so their retrieval quality is unmeasured.

This skill walks the corpus, checks each file against every query's expected results,
and flags files with zero coverage.

---

## When to use

- Before a parity audit, to know the coverage denominator.
- After ingesting a new batch of docs (new ADRs, new BCs, new roadmap files).
- When the audit shows a file unexpectedly missing — could be a ranking issue OR could
  be that no query actually targets it.

## When NOT to use

- Coverage is already known (run within the last few days, no new docs since).
- Project doesn't use named queries — `queries.yaml` is empty or absent. (In that case,
  start with [`.github/skills/generate-eval-questions/SKILL.md`](../generate-eval-questions/SKILL.md).)
- For a single file you already know is uncovered — go straight to
  `generate-eval-questions` and write the Q.

---

## Steps

### 1. Enumerate the corpus

Use the glob argument (or default to the full corpus). Cross-platform with
`ctx_execute("sh", ...)`:

```sh
find docs .github/context -type f -name '*.md' | sort > /tmp/corpus-files.txt
wc -l /tmp/corpus-files.txt
```

For Windows hosts without `find`:

```pwsh
Get-ChildItem -Recurse -Path docs,.github/context -Filter *.md |
  Select-Object -ExpandProperty FullName |
  Sort-Object |
  Set-Content /tmp/corpus-files.txt
```

### 2. Enumerate covered files

A file is "covered" if it appears in the `expected` (or top-result) list of any query
in `queries.yaml`. Extract:

```sh
python tools/rag/coverage_dump.py > /tmp/covered-files.txt
```

If `coverage_dump.py` doesn't exist yet (NEW project), write a 20-line equivalent:

```python
# tools/rag/coverage_dump.py
import yaml, sys, pathlib
qs = yaml.safe_load(open('tools/rag/queries.yaml'))
covered = set()
for q in qs.get('queries', []):
    for hit in q.get('expected', []):
        covered.add(hit['path'])
for p in sorted(covered):
    print(p)
```

### 3. Compute the gap

```sh
comm -23 /tmp/corpus-files.txt /tmp/covered-files.txt > /tmp/uncovered.txt
wc -l /tmp/uncovered.txt
```

PowerShell equivalent:

```pwsh
$corpus = Get-Content /tmp/corpus-files.txt
$covered = Get-Content /tmp/covered-files.txt
$corpus | Where-Object { $_ -notin $covered } | Set-Content /tmp/uncovered.txt
```

### 4. Prioritise

Not every uncovered file deserves a query. Filter by relevance:

| File path pattern | Priority |
|---|---|
| `docs/adr/**/*.md` (main ADR file) | High — ADRs are the most-queried content |
| `docs/architecture/*.md` | High — bounded-context map, ownership |
| `docs/roadmap/*.md` | Medium — active work, refresh often |
| `.github/context/*.md` | Medium — known-issues, project-state |
| `docs/adr/*/amendments/*.md` | Low — amendments retrieved via parent ADR |
| `docs/adr/*/README.md` | Skip — routers, not content |
| `docs/playbooks/*.md` | Medium — long-form, multiple queries fit |
| `**/CHANGELOG.md` | Skip — content rotates rapidly, queries go stale |

### 5. Propose queries

For each high/medium priority uncovered file, draft a query. Pattern (matches
`queries.yaml` shape):

```yaml
- id: q-<short-slug>
  question: "<natural-language question whose answer is in this file>"
  expected:
    - path: <relative path to the file>
      hint: <the section the query targets, e.g. "§Decision">
```

Drafting heuristic: read the file's top-level heading + first paragraph; ask
yourself "what would a developer type to get to this content?" Skip queries that are
too vague ("what is X" — matches everything) or too specific (only one chunk in the
whole corpus has the answer — query becomes brittle).

### 6. Write the queries and re-run coverage

Append to `tools/rag/queries.yaml` and:

```sh
python tools/rag/coverage_dump.py > /tmp/covered-files.txt
comm -23 /tmp/corpus-files.txt /tmp/covered-files.txt | wc -l
```

Coverage should drop by the number of files you covered.

### 7. Add to `compare_queries.py`

If the project uses parity tracking, add the new query IDs to the slices in
`compare_queries.py` so the next audit picks them up:

```python
SLICES = {
    "ADR-core": ["q-adr-0027", "q-adr-0028", "q-adr-0029", "q-<your-new-id>"],
    ...
}
```

---

## Common mistakes

- **Writing too-specific queries.** "What is the exact bcrypt round count in §3.2 of
  ADR-0034" matches one chunk in the corpus → query is brittle, breaks on every minor
  edit. Aim for queries that are specific enough to match the target file but vague
  enough to survive content drift.
- **Writing too-vague queries.** "What is the architecture" → top-1 is always
  `agent-decisions.md` or the most-weighted single file. Query is unhelpful for
  coverage. Add a distinguishing keyword from the target file.
- **Adding the query to `queries.yaml` but forgetting `compare_queries.py`.** The
  query exists but the parity audit ignores it → coverage looks better on paper but
  drift goes unmonitored. Always update both.
- **Treating `*/README.md` files as uncovered.** READMEs in ADR folders are routers,
  not content. Suppress them in step 4 prioritisation.
- **Re-running coverage WITHOUT re-running ingest.** The coverage check only reads
  `queries.yaml` vs the filesystem — it does NOT check whether the corpus has been
  ingested. A query targeting a brand-new file passes coverage but the file isn't
  retrievable until `python tools/rag/ingest.py` runs.

---

## Worked example: post-ADR-0030 coverage check

Scenario: ADR-0030 just merged with 3 new files
(`docs/adr/0030/0030-foo.md`, `0030-bar.md`, `amendments/0030-001-baz.md`).

1. `find docs -name '*.md' | wc -l` → 312 files.
2. `python tools/rag/coverage_dump.py | wc -l` → 187 covered.
3. `comm -23` → 125 uncovered.
4. Filter for `docs/adr/0030/**` → 3 files.
5. Skip `0030-baz.md` (amendment, retrieved via parent), prioritise main two.
6. Draft:

   ```yaml
   - id: q-adr-0030-foo
     question: "How does the foo subsystem handle multi-tenant config?"
     expected:
       - path: docs/adr/0030/0030-foo.md
         hint: "§Decision"
   - id: q-adr-0030-bar
     question: "What is the bar pattern for cross-BC events?"
     expected:
       - path: docs/adr/0030/0030-bar.md
         hint: "§Pattern"
   ```

7. Add IDs to `compare_queries.py` "ADR-core" slice.
8. Re-run coverage → 187 + 2 = 189 covered.

---

## Related skills / docs

- [.github/skills/generate-eval-questions/SKILL.md](../generate-eval-questions/SKILL.md) — generative pair to this gap-detection skill
- [.github/skills/tune-rag-weights/SKILL.md](../tune-rag-weights/SKILL.md) — fix ranking once coverage is solid
- [.github/skills/rag-query-debug/SKILL.md](../rag-query-debug/SKILL.md) — when a covered file returns wrong chunk
- [.github/prompts/rag-sync.prompt.md](../../prompts/rag-sync.prompt.md) — full sync cycle invokes this skill
- [tools/rag/queries.yaml](../../../tools/rag/queries.yaml) — eval source of truth
- [tools/rag/compare_queries.py](../../../tools/rag/compare_queries.py) — parity audit driver
- [docs/rag/rag-architecture.md](../../../docs/rag/rag-architecture.md)
