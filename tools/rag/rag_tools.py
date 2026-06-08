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
from common import sanitize_text

# ── input caps ────────────────────────────────────────────────────────────────
# Defensive limits to keep tool calls bounded regardless of caller schema.

_MAX_QUESTION_CHARS = 4096
_MAX_TOP_K = 15
_MAX_TOP_FILES = 5
_MAX_HISTORY_ID_CHARS = 128


def _clamp_int(raw, lo: int, hi: int, default: int) -> int:
    try:
        v = int(raw)
    except (TypeError, ValueError):
        return default
    if v < lo:
        return lo
    if v > hi:
        return hi
    return v


def _cap_question(raw) -> str:
    q = "" if raw is None else str(raw)
    if len(q) > _MAX_QUESTION_CHARS:
        return q[:_MAX_QUESTION_CHARS]
    return q

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


# ── per-collection glossary resolver ─────────────────────────────────────────

async def _resolve_glossary_entries(collection: "str | None") -> "tuple | None":
    """Fetch per-collection glossary_entries from the active config source.

    Returns ``None`` when no config source is wired (STDIO / file mode) so
    ``QueryEngine.search`` keeps using its constructor-supplied mounted glossary.
    Returns the per-collection tuple (possibly empty) when a Qdrant-backed source
    is active — overrides the mounted glossary for that search call.
    """
    if state.CONFIG_SOURCE is None:
        return None
    target = collection or (state.CFG.collection if state.CFG is not None else None)
    if target is None:
        return None
    try:
        payload = await state.CONFIG_SOURCE.get_effective(target)
        return payload.glossary_entries
    except Exception:
        return None


# ── tool: query_docs ──────────────────────────────────────────────────────────


async def _tool_query_docs(args: dict) -> list[TextContent]:
    question = _cap_question(args.get("question", ""))
    bc = args.get("bc")
    top_k = _clamp_int(args.get("top_k", 5), 1, _MAX_TOP_K, 5)
    collection = state._session_collection.get(None)
    glossary_entries = await _resolve_glossary_entries(collection)
    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    question,
                    top_k=top_k,
                    bc_filter=bc,
                    collection=collection,
                    glossary_entries=glossary_entries,
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
    question = _cap_question(args.get("question", ""))
    bc = args.get("bc")
    top_files = _clamp_int(args.get("top_files", 3), 1, _MAX_TOP_FILES, 3)
    full_mode = _wants_full_content(question)
    collection = state._session_collection.get(None)
    glossary_entries = await _resolve_glossary_entries(collection)

    # Fetch enough chunks globally so that each file gets good per-file coverage.
    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    question,
                    top_k=max(30, top_files * 15),
                    bc_filter=bc,
                    collection=collection,
                    glossary_entries=glossary_entries,
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
                content = abs_path.read_text(encoding="utf-8-sig", errors="replace")
                content = sanitize_text(content, rel_path)
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


# ── tool: query_docs_cached ───────────────────────────────────────────────────
# Phase 7 L2 — collapses the manual 3-step RAG → context-mode handoff.
# Strategy C (return-and-let-caller-cache): returns formatted markdown + a
# deterministic source label. The caller (agent) makes the follow-up
# ctx_index(content=markdown, source=source) call. No cross-MCP coupling.

import hashlib
from datetime import datetime, timezone

_ADR_ID_RE = re.compile(r"\b(?:adr[-\s]?)?(\d{3,4})\b", re.IGNORECASE)
_SLUG_RE = re.compile(r"[^a-z0-9]+")


def _slugify(text: str, max_len: int = 30) -> str:
    s = _SLUG_RE.sub("-", text.lower()).strip("-")
    return s[:max_len].rstrip("-") or "q"


def _derive_source_label(question: str, scope_attrs: "dict | None") -> str:
    """Deterministic ``rag-cache-...`` label for ctx_index source.

    Rules (in priority order):
      1. Question mentions an ADR id (``ADR-0028`` / ``adr 28`` / ``0028``)
         → ``rag-cache-adr<NNNN>-<hash8>``
      2. ``scope_attrs`` dict has at least one entry → first key-value pair used:
         ``rag-cache-<slug(key)>-<slug(value)>-<hash8>``
      3. Fallback → ``rag-cache-q-<hash8>``

    ``<hash8>`` is the first 8 chars of sha256 of the normalized question.
    Same (question, scope_attrs first key-value) → same label.
    """
    norm = question.strip().lower()
    h8 = hashlib.sha256(norm.encode("utf-8")).hexdigest()[:8]
    m = _ADR_ID_RE.search(question)
    if m:
        adr_id = m.group(1).zfill(4)
        return f"rag-cache-adr{adr_id}-{h8}"
    if scope_attrs:
        first_key, first_value = next(iter(scope_attrs.items()))
        return f"rag-cache-{_slugify(first_key)}-{_slugify(str(first_value))}-{h8}"
    return f"rag-cache-q-{h8}"


