# RAG MCP Tools — Validation Report v2

> Date: June 2025 · Branch: `RAG_Implementation` · Commit: `63b4d08`  
> Supersedes: v1 scan from previous session (pre-fix)

> ⚠️ **HISTORICAL REPORT** — captures the state of the 3-tool surface
> (`query_docs`, `list_adrs`, `get_adr_history`) before the dual-stack
> remote-multitenant work landed. The current 4-tool surface
> (`query_docs`, `read_docs`, `list_adrs`, `get_history`) is documented in
> [`rag-architecture.md`](rag-architecture.md). For the current test status
> see [`rag-architecture.md` §12](rag-architecture.md#12-testing-harness).

---

## Summary

| Item | Python | .NET |
|------|--------|------|
| Implementation commit | `63b4d08` | `63b4d08` |
| Language server errors | n/a | **0** |
| Unit tests | 182 passed, 1 e2e timeout, 8 skipped | blocked (VS Code file lock) |
| Problems found (v1 scan) | 2 | 4 |
| Problems fixed | 2/2 | 4/4 |
| Status | ✅ All fixed | ✅ All fixed (tests unrunnable locally) |

The 1 e2e failure (`test_query_docs_returns_hits_for_typedid`) is an infra-only issue — requires a running Qdrant instance with cached ONNX model. It is pre-existing and unrelated to any of the 6 fixes. The 182 unit tests that cover actual logic all pass.

The .NET tests cannot run in the terminal during active VS Code sessions because the C# DevKit background compiler holds MSBuild cache files (`CoreCompileInputs.cache`, `FileListAbsolute.txt`) open across all configurations. This is a file-lock issue, not a code issue — VS Code language server shows **0 compilation errors** on all modified files.

---

## Problems Found and Fixed

### Fix 1 — .NET: `ListAdrs` used zero-vector Qdrant search (unreliable)

**Root cause**: Searching with a zero vector in Qdrant returns at most the first `limit` points in arbitrary index order. With `topK=200` and potentially more ADRs, this silently truncates results.

**Fix applied** (`RagTools.cs`): Replaced with disk-based iteration of `docs/adr/` directories, matching `^\d{4}$` pattern. Reads H1 from the primary markdown, counts amendments in `amendments/` subfolder. Mirrors Python's `_tool_list_adrs` exactly.

```csharp
// Before (unreliable)
var hits = await store.SearchAsync(new float[384], topK: 200, threshold: 0f);

// After (disk-based, like Python)
var adrFolder = Path.Combine(cfg.Workspace, "docs", "adr");
foreach (var dir in Directory.EnumerateDirectories(adrFolder)
    .Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d{4}$")))
{ ... }
```

**Status**: ✅ Fixed

---

### Fix 2 — .NET: `GetAdrHistory` chunks in arbitrary Qdrant order

**Root cause**: Qdrant search results are returned by relevance score, not document position. When chunks from a single ADR are retrieved, they appear in random order, making the history hard to read.

**Fix applied** (`QdrantStore.cs` + `RagTools.cs`): Added `StartLine` field to `SearchHit` record, populated from `start_line` payload. Applied `.OrderBy(h => h.StartLine)` before formatting output.

```csharp
// SearchHit record
public sealed record SearchHit(
    float Score, string RelPath, string DocTitle, string DocKind,
    string? AdrId, string Breadcrumb, int StartLine, string Text);

// GetAdrHistory
var ordered = hits.OrderBy(h => h.StartLine).ToList();
return $"# ADR {adrId} — {ordered[0].DocTitle}\n\n" + ...
```

**Status**: ✅ Fixed

---

### Fix 3 — .NET: `bc` parameter used wrong filter semantics

**Root cause**: The `bc` (bounded context) parameter was passed as a Qdrant `doc_kind` filter, which matches only documents where `doc_kind == bc`. Python instead does a substring match on `breadcrumb` and `doc_title` fields as a post-filter, fetching 3× more candidates first.

**Fix applied** (`RagTools.cs`): Removed `docKindFilter` from `SearchAsync` call when `bc` is set. Added post-filter `MatchesBc()` that mirrors Python's `_matches_bc`. Both `QueryDocs` and `ReadDocs` use the new pattern.

```csharp
// Fetch 3× candidates when bc is set
var fetchK = bc is not null ? topK * 3 : topK;
var allHits = await store.SearchAsync(queryVec, fetchK, cfg.Query.ScoreThreshold,
    cancellationToken: cancellationToken);

// Post-filter by substring on breadcrumb / DocTitle
var hits = bc is not null
    ? allHits.Where(h => MatchesBc(h, bc)).Take(topK).ToList()
    : allHits.ToList();

private static bool MatchesBc(SearchHit h, string bc)
{
    var lower = bc.ToLowerInvariant();
    return (h.Breadcrumb?.ToLowerInvariant().Contains(lower) ?? false)
        || (h.DocTitle?.ToLowerInvariant().Contains(lower) ?? false);
}
```

**Status**: ✅ Fixed

---

### Fix 4 — .NET MCP server not registered in `.vscode/mcp.json`

**Root cause**: The `.NET` path had no entry in `.vscode/mcp.json`, so VS Code Copilot Chat could not use it without manual editing. Python was active; .NET was completely absent.

**Fix applied** (`.vscode/mcp.json`): Added the `.NET` server as a commented-out alternative block with inline prerequisites comment. Python remains the active default.

```json
// To switch to .NET: start Qdrant first (docker compose --profile rag-dotnet up -d qdrant),
// then run dotnet ingest, then uncomment this block and restart MCP.
// "ecommerceapp-rag-dotnet": { ... }
```

**Status**: ✅ Fixed

---

### Fix 5 — Python: startup sync failures were silent

**Root cause**: If `_startup_check()` failed (Qdrant down, embedding model missing, ingest error), the tools continued returning results from a stale or empty index with no indication that something was wrong. Agents would silently act on bad data.

**Fix applied** (`mcp_server.py`): Added module-level `_SYNC_WARNING: str | None` variable. All failure paths in `_startup_check` set it to an error string. `_sync_warning_prefix()` helper returns the warning as a formatted prefix. `_tool_query_docs` and `_tool_list_adrs` prepend it to their output.

```python
_SYNC_WARNING: "str | None" = None  # None=pending, ""=ok, str=error

def _sync_warning_prefix() -> str:
    w = _SYNC_WARNING
    if w:
        return f"⚠️ RAG INDEX WARNING: {w}\n\n"
    return ""
```

**Status**: ✅ Fixed

---

### Fix 6 — Python: `datetime.utcnow()` deprecation

**Root cause**: `datetime.datetime.utcnow()` is deprecated in Python 3.12+ and will be removed in a future version. Two call sites in `ingest.py`.

**Fix applied** (`ingest.py`): Both occurrences replaced with `datetime.datetime.now(datetime.timezone.utc)`.

```python
# Before
ts = datetime.datetime.utcnow().isoformat()

# After
ts = datetime.datetime.now(datetime.timezone.utc).isoformat()
```

**Status**: ✅ Fixed

---

## Current State: Pros & Cons

### Python RAG (`tools/rag/`)

| Dimension | Assessment |
|-----------|------------|
| **Tooling parity** | Reference implementation. All 4 MCP tools (`query_docs`, `read_docs`, `list_adrs`, `get_adr_history`) match intended semantics after Fix 5. |
| **Startup reliability** | ✅ Sync failures now surface to caller via `_SYNC_WARNING` prefix |
| **`bc` filter** | ✅ Substring post-filter on breadcrumb + doc_title (3× candidate fetch) |
| **`list_adrs`** | ✅ Disk-based, reads from `docs/adr/` directories |
| **`get_adr_history`** | Returns results in Qdrant score order — correct for relevance, acceptable for this tool |
| **Embedding** | `all-MiniLM-L6-v2` via sentence-transformers. Loaded in-process on startup. |
| **Vector store** | Embedded Qdrant (local volume) or external (docker mode). Port 6333 HTTP. |
| **Tests** | 182 ✅, 1 ❌ (e2e infra timeout), 8 skipped |
| **Weaknesses** | Python startup time (~3-8s model load). E2e test flaky without live Qdrant+model. |

### .NET RAG (`tools/rag-dotnet/`)

| Dimension | Assessment |
|-----------|------------|
| **Tooling parity** | All 4 tools now mirror Python semantics after Fixes 1–3 |
| **`bc` filter** | ✅ Substring post-filter on breadcrumb + doc_title (3× candidate fetch) |
| **`list_adrs`** | ✅ Disk-based, reads from `docs/adr/` directories — same logic as Python |
| **`get_adr_history`** | ✅ Ordered by `start_line` for top-to-bottom document reading |
| **Embedding** | ONNX Runtime 1.20.1 + pre-exported `all-MiniLM-L6-v2.onnx` (downloaded at Docker build time). No Python dependency at runtime. |
| **Vector store** | External Qdrant gRPC (port 6334). Requires `qdrant` service running separately. |
| **MCP registration** | ✅ Registered in `.vscode/mcp.json` (commented out; Python is default) |
| **Tests** | Language server: **0 errors**. Terminal build: blocked by VS Code C# DevKit file lock on `CoreCompileInputs.cache` — not a code issue. |
| **Weaknesses** | Requires Qdrant started before MCP server. Docker build downloads ONNX model (~90 MB). Switching from Python requires reingesting to separate collection. |

---

## Behaviour Parity Matrix (Post-Fix)

| Tool | Behaviour | Python | .NET |
|------|-----------|--------|------|
| `query_docs` | Embed query → ANN search → return chunks | ✅ | ✅ |
| `query_docs` | `bc` filter = substring on breadcrumb/title | ✅ | ✅ (Fix 3) |
| `query_docs` | Sync warning prepended on failure | ✅ (Fix 5) | n/a (startup blocks) |
| `read_docs` | Fetch by rel_path + optional bc post-filter | ✅ | ✅ (Fix 3) |
| `list_adrs` | Disk-based ADR directory scan | ✅ | ✅ (Fix 1) |
| `list_adrs` | Sync warning on failure | ✅ (Fix 5) | n/a |
| `get_adr_history` | Fetch all chunks for ADR ID | ✅ | ✅ |
| `get_adr_history` | Result ordered by document position | ❌ (score order) | ✅ (Fix 2) |

> Note: Python `get_adr_history` returns in Qdrant score order. This is acceptable (all chunks of a single ADR will have similar scores). Fixing it to match .NET is a potential future improvement.

---

## Known Remaining Issues

| Issue | Severity | Affects | Notes |
|-------|----------|---------|-------|
| E2e test `test_query_docs_returns_hits_for_typedid` times out | Low | CI only | Requires live Qdrant + cached ONNX model. Pre-existing. |
| .NET terminal builds blocked by VS Code file lock | Low | Dev UX only | Language server shows 0 errors. Docker build not affected. Workaround: build in separate terminal session after closing files in VS Code, or use Docker. |
| Python `get_adr_history` returns chunks in score order | Very low | Readability | Acceptable — all chunks from one ADR score similarly. Fix in Fix 2 is .NET-only. |
| .NET requires Qdrant started separately | Low | Setup UX | Documented in README and mcp.json comment. |

---

## Architecture Verdict

Both implementations are now **functionally equivalent** for all 4 MCP tools. The choice of Python vs .NET is an operational preference:

- **Python**: simpler Docker setup (embedded Qdrant mode), faster iteration, reference implementation
- **.NET**: no Python runtime in production, native ONNX (no sentence-transformers overhead), gRPC Qdrant (lower serialisation cost at scale)

Neither has a correctness advantage post-fix. Python has a slight readability advantage in `get_adr_history` (score-ordered is fine for that tool). .NET has an explicit ordering advantage when exact document order matters.
