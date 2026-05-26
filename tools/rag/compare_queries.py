"""Compare query_docs/read_docs across Python (:3002) and .NET (:3001) SSE servers.

Runs the same 5 specific + 3 generic queries against both servers and writes
.rag/compare_servers.out.txt with side-by-side hit summaries.
"""
from __future__ import annotations
import json, sys, time, urllib.request

QUERIES = [
    ("Q1-spec", "What is the maximum number of coupons per order and where is it configured?"),
    ("Q2-spec", "How does the order placement saga handle compensation when payment fails?"),
    ("Q3-spec", "What are the API purchase limits for trusted vs regular users?"),
    ("Q4-spec", "What are the known issues with FluentAssertions or the .NET 8 upgrade?"),
    ("Q5-spec", "What bounded contexts are currently blocked or in progress in the BC migration?"),
    ("G1-gen",  "How is dependency injection wired across the application?"),
    ("G2-gen",  "What architecture style does the project follow?"),
    ("G3-gen",  "Where are validation rules defined for incoming DTOs?"),
]


def http_session(port: int):
    base = f"http://localhost:{port}"
    headers = {"Content-Type": "application/json",
               "Accept": "application/json, text/event-stream"}

    def _post(body, extra=None):
        req = urllib.request.Request(base, data=json.dumps(body).encode(),
                                     method="POST",
                                     headers={**headers, **(extra or {})})
        return urllib.request.urlopen(req, timeout=120)

    r = _post({"jsonrpc": "2.0", "id": 1, "method": "initialize",
               "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                          "clientInfo": {"name": "cmp", "version": "0.1"}}})
    sid = r.headers.get("mcp-session-id", "")
    r.read()
    extra = {"mcp-session-id": sid} if sid else {}
    _post({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}, extra).read()

    cid = [1]

    def call(tool, args):
        cid[0] += 1
        r = _post({"jsonrpc": "2.0", "id": cid[0], "method": "tools/call",
                   "params": {"name": tool, "arguments": args}}, extra)
        text = r.read().decode()
        ct = r.headers.get("content-type", "")
        body = None
        if "text/event-stream" in ct:
            for line in text.splitlines():
                if line.startswith("data:"):
                    body = json.loads(line[5:].strip())
                    break
        else:
            body = json.loads(text)
        raw = body.get("result", {}).get("content", [{}])[0].get("text", "{}")
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            return {"text": raw, "_parse_error": True}

    return call


def main():
    out_lines = []

    def emit(s=""):
        print(s); out_lines.append(s)

    sessions = {"python(:3002)": http_session(3002),
                "dotnet(:3001)": http_session(3001)}

    for tag, q in QUERIES:
        emit("=" * 100)
        emit(f"[{tag}] {q}")
        emit("=" * 100)
        for label, call in sessions.items():
            t0 = time.monotonic()
            try:
                r = call("query_docs", {"question": q, "top_k": 5})
                dt = (time.monotonic() - t0) * 1000
                hits = r.get("hits", [])
                emit(f"  {label}  query_docs  {dt:6.0f} ms  hits={len(hits)}")
                for i, h in enumerate(hits[:5], 1):
                    src = (h.get("source") or h.get("rel_path") or h.get("doc_key") or "?")
                    score = h.get("score", 0.0)
                    emit(f"    {i}. score={score:.3f}  {src}")
            except Exception as e:
                emit(f"  {label}  query_docs  ERROR {type(e).__name__}: {e}")
        # read_docs only on specific queries
        if tag.endswith("-spec"):
            for label, call in sessions.items():
                t0 = time.monotonic()
                try:
                    r = call("read_docs", {"question": q, "top_files": 2})
                    dt = (time.monotonic() - t0) * 1000
                    files = r.get("files", [])
                    emit(f"  {label}  read_docs   {dt:6.0f} ms  files={len(files)}")
                    for i, f in enumerate(files[:2], 1):
                        emit(f"    {i}. {f.get('rel_path') or f.get('source')}")
                except Exception as e:
                    emit(f"  {label}  read_docs  ERROR {type(e).__name__}: {e}")
        emit("")

    out = "\n".join(out_lines)
    from pathlib import Path
    Path(".rag/compare_servers.out.txt").write_text(out, encoding="utf-8")
    print("\nwritten: .rag/compare_servers.out.txt")


if __name__ == "__main__":
    sys.exit(main())
