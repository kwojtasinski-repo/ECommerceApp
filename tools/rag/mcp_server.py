"""MCP server exposing 3 tools backed by the RAG index.

Tools:
- query_docs(question, bc=None, top_k=5) -> structured hits (rel_path + breadcrumb + line range + score + text)
- get_adr_history(adr_id) -> main ADR + amendments in chronological order
- list_adrs() -> table of all ADRs (id, title, kind counts)

Run (VS Code Copilot will start this automatically via .github/copilot/mcp.json):
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
                "[rag-mcp] It will be created on first ingest. Run /rag-sync to initialise.",
                file=sys.stderr,
            )
            # Do not return — ingest will create the path.
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
    result = subprocess.run([sys.executable, str(script), "--mode", "docker"])
    if result.returncode != 0:
        print(f"[rag-mcp] WARNING: ingest exited with code {result.returncode}", file=sys.stderr)


# ---------------------------- tool: query_docs ----------------------------


@SERVER.list_tools()
async def list_tools() -> list[Tool]:
    return [
        Tool(
            name="query_docs",
            description=(
                "Semantic search across project documentation (ADRs, architecture, patterns, "
                "reference, roadmap). Returns the top-k retrieved chunks with breadcrumb, "
                "file path, line range, weighted score, and text. Use bc to substring-filter "
                "by bounded context name (e.g. 'Sales/Orders')."
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
    _startup_check()
    async with stdio_server() as (read, write):
        await SERVER.run(read, write, SERVER.create_initialization_options())


if __name__ == "__main__":
    asyncio.run(_run())
