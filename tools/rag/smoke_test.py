"""Quick MCP smoke test — start server, handshake, one tool call."""
import json, os, subprocess, sys, threading, time

PYTHON = ".venv/Scripts/python.exe"
SERVER = "mcp_server.py"

print("Starting MCP server...", flush=True)
p = subprocess.Popen(
    [PYTHON, SERVER],
    stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
    env={**os.environ, "PYTHONUNBUFFERED": "1",
         "VECTOR_MODE": "docker", "QDRANT_URL": "http://localhost:6333"},
)

stderr_chunks = []

def _drain():
    for chunk in iter(lambda: p.stderr.read1(4096), b""):
        text = chunk.decode("utf-8", errors="replace")
        stderr_chunks.append(text)
        print("[stderr]", text.rstrip(), flush=True)

threading.Thread(target=_drain, daemon=True).start()

# Wait up to 60s for "embedding model ready"
deadline = time.time() + 60
while time.time() < deadline:
    combined = "".join(stderr_chunks)
    if "embedding model ready" in combined:
        print("[OK] Model loaded — sending handshake", flush=True)
        break
    if p.poll() is not None:
        print("[FAIL] Server exited unexpectedly", flush=True)
        sys.exit(1)
    time.sleep(0.5)
else:
    print("[WARN] 60s elapsed, model not ready yet — continuing anyway", flush=True)


def send(msg):
    p.stdin.write(json.dumps(msg).encode() + b"\n")
    p.stdin.flush()


def recv(timeout=30):
    result, err = {}, {}
    def _r():
        try:
            line = p.stdout.readline()
            if not line:
                err["e"] = EOFError("stdout closed")
                return
            result["v"] = json.loads(line)
        except Exception as e:
            err["e"] = e
    t = threading.Thread(target=_r, daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        return {"timeout": True}
    if "e" in err:
        return {"error": str(err["e"])}
    return result["v"]


# initialize
t0 = time.time()
send({"jsonrpc": "2.0", "id": 0, "method": "initialize",
      "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                 "clientInfo": {"name": "smoke", "version": "0"}}})
r = recv(timeout=30)
status = "OK" if "result" in r else str(r)
print(f"initialize in {time.time()-t0:.1f}s: {status}", flush=True)

send({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})

# query_docs
print("Sending query_docs...", flush=True)
t0 = time.time()
send({"jsonrpc": "2.0", "id": 1, "method": "tools/call",
      "params": {"name": "query_docs",
                 "arguments": {"question": "max coupons per order", "top_k": 2}}})
r = recv(timeout=90)
elapsed = time.time() - t0
if "timeout" in r:
    print(f"[FAIL] query_docs timed out after {elapsed:.1f}s", flush=True)
elif "result" in r:
    content = r["result"].get("content", [])
    print(f"  raw content: {json.dumps(content)[:500]}", flush=True)
    if not content:
        print(f"[FAIL] empty content in {elapsed:.1f}s", flush=True)
    else:
        text = content[0].get("text", "")
        print(f"  text ({len(text)} chars): {repr(text[:200])}", flush=True)
        try:
            data = json.loads(text)
            hits = data.get("hits", [])
            top = hits[0]["rel_path"] if hits else "none"
            print(f"[OK] query_docs in {elapsed:.1f}s — {len(hits)} hits, top: {top}", flush=True)
        except json.JSONDecodeError as je:
            print(f"[FAIL] JSON parse error: {je}", flush=True)
else:
    print(f"[FAIL] {elapsed:.1f}s: {json.dumps(r)[:300]}", flush=True)

p.kill()
print("Done.", flush=True)
