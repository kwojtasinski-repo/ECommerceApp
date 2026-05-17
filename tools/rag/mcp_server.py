"""MCP server exposing 4 tools backed by the RAG index.

Tools:
- query_docs(question, bc=None, top_k=5)  -> ranked chunks: rel_path + breadcrumb + score + text snippet
- read_docs(question, bc=None, top_files=3) -> relevant chunks grouped by file (default) OR full file content
                                              when query signals full-content intent ("show me all details", etc.)
- get_adr_history(adr_id)                 -> main ADR + amendments in chronological order
- list_adrs()                             -> table of all ADRs (id, title, kind counts)

Typical agent flow:
  1. list_adrs()           — orientation: what exists?
  2. query_docs(question)  — discovery: which files are relevant?
  3. read_docs(question)   — depth: best chunks per file (or full file when "show me all details about...")
  4. get_adr_history(id)   — evolution: how did a specific ADR change over time?

Run (VS Code Copilot will start this automatically via .vscode/mcp.json):
    python tools/rag/mcp_server.py
"""
from __future__ import annotations

import asyncio
import hashlib
import json
import re
import subprocess
import sys
from dataclasses import asdict
from pathlib import Path

from mcp.server import Server
from mcp.server.stdio import stdio_server
from mcp.types import TextContent, Tool

from common import REPO_ROOT, iter_markdown_files, load_config
from query import QueryEngine

CFG = load_config()
ENGINE = QueryEngine(CFG)
SERVER = Server("ecommerceapp-rag")


# ---------------------------- startup auto-sync ----------------------------


def _detect_changed_files(stored_hashes: dict[str, str]) -> list[str]:
    """Return rel_paths of files that are new, changed, or deleted since last ingest."""
    current_rels: set[str] = set()
    changed: list[str] = []
    for path in iter_markdown_files(CFG):
        rel = path.relative_to(REPO_ROOT).as_posix()
        current_rels.add(rel)
        h = hashlib.sha256(path.read_bytes()).hexdigest()
        if stored_hashes.get(rel) != h:
            changed.append(rel)
    for rel in stored_hashes:
        if rel not in current_rels:
            changed.append(rel)
    return changed


def _startup_check() -> None:
    """Auto-sync the Qdrant index on MCP server startup if any docs changed since last ingest."""
    if CFG.vector_mode == "memory":
        return

    if CFG.vector_mode == "local":
        # Embedded Qdrant — no HTTP server to check. Verify the storage path is accessible.
        local_path = Path(CFG.vector_local_path)
        if not local_path.exists():
            print(
                f"[rag-mcp] WARNING: Qdrant local storage path does not exist: {local_path}\n"
                "[rag-mcp] Run: python tools/rag/ingest.py to initialise the index.",
                file=sys.stderr,
            )
            return  # Nothing to sync — storage doesn't exist yet.
    else:
        # docker mode — check Qdrant HTTP server is reachable.
        import urllib.request
        try:
            with urllib.request.urlopen(f"{CFG.vector_url}/", timeout=3) as r:
                if r.status not in (200, 206):
                    raise OSError(f"HTTP {r.status}")
        except Exception as exc:
            print(
                f"[rag-mcp] WARNING: Qdrant not reachable at {CFG.vector_url} ({exc}).\n"
                "[rag-mcp] Start it with: docker start qdrant\n"
                "[rag-mcp] Then run /rag-sync to verify the index.",
                file=sys.stderr,
            )
            return

    manifest_path = CFG.manifest_path
    if not manifest_path.exists():
        print(
            "[rag-mcp] No manifest found — run: python tools/rag/ingest.py --mode docker",
            file=sys.stderr,
        )
        return

    with manifest_path.open("r", encoding="utf-8") as fh:
        data = json.load(fh)

    stored_hashes: dict[str, str] = data.get("file_hashes", {})
    if not stored_hashes:
        return  # old manifest format without hash tracking — skip incremental check

    changed = _detect_changed_files(stored_hashes)
    if not changed:
        last = data.get("last_indexed", "unknown")
        print(f"[rag-mcp] Index up to date (last indexed: {last})", file=sys.stderr)
        return

    print(f"[rag-mcp] {len(changed)} file(s) changed — running incremental ingest...", file=sys.stderr)
    script = Path(__file__).parent / "ingest.py"
    ingest_mode = CFG.vector_mode if CFG.vector_mode in {"local", "docker", "memory"} else "local"
    # IMPORTANT: keep stdout reserved for MCP framed JSON-RPC messages.
    # Any plain-text logs during startup must go to stderr.
    try:
        # Do not stream ingest logs through MCP stderr directly.
        # In console smoke tests stderr may be unread until process exit,
        # which can deadlock the child process on a full pipe buffer.
        result = subprocess.run(
            [sys.executable, str(script), "--mode", ingest_mode],
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=300,
        )
        if result.returncode != 0:
            print(f"[rag-mcp] WARNING: ingest exited with code {result.returncode}", file=sys.stderr)
    except subprocess.TimeoutExpired:
        print("[rag-mcp] WARNING: ingest timed out after 300s; continuing without startup sync", file=sys.stderr)


