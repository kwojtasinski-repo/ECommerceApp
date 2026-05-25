"""RAG tool handler functions for the MCP server.

Each public ``_tool_*`` coroutine corresponds to one MCP tool.  They read
engine state from ``state`` so that they can be imported by ``mcp_server``
without creating a circular dependency.

Tools:
- ``_tool_query_docs``  — semantic search, returns ranked chunks
- ``_tool_read_docs``   — semantic search grouped by file (chunk or full mode)
- ``_tool_get_history`` — all chunks for a history group (e.g. ADR id)
- ``_tool_list_adrs``   — directory scan of docs/adr/**
"""
from __future__ import annotations

import asyncio
import collections
import json
import re

from mcp.types import TextContent

import state

# ── full-content intent detection ─────────────────────────────────────────────

_FULL_INTENT_RE = re.compile(
    r"\b("
    r"all details|full details|full content|full text|entire|whole file"
    r"|show me all|explain everything|everything about|complete picture"
    r"|all about|deep dive|in full|from start to finish"
    r")\b",
    re.IGNORECASE,
)


def _wants_full_content(question: str) -> bool:
    """Return True when the question words signal the caller wants the whole file, not just chunks."""
    return bool(_FULL_INTENT_RE.search(question))


# ── tool: query_docs ──────────────────────────────────────────────────────────


async def _tool_query_docs(args: dict) -> list[TextContent]:
    question = args["question"]
    bc = args.get("bc")
    top_k = int(args.get("top_k", 5))
    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    question,
                    top_k=top_k,
                    bc_filter=bc,
                    collection=state._session_collection.get(None),
                )
            ),
            timeout=state.TOOL_TIMEOUT,
        )
    except asyncio.TimeoutError:
        return [TextContent(type="text", text=json.dumps({
            "error": (
                f"query_docs timed out after {state.TOOL_TIMEOUT:.0f}s "
                "— the index may be loading or Qdrant is unresponsive."
            )
        }))]
    payload = {
        "query": question,
        "bc_filter": bc,
        "hits": [
            {
                "rel_path": h.rel_path,
                "breadcrumb": h.breadcrumb,
                "lines": f"{h.start_line}-{h.end_line}",
                "score": round(h.final_score, 4),
                "raw_score": round(h.raw_score, 4),
                "weight": h.weight,
                "doc_kind": h.doc_kind,
                "adr_id": h.adr_id,
                "text": h.text,
            }
            for h in hits
        ],
    }
    return [TextContent(type="text", text=json.dumps(payload, indent=2))]


# ── tool: read_docs ───────────────────────────────────────────────────────────


async def _tool_read_docs(args: dict) -> list[TextContent]:
    """Return relevant content for the top-ranked unique files matching the query.

    Strategy (two modes, detected from question text):

    DEFAULT — chunk mode (question does NOT signal full-content intent):
      1. Run vector search with a generous top_k.
      2. Group hits by file, keep all chunks per file sorted by score.
      3. Return the top chunks per file — no disk read, minimal tokens.

    FULL mode — question contains explicit full-content intent phrases
    (e.g. "show me all details", "full content of", "explain everything about"):
      1. Same vector search to rank files.
      2. Read each top-ranked file in full from disk.
      3. Return complete text — caller gets every section including Alternatives/Consequences.
    """
    question = args["question"]
    bc = args.get("bc")
    top_files = min(int(args.get("top_files", 3)), 5)
    full_mode = _wants_full_content(question)

    # Fetch enough chunks globally so that each file gets good per-file coverage.
    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    question,
                    top_k=max(30, top_files * 15),
                    bc_filter=bc,
                    collection=state._session_collection.get(None),
                )
            ),
            timeout=state.TOOL_TIMEOUT,
        )
    except asyncio.TimeoutError:
        return [TextContent(type="text", text=json.dumps({
            "error": (
                f"read_docs timed out after {state.TOOL_TIMEOUT:.0f}s "
                "— the index may be loading or Qdrant is unresponsive."
            )
        }))]

    chunks_per_file: dict[str, list] = collections.defaultdict(list)
    best_score_per_file: dict[str, float] = {}
    for h in hits:
        chunks_per_file[h.rel_path].append(h)
        if h.final_score > best_score_per_file.get(h.rel_path, 0.0):
            best_score_per_file[h.rel_path] = h.final_score

    ranked_files = sorted(best_score_per_file.items(), key=lambda x: x[1], reverse=True)[:top_files]

    files_out = []
    for rel_path, score in ranked_files:
        file_hits = sorted(chunks_per_file[rel_path], key=lambda h: h.final_score, reverse=True)
        if full_mode:
            abs_path = state.CFG.workspace / rel_path
            try:
                content = abs_path.read_text(encoding="utf-8")
            except OSError as exc:
                content = f"[ERROR: could not read file — {exc}]"
            files_out.append({
                "rel_path": rel_path,
                "score": round(score, 4),
                "mode": "full",
                "size_chars": len(content),
                "content": content,
            })
        else:
            chunks_out = [
                {
                    "lines": f"{h.start_line}-{h.end_line}",
                    "score": round(h.final_score, 4),
                    "text": h.text,
                }
                for h in file_hits[:8]
            ]
            files_out.append({
                "rel_path": rel_path,
                "score": round(score, 4),
                "mode": "chunks",
                "chunks_returned": len(chunks_out),
                "chunks": chunks_out,
            })

    payload = {
        "query": question,
        "bc_filter": bc,
        "mode": "full" if full_mode else "chunks",
        "files_returned": len(files_out),
        "files": files_out,
    }
    return [TextContent(type="text", text=json.dumps(payload, indent=2))]