def _format_chunks_to_markdown(
    question: str,
    scope_attrs: "dict | None",
    files_out: list[dict],
) -> str:
    """Produce the cache markdown matching the template in
    .github/instructions/mcp-routing.instructions.md
    ("Markdown template for cached content")."""
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d")
    scope_arg = f", scope_attrs={json.dumps(scope_attrs, ensure_ascii=False)}" if scope_attrs else ""
    head_topic = question.strip().rstrip("?.!") or "RAG cache"
    lines: list[str] = [
        f"# {head_topic}",
        "",
        f"> Cached from RAG on {now}. Source: query_docs_cached(\"{question}\"{scope_arg}).",
        "> Refresh: re-run query_docs_cached with the same parameters to overwrite.",
        "",
    ]
    for f in files_out:
        title = f["rel_path"].split("/")[-1]
        lines.append(f"## {title}")
        lines.append("")
        line_range = ""
        for c in f.get("chunks", []):
            line_range = c["lines"]
            break
        if line_range:
            lines.append(f"**Path**: `{f['rel_path']}#L{line_range.replace('-', '-L')}`")
        else:
            lines.append(f"**Path**: `{f['rel_path']}`")
        breadcrumbs = []
        for c in f.get("chunks", []):
            bc_v = c.get("breadcrumb")
            if bc_v and bc_v not in breadcrumbs:
                breadcrumbs.append(bc_v)
        if breadcrumbs:
            lines.append(f"**Breadcrumb**: {breadcrumbs[0]}")
        lines.append("")
        for c in f.get("chunks", []):
            lines.append(c["text"].rstrip())
            lines.append("")
            lines.append("---")
            lines.append("")
    return "\n".join(lines).rstrip() + "\n"


async def _tool_query_docs_cached(args: dict) -> list[TextContent]:
    """Run a RAG search and return formatted markdown ready for ctx_index.

    Returns JSON: ``{source, markdown, files_count, chunks_count, query, scope_attrs}``.
    ``scope_attrs`` is an optional dict of arbitrary key-value filters, e.g.
    ``{"bc": "Sales"}`` or ``{"topic": "Catalog", "region": "PL"}``.
    The first entry is used as the Qdrant breadcrumb/title filter and as the
    slug in the deterministic source label.

    The caller should follow up with::

        ctx_index(content=<markdown>, source=<source>)

    Subsequent recalls use::

        ctx_search(queries=[...], source="rag-cache-...")  # partial prefix match
    """
    question = _cap_question(args.get("question", ""))
    top_files = _clamp_int(args.get("top_files", 3), 1, _MAX_TOP_FILES, 3)

    # Accept an arbitrary dict of scope attributes.  Any non-dict value is silently ignored.
    raw_scope = args.get("scope_attrs")
    scope_attrs: "dict[str, str] | None" = (
        {str(k): str(v) for k, v in raw_scope.items() if v is not None}
        if isinstance(raw_scope, dict) else None
    ) or None

    # Use the first scope value as the Qdrant bc_filter (breadcrumb/title substring).
    bc_filter = next(iter(scope_attrs.values())) if scope_attrs else None

    collection = state._session_collection.get(None)
    glossary_entries = await _resolve_glossary_entries(collection)

    try:
        hits = await asyncio.wait_for(
            asyncio.to_thread(
                lambda: state.ENGINE.search(
                    question,
                    top_k=max(30, top_files * 15),
                    bc_filter=bc_filter,
                    collection=collection,
                    glossary_entries=glossary_entries,
                )
            ),
            timeout=state.TOOL_TIMEOUT,
        )
    except asyncio.TimeoutError:
        return [TextContent(type="text", text=json.dumps({
            "error": (
                f"query_docs_cached timed out after {state.TOOL_TIMEOUT:.0f}s "
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

    files_out: list[dict] = []
    total_chunks = 0
    for rel_path, score in ranked_files:
        file_hits = sorted(chunks_per_file[rel_path], key=lambda h: h.final_score, reverse=True)[:5]
        chunks_out = [
            {
                "lines": f"{h.start_line}-{h.end_line}",
                "score": round(h.final_score, 4),
                "breadcrumb": h.breadcrumb,
                "text": h.text,
            }
            for h in file_hits
        ]
        total_chunks += len(chunks_out)
        files_out.append({
            "rel_path": rel_path,
            "score": round(score, 4),
            "chunks": chunks_out,
        })

    source = _derive_source_label(question, scope_attrs)
    markdown = _format_chunks_to_markdown(question, scope_attrs, files_out)

    payload = {
        "source": source,
        "markdown": markdown,
        "files_count": len(files_out),
        "chunks_count": total_chunks,
        "query": question,
        "scope_attrs": scope_attrs,
        "next_step": (
            f"ctx_index(content=<markdown>, source=\"{source}\"); "
            f"then ctx_search(queries=[...], source=\"{source}\") for recalls."
        ),
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
    history_id = str(args.get("id", ""))[:_MAX_HISTORY_ID_CHARS]
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
                text = main_files[0].read_text(encoding="utf-8-sig", errors="replace")
                text = sanitize_text(text, main_files[0].relative_to(state.CFG.workspace).as_posix())
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
