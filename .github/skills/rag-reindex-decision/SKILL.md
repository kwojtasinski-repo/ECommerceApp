---
name: rag-reindex-decision
description: >
  Decide whether a RAG configuration change requires re-indexing (full or incremental)
  or is query-time only. Saves expensive index rebuilds for changes that don't need them.
  Use BEFORE editing rag-config.yaml, metadata-rules.yaml, multilingual-glossary.yaml,
  queries.yaml, or chunker / embedder settings.
argument-hint: "<file-path-being-changed>"
---

# RAG re-index decision matrix

The RAG index lives in Qdrant collections (`ecommerceapp_docs` for Python,
`ecommerceapp_docs_dotnet` for .NET). Some configuration changes take effect at query
time only (no rebuild). Others demand an incremental ingest. A few require a full
rebuild because they change the embedding space or chunk boundaries.

**Always run this skill BEFORE touching anything under `tools/rag/` or `tools/rag-dotnet/`.**

---

## Decision matrix

| Change                                                                     | Type            | Command                                          |
| -------------------------------------------------------------------------- | --------------- | ------------------------------------------------ |
| `multilingual-glossary.yaml` — add/remove an entry                         | ❌ Query-time   | None — restart `rag-*-http` to reload            |
| `rag-config.yaml` — `ranking.weights` change                               | ❌ Query-time   | None — restart `rag-*-http` to reload            |
| `rag-config.yaml` — `query.fetch_k` change                                 | ❌ Query-time   | None — restart `rag-*-http` to reload            |
| `queries.yaml` — add/edit/remove a named query                             | ❌ Not at ingest | None — used only by eval scripts                |
| Any file in `docs/`, `.github/context/`, etc. (content edit)               | ✅ Incremental   | `python tools/rag/ingest.py`                     |
| `metadata-rules.yaml` — add/remove glob, change `kind` value               | ✅ Force-full    | `python tools/rag/ingest.py --force-full`        |
| `rag-config.yaml` — `chunker.max_tokens` / `chunker.overlap` change        | ✅ Force-full    | `python tools/rag/ingest.py --force-full`        |
| `rag-config.yaml` — `embedder.model` change                                | ✅ Force-full    | `python tools/rag/ingest.py --force-full`        |
| `rag-config.yaml` — `embedder.dim` (rare; together with model)             | ✅ Force-full + collection drop | drop collection in Qdrant, then `--force-full` |
| `.NET` config (`tools/rag-dotnet/rag-config.yaml`) — same categories apply | (mirror)        | rebuild `rag-dotnet-http` image                  |

---

## Steps

1. **Identify the file being changed.** If it lives under `tools/rag/` or `tools/rag-dotnet/`, continue. Otherwise this skill doesn't apply.
2. **Look up the change in the matrix above.** If your change type isn't listed, default to **`--force-full`** to be safe.
3. **For query-time changes**: edit the file, then restart the affected HTTP server:

   ```powershell
   docker compose --profile rag-python-http up -d --force-recreate rag-python-http
   # or
   docker compose --profile rag-dotnet-http up -d --force-recreate rag-dotnet-http
   ```

   For local `dotnet run` of the .NET server, just restart the process — config is loaded at startup.

4. **For incremental ingest** (`docs/` or `.github/context/` content edits):

   ```powershell
   python tools/rag/ingest.py
   ```

   This re-ingests only files whose content hash changed.

5. **For force-full**:

   ```powershell
   python tools/rag/ingest.py --force-full
   ```

   Wipes the chunk-hash manifest and re-embeds every file. Takes ~5–15 min depending on corpus size.

6. **Verify** with the affected eval slice:

   ```powershell
   python tools/rag/compare_queries.py
   ```

   Should write `.rag/compare_servers.out.txt` and `docs/reports/rag-parity-audit-<date>.md`.

---

## Common mistakes

- **Editing `metadata-rules.yaml` and skipping `--force-full`**. The change updates `doc_kind` classification, but existing chunks keep their old `doc_kind` until re-embedded. Symptom: filters by `doc_kind` return stale results.
- **Editing `chunker.*` and skipping `--force-full`**. New chunk boundaries are not applied to existing files. Symptom: parts of the corpus return shorter / longer chunks than expected.
- **Editing `multilingual-glossary.yaml` and running `--force-full`**. Pointless — glossary is query-time only. Costs ~10 min of compute for zero benefit.
- **Editing `tools/rag/multilingual-glossary.yaml` but forgetting to mirror to `tools/rag-dotnet/multilingual-glossary.yaml`**. The .NET local `dotnet run` reads the local copy; the mounted HTTP container reads the canonical one. Drift causes asymmetric multilingual behaviour. See `docs/reports/rag-parity-fix-diagnosis-2026-05-28.md` §R3.

---

## Related skills / docs

- `.github/skills/rag-collection-rebuild/SKILL.md` — full collection-drop procedure (for `embedder.dim` change)
- `.github/skills/tune-rag-weights/SKILL.md` — query-time weight tuning
- `.github/skills/expand-rag-glossary/SKILL.md` — adding multilingual entries
- `docs/rag/rag-architecture.md` — full pipeline architecture
- `.github/instructions/rag.instructions.md` — operational rules
