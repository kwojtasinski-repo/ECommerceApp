---
agent: agent
description: Full RAG maintenance cycle — checks Qdrant health, runs incremental ingest, validates recall, reviews eval question coverage.
---

# /rag-sync — RAG Maintenance Agent

Execute the following steps **in order**. Report clearly after each step before proceeding to the next. Stop and surface to the user if any step fails.

---

## Step 1 — Check the rag-tools image is available

Run:

```
docker image ls rag-tools --format "{{.Repository}}:{{.Tag}}"
```

- If output is **empty**: stop and tell the user:
  > "The `rag-tools` image has not been built yet. Build it with:
  >
  > ```
  > docker build -t rag-tools tools/rag/
  > ```
  >
  > Then re-run `/rag-sync`."
- If output shows **`rag-tools:latest`**: continue to Step 2.

---

## Step 2 — Report last-indexed state

Read `tools/rag/.cache/manifest.json`. Report:

- `last_indexed` timestamp
- Number of files and chunks recorded in the manifest
- Whether the manifest contains `file_hashes` (new incremental format) or not (old summary-only format)

If the manifest **does not exist**: skip to Step 3 and run a full ingest.

---

## Step 3 — Run incremental ingest

Run:

```
docker run --rm \
  -v "%CD%:/workspace" \
  -v qdrant_data:/data/qdrant \
  -e RAG_WORKSPACE=/workspace \
  -e PYTHONUNBUFFERED=1 \
  rag-tools python ingest.py
```

Wait for completion. Report:

- How many files were re-indexed vs skipped as unchanged
- How long it took
- Any errors printed by the script

---

## Step 4 — Check eval question coverage

Read `tools/rag/eval/questions.json`.

Report:

- Total questions in the file
- Questions with `"reviewed": false` (pending human confirmation) — list each one
- Indexed files (from `file_hashes` in the new manifest) that have **no matching question** in `expect_any` — list up to 10

For each **uncovered file**, invoke the `generate-eval-questions` skill to generate a template question.
Append each generated question to `tools/rag/eval/questions.json` with `"reviewed": false`.

After appending, show the user the newly added questions and ask them to confirm or rewrite each one.
Mark confirmed questions `"reviewed": true`. Remove or rewrite rejected ones.

---

## Step 5 — Run eval

Run:

```
python tools/rag/eval/eval.py
```

Report:

- Total questions tested (only those with `"reviewed": true` or no `reviewed` field)
- Pass count and percentage at recall@5 and recall@8
- Any failures: question text, expected path, what was actually returned (top-3)
- Whether the **80% recall@5 threshold** is met

If recall@5 < 80%: flag this and suggest checking the weights for recently changed files via the `tune-rag-weights` prompt.

---

## Step 6 — Summary

Report a final summary:

- Files re-indexed this run
- New eval questions added (pending user review)
- Recall score vs 80% threshold
- Next action needed (if any): confirm questions / investigate recall failures / adjust weights
