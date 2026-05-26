"""Vulnerability / validation probe for RAG MCP + Ingest endpoints.

Runs adversarial inputs against the running SSE servers (Python :3002,
.NET :3001) and reports response code + first-line of body for each probe.

No teardown — assumes docker compose stack is already up.
"""
from __future__ import annotations
import io, json, os, sys, uuid, zipfile, urllib.request, urllib.error, urllib.parse

PY = "http://localhost:3002"
NET = "http://localhost:3001"
COLL_PY = "ecommerceapp_docs"
COLL_NET = "ecommerceapp_docs_dotnet"

RESULTS: list[dict] = []


def _post(url: str, body: bytes, ctype: str, extra_headers: dict | None = None,
          method: str = "POST") -> tuple[int, str]:
    req = urllib.request.Request(url, data=body, method=method,
                                 headers={"Content-Type": ctype, **(extra_headers or {})})
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, (r.read(800).decode("utf-8", "replace") or "")
    except urllib.error.HTTPError as e:
        return e.code, (e.read(800).decode("utf-8", "replace") if e.fp else "")
    except urllib.error.URLError as e:
        return -1, str(e.reason)


def _get(url: str, headers: dict | None = None) -> tuple[int, str]:
    req = urllib.request.Request(url, headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=15) as r:
            return r.status, (r.read(800).decode("utf-8", "replace") or "")
    except urllib.error.HTTPError as e:
        return e.code, (e.read(800).decode("utf-8", "replace") if e.fp else "")
    except urllib.error.URLError as e:
        return -1, str(e.reason)


def _record(server: str, name: str, status: int, body: str, expect: str = ""):
    snippet = (body or "").replace("\n", " ")[:200]
    RESULTS.append({"server": server, "probe": name, "status": status,
                    "expect": expect, "body": snippet})
    flag = "OK " if expect == "any" or expect == "" or str(status).startswith(expect[0]) else "??"
    print(f"  [{server:<6}] {name:<48} HTTP {status:<4} {snippet[:120]}")


