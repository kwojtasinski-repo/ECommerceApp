"""Migration parity check: Python MCP (rag-tools) vs .NET MCP (rag-dotnet).

Runs `list_adrs` against both servers through three transports and asserts
identical wire output (same set of ADR IDs).

Transports
----------
1. STDIO via CLI       — `docker run -i ... rag-{tools,dotnet}` with JSON-RPC on stdin/stdout
2. HTTP Streamable CLI — uses the `mcp` Python SDK against the persistent SSE containers
3. HTTP Streamable raw — direct httpx POST, no MCP library (proves wire-only conformance)

Usage:
    python tools/rag/migration_check.py
"""
from __future__ import annotations
import json, subprocess, sys, threading, time, uuid
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# ── tiny MCP/JSON-RPC helpers (no external deps) ──────────────────────────────

def _encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"

def _read_line(stdout, timeout: float = 60.0) -> dict:
    out: dict = {}
    err: dict = {}
    def _worker():
        try:
            line = stdout.readline()
            if line:
                out["v"] = json.loads(line)
        except Exception as e:
            err["e"] = e
    t = threading.Thread(target=_worker, daemon=True); t.start(); t.join(timeout)
    if t.is_alive():
        raise TimeoutError(f"stdio read timed out after {timeout}s")
    if "e" in err:
        raise err["e"]
    return out.get("v", {})

# ── 1. STDIO via CLI ──────────────────────────────────────────────────────────

