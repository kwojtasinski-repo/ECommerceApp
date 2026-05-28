"""Compare query_docs/read_docs across Python (:3002) and .NET (:3001) HTTP servers.

Runs the specific + generic + Sprint-1 + multilingual queries against both servers and
writes both a plain side-by-side output and a structured markdown parity report with
diff summary (files only in one server, top-1 mismatches, score deltas).

Usage (from host, NOT inside rag-tools container):
    python tools/rag/compare_queries.py
"""
from __future__ import annotations
import json, sys, time, urllib.request
from pathlib import Path
from datetime import datetime, timezone

# ---------------------------------------------------------------------------
# Query catalogue
# ---------------------------------------------------------------------------
# Tags:
#   *-spec   — specific question, expected to hit a single canonical doc
#   *-gen    — generic question, lower expectation of parity
#   S1-*     — added in Sprint 1 (queries.yaml +6 entries from 2026-05-28)
#   ML-*     — multilingual sample testing glossary expansion
QUERIES = [
    ("Q1-spec",   "What is the maximum number of coupons per order and where is it configured?"),
    ("Q2-spec",   "How does the order placement saga handle compensation when payment fails?"),
    ("Q3-spec",   "What are the API purchase limits for trusted vs regular users?"),
    ("Q4-spec",   "What are the known issues with FluentAssertions or the .NET 8 upgrade?"),
    ("Q5-spec",   "What bounded contexts are currently blocked or in progress in the BC migration?"),
    ("G1-gen",    "How is dependency injection wired across the application?"),
    ("G2-gen",    "What architecture style does the project follow?"),
    ("G3-gen",    "Where are validation rules defined for incoming DTOs?"),
    # Sprint 1 additions — must map onto queries.yaml entries committed on 2026-05-28
    ("S1-saga",   "How is the order placement saga orchestrated and where are compensations defined?"),
    ("S1-rag",    "How does the RAG pipeline ingest, chunk and rank documents?"),
    ("S1-mt",     "How is multitenant isolation enforced in the remote RAG deployment?"),
    ("S1-ctx",    "How does the context-mode sandbox bootstrap and what hardening flags does it apply?"),
    ("S1-boot",   "What is the bootstrap flow of the context-mode container at startup?"),
    ("S1-cache",  "What is the L3 auto-cache hook and how does it persist RAG responses?"),
    # Multilingual samples — exercise the PL/DE glossary added in Sprint 1
    ("ML-pl-ctx", "Jak działa piaskownica context-mode i jakie ma zabezpieczenia?"),
    ("ML-pl-ref", "Jak są obsługiwane refresh tokeny w IAM?"),
    ("ML-de-ctx", "Wie funktioniert die context-mode Sandbox und welche Härtungsflags hat sie?"),
    ("ML-de-ada", "Wie wird AdGuard für die DNS-Allowlist konfiguriert?"),
    # Q-PRECISE additions (2026-05-28) — designed to expose Python over-boost of
    # agent-decisions.md vs the canonical ADR (Category B mismatch).
    ("QP-0027-chunk",  "ADR-0027 RAG chunking strategy max tokens overlap heading boundaries decision"),
    ("QP-0029-sand",   "ADR-0029 context-mode sandbox runtime Node JavaScript shell allowlist decision"),
    ("QP-0016-coup",   "ADR-0016 coupon maximum per order limit five ten ceiling decision"),
    ("QP-0019-curr",   "ADR-0019 NBP exchange rate currency conversion decision API integration"),
    ("QP-0028-batch",  "ADR-0028 batch manifest ZIP upload pipeline versus direct ingest decision"),
    ("QP-which-rag",   "Which ADR specifically defines the RAG architecture and embedder model choice?"),
    ("QP-which-mt",    "Which ADR specifically governs remote multitenant RAG ingest and per-collection storage?"),
    ("QP-cross-bc",    "What architectural decision record covers cross-bounded-context messaging and event publishing?"),
]


# ---------------------------------------------------------------------------
# MCP HTTP session
# ---------------------------------------------------------------------------
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


def _hit_src(h):
    return (h.get("source") or h.get("rel_path") or h.get("doc_key") or "?")


def _hit_score(h):
    try:
        return float(h.get("score", 0.0))
    except Exception:
        return 0.0


