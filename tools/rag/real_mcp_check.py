"""REAL MCP verification across all transports and both servers.

Three phases, each driving BOTH the Python (rag-tools) and .NET (rag-dotnet)
MCP servers and ALL four MCP tools (list_adrs, query_docs, read_docs, get_history):

Phase A — STDIO via CLI (`docker run -i ... <image>` per server) against the
          existing real ADR data that was ingested into qdrant by prior CLI
          `--force-full` runs (collections: ecommerceapp_docs / _dotnet).

Phase B — HTTP Streamable upload via CLI (`ingest.py --remote` for Python;
          `RagTools.Ingest -- --remote` for .NET) against the running HTTP
          containers on ports 3001 / 3002, followed by HTTP MCP tool calls.

Phase C — HTTP Streamable upload WITHOUT CLI (pure stdlib urllib POST of a
          synthetic ZIP to /ingest/{collection}/batch on each HTTP server),
          followed by HTTP MCP tool calls (stdlib-only) against the test
          collection via the `?project=<coll>` route parameter.

Usage:
    python tools/rag/real_mcp_check.py
"""
from __future__ import annotations
import io, json, os, subprocess, sys, textwrap, threading, time, zipfile
from pathlib import Path

REPO_ROOT  = Path(__file__).resolve().parents[2]
PY_COLL    = "ecommerceapp_docs"
NET_COLL   = "ecommerceapp_docs_dotnet"
TEST_COLL  = "realmcptest"  # must match [a-z0-9][a-z0-9_-]*
PY_PORT    = 3002
NET_PORT   = 3001

DOTNET_REMOTE_ENDPOINTS = [
    "http://rag-dotnet-http:3001",
    "http://host.docker.internal:3001",
]

# ── tiny JSON-RPC over stdio (no MCP SDK) ─────────────────────────────────────