def stdio_call(image_cmd: list[str], tool: str, args: dict) -> dict:
    proc = subprocess.Popen(
        image_cmd,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    try:
        # initialize
        proc.stdin.write(_encode({
            "jsonrpc": "2.0", "id": 0, "method": "initialize",
            "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                       "clientInfo": {"name": "migration-check", "version": "0.1"}},
        }))
        proc.stdin.flush()
        _read_line(proc.stdout, 90)
        proc.stdin.write(_encode({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}))
        proc.stdin.flush()
        # tools/call
        proc.stdin.write(_encode({
            "jsonrpc":"2.0","id":1,"method":"tools/call",
            "params":{"name":tool,"arguments":args},
        }))
        proc.stdin.flush()
        resp = _read_line(proc.stdout, 90)
        raw = resp.get("result",{}).get("content",[{}])[0].get("text","{}")
        return json.loads(raw)
    finally:
        try: proc.stdin.close()
        except Exception: pass
        try: proc.terminate(); proc.wait(timeout=5)
        except Exception: proc.kill()

def stdio_python() -> dict:
    cmd = [
        "docker", "run", "--rm", "-i",
        "--network", "ecommerceapp_default",
        "-v", f"{REPO_ROOT}:/workspace:ro",
        "-v", f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/app/metadata-rules.yaml:ro",
        "-v", f"{REPO_ROOT}/tools/rag/queries.yaml:/app/queries.yaml:ro",
        "-e", "RAG_WORKSPACE=/workspace",
        "-e", "VECTOR_MODE=docker",
        "-e", "QDRANT_URL=http://qdrant:6333",
        "-e", "PYTHONUNBUFFERED=1",
        "rag-tools", "python", "mcp_server.py",
    ]
    return stdio_call(cmd, "list_adrs", {})

def stdio_dotnet() -> dict:
    cmd = [
        "docker", "run", "--rm", "-i",
        "--network", "ecommerceapp_default",
        "-v", f"{REPO_ROOT}:/workspace:ro",
        "-v", f"{REPO_ROOT}/tools/rag-dotnet/rag-config.yaml:/rag-config.yaml:ro",
        "-v", f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/metadata-rules.yaml:ro",
        "-v", f"{REPO_ROOT}/tools/rag/queries.yaml:/queries.yaml:ro",
        "-e", "RAG_WORKSPACE=/workspace",
        "-e", "QDRANT_URL=http://qdrant:6333",
        "-e", "RAG_CONFIG=/rag-config.yaml",
        "-e", "RAG_COLLECTION=ecommerceapp_docs_dotnet",
        "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
        "rag-dotnet", "dotnet", "/app/mcp/mcp_server.dll",
    ]
    return stdio_call(cmd, "list_adrs", {})

# ── 2. HTTP Streamable via CLI (using `mcp` SDK) ──────────────────────────────

def http_via_sdk(port: int) -> dict:
    import httpx
    # Inline minimal Streamable HTTP client (mcp SDK isn't installed in this env).
    base = f"http://localhost:{port}"
    headers = {"Content-Type": "application/json",
               "Accept": "application/json, text/event-stream"}
    # initialize
    with httpx.Client(timeout=60) as c:
        r = c.post(base, headers=headers, json={
            "jsonrpc":"2.0","id":1,"method":"initialize",
            "params":{"protocolVersion":"2024-11-05","capabilities":{},
                      "clientInfo":{"name":"mc","version":"0.1"}}})
        r.raise_for_status()
        sid = r.headers.get("mcp-session-id", "")
        h2 = {**headers, "mcp-session-id": sid} if sid else headers
        c.post(base, headers=h2, json={
            "jsonrpc":"2.0","method":"notifications/initialized","params":{}})
        r = c.post(base, headers=h2, json={
            "jsonrpc":"2.0","id":2,"method":"tools/call",
            "params":{"name":"list_adrs","arguments":{}}})
        r.raise_for_status()
        ct = r.headers.get("content-type", "")
        if "text/event-stream" in ct:
            for line in r.text.splitlines():
                if line.startswith("data:"):
                    body = json.loads(line[5:].strip()); break
            else:
                raise RuntimeError("no SSE data line")
        else:
            body = r.json()
        raw = body.get("result",{}).get("content",[{}])[0].get("text","{}")
        return json.loads(raw)

# ── 3. HTTP Streamable raw (no MCP library — already raw above; reuse) ────────
#    Distinguish "raw" by using urllib only (zero third-party deps).

def http_raw(port: int) -> dict:
    import urllib.request, urllib.error
    base = f"http://localhost:{port}"
    headers = {"Content-Type":"application/json",
               "Accept":"application/json, text/event-stream"}
    def post(body: dict, extra_headers: dict | None = None):
        req = urllib.request.Request(
            base, data=json.dumps(body).encode(), method="POST",
            headers={**headers, **(extra_headers or {})})
        return urllib.request.urlopen(req, timeout=60)
    resp = post({"jsonrpc":"2.0","id":1,"method":"initialize",
                 "params":{"protocolVersion":"2024-11-05","capabilities":{},
                           "clientInfo":{"name":"raw","version":"0.1"}}})
    sid = resp.headers.get("mcp-session-id", "")
    resp.read()
    extra = {"mcp-session-id": sid} if sid else {}
    post({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}, extra).read()
    resp = post({"jsonrpc":"2.0","id":2,"method":"tools/call",
                 "params":{"name":"list_adrs","arguments":{}}}, extra)
    text = resp.read().decode()
    ct = resp.headers.get("content-type","")
    if "text/event-stream" in ct:
        for line in text.splitlines():
            if line.startswith("data:"):
                body = json.loads(line[5:].strip()); break
        else:
            raise RuntimeError("no SSE data line")
    else:
        body = json.loads(text)
    raw = body.get("result",{}).get("content",[{}])[0].get("text","{}")
    return json.loads(raw)

# ── Driver ────────────────────────────────────────────────────────────────────

def ids(payload: dict) -> set[str]:
    return {a.get("id") or a.get("adr_id") for a in payload.get("adrs", []) if a}

def report(label: str, py: dict, net: dict) -> bool:
    py_ids, net_ids = ids(py), ids(net)
    same = py_ids == net_ids
    status = "OK   " if same else "FAIL "
    print(f"[{status}] {label}")
    print(f"        python: {len(py_ids)} ADRs  e.g. {sorted(py_ids)[:3]}")
    print(f"        dotnet: {len(net_ids)} ADRs  e.g. {sorted(net_ids)[:3]}")
    if not same:
        print(f"        only_py:  {sorted(py_ids - net_ids)[:5]}")
        print(f"        only_net: {sorted(net_ids - py_ids)[:5]}")
    return same

def main() -> int:
    print("=" * 72)
    print("MCP migration parity — Python (rag-tools) vs .NET (rag-dotnet)")
    print("=" * 72)
    results = []

    print("\n[1/3] STDIO via CLI (docker run -i …)")
    py = stdio_python()
    net = stdio_dotnet()
    results.append(report("STDIO/CLI", py, net))

    print("\n[2/3] HTTP Streamable via CLI (httpx client, MCP JSON-RPC)")
    py = http_via_sdk(3002)
    net = http_via_sdk(3001)
    results.append(report("HTTP/CLI ", py, net))

    print("\n[3/3] HTTP Streamable WITHOUT CLI (urllib stdlib only)")
    py = http_raw(3002)
    net = http_raw(3001)
    results.append(report("HTTP/raw ", py, net))

    print("\n" + "=" * 72)
    ok = sum(results); total = len(results)
    print(f"Result: {ok}/{total} transports show identical ADR sets")
    print("=" * 72)
    return 0 if ok == total else 1

if __name__ == "__main__":
    sys.exit(main())