def main():
    out_lines = []
    rows = {}

    def emit(s=""):
        print(s); out_lines.append(s)

    sessions = {"python(:3002)": http_session(3002),
                "dotnet(:3001)": http_session(3001)}

    for tag, q in QUERIES:
        emit("=" * 100)
        emit(f"[{tag}] {q}")
        emit("=" * 100)
        rows[tag] = {"q": q, "python": [], "dotnet": [], "errors": {}}
        for label, call in sessions.items():
            short = "python" if "python" in label else "dotnet"
            t0 = time.monotonic()
            try:
                r = call("query_docs", {"question": q, "top_k": 5})
                dt = (time.monotonic() - t0) * 1000
                hits = r.get("hits", [])
                emit(f"  {label}  query_docs  {dt:6.0f} ms  hits={len(hits)}")
                for i, h in enumerate(hits[:5], 1):
                    emit(f"    {i}. score={_hit_score(h):.3f}  {_hit_src(h)}")
                rows[tag][short] = [{"src": _hit_src(h), "score": _hit_score(h)} for h in hits[:5]]
            except Exception as e:
                emit(f"  {label}  query_docs  ERROR {type(e).__name__}: {e}")
                rows[tag]["errors"][short] = f"{type(e).__name__}: {e}"
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

    # Plain text output
    out = "\n".join(out_lines)
    rag_dir = Path(".rag"); rag_dir.mkdir(parents=True, exist_ok=True)
    (rag_dir / "compare_servers.out.txt").write_text(out, encoding="utf-8")
    print("\nwritten: .rag/compare_servers.out.txt")

    # Markdown parity report (Sprint 2 B1 deliverable)
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    md = []
    md.append("# RAG parity audit — Python (:3002) vs .NET (:3001)")
    md.append("")
    md.append(f"Generated: {ts}  ")
    md.append(f"Queries: {len(QUERIES)} ({sum(1 for t,_ in QUERIES if t.endswith('-spec'))} specific, "
              f"{sum(1 for t,_ in QUERIES if t.endswith('-gen'))} generic, "
              f"{sum(1 for t,_ in QUERIES if t.startswith('S1-'))} Sprint-1, "
              f"{sum(1 for t,_ in QUERIES if t.startswith('ML-'))} multilingual)")
    md.append("")
    md.append("Source script: `tools/rag/compare_queries.py`. Run from host, not inside "
              "`rag-tools` container (script targets host loopback `localhost:3001/3002`).")
    md.append("")

    md.append("## Summary")
    md.append("")
    top1_match = 0
    top1_mismatch = 0
    only_python = 0
    only_dotnet = 0
    errors = 0
    score_deltas = []
    for tag, r in rows.items():
        if r["errors"]:
            errors += 1
            continue
        py = r["python"]; dn = r["dotnet"]
        if not py or not dn:
            continue
        if py[0]["src"] == dn[0]["src"]:
            top1_match += 1
        else:
            top1_mismatch += 1
        score_deltas.append(abs(py[0]["score"] - dn[0]["score"]))
        py_paths = {h["src"] for h in py}
        dn_paths = {h["src"] for h in dn}
        only_python += len(py_paths - dn_paths)
        only_dotnet += len(dn_paths - py_paths)
    avg_delta = (sum(score_deltas) / len(score_deltas)) if score_deltas else 0.0
    md.append(f"- Total queries: **{len(rows)}**")
    md.append(f"- Top-1 path match: **{top1_match}**")
    md.append(f"- Top-1 mismatch: **{top1_mismatch}**")
    md.append(f"- Queries with errors: **{errors}**")
    md.append(f"- Files only in Python top-5 (sum across queries): **{only_python}**")
    md.append(f"- Files only in .NET top-5 (sum across queries): **{only_dotnet}**")
    md.append(f"- Avg |score delta| at top-1: **{avg_delta:.3f}**")
    md.append("")

    md.append("## Top-1 mismatches")
    md.append("")
    md.append("| Tag | Question | Python top-1 | .NET top-1 |")
    md.append("|---|---|---|---|")
    for tag, r in rows.items():
        if r["errors"] or not r["python"] or not r["dotnet"]:
            continue
        if r["python"][0]["src"] == r["dotnet"][0]["src"]:
            continue
        q = r["q"].replace("|", "\\|")
        py = r["python"][0]; dn = r["dotnet"][0]
        md.append(f"| `{tag}` | {q} | `{py['src']}` ({py['score']:.3f}) | `{dn['src']}` ({dn['score']:.3f}) |")
    md.append("")

    md.append("## Per-query detail")
    md.append("")
    for tag, r in rows.items():
        md.append(f"### `{tag}` — {r['q']}")
        md.append("")
        if r["errors"]:
            for k, v in r["errors"].items():
                md.append(f"- **{k} error**: `{v}`")
            md.append("")
            continue
        md.append("| # | Python (:3002) | score | .NET (:3001) | score |")
        md.append("|---|---|---|---|---|")
        for i in range(max(len(r["python"]), len(r["dotnet"]))):
            py = r["python"][i] if i < len(r["python"]) else None
            dn = r["dotnet"][i] if i < len(r["dotnet"]) else None
            py_s = f"`{py['src']}`" if py else ""
            py_sc = f"{py['score']:.3f}" if py else ""
            dn_s = f"`{dn['src']}`" if dn else ""
            dn_sc = f"{dn['score']:.3f}" if dn else ""
            md.append(f"| {i+1} | {py_s} | {py_sc} | {dn_s} | {dn_sc} |")
        md.append("")

    report_path = Path("docs/reports/rag-parity-audit-2026-05-28.md")
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(md), encoding="utf-8")
    print(f"written: {report_path.as_posix()}")


if __name__ == "__main__":
    sys.exit(main())
