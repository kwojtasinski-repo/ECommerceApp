---
name: rag-query-debug
description: >
  Debug a RAG query that returns wrong, low-quality, or empty results. Walks
  through hypothesis-ordered checks (chunk presence → score gap → weight policy
  → multilingual expansion → server-asymmetry). Uses probe_weights.py and
  compare_queries.py. Never modifies code without confirming a root cause.
argument-hint: '"<question>" [--bc=<filter>]'
---

# RAG query debug

A query returns the wrong file at top-1, low scores across the board, or empty
results. Walk this checklist top-to-bottom; stop at the first concrete finding.

---

## 0. Define what "wrong" means

Before debugging, write down:

- **Question**: exact string the user typed.
- **Expected top-1**: which `rel_path` SHOULD have won, and why (your prior knowledge of the corpus).
- **Actual top-1**: what came back.
- **Server**: Python (:3002) or .NET (:3001), or both.

If the user can't articulate an expected top-1, the query may be genuinely ambiguous — escalate to chunker / metadata-rules review, not query-time tuning.

---

## 1. Is the file even in the corpus?

```powershell
# Python:
docker exec ecommerceapp-rag-python-http-1 curl -s \
  "http://qdrant:6333/collections/ecommerceapp_docs/points/scroll" \
  -H "Content-Type: application/json" \
  -d '{"limit":1,"filter":{"must":[{"key":"rel_path","match":{"value":"<expected-path>"}}]}}'

# .NET:
docker exec ecommerceapp-rag-dotnet-http-1 curl -s \
  "http://qdrant:6333/collections/ecommerceapp_docs_dotnet/points/scroll" \
  -H "Content-Type: application/json" \
  -d '{"limit":1,"filter":{"must":[{"key":"rel_path","match":{"value":"<expected-path>"}}]}}'
```

- **No hits** → file was never ingested. Check `metadata-rules.yaml` for an `exclude:` pattern matching it, or run `python tools/rag/ingest.py` to ingest pending files.
- **Hits returned** → continue to step 2.

---

## 2. What scores does this query actually produce on each server?

```powershell
python tools/rag/probe_weights.py "<question>"
```

Output is side-by-side top-10 from both servers with raw scores AND post-weight scores.

- **All scores < 0.25 on both** → embedder doesn't understand the query. Rephrase, add synonyms, or check whether the language matches the corpus (English vs Polish vs German).
- **Expected file present in top-10 but not top-1** → continue to step 3.
- **Expected file not in top-10 at all** → continue to step 4.

---

## 3. Top-1 mismatch — is it a weight problem?

Look at the post-weight scores in `probe_weights.py` output.

- If the winner has `weight > 1.0` and the expected file has `weight = 1.0`, the winner is being **over-boosted**. Candidates: `agent-decisions.md` (1.20), amendments (1.20).
- If the expected file has `weight < 1.0`, it's being **suppressed**. Check `future-skills.md` (0.80).

**Verify the weight is the cause** by lowering the suspect weight temporarily and re-querying:

```powershell
# Edit tools/rag/rag-config.yaml — set suspect pattern weight to 1.00
# Restart server:
docker compose --profile rag-python-http up -d --force-recreate rag-python-http
# Re-run:
python tools/rag/probe_weights.py "<question>"
```

- **Mismatch resolved** → use `.github/skills/tune-rag-weights/SKILL.md` to make the change properly with rationale.
- **Mismatch unchanged** → the raw cosine score on the wrong-winner is too high for any reasonable weight to fix. See step 5.

---

## 4. Expected file not in top-10

Possible causes:

a. **Chunker split it badly** — the chunk containing the answer is too small / too large / split mid-sentence. Inspect:

   ```powershell
   docker exec ecommerceapp-rag-python-http-1 curl -s \
     "http://qdrant:6333/collections/ecommerceapp_docs/points/scroll" \
     -H "Content-Type: application/json" \
     -d '{"limit":20,"filter":{"must":[{"key":"rel_path","match":{"value":"<expected-path>"}}]},"with_payload":true}' \
     | jq '.result.points[].payload.text[0:120]'
   ```

   Look for chunks that cut off mid-paragraph or contain headings without body. Fix in `rag-config.yaml` `chunker.max_tokens` / `chunker.overlap`, then **`--force-full`** re-index.

b. **Language mismatch** — query is in Polish, corpus chunk is in English (or vice versa). Try expanding the glossary: `.github/skills/expand-rag-glossary/SKILL.md`.

c. **Topic filter excludes it** — if the call uses `bc="<X>"`, the substring match on breadcrumb/title may not match the expected file. Re-query without the filter to confirm.

---

## 5. Both servers — different winners?

If Python and .NET pick different files for the same question, the issue is **asymmetric retrieval**, not weights.

Common patterns:

- **Python over-boosts `.github/context/agent-decisions.md`** when query has past-tense / "we decided" wording.
- **.NET picks canonical ADR** more reliably when query explicitly names an ADR id (e.g. "ADR-0027").
- **.NET indexer bug** — `tech-details-*.md` was misclassified as `adr_main` for ADR sub-folders (fixed 2026-05-28 for the `list_adrs` tool; query retrieval still affected because chunk-level `doc_kind` is the same).

If you're hunting this, run the parity audit:

```powershell
python tools/rag/compare_queries.py
# Read docs/reports/rag-parity-audit-<date>.md for the per-query breakdown.
```

---

## Common dead ends (do not chase)

- **Raising weights above 1.30** — rapidly destabilises ranking on unrelated queries. Stop at 1.30.
- **Lowering weights below 0.70** — hides genuinely relevant content. Stop at 0.80.
- **Re-running `ingest.py` to "fix" a query** — content hashes prevent re-embedding. Use `--force-full` only if you've changed chunker or metadata-rules.
- **Tuning embedder model** — touches every chunk, requires full collection rebuild. Reserved for systematic embedder failures, never for one bad query.

---

## Related skills / docs

- `.github/skills/probe-weights/` (not yet a skill — use `python tools/rag/probe_weights.py "..."` directly)
- `.github/skills/tune-rag-weights/SKILL.md` — applying weight changes properly
- `.github/skills/expand-rag-glossary/SKILL.md` — multilingual query expansion
- `.github/skills/rag-reindex-decision/SKILL.md` — when to re-ingest
- `docs/rag/rag-architecture.md` — pipeline architecture
- `docs/reports/rag-parity-fix-diagnosis-2026-05-28.md` — worked examples of category A / B / C debugging
