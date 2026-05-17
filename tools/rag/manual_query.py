"""
Manual console query runner for the MCP RAG server.
Shows exactly what text/description comes back for a question.

  python manual_query.py --docker query_docs  "entity ID pattern"
  python manual_query.py --docker read_docs   "TypedId abstract record"
  python manual_query.py --docker list_adrs
  python manual_query.py --docker get_adr_history 0016

Usage:
  python manual_query.py [--local|--docker] <tool> [question] [extra_arg]
"""
from __future__ import annotations
import argparse, json, os, subprocess, sys, threading
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# ── Transport ─────────────────────────────────────────────────────────────────

def _encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"

def _read(stdout, timeout: float = 60.0) -> dict:
    import threading
    result: dict = {}
    error: dict = {}

    def _worker():
        try:
            line = stdout.readline()
            if line:
                result["data"] = json.loads(line)
        except Exception as e:
            error["err"] = e

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        raise TimeoutError("Server did not respond in time")
    if "err" in error:
        raise error["err"]
    return result.get("data", {})

def _start(docker: bool):
    root = Path(__file__).resolve().parents[2]
    if docker:
        cmd = [
            "docker", "run", "--rm", "--interactive",
            "--network", "ecommerceapp_default",
            "--volume", f"{root}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "--env", "VECTOR_MODE=docker",
            "--env", "QDRANT_URL=http://qdrant:6333",
            "rag-tools", "python", "/workspace/tools/rag/mcp_server.py",
        ]
    else:
        cmd = [sys.executable, str(Path(__file__).parent / "mcp_server.py")]
    import os
    env = os.environ.copy(); env["PYTHONUNBUFFERED"] = "1"
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                             stderr=subprocess.PIPE, env=env)
    import io as _io
    buf = _io.BytesIO()
    def _drain():
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""): buf.write(chunk)
        except Exception: pass
    threading.Thread(target=_drain, daemon=True).start()
    return proc

def _handshake(proc):
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "id": 0, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "manual-query", "version": "0.1"}},
    })); proc.stdin.flush()
    _read(proc.stdout, 30)
    proc.stdin.write(_encode({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}))
    proc.stdin.flush()

_ID = [1]
def _call(proc, tool: str, args: dict) -> dict:
    _ID[0] += 1
    proc.stdin.write(_encode({"jsonrpc":"2.0","id":_ID[0],"method":"tools/call",
                              "params":{"name":tool,"arguments":args}}))
    proc.stdin.flush()
    resp = _read(proc.stdout, 60)
    raw  = resp.get("result",{}).get("content",[{}])[0].get("text","{}")
    return json.loads(raw)


# ── Display helpers ───────────────────────────────────────────────────────────

def _show_query_docs(r: dict):
    hits = r.get("hits", [])
    print(f"\n  query_docs returned {len(hits)} hit(s):\n")
    for i, h in enumerate(hits, 1):
        snippet = h.get("text", "").strip()
        print(f"  ── Hit {i}  (score {h['score']:.3f}) ─────────────────────────────────────")
        print(f"     File   : {h['rel_path']}")
        print(f"     Snippet: {snippet[:300]}")
        print()

def _show_read_docs(r: dict):
    files = r.get("files", [])
    mode = r.get("mode", "?")
    print(f"\n  read_docs returned {len(files)} file(s)  [mode: {mode}]:\n")
    for i, f in enumerate(files, 1):
        print(f"  ── File {i}  (score {f['score']:.3f}) ─────────────────────────────────────")
        print(f"     Path   : {f['rel_path']}")
        if f.get("mode") == "full":
            lines = f["content"].count("\n")
            print(f"     Size   : {f['size_chars']} chars, ~{lines} lines")
            print(f"     FULL CONTENT (this is exactly what the agent receives):")
            print()
            for line in f["content"].splitlines():
                print(f"       {line}")
        else:
            chunks = f.get("chunks", [])
            print(f"     Chunks : {len(chunks)} relevant chunk(s) returned")
            for j, c in enumerate(chunks, 1):
                print(f"\n       ── Chunk {j}  (score {c['score']:.3f}, lines {c['lines']})")
                for line in c["text"].splitlines():
                    print(f"          {line}")
        print()

def _show_list_adrs(r: dict):
    adrs = r.get("adrs", [])
    print(f"\n  list_adrs returned {len(adrs)} ADR(s):\n")
    for a in adrs:
        amendments = f"  [{a.get('amendments',0)} amendment(s)]" if a.get("amendments") else ""
        file_short = a.get("main_file","").replace("docs/adr/","")
        print(f"    {a.get('id','?'):8}  {file_short}{amendments}")

def _show_adr_history(r: dict):
    main = r.get("main")
    amendments = r.get("amendments", [])
    total = 1 + len(amendments) if main else len(amendments)
    print(f"\n  get_adr_history returned main + {len(amendments)} amendment(s):\n")
    docs = []
    if main:
        docs.append(("MAIN", main))
    for i, a in enumerate(amendments, 1):
        docs.append((f"AMENDMENT {i}", a))
    for label, d in docs:
        print(f"  ── {label} ─────────────────────────────────────────────────────────")
        print(f"     Path   : {d.get('rel_path','?')}")
        print(f"     Size   : {d.get('size_chars','?')} chars")
        print(f"     FULL CONTENT (this is exactly what the agent receives):")
        print()
        for line in d.get("content","").splitlines():
            print(f"       {line}")
        print()


# ── Main ──────────────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--docker", action="store_true")
    ap.add_argument("--local",  action="store_true")
    ap.add_argument("tool", choices=["query_docs","read_docs","list_adrs","get_adr_history"])
    ap.add_argument("question", nargs="?", default="")
    ap.add_argument("extra",    nargs="?", default="")
    args = ap.parse_args()

    docker = args.docker
    print(f"\n[manual-query] Starting MCP server ({'Docker' if docker else 'local'})…")
    proc = _start(docker)
    _handshake(proc)
    print(f"[manual-query] Handshake OK — calling {args.tool}('{args.question}')\n")

    try:
        if args.tool == "query_docs":
            r = _call(proc, "query_docs", {"question": args.question, "top_k": 5})
            _show_query_docs(r)

        elif args.tool == "read_docs":
            r = _call(proc, "read_docs", {"question": args.question, "top_files": 2})
            _show_read_docs(r)

        elif args.tool == "list_adrs":
            r = _call(proc, "list_adrs", {})
            _show_list_adrs(r)

        elif args.tool == "get_adr_history":
            adr_id = args.question or args.extra
            r = _call(proc, "get_adr_history", {"adr_id": adr_id})
            _show_adr_history(r)

    except TimeoutError:
        print("  ⚠  Timed out — is the Docker stack running?")
    finally:
        proc.terminate()

if __name__ == "__main__":
    main()
