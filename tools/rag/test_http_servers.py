"""
Live MCP server tests — HTTP (Python, port 3002) + Streamable HTTP (.NET, port 3001).

Usage:
    python test_http_servers.py              # test both servers
    python test_http_servers.py --python     # Python HTTP only
    python test_http_servers.py --dotnet     # .NET Streamable HTTP only
"""
from __future__ import annotations

import argparse
import json
import sys

import httpx

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PYTHON_HTTP_URL = "http://localhost:3002"
DOTNET_HTTP_URL = "http://localhost:3001"

BANNER = "═" * 68


# ── .NET Streamable HTTP helpers ─────────────────────────────────────────────

def _parse_sse_body(text: str) -> dict:
    """Parse the first data: line from an SSE response body."""
    for line in text.splitlines():
        if line.startswith("data:"):
            return json.loads(line[5:].strip())
    raise ValueError(f"No data: line in SSE response: {text[:200]!r}")


def _dotnet_post(client: httpx.Client, body: dict, session_id: str | None = None) -> dict:
    """Send a single MCP JSON-RPC request to the .NET Streamable HTTP server."""
    headers: dict[str, str] = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if session_id:
        headers["mcp-session-id"] = session_id
    r = client.post("/", json=body, headers=headers, timeout=60)
    r.raise_for_status()
    ct = r.headers.get("content-type", "")
    text = r.text or ""
    if "text/event-stream" in ct:
        if not text.strip():
            return {}
        return _parse_sse_body(text)
    if not text.strip():
        return {}
    return json.loads(text)


def _dotnet_initialize(client: httpx.Client) -> str:
    """Perform MCP initialize handshake and return the session ID."""
    body = {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "http-tester", "version": "0.1"},
        },
    }
    r = client.post(
        "/",
        json=body,
        headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"},
        timeout=30,
    )
    r.raise_for_status()
    session_id = r.headers.get("mcp-session-id", "")
    resp = _parse_sse_body(r.text)
    ver = resp.get("result", {}).get("protocolVersion", "?")
    print(f"  Handshake OK  (protocolVersion={ver}, session={session_id})")
    # Send initialized notification before any tools/list or tools/call requests.
    _dotnet_post(client, {
        "jsonrpc": "2.0",
        "method": "notifications/initialized",
        "params": {},
    }, session_id=session_id)
    return session_id


def _dotnet_call_raw(client: httpx.Client, tool: str, args: dict, session_id: str) -> str:
    """Call a .NET tool and return the raw text content (not JSON-parsed)."""
    resp = _dotnet_post(client, {
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }, session_id=session_id)
    return resp.get("result", {}).get("content", [{}])[0].get("text", "")


# ── Python HTTP tests (Streamable HTTP) ─────────────────────────────────────

def _run_python_http_tests(failures: list[str]) -> None:
    print(f"\n{'─' * 68}")
    print("  Python HTTP server  (http://localhost:3002/)")
    print(f"{'─' * 68}")
    try:
        with httpx.Client(base_url=PYTHON_HTTP_URL, timeout=60) as client:
            session_id = _dotnet_initialize(client)

            # tools/list
            resp = _dotnet_post(client, {
                "jsonrpc": "2.0", "id": 10, "method": "tools/list",
            }, session_id=session_id)
            tool_names = [t["name"] for t in resp.get("result", {}).get("tools", [])]
            print(f"  Tools: {tool_names}")
            for expected in ("query_docs", "read_docs", "get_history"):
                if expected in tool_names:
                    print(f"  ✓  {expected}")
                else:
                    print(f"  ✗  {expected} NOT in tools list")
                    failures.append(f"[Python HTTP] tool missing: {expected}")

            # query_docs smoke test
            print("\n  [query_docs — TypedId]")
            text = _dotnet_call_raw(client, "query_docs", {
                "question": "strongly typed entity ID TypedId",
                "top_k": 3,
            }, session_id=session_id)
            try:
                payload = json.loads(text)
            except json.JSONDecodeError:
                payload = None
            hits = payload.get("hits", []) if isinstance(payload, dict) else None
            if isinstance(hits, list):
                print(f"    hits: {len(hits)}")
                print("  ✓  query_docs returned a valid hits list")
            else:
                print("  ✗  query_docs did not return JSON hits payload")
                failures.append("[Python HTTP] query_docs: invalid payload shape")

            # Use live list_adrs data so get_history assertions are corpus-agnostic.
            adrs_text = _dotnet_call_raw(client, "list_adrs", {}, session_id=session_id)
            adrs_payload = json.loads(adrs_text)
            adrs = adrs_payload.get("adrs", [])
            history_id = (adrs[0].get("id") if adrs else "0006")

            # get_history smoke test
            print(f"\n  [get_history — {history_id}]")
            hist_text = _dotnet_call_raw(client, "get_history", {"id": history_id}, session_id=session_id)
            try:
                hist_payload = json.loads(hist_text)
            except json.JSONDecodeError:
                hist_payload = None
            if isinstance(hist_payload, dict) and isinstance(hist_payload.get("chunks", []), list):
                print("  ✓  get_history returned a valid chunk list")
            else:
                print("  ✗  get_history did not return JSON chunk payload")
                failures.append("[Python HTTP] get_history: invalid payload shape")

    except Exception as exc:
        print(f"  ERROR: {exc}")
        failures.append(f"[Python HTTP] exception: {exc}")