def _good_zip() -> bytes:
    """Build a valid minimal batch zip (rag-config + 2 yaml + 1 md)."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("rag-config.yaml",
            "collection: realmcptest\n"
            "embedding_model: sentence-transformers/all-MiniLM-L6-v2\n"
            "include: ['**/*.md']\n"
            "exclude: []\n"
            "config_files:\n"
            "  metadata_rules: metadata-rules.yaml\n"
            "  queries: queries.yaml\n")
        z.writestr("metadata-rules.yaml", "rules: []\n")
        z.writestr("queries.yaml", "queries: []\n")
        z.writestr("docs/adr/9001/9001-x.md", "# ADR-9001\nbody\n")
    return buf.getvalue()


# ---------- Ingest controller probes ----------
def probe_ingest(server: str, base: str, coll: str):
    print(f"\n--- INGEST probes against {server} ({base}) ---")

    # 1. wrong Content-Type (json instead of zip)
    s, b = _post(f"{base}/ingest/{coll}/batch", b'{"x":1}', "application/json")
    _record(server, "batch wrong content-type (json)", s, b, "4")

    # 2. empty body, correct content-type
    s, b = _post(f"{base}/ingest/{coll}/batch", b"", "application/zip")
    _record(server, "batch empty body", s, b, "4")

    # 3. random bytes pretending to be a zip
    s, b = _post(f"{base}/ingest/{coll}/batch", b"PK\x05\x06garbage" * 50, "application/zip")
    _record(server, "batch garbage bytes", s, b, "4")

    # 4. valid zip but no rag-config.yaml
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("docs/x.md", "hello")
    s, b = _post(f"{base}/ingest/{coll}/batch", buf.getvalue(), "application/zip")
    _record(server, "batch missing rag-config.yaml", s, b, "4")

    # 5. valid zip with path traversal entry name
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("rag-config.yaml",
            "collection: realmcptest\nembedding_model: sentence-transformers/all-MiniLM-L6-v2\n"
            "include: ['**/*.md']\nexclude: []\nconfig_files:\n  metadata_rules: metadata-rules.yaml\n  queries: queries.yaml\n")
        z.writestr("metadata-rules.yaml", "rules: []\n")
        z.writestr("queries.yaml", "queries: []\n")
        z.writestr("../../etc/passwd.md", "root:x:0:0:/root:/bin/bash\n")
        z.writestr("..\\..\\windows\\system32\\evil.md", "evil\n")
    s, b = _post(f"{base}/ingest/{coll}/batch", buf.getvalue(), "application/zip")
    _record(server, "batch path traversal entries", s, b, "any")

    # 6. zip-bomb (small zip → huge expansion)
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        z.writestr("rag-config.yaml",
            "collection: realmcptest\nembedding_model: sentence-transformers/all-MiniLM-L6-v2\n"
            "include: ['**/*.md']\nexclude: []\nconfig_files:\n  metadata_rules: metadata-rules.yaml\n  queries: queries.yaml\n")
        z.writestr("metadata-rules.yaml", "rules: []\n")
        z.writestr("queries.yaml", "queries: []\n")
        z.writestr("docs/huge.md", "A" * (20 * 1024 * 1024))  # 20MB highly compressible
    s, b = _post(f"{base}/ingest/{coll}/batch", buf.getvalue(), "application/zip")
    _record(server, "batch zip-bomb (20MB compressible)", s, b, "any")

    # 7. invalid collection names
    for bad in ["BAD-NAME", "_leading", "with space", "with/slash", "../etc",
                "drop;table", "a" * 200, "x"]:
        path = urllib.parse.quote(bad, safe="")
        s, b = _post(f"{base}/ingest/{path}/batch", _good_zip(), "application/zip")
        _record(server, f"batch collection='{bad[:20]}'", s, b, "any")

    # 8. unknown collection /operations
    s, b = _get(f"{base}/ingest/zzzznonexistent/operations")
    _record(server, "operations unknown collection", s, b, "any")

    # 9. malformed operation id
    s, b = _get(f"{base}/ingest/{coll}/operations/{'A' * 500}")
    _record(server, "operations huge opId", s, b, "any")

    # 10. GET on POST endpoint
    s, b = _get(f"{base}/ingest/{coll}/batch")
    _record(server, "batch GET (wrong method)", s, b, "4")

    # 11. legacy per-file route (the broken CLI route)
    s, b = _post(f"{base}/ingest/{coll}",
                 json.dumps({"rel_path":"x.md","content":"hi","chunks":[]}).encode(),
                 "application/json")
    _record(server, "legacy per-file POST /ingest/{coll}", s, b, "any")


# ---------- MCP JSON-RPC probes ----------
def _mcp_init(base: str, coll: str) -> tuple[str, dict]:
    """Initialize and return (session_id, headers_for_subsequent_calls)."""
    body = json.dumps({"jsonrpc":"2.0","id":1,"method":"initialize",
                       "params":{"protocolVersion":"2024-11-05",
                                 "capabilities":{},
                                 "clientInfo":{"name":"vuln-probe","version":"0.1"}}})
    req = urllib.request.Request(f"{base}/?project={coll}", data=body.encode(),
                                 headers={"Content-Type":"application/json",
                                          "Accept":"application/json, text/event-stream"})
    sid = ""
    with urllib.request.urlopen(req, timeout=20) as r:
        sid = r.headers.get("mcp-session-id", "")
        r.read(2000)
    # send notifications/initialized
    n_body = json.dumps({"jsonrpc":"2.0","method":"notifications/initialized"})
    nreq = urllib.request.Request(f"{base}/?project={coll}", data=n_body.encode(),
                                  headers={"Content-Type":"application/json",
                                           "Accept":"application/json, text/event-stream",
                                           "mcp-session-id":sid})
    try:
        urllib.request.urlopen(nreq, timeout=10).read(200)
    except Exception:
        pass
    return sid, {"Content-Type":"application/json",
                 "Accept":"application/json, text/event-stream",
                 "mcp-session-id":sid}


def _mcp_call(base: str, coll: str, headers: dict, name: str, args) -> tuple[int, str]:
    body = json.dumps({"jsonrpc":"2.0","id":str(uuid.uuid4()),"method":"tools/call",
                       "params":{"name":name,"arguments":args}})
    req = urllib.request.Request(f"{base}/?project={coll}", data=body.encode(), headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, r.read(1200).decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, (e.read(1200).decode("utf-8", "replace") if e.fp else "")


def probe_mcp(server: str, base: str, coll: str):
    print(f"\n--- MCP probes against {server} ({base}) ---")
    try:
        sid, h = _mcp_init(base, coll)
    except Exception as e:
        print(f"  [INIT FAIL] {e}")
        return

    cases = [
        ("query_docs missing required", "query_docs", {}),
        ("query_docs empty question", "query_docs", {"question": ""}),
        ("query_docs wrong-type top_k", "query_docs", {"question": "x", "top_k": "abc"}),
        ("query_docs huge top_k=9999", "query_docs", {"question": "x", "top_k": 9999}),
        ("query_docs negative top_k", "query_docs", {"question": "x", "top_k": -1}),
        ("query_docs 100kB question", "query_docs", {"question": "A" * 100_000}),
        ("query_docs sql-ish injection", "query_docs",
            {"question": "'; DROP TABLE points; --"}),
        ("query_docs unknown bc", "query_docs", {"question": "x", "bc": "no-such-bc"}),
        ("read_docs missing required", "read_docs", {}),
        ("get_history missing id", "get_history", {}),
        ("get_history wrong-type id (int)", "get_history", {"id": 1234}),
        ("get_history wrong-type id (array)", "get_history", {"id": ["a", "b"]}),
        ("get_history bogus id", "get_history", {"id": "9999"}),
        ("list_adrs (sanity)", "list_adrs", {}),
        ("unknown tool name", "evil_tool", {"x": 1}),
    ]
    for name, tool, args in cases:
        s, b = _mcp_call(base, coll, h, tool, args)
        snip = b.replace("\n", " ")[:200]
        leak = "traceback" in b.lower() or "/app/" in b or "stack" in b.lower()
        print(f"  [{server:<6}] {name:<42} HTTP {s} leak={leak} {snip[:140]}")
        RESULTS.append({"server": server, "probe": f"mcp:{name}", "status": s,
                        "leak": leak, "body": snip})

    # No-session call
    body = json.dumps({"jsonrpc":"2.0","id":"x","method":"tools/list","params":{}})
    s, b = _post(f"{base}/?project={coll}", body.encode(), "application/json",
                 {"Accept":"application/json, text/event-stream"})
    _record(server, "mcp tools/list without session", s, b, "any")

    # Malformed JSON-RPC
    for bad in [b"not-json", b'{"jsonrpc":"99"}', b'{"method":"x"}']:
        s, b2 = _post(f"{base}/?project={coll}", bad, "application/json",
                      {"Accept":"application/json, text/event-stream",
                       "mcp-session-id": sid})
        _record(server, f"mcp malformed jsonrpc bytes={bad[:20]!r}", s, b2, "any")


def main():
    probe_ingest("python", PY, COLL_PY)
    probe_ingest("dotnet", NET, COLL_NET)
    probe_mcp("python", PY, COLL_PY)
    probe_mcp("dotnet", NET, COLL_NET)

    print("\n" + "=" * 76)
    print(f"DONE — {len(RESULTS)} probes recorded")
    leaks = [r for r in RESULTS if r.get("leak")]
    print(f"  leaks detected: {len(leaks)}")
    for l in leaks:
        print(f"   - {l['server']} {l['probe']} HTTP {l['status']}")

    out = os.path.join(os.path.dirname(__file__), "vuln_probe_results.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(RESULTS, f, indent=2)
    print(f"  written: {out}")


if __name__ == "__main__":
    main()
