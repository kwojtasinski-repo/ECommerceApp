"""
Minimal MCP stdio client for smoke-testing mcp_server.py.

MCP uses stdio transport — no TCP ports are needed.
The server is spawned as a subprocess; stdin/stdout are piped directly.

Usage (local .venv Python — mirrors .vscode/mcp.json, default):
    python test_mcp_client.py

Usage (Docker — mirrors .github/copilot/mcp.json, Codespaces only):
    python test_mcp_client.py --docker

Sends: initialize -> initialized -> tools/list -> tools/call(list_adrs) -> tools/call(query_docs)
Prints each server response as pretty JSON.
"""
from __future__ import annotations

import argparse
import io
import json
import subprocess
import sys
import os
import threading
from pathlib import Path


def _encode(msg: dict) -> bytes:
    """Encode a message as newline-delimited JSON (what mcp.server.stdio expects)."""
    return json.dumps(msg).encode() + b"\n"


def _read_response(stdout) -> dict:
    """Read one newline-terminated JSON message from stdout."""
    line = stdout.readline()
    if not line:
        raise EOFError("Server closed stdout unexpectedly")
    return json.loads(line)


def _read_response_timeout(stdout, timeout: float = 30.0) -> dict:
    """Read a response with a deadline. Raises TimeoutError if nothing arrives."""
    result = {}
    error = {}

    def _worker():
        try:
            result["data"] = _read_response(stdout)
        except Exception as exc:
            error["exc"] = exc

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        raise TimeoutError(f"No response from server after {timeout}s")
    if "exc" in error:
        raise error["exc"]
    return result["data"]


def _build_cmd(docker: bool, workspace: Path, server_py: Path) -> list[str]:
    """Build the command to start the MCP server.

    Docker mode joins the ecommerceapp_default compose network so the MCP server
    can reach the Qdrant container at http://qdrant:6333 (docker mode, HTTP REST).
    No TCP ports are needed for MCP itself — Docker pipes stdin/stdout directly.

    Local mode uses the .venv Python directly (mirrors .vscode/mcp.json).
    query_docs won't work until you run: python ingest.py --mode docker (host)
    or: docker compose --profile rag run rag-tools python ingest.py (container).
    """
    if docker:
        # RAG_WORKSPACE=/workspace is the only path-related knob.
        # The image WORKDIR is /app — scripts run from there, no /workspace/ paths needed.
        # RAG_WORKSPACE drives config derivation: <workspace>/tools/rag/rag-config.yaml.
        return [
            "docker", "run", "--rm", "--interactive",
            "--network", "ecommerceapp_default",   # joins compose network → qdrant:6333 reachable
            "--volume", f"{workspace}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "--env", "VECTOR_MODE=docker",
            "--env", "QDRANT_URL=http://qdrant:6333",
            "rag-tools",
            "python", "mcp_server.py",  # relative to WORKDIR /app — uses baked scripts
        ]
    return [sys.executable, str(server_py)]


