---
name: rag-collection-rebuild
description: >
  Full procedure for dropping and rebuilding a Qdrant collection from scratch.
  Use when changing embedder dimensions, switching embedder models, recovering from
  index corruption, or migrating to a new collection name. Destructive — confirms
  scope before any drop.
argument-hint: "<collection-name> [--keep-config]"
---

# RAG collection rebuild

A **full rebuild** wipes a Qdrant collection and re-ingests every document from
disk. Use only when an incremental ingest (`python tools/rag/ingest.py`) won't
solve the problem.

> Destructive. Always confirm the target collection name before proceeding. The
> two collections in this repo are `ecommerceapp_docs` (Python) and
> `ecommerceapp_docs_dotnet` (.NET). Dropping the wrong one will require ~10–15
> minutes of re-ingest to recover.

---

## When to use this skill

Use when one or more of these is true:

- `embedder.dim` changed in `rag-config.yaml` (e.g. switching from MiniLM-384 to e5-768).
- `embedder.model` changed and the new model produces different vectors.
- Qdrant collection has a corrupted segment (queries return `vector dimension mismatch` errors).
- You want to migrate to a different collection name (e.g. `ecommerceapp_docs_v2`).
- Per-collection config (ADR-0028 Amendment 002) was added/changed and you want a clean state.

Do **NOT** use this skill when:

- A few files have stale content → use `ingest.py` (incremental).
- `metadata-rules.yaml` changed → use `ingest.py --force-full` (re-embeds without dropping).
- A query returns wrong results → use `rag-query-debug` skill first.

---

## Pre-flight checklist

1. **Confirm collection name** with the user. Repeat back what you're about to drop.
2. **Stop the affected HTTP server** so no queries hit a dropped collection:

   ```powershell
   docker compose stop rag-python-http   # or rag-dotnet-http
   ```

3. **Backup the existing collection** (optional but recommended for one-of-a-kind data):

   ```powershell
   docker exec ecommerceapp-qdrant-1 wget -O /qdrant/storage/snapshots/<coll>-pre-rebuild.snapshot \
     http://localhost:6333/collections/<coll>/snapshots
   ```

4. **Capture current state** for comparison:

   ```powershell
   curl http://localhost:6333/collections/<coll>
   ```

---

## Steps

### 1. Drop the collection

```powershell
curl -X DELETE http://localhost:6333/collections/<coll>
```

Expect HTTP 200 with `{"result": true}`. If you get 404, the collection didn't exist (still safe to proceed).

### 2. Clear the chunk-hash manifest

The ingest pipeline tracks per-file content hashes in `.rag/ingest-state.json`. After a full drop, the manifest must be cleared so all files re-ingest:

```powershell
# For Python ingest:
Remove-Item -ErrorAction SilentlyContinue .rag\ingest-state.json

# For .NET ingest:
Remove-Item -ErrorAction SilentlyContinue .rag\ingest-state-dotnet.json
```

### 3. Re-ingest

```powershell
python tools/rag/ingest.py --force-full
```

For .NET (rare — local `dotnet run` mode):

```powershell
cd tools\rag-dotnet
dotnet run --project src\RagTools.Mcp -- ingest --force-full
```

Watch for `ingested N chunks for M files` summary at the end.

### 4. Restart HTTP server

```powershell
docker compose --profile rag-python-http up -d rag-python-http
# or
docker compose --profile rag-dotnet-http up -d rag-dotnet-http
```

### 5. Verify

```powershell
# Smoke test
curl http://localhost:6333/collections/<coll> | Select-String "points_count"
# Expect a non-zero count, similar to your previous snapshot.

# End-to-end probe
python tools/rag/compare_queries.py
# Expect parity_audit report to be re-generated with similar top-1 distribution.
```

---

## Recovery if something goes wrong

- **HTTP server returns "collection not found" after restart**: collection re-create is automatic on first ingest. Re-run `ingest.py --force-full`.
- **Snapshot restore needed**: copy snapshot back and POST to `http://localhost:6333/collections/<coll>/snapshots/recover`.
- **Re-ingest hangs**: check `docker logs ecommerceapp-qdrant-1` for memory pressure. Restart Qdrant: `docker compose restart qdrant`.

---

## Related skills / docs

- `.github/skills/rag-reindex-decision/SKILL.md` — pre-check whether a rebuild is really necessary
- `.github/skills/diagnose-rag/SKILL.md` — debug a failing RAG server before assuming the collection is broken
- `docs/rag/rag-architecture.md` — full pipeline details
- `docs/adr/0028/0028-remote-multitenant-rag-ingest.md` — collection naming & per-tenant isolation