# ---------------------------- full-content intent detection ----------------------------

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


# ---------------------------- tool: query_docs ----------------------------


@SERVER.list_tools()
async def list_tools() -> list[Tool]:
    return [
        Tool(
            name="query_docs",
            description=(
                "Semantic search across project documentation (ADRs, architecture, patterns, "
                "reference, roadmap). Returns the top-k retrieved chunks with breadcrumb, "
                "file path, line range, weighted score, and text snippet. "
                "Use this for orientation — to discover which files are relevant. "
                "Follow up with read_docs to get full file content. "
                "Use bc to substring-filter by bounded context name (e.g. 'Sales/Orders')."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "bc": {"type": "string", "description": "Optional substring filter on breadcrumb / doc title"},
                    "top_k": {"type": "integer", "default": 5, "minimum": 1, "maximum": 15},
                },
                "required": ["question"],
            },
        ),
        Tool(
            name="read_docs",
            description=(
                "Semantic search that returns the best matching chunks grouped by file. "
                "When the question contains explicit full-content intent phrases "
                "(e.g. 'show me all details about', 'full content of', 'explain everything about', "
                "'entire', 'whole file', 'in full', 'complete picture') the server instead reads "
                "the whole file from disk and returns the complete text. "
                "Use this over query_docs when you need to reason over document context, not "
                "just a single fragment. Use bc to substring-filter by bounded context."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "question": {"type": "string"},
                    "bc": {"type": "string", "description": "Optional substring filter on breadcrumb / doc title"},
                    "top_files": {"type": "integer", "default": 3, "minimum": 1, "maximum": 5,
                                  "description": "Max unique files to return (each may be large)"},
                },
                "required": ["question"],
            },
        ),
        Tool(
            name="get_adr_history",
            description=(
                "Return the main ADR file plus all its amendments in chronological order. "
                "Use when a query is about how a decision evolved over time, or when an "
                "amendment supersedes a section of the original ADR."
            ),
            inputSchema={
                "type": "object",
                "properties": {
                    "adr_id": {"type": "string", "description": "4-digit ADR id, e.g. '0014'"},
                },
                "required": ["adr_id"],
            },
        ),
        Tool(
            name="list_adrs",
            description=(
                "List all ADRs in the repository with id, title, and presence of amendments / "
                "example-implementation files. Use for orientation queries like 'what ADRs exist?'."
            ),
            inputSchema={"type": "object", "properties": {}},
        ),
    ]


@SERVER.call_tool()
async def call_tool(name: str, arguments: dict) -> list[TextContent]:
    if name == "query_docs":
        return await _tool_query_docs(arguments)
    if name == "read_docs":
        return await _tool_read_docs(arguments)
    if name == "get_adr_history":
        return await _tool_get_adr_history(arguments)
    if name == "list_adrs":
        return await _tool_list_adrs(arguments)
    return [TextContent(type="text", text=f"Unknown tool: {name}")]