# ── .NET Streamable HTTP tests ────────────────────────────────────────────────

def _run_dotnet_tests(failures: list[str]) -> None:
    print(f"\n{'─' * 68}")
    print("  .NET Streamable HTTP server  (http://localhost:3001/)")
    print(f"{'─' * 68}")
    try:
        with httpx.Client(base_url=DOTNET_HTTP_URL, timeout=60) as client:
            session_id = _dotnet_initialize(client)

            # tools/list
            resp = _dotnet_post(client, {
                "jsonrpc": "2.0", "id": 3, "method": "tools/list", "params": {},
            }, session_id=session_id)
            tool_names = [t["name"] for t in resp.get("result", {}).get("tools", [])]
            print(f"  Tools: {tool_names}")
            for expected in ("query_docs", "read_docs", "get_history"):
                if expected in tool_names:
                    print(f"  ✓  {expected}")
                else:
                    print(f"  ✗  {expected} NOT in tools list")
                    failures.append(f"[.NET HTTP] tool missing: {expected}")

            # query_docs smoke test
            print("\n  [query_docs — TypedId]")
            text = _dotnet_call_raw(client, "query_docs", {
                "question": "strongly typed entity ID TypedId",
                "top_k": 3,
            }, session_id=session_id)
            print(f"    response ({len(text)} chars)")
            try:
                payload = json.loads(text)
            except json.JSONDecodeError:
                payload = None
            if isinstance(payload, dict) and isinstance(payload.get("hits", []), list):
                print("  ✓  query_docs returned a valid hits list")
            else:
                print("  ✗  query_docs did not return JSON hits payload")
                failures.append("[.NET HTTP] query_docs: invalid payload shape")

            # Use live list_adrs data so get_history assertions are corpus-agnostic.
            adrs_text = _dotnet_call_raw(client, "list_adrs", {}, session_id=session_id)
            adrs_payload = json.loads(adrs_text)
            adrs = adrs_payload.get("adrs", [])
            history_id = (adrs[0].get("id") if adrs else "0006")

            # get_history smoke test
            print(f"\n  [get_history — {history_id}]")
            hist_text = _dotnet_call_raw(client, "get_history", {"id": history_id}, session_id=session_id)
            try:
                hist_payload = json.loads(hist_text)
            except json.JSONDecodeError:
                hist_payload = None
            if isinstance(hist_payload, dict) and isinstance(hist_payload.get("chunks", []), list):
                print("  ✓  get_history returned a valid chunk list")
            else:
                print("  ✗  get_history did not return JSON chunk payload")
                failures.append("[.NET HTTP] get_history: invalid payload shape")

    except Exception as exc:
        print(f"  ERROR: {exc}")
        failures.append(f"[.NET HTTP] exception: {exc}")


# ── main ──────────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(description="Live MCP HTTP server tests")
    grp = parser.add_mutually_exclusive_group()
    grp.add_argument("--python", action="store_true", help="Python HTTP server only (port 3002)")
    grp.add_argument("--dotnet", action="store_true", help=".NET HTTP server only (port 3001)")
    args = parser.parse_args()

    failures: list[str] = []
    print(f"\n{BANNER}")
    print("  Live MCP Server Tests")
    print(BANNER)

    run_python = not args.dotnet
    run_dotnet = not args.python

    if run_python:
        _run_python_http_tests(failures)

    if run_dotnet:
        _run_dotnet_tests(failures)

    print(f"\n{BANNER}")
    if failures:
        print(f"  RESULT: {len(failures)} FAILURE(S)")
        for f in failures:
            print(f"    ✗ {f}")
        return 1
    print(f"  RESULT: ALL CHECKS PASSED ✓")
    return 0


if __name__ == "__main__":
    sys.exit(main())