def main() -> None:
    parser = argparse.ArgumentParser(description="MCP stdio smoke-test client")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument(
        "--local", dest="docker", action="store_false", default=False,
        help="(default) Spawn server using local .venv Python — mirrors .vscode/mcp.json",
    )
    mode.add_argument(
        "--docker", dest="docker", action="store_true",
        help="Spawn server via Docker — mirrors .github/copilot/mcp.json (Codespaces)",
    )
    args = parser.parse_args()

    workspace = Path(__file__).parent.parent.parent  # repo root
    server_py = Path(__file__).parent / "mcp_server.py"
    cmd = _build_cmd(args.docker, workspace, server_py)

    print(f"[test] Starting MCP server: {' '.join(str(c) for c in cmd)}\n")

    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"

    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
    )

    # Drain stderr in a background thread to prevent pipe-buffer deadlock.
    # On Windows the default pipe buffer is ~4 KB; if the server writes more
    # before we read it, its stderr write blocks and can stall the process.
    stderr_buf = io.BytesIO()

    def _drain_stderr():
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""):
                stderr_buf.write(chunk)
        except Exception:
            pass

    stderr_thread = threading.Thread(target=_drain_stderr, daemon=True)
    stderr_thread.start()

    try:
        # 1 — initialize
        print("[test] Sending initialize...", flush=True)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 1, "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "test-client", "version": "0.1"},
            },
        }))
        proc.stdin.flush()
        try:
            resp = _read_response_timeout(proc.stdout, timeout=30)
        except Exception as exc:
            print(f"\n[test] FAILED at initialize: {type(exc).__name__}: {exc}", flush=True)
            raise
        print("=== initialize response ===")
        print(json.dumps(resp, indent=2))

        # 2 — initialized notification (no response expected)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "method": "notifications/initialized", "params": {},
        }))
        proc.stdin.flush()

        # 3 — tools/list
        print("[test] Sending tools/list...", flush=True)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 2, "method": "tools/list", "params": {},
        }))
        proc.stdin.flush()
        resp = _read_response_timeout(proc.stdout, timeout=15)
        print("\n=== tools/list response ===")
        print(json.dumps(resp, indent=2))

        # 4 — call list_adrs
        print("[test] Sending list_adrs...", flush=True)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 3, "method": "tools/call",
            "params": {"name": "list_adrs", "arguments": {}},
        }))
        proc.stdin.flush()
        resp = _read_response_timeout(proc.stdout, timeout=15)
        print("\n=== tools/call list_adrs response ===")
        # The text field contains the JSON payload — pretty-print it too
        if resp.get("result", {}).get("content"):
            inner = json.loads(resp["result"]["content"][0]["text"])
            resp["result"]["content"][0]["text"] = inner
        print(json.dumps(resp, indent=2))

        # 5 — call query_docs (discovery: which files are relevant?)
        print("[test] Sending query_docs...", flush=True)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 4, "method": "tools/call",
            "params": {
                "name": "query_docs",
                "arguments": {"question": "coupons ADR decision", "top_k": 3},
            },
        }))
        proc.stdin.flush()
        try:
            resp = _read_response_timeout(proc.stdout, timeout=60)
            print("\n=== tools/call query_docs response ===")
            if resp.get("result", {}).get("content"):
                inner = json.loads(resp["result"]["content"][0]["text"])
                # Print just the hit rel_paths and scores — the texts can be huge
                summary = {
                    "query": inner.get("query"),
                    "hits": [{"rel_path": h["rel_path"], "score": h["score"]} for h in inner.get("hits", [])],
                }
                print(json.dumps(summary, indent=2))
        except TimeoutError:
            print(
                "\n[test] query_docs timed out — expected if Qdrant index not yet initialised.\n"
                "[test] Run: python ingest.py  to build the index first.",
                flush=True,
            )

        # 6 — call read_docs (depth: full file content of top relevant files)
        print("[test] Sending read_docs...", flush=True)
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 5, "method": "tools/call",
            "params": {
                "name": "read_docs",
                "arguments": {"question": "coupons ADR decision", "top_files": 2},
            },
        }))
        proc.stdin.flush()
        try:
            resp = _read_response_timeout(proc.stdout, timeout=60)
            print("\n=== tools/call read_docs response ===")
            if resp.get("result", {}).get("content"):
                inner = json.loads(resp["result"]["content"][0]["text"])
                # Print metadata only — full file content would flood the terminal
                summary = {
                    "query": inner.get("query"),
                    "mode": inner.get("mode"),
                    "files_returned": inner.get("files_returned"),
                    "files": [
                        {
                            "rel_path": f["rel_path"],
                            "score": f["score"],
                            # full mode has size_chars; chunk mode has chunks_returned
                            **({
                                "size_chars": f["size_chars"]
                            } if f.get("mode") == "full" else {
                                "chunks_returned": f.get("chunks_returned")
                            }),
                        }
                        for f in inner.get("files", [])
                    ],
                }
                print(json.dumps(summary, indent=2))
        except TimeoutError:
            print("\n[test] read_docs timed out — index not initialised?", flush=True)

    finally:
        proc.stdin.close()
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()
        stderr_thread.join(timeout=5)
        stderr = stderr_buf.getvalue().decode(errors="replace")
        if stderr.strip():
            print("\n=== server stderr ===")
            print(stderr)


if __name__ == "__main__":
    main()