async def _tool_query_docs(args: dict) -> list[TextContent]:
    question = args["question"]
    bc = args.get("bc")
    top_k = int(args.get("top_k", 5))
    hits = ENGINE.search(question, top_k=top_k, bc_filter=bc)
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


# ---------------------------- tool: read_docs ----------------------------


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
    # Multiplier of 15 ensures that even later-ranked chunks within a file (e.g. the
    # "Alternatives considered" section at the end of an ADR) make the global top-k
    # cutoff when there are competing chunks from other files.
    hits = ENGINE.search(question, top_k=max(30, top_files * 15), bc_filter=bc)

    # Group hits by file, tracking best score and all chunks per file.
    from collections import defaultdict
    chunks_per_file: dict[str, list] = defaultdict(list)
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
            abs_path = REPO_ROOT / rel_path
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
            # Return the top chunks for this file — no disk read.
            chunks_out = [
                {
                    "lines": f"{h.start_line}-{h.end_line}",
                    "score": round(h.final_score, 4),
                    "text": h.text,
                }
                for h in file_hits[:8]  # at most 8 chunks per file
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


# ---------------------------- tool: get_adr_history ----------------------------


_ADR_FOLDER = REPO_ROOT / "docs" / "adr"


async def _tool_get_adr_history(args: dict) -> list[TextContent]:
    adr_id = str(args["adr_id"]).zfill(4)
    folder = _ADR_FOLDER / adr_id
    if not folder.exists():
        return [TextContent(type="text", text=json.dumps({"error": f"ADR {adr_id} not found"}))]

    main_files = sorted(p for p in folder.glob(f"{adr_id}-*.md"))
    main_path = main_files[0] if main_files else None
    amendments_dir = folder / "amendments"
    amendments = []
    if amendments_dir.exists():
        for path in sorted(amendments_dir.glob("*.md")):
            amendments.append({
                "rel_path": path.relative_to(REPO_ROOT).as_posix(),
                "filename": path.name,
                "content": path.read_text(encoding="utf-8"),
            })

    result = {
        "adr_id": adr_id,
        "main": {
            "rel_path": main_path.relative_to(REPO_ROOT).as_posix() if main_path else None,
            "content": main_path.read_text(encoding="utf-8") if main_path else None,
        },
        "amendments": amendments,
        "amendment_count": len(amendments),
    }
    return [TextContent(type="text", text=json.dumps(result, indent=2))]


# ---------------------------- tool: list_adrs ----------------------------


_TITLE_RE = re.compile(r"^#\s+(?:ADR-\d+\s*[—:-]\s*)?(.+?)\s*$", re.MULTILINE)


async def _tool_list_adrs(_: dict) -> list[TextContent]:
    rows = []
    for folder in sorted(_ADR_FOLDER.iterdir()):
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
        amendments = sorted((folder / "amendments").glob("*.md")) if (folder / "amendments").exists() else []
        examples = sorted((folder / "example-implementation").glob("*.md")) if (folder / "example-implementation").exists() else []
        rows.append({
            "id": adr_id,
            "title": title,
            "main_file": main_files[0].relative_to(REPO_ROOT).as_posix() if main_files else None,
            "amendments": len(amendments),
            "examples": len(examples),
        })
    return [TextContent(type="text", text=json.dumps({"adrs": rows, "count": len(rows)}, indent=2))]


# ---------------------------- entrypoint ----------------------------


async def _run() -> None:
    async with stdio_server() as (read, write):
        await SERVER.run(read, write, SERVER.create_initialization_options())


if __name__ == "__main__":
    # Run startup sync in a plain daemon thread BEFORE entering the asyncio loop.
    # Using asyncio.create_task / asyncio.to_thread caused Python 3.14 compatibility
    # issues (CancelledError propagating into the MCP session before initialize completes).
    # A plain thread is simpler and avoids any interaction with the event loop.
    import threading
    threading.Thread(target=_startup_check, daemon=True, name="rag-startup-sync").start()
    asyncio.run(_run())
