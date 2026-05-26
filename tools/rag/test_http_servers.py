"""
Live MCP server tests — HTTP (Python, port 3002) + Streamable HTTP (.NET, port 3001).

Usage:
    python test_http_servers.py              # test both servers
    python test_http_servers.py --python     # Python HTTP only
    python test_http_servers.py --dotnet     # .NET Streamable HTTP only
"""
from __future__ import annotations

import argparse
import asyncio
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
    if "text/event-stream" in ct:
        return _parse_sse_body(r.text)
    return r.json()


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
    # Send initialized notification
    try:
        client.post(
            "/",
            json={"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}},
            headers={"Content-Type": "application/json", "mcp-session-id": session_id},
            timeout=10,
        )
    except Exception:
        pass
    return session_id


def _dotnet_call_raw(client: httpx.Client, tool: str, args: dict, session_id: str) -> str:
    """Call a .NET tool and return the raw text content (not JSON-parsed)."""
    resp = _dotnet_post(client, {
        "jsonrpc": "2.0", "id": 2, "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }, session_id=session_id)
    return resp.get("result", {}).get("content", [{}])[0].get("text", "")


# ── Python HTTP helpers (via mcp.client.sse + mcp.ClientSession) ─────────────

async def _run_python_http_tests(failures: list[str]) -> None:
    from mcp.client.sse import sse_client
    from mcp.client.session import ClientSession
    from mcp.types import InitializeResult

    print(f"\n{'─' * 68}")
    print("  Python HTTP server  (http://localhost:3002/sse)")
    print(f"{'─' * 68}")
    try:
        async with sse_client(f"{PYTHON_HTTP_URL}/sse", timeout=15) as (read, write):
            async with ClientSession(read, write) as session:
                result: InitializeResult = await session.initialize()
                print(f"  Handshake OK  (protocolVersion={result.protocolVersion})")

                # tools/list
                tools = await session.list_tools()
                tool_names = [t.name for t in tools.tools]
                print(f"  Tools: {tool_names}")
                for expected in ("query_docs", "read_docs", "get_adr_history"):
                    if expected in tool_names:
                        print(f"  ✓  {expected}")
                    else:
                        print(f"  ✗  {expected} NOT in tools list")
                        failures.append(f"[Python HTTP] tool missing: {expected}")

                # query_docs smoke test
                print("\n  [query_docs — TypedId]")
                r = await session.call_tool("query_docs", {
                    "question": "strongly typed entity ID TypedId",
                    "top_k": 3,
                })
                raw = r.content[0].text if r.content else "{}"
                result_dict = json.loads(raw)
                hits = result_dict.get("hits", [])
                paths = [h["rel_path"] for h in hits]
                print(f"    hits: {len(hits)}")
                for p in paths:
                    print(f"      {p}")
                adr6 = any("0006" in p for p in paths)
                if adr6:
                    print("  ✓  ADR-0006 surfaced")
                else:
                    print("  ✗  ADR-0006 NOT surfaced")
                    failures.append("[Python HTTP] query_docs: ADR-0006 not in results")

                # get_adr_history smoke test
                print("\n  [get_adr_history — ADR-0006]")
                r2 = await session.call_tool("get_adr_history", {"adr_id": "0006"})
                raw2 = r2.content[0].text if r2.content else "{}"
                hist = json.loads(raw2)
                main_content = hist.get("main", {}).get("content", "")
                if "TypedId" in main_content:
                    print("  ✓  ADR-0006 content contains 'TypedId'")
                else:
                    print("  ✗  ADR-0006 content missing 'TypedId'")
                    failures.append("[Python HTTP] get_adr_history: ADR-0006 content missing 'TypedId'")

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
            for expected in ("query_docs", "read_docs", "get_adr_history"):
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
            adr6 = "0006" in text
            if adr6:
                print("  ✓  ADR-0006 content surfaced")
            else:
                print("  ✗  ADR-0006 NOT in response")
                failures.append("[.NET HTTP] query_docs: ADR-0006 not in results")
            typedid = "TypedId" in text
            if typedid:
                print("  ✓  'TypedId' keyword present")
            else:
                print("  ✗  'TypedId' NOT in response")
                failures.append("[.NET HTTP] query_docs: 'TypedId' not in results")

            # get_adr_history smoke test
            print("\n  [get_adr_history — ADR-0006]")
            hist_text = _dotnet_call_raw(client, "get_adr_history", {"adr_id": "0006"}, session_id=session_id)
            if "TypedId" in hist_text:
                print("  ✓  ADR-0006 content contains 'TypedId'")
            else:
                print("  ✗  ADR-0006 content missing 'TypedId'")
                failures.append("[.NET HTTP] get_adr_history: ADR-0006 content missing 'TypedId'")

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
        asyncio.run(_run_python_http_tests(failures))

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