# ── tool: get_history ─────────────────────────────────────────────────────────

_HISTORY_FIELD_DEFAULT = "adr_id"


async def _tool_get_history(args: dict) -> list[TextContent]:
    """Qdrant-based history lookup — collection-agnostic.

    Uses a field_filter on the collection's configured history field (defaults to
    'adr_id' for backward compatibility with existing indexed collections).
    Works in hosted/remote mode (no disk access required).
    """
    history_id = str(args["id"])
    collection = state._session_collection.get(None)

    # Read history_field from the collection config point via the public API.
    history_field = _HISTORY_FIELD_DEFAULT
    try:
        if state.ENGINE is not None:
            cfg_payload = state.ENGINE.get_collection_config(collection)
            history_field = cfg_payload.get("history_field", _HISTORY_FIELD_DEFAULT)
    except Exception:
        pass  # config point not present — use default

    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    f"history {history_id}",
                    top_k=50,
                    fetch_k=200,
                    field_filter=(history_field, history_id),
                    collection=collection,
                )
            ),
            timeout=state.TOOL_TIMEOUT,
        )
    except asyncio.TimeoutError:
        return [TextContent(type="text", text=json.dumps({
            "error": f"get_history timed out after {state.TOOL_TIMEOUT:.0f}s"
        }))]

    if not hits:
        return [TextContent(type="text", text=json.dumps({
            "id": history_id,
            "history_field": history_field,
            "chunk_count": 0,
            "chunks": [],
            "message": f"No chunks found for {history_field}={history_id!r}. Ensure the document is indexed.",
        }))]

    ordered = sorted(hits, key=lambda h: h.start_line)
    result = {
        "id": history_id,
        "history_field": history_field,
        "chunk_count": len(ordered),
        "chunks": [
            {
                "rel_path": h.rel_path,
                "breadcrumb": h.breadcrumb,
                "doc_kind": h.doc_kind,
                "start_line": h.start_line,
                "text": h.text,
            }
            for h in ordered
        ],
    }
    return [TextContent(type="text", text=json.dumps(result, indent=2))]


# ── tool: list_adrs ───────────────────────────────────────────────────────────

_TITLE_RE = re.compile(r"^#\s+(?:ADR-\d+\s*[—:-]\s*)?(.+?)\s*$", re.MULTILINE)


async def _tool_list_adrs(_: dict) -> list[TextContent]:
    adr_folder = state.CFG.workspace / "docs" / "adr"

    def _scan_adrs() -> list[dict]:
        rows = []
        for folder in sorted(adr_folder.iterdir()):
            if not folder.is_dir() or not re.match(r"^\d{4}$", folder.name):
                continue
            adr_id = folder.name
            main_files = sorted(folder.glob(f"{adr_id}-*.md"))
            title = ""
            if main_files:
                text = main_files[0].read_text(encoding="utf-8")
                m = _TITLE_RE.search(text)
                if m:
                    title = m.group(1).strip()
            amendments = (
                sorted((folder / "amendments").glob("*.md"))
                if (folder / "amendments").exists()
                else []
            )
            examples = (
                sorted((folder / "example-implementation").glob("*.md"))
                if (folder / "example-implementation").exists()
                else []
            )
            rows.append({
                "id": adr_id,
                "title": title,
                "main_file": main_files[0].relative_to(state.CFG.workspace).as_posix() if main_files else None,
                "amendments": len(amendments),
                "examples": len(examples),
            })
        return rows

    rows = await asyncio.to_thread(_scan_adrs)
    return [TextContent(type="text", text=json.dumps({"adrs": rows, "count": len(rows)}, indent=2))]