def _enc(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"

def _read_line(stdout, timeout: float = 90.0) -> dict:
    out: dict = {}
    err: dict = {}
    def _w():
        try:
            line = stdout.readline()
            if line:
                out["v"] = json.loads(line)
        except Exception as e:
            err["e"] = e
    t = threading.Thread(target=_w, daemon=True); t.start(); t.join(timeout)
    if t.is_alive():
        raise TimeoutError(f"stdio read timed out after {timeout}s")
    if "e" in err: raise err["e"]
    return out.get("v", {})


def stdio_session_call(image_cmd: list[str], calls: list[tuple[str, dict]]) -> list[dict]:
    """Spawn server via docker run -i, perform initialize handshake, then run all calls."""
    proc = subprocess.Popen(
        image_cmd,
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    )
    results: list[dict] = []
    try:
        proc.stdin.write(_enc({"jsonrpc":"2.0","id":0,"method":"initialize",
            "params":{"protocolVersion":"2024-11-05","capabilities":{},
                      "clientInfo":{"name":"real-check","version":"0.1"}}}))
        proc.stdin.flush(); _read_line(proc.stdout, 120)
        proc.stdin.write(_enc({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}))
        proc.stdin.flush()
        for i, (tool, args) in enumerate(calls, start=1):
            proc.stdin.write(_enc({"jsonrpc":"2.0","id":i,"method":"tools/call",
                "params":{"name":tool,"arguments":args}}))
            proc.stdin.flush()
            resp = _read_line(proc.stdout, 120)
            raw = resp.get("result",{}).get("content",[{}])[0].get("text","{}")
            try: results.append(json.loads(raw))
            except json.JSONDecodeError: results.append({"text": raw, "_parse_error": True})
        return results
    finally:
        try: proc.stdin.close()
        except Exception: pass
        try: proc.terminate(); proc.wait(timeout=5)
        except Exception: proc.kill()


# ── HTTP Streamable client (stdlib only — proves wire-only compat) ────────────

def http_session(port: int, project: str | None = None):
    import urllib.request, urllib.error
    base = f"http://localhost:{port}" + (f"/?project={project}" if project else "")
    headers = {"Content-Type":"application/json",
               "Accept":"application/json, text/event-stream"}
    def post(body: dict, extra: dict | None = None):
        req = urllib.request.Request(
            base, data=json.dumps(body).encode(), method="POST",
            headers={**headers, **(extra or {})})
        return urllib.request.urlopen(req, timeout=90)
    resp = post({"jsonrpc":"2.0","id":1,"method":"initialize",
                 "params":{"protocolVersion":"2024-11-05","capabilities":{},
                           "clientInfo":{"name":"real","version":"0.1"}}})
    sid = resp.headers.get("mcp-session-id",""); resp.read()
    extra = {"mcp-session-id": sid} if sid else {}
    post({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}, extra).read()
    cid = [1]
    def call(tool: str, args: dict) -> dict:
        cid[0] += 1
        r = post({"jsonrpc":"2.0","id":cid[0],"method":"tools/call",
                  "params":{"name":tool,"arguments":args}}, extra)
        text = r.read().decode(); ct = r.headers.get("content-type","")
        body = None
        if "text/event-stream" in ct:
            for line in text.splitlines():
                if line.startswith("data:"):
                    body = json.loads(line[5:].strip()); break
            if body is None: raise RuntimeError("no event-stream data line")
        else:
            body = json.loads(text)
        if "error" in body:
            return {"_jsonrpc_error": body["error"]}
        raw = body.get("result",{}).get("content",[{}])[0].get("text","{}")
        try: return json.loads(raw)
        except json.JSONDecodeError: return {"text": raw, "_parse_error": True}
    return call


# ── tool-set assertions ───────────────────────────────────────────────────────

def assess_tools(label: str, call_fn, project_has_real_data: bool = True) -> dict[str, str]:
    """Call all 4 MCP tools and grade each as OK / WARN / FAIL. Returns per-tool status."""
    status: dict[str, str] = {}
    # 1. list_adrs
    try:
        r = call_fn("list_adrs", {})
        n = len(r.get("adrs", []))
        if project_has_real_data:
            status["list_adrs"] = f"OK   ({n} ADRs)" if n >= 20 else f"WARN ({n} ADRs)"
        else:
            status["list_adrs"] = f"OK   ({n} ADRs)" if n >= 1 else f"FAIL (got {n})"
    except Exception as e:
        status["list_adrs"] = f"FAIL ({type(e).__name__}: {e})"
    # 2. query_docs — use a simple high-frequency token
    probe_q = "TypedId" if project_has_real_data else "TypedId pattern"
    try:
        r = call_fn("query_docs", {"question": probe_q, "top_k": 5})
        n = len(r.get("hits", []))
        status["query_docs"] = f"OK   ({n} hits)" if n >= 1 else f"WARN (0 hits for '{probe_q}')"
    except Exception as e:
        status["query_docs"] = f"FAIL ({type(e).__name__}: {e})"
    # 3. read_docs
    probe_r = "CQRS" if project_has_real_data else "CQRS handler"
    try:
        r = call_fn("read_docs", {"question": probe_r, "top_files": 3})
        n = len(r.get("files", []))
        status["read_docs"] = f"OK   ({n} files)" if n >= 1 else f"WARN (0 files for '{probe_r}')"
    except Exception as e:
        status["read_docs"] = f"FAIL ({type(e).__name__}: {e})"
    # 4. get_history (uses an ADR ID that exists in real data; for test coll use any present id)
    try:
        adr_id = "0001" if project_has_real_data else "9001"
        r = call_fn("get_history", {"id": adr_id})
        chunks = r.get("history") or r.get("chunks") or []
        status["get_history"] = (
            f"OK   ({len(chunks)} chunks)" if len(chunks) >= 1
            else f"WARN ({adr_id}: 0 chunks)"
        )
    except Exception as e:
        status["get_history"] = f"FAIL ({type(e).__name__}: {e})"

    print(f"\n  [{label}]")
    for tool, st in status.items():
        marker = "✅" if st.startswith("OK") else ("⚠️ " if st.startswith("WARN") else "❌")
        print(f"    {marker} {tool:<14} → {st}")
    return status


# ── PHASE A — STDIO MCP tools against real data ───────────────────────────────

def stdio_python_cmd():
    return [
        "docker","run","--rm","-i",
        "--network","ecommerceapp_default",
        "-v",f"{REPO_ROOT}:/workspace:ro",
        "-v",f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/app/metadata-rules.yaml:ro",
        "-v",f"{REPO_ROOT}/tools/rag/queries.yaml:/app/queries.yaml:ro",
        "-e","RAG_WORKSPACE=/workspace","-e","VECTOR_MODE=docker",
        "-e","QDRANT_URL=http://qdrant:6333","-e","PYTHONUNBUFFERED=1",
        "rag-tools","python","mcp_server.py",
    ]

def stdio_dotnet_cmd():
    return [
        "docker","run","--rm","-i",
        "--network","ecommerceapp_default",
        "-v",f"{REPO_ROOT}:/workspace:ro",
        "-v",f"{REPO_ROOT}/tools/rag-dotnet/rag-config.yaml:/rag-config.yaml:ro",
        "-v",f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/metadata-rules.yaml:ro",
        "-v",f"{REPO_ROOT}/tools/rag/queries.yaml:/queries.yaml:ro",
        "-e","RAG_WORKSPACE=/workspace","-e","QDRANT_URL=http://qdrant:6333",
        "-e","RAG_CONFIG=/rag-config.yaml","-e","RAG_COLLECTION=ecommerceapp_docs_dotnet",
        "-e","DOTNET_CLI_TELEMETRY_OPTOUT=1",
        "rag-dotnet","dotnet","/app/mcp/mcp_server.dll",
    ]

def phase_a_stdio():
    print("=" * 76)
    print("PHASE A — STDIO MCP tools (real ingested data)")
    print("=" * 76)
    summary = {}

    # Python STDIO — call all 4 tools in one session
    print("\n[A1] Python rag-tools — STDIO")
    try:
        results = stdio_session_call(stdio_python_cmd(),
            [("list_adrs",{}), ("query_docs",{"question":"TypedId","top_k":5}),
             ("read_docs",{"question":"CQRS","top_files":3}), ("get_history",{"id":"0001"})])
        py_status = _grade_results("python-stdio", results, real=True)
        summary["python-stdio"] = py_status
    except Exception as e:
        print(f"  ❌ python STDIO session crashed: {type(e).__name__}: {e}")
        summary["python-stdio"] = {"_session": "FAIL"}

    # .NET STDIO
    print("\n[A2] .NET rag-dotnet — STDIO")
    try:
        results = stdio_session_call(stdio_dotnet_cmd(),
            [("list_adrs",{}), ("query_docs",{"question":"TypedId","top_k":5}),
             ("read_docs",{"question":"CQRS","top_files":3}), ("get_history",{"id":"0001"})])
        net_status = _grade_results("dotnet-stdio", results, real=True)
        summary["dotnet-stdio"] = net_status
    except Exception as e:
        print(f"  ❌ .NET STDIO session crashed: {type(e).__name__}: {e}")
        summary["dotnet-stdio"] = {"_session": "FAIL"}

    return summary


def _grade_results(label: str, results: list[dict], real: bool) -> dict[str,str]:
    """Grade a list of [list_adrs, query_docs, read_docs, get_history] results."""
    out = {}
    names = ["list_adrs","query_docs","read_docs","get_history"]
    checks = [
        lambda r: (len(r.get("adrs",[])), 20 if real else 1),
        lambda r: (len(r.get("hits",[])), 1),
        lambda r: (len(r.get("files",[])), 1),
        lambda r: (len(r.get("history") or r.get("chunks") or []), 1),
    ]
    probe = ["", "TypedId", "CQRS", "0001"]
    for name, r, check, p in zip(names, results, checks, probe):
        n, expected = check(r)
        st = "OK   " if n >= expected else "WARN "
        suffix = f" '{p}'" if p else ""
        out[name] = f"{st}({n}){suffix}"
    print(f"  [{label}]")
    for n,s in out.items():
        marker = "✅" if s.startswith("OK") else "⚠️ "
        print(f"    {marker} {n:<14} → {s}")
    return out


# ── PHASE B — HTTP Streamable upload via CLI ──────────────────────────────────

def phase_b_cli_upload():
    print("\n" + "=" * 76)
    print("PHASE B — HTTP Streamable upload via CLI (ingest --remote)")
    print("=" * 76)
    summary = {}

    # B1: Python ingest --remote against rag-python-http:3002
    print("\n[B1] python tools/rag/ingest.py --remote http://rag-python-http:3002")
    py_rc, py_tail = _run_cli(
        ["docker","run","--rm",
         "--network","ecommerceapp_default",
         "-v",f"{REPO_ROOT}:/workspace:ro",
         "-v",f"{REPO_ROOT}/.rag:/workspace/.rag",
         "-v",f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/app/metadata-rules.yaml:ro",
         "-v",f"{REPO_ROOT}/tools/rag/queries.yaml:/app/queries.yaml:ro",
         "-e","RAG_WORKSPACE=/workspace",
         "-e","RAG_CONFIG=/workspace/tools/rag/rag-config.yaml",
         "rag-tools","python","ingest.py",
         "--remote","http://rag-python-http:3002","--force-full"])
    summary["python-cli-upload"] = "OK" if py_rc == 0 else f"FAIL (exit {py_rc})"
    print(f"  ↳ exit {py_rc}")
    print(textwrap.indent(py_tail or "(no output)", "    "))

    # B2: .NET RagTools.Ingest --remote with endpoint fallback.
    # Override list when needed:
    #   set RAG_DOTNET_REMOTE_ENDPOINTS=http://a:3001,http://b:3001
    dotnet_endpoints = [e.strip() for e in os.getenv(
        "RAG_DOTNET_REMOTE_ENDPOINTS",
        ",".join(DOTNET_REMOTE_ENDPOINTS),
    ).split(",") if e.strip()]

    print("\n[B2] dotnet RagTools.Ingest --remote (endpoint fallback)")

    def _dotnet_ingest_cmd(endpoint: str) -> list[str]:
        return [
            "docker", "run", "--rm",
            "--network", "ecommerceapp_default",
            "-v", f"{REPO_ROOT}:/workspace:ro",
            "-v", f"{REPO_ROOT}/.rag:/workspace/.rag",
            "-v", f"{REPO_ROOT}/tools/rag-dotnet/rag-config.yaml:/rag-config.yaml:ro",
            "-v", f"{REPO_ROOT}/tools/rag-dotnet/multilingual-glossary.yaml:/multilingual-glossary.yaml:ro",
            "-v", f"{REPO_ROOT}/tools/rag/metadata-rules.yaml:/metadata-rules.yaml:ro",
            "-v", f"{REPO_ROOT}/tools/rag/queries.yaml:/queries.yaml:ro",
            "-e", "RAG_WORKSPACE=/workspace",
            "-e", "RAG_CONFIG=/rag-config.yaml",
            "-e", "RAG_COLLECTION=ecommerceapp_docs_dotnet",
            "-e", "DOTNET_CLI_TELEMETRY_OPTOUT=1",
            "--entrypoint", "dotnet",
            "rag-dotnet", "/app/ingest/ingest.dll",
            "--remote", endpoint, "--force-full",
        ]

    net_rc, net_tail, net_endpoint = _run_cli_with_endpoint_fallback(
        _dotnet_ingest_cmd,
        dotnet_endpoints,
    )
    summary["dotnet-cli-upload"] = "OK" if net_rc == 0 else f"FAIL (exit {net_rc})"
    print(f"  ↳ endpoint {net_endpoint}")
    print(f"  ↳ exit {net_rc}")
    print(textwrap.indent(net_tail or "(no output)", "    "))

    # B3/B4: HTTP MCP tool calls against the production collections (default route)
    print("\n[B3] HTTP MCP tools (python http :3002, default project)")
    summary["python-http-tools"] = assess_tools("python-http",
        http_session(PY_PORT), project_has_real_data=True)
    print("\n[B4] HTTP MCP tools (dotnet http :3001, default project)")
    summary["dotnet-http-tools"] = assess_tools("dotnet-http",
        http_session(NET_PORT), project_has_real_data=True)

    return summary


def _run_cli(cmd: list[str], timeout: float = 600.0) -> tuple[int, str]:
    """Run a CLI command and return (rc, last_20_lines_of_combined_output)."""
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        combined = (p.stdout or "") + (p.stderr or "")
        tail = "\n".join(combined.splitlines()[-15:])
        return p.returncode, tail
    except subprocess.TimeoutExpired as e:
        return 124, f"<timeout after {timeout}s>\n{(e.stdout or '')[-1000:]}\n{(e.stderr or '')[-1000:]}"
    except Exception as e:
        return 99, f"<launch error> {type(e).__name__}: {e}"


def _run_cli_with_endpoint_fallback(
    cmd_factory,
    endpoints: list[str],
    timeout_each: float = 420.0,
) -> tuple[int, str, str]:
    """Try endpoint variants in order and return first success.

    Returns: (rc, tail, endpoint_used)
    """
    attempts: list[str] = []
    last_rc = 99
    last_tail = ""

    for endpoint in endpoints:
        rc, tail = _run_cli(cmd_factory(endpoint), timeout=timeout_each)
        attempts.append(f"[endpoint={endpoint}] rc={rc}\n{tail}")
        last_rc = rc
        last_tail = tail
        if rc == 0:
            return rc, tail, endpoint

    joined = "\n\n".join(attempts[-2:]) if attempts else last_tail
    return last_rc, joined, (endpoints[-1] if endpoints else "")


# ── PHASE C — HTTP Streamable upload WITHOUT CLI (stdlib only) ────────────────

def _build_test_zip() -> bytes:
    """Build a small synthetic batch: rag-config + companions + 2 ADR markdowns."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("rag-config.yaml", textwrap.dedent("""\
            config_files:
              metadata_rules: "metadata-rules.yaml"
              queries:        "queries.yaml"
            """))
        for name in ("metadata-rules.yaml","queries.yaml"):
            src = REPO_ROOT/"tools"/"rag"/name
            if src.exists():
                zf.write(src, arcname=name)
        # Two synthetic ADRs with distinctive content so query_docs has something to hit
        zf.writestr("docs/adr/9001/9001-real-mcp-check-typedid-pattern.md", textwrap.dedent("""\
            # ADR-9001: Real MCP Check synthetic ADR about TypedId pattern

            ## Status
            Accepted

            ## Context
            This is a synthetic ADR injected by tools/rag/real_mcp_check.py to verify
            the HTTP Streamable upload path without using the bundled CLI tools.
            It mentions the TypedId pattern as a unique anchor for query_docs probes.

            ## Decision
            Use TypedId records as primary keys across the domain layer.

            ## Consequences
            Strong typing at compile time, no accidental ID mixing across entities.
            """))
        zf.writestr("docs/adr/9002/9002-real-mcp-check-cqrs-handler.md", textwrap.dedent("""\
            # ADR-9002: Real MCP Check synthetic ADR about CQRS handler pattern

            ## Status
            Accepted

            ## Context
            Synthetic ADR for query_docs verification on the term CQRS handler.
            CQRS separates read and write concerns through dedicated handlers.

            ## Decision
            Each command/query handler is a single-purpose class implementing the
            IRequestHandler abstraction.

            ## Consequences
            Clear single responsibility, simple to test in isolation.
            """))
    return buf.getvalue()


def _stdlib_post_batch(port: int, collection: str, zip_bytes: bytes) -> tuple[int, str]:
    import urllib.request, urllib.error
    url = f"http://localhost:{port}/ingest/{collection}/batch"
    req = urllib.request.Request(url, data=zip_bytes, method="POST",
                                  headers={"Content-Type":"application/zip"})
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            return resp.status, resp.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode()[:500]


def _wait_ops(port: int, collection: str, deadline: float = 240.0) -> tuple[bool, list[dict]]:
    import urllib.request
    end = time.monotonic() + deadline
    terminal = {"completed","succeeded","failed"}
    last: list[dict] = []
    while time.monotonic() < end:
        try:
            with urllib.request.urlopen(
                f"http://localhost:{port}/ingest/{collection}/operations", timeout=10) as r:
                body = json.loads(r.read())
                last = body.get("operations", []) if isinstance(body, dict) else (body if isinstance(body, list) else [])
                if last and all((op.get("status","").lower() in terminal) for op in last):
                    return True, last
        except Exception:
            pass
        time.sleep(2)
    return False, last


def phase_c_raw_upload():
    print("\n" + "=" * 76)
    print("PHASE C — HTTP Streamable upload WITHOUT CLI (stdlib urllib)")
    print("=" * 76)
    summary = {}
    zip_bytes = _build_test_zip()
    print(f"  test ZIP size: {len(zip_bytes)} bytes  → collection: {TEST_COLL}")

    for label, port in [("python-raw-upload", PY_PORT), ("dotnet-raw-upload", NET_PORT)]:
        print(f"\n[C-{label[0:3]}] POST /ingest/{TEST_COLL}/batch → :{port}")
        code, body = _stdlib_post_batch(port, TEST_COLL, zip_bytes)
        print(f"  ↳ HTTP {code}: {body[:200]}")
        if code not in (200, 202):
            summary[label] = f"FAIL (HTTP {code})"
            continue
        ok, ops = _wait_ops(port, TEST_COLL)
        statuses = [op.get("status","?") for op in ops]
        if not ok:
            summary[label] = f"TIMEOUT (statuses={statuses})"
            continue
        failed = [op for op in ops if op.get("status","").lower() == "failed"]
        summary[label] = "OK" if not failed else f"FAIL ({len(failed)} ops failed)"
        print(f"  ↳ {len(ops)} operations  statuses={statuses}")

    # Query the test collection on each server via ?project=
    if summary.get("python-raw-upload") == "OK":
        print(f"\n[C-pyq] HTTP MCP tools (python http, ?project={TEST_COLL})")
        summary["python-raw-tools"] = assess_tools("python-raw",
            http_session(PY_PORT, TEST_COLL), project_has_real_data=False)
    if summary.get("dotnet-raw-upload") == "OK":
        print(f"\n[C-ntq] HTTP MCP tools (dotnet http, ?project={TEST_COLL})")
        summary["dotnet-raw-tools"] = assess_tools("dotnet-raw",
            http_session(NET_PORT, TEST_COLL), project_has_real_data=False)

    return summary


# ── driver ────────────────────────────────────────────────────────────────────

def main() -> int:
    print("=" * 76)
    print(" REAL MCP CHECK — STDIO/CLI + HTTP-via-CLI + HTTP-raw, Python + .NET")
    print("=" * 76)
    a = phase_a_stdio()
    b = phase_b_cli_upload()
    c = phase_c_raw_upload()

    print("\n" + "=" * 76)
    print(" SUMMARY")
    print("=" * 76)
    def _emit(group_name: str, data: dict):
        print(f"\n  {group_name}")
        for k, v in data.items():
            if isinstance(v, dict):
                print(f"    {k}:")
                for tool, st in v.items():
                    marker = "✅" if st.startswith("OK") else ("⚠️ " if st.startswith("WARN") else "❌")
                    print(f"      {marker} {tool:<14} → {st}")
            else:
                marker = "✅" if str(v).startswith("OK") else "❌"
                print(f"    {marker} {k:<24} → {v}")
    _emit("PHASE A (STDIO MCP tools, real data)", a)
    _emit("PHASE B (HTTP via CLI upload + HTTP MCP tools)", b)
    _emit("PHASE C (HTTP raw upload + HTTP MCP tools, test collection)", c)
    print("=" * 76)
    return 0


if __name__ == "__main__":
    sys.exit(main())
