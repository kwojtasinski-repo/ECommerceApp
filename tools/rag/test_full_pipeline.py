"""
Full E2E pipeline test for both RAG MCP servers.

Phases:
  0  Prerequisites check (Docker daemon, Qdrant container)
  1  Stop running MCP SSE containers (clean slate)
  2  Docker build --no-cache for rag-tools (Python) and rag-dotnet (.NET)
  3  STDIO ingest + query  —  Python mcp_server.py
  4  STDIO ingest + query  —  .NET mcp_server.dll
  5  SSE server tests      —  start rag-python-sse + rag-dotnet-sse, query
                               both via HTTP (uses docs already indexed in 3/4)
  6  Flow queries          —  run a curated subset of test_flows.py flows
                               against Docker STDIO (Python)
  7  Hosted ingest (HTTP)  —  upload a synthetic document to both Python and
                               .NET SSE servers via POST /ingest/{collection}/batch,
                               poll for completion, query via MCP SSE to verify
  8  Report                —  write docs/rag/pipeline-test-report.md

Usage:
    python tools/rag/test_full_pipeline.py              # all phases
    python tools/rag/test_full_pipeline.py --phase 3    # single phase
    python tools/rag/test_full_pipeline.py --skip-build # skip phase 2
    python tools/rag/test_full_pipeline.py --dry-run    # discover + report only
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import queue
import subprocess
import sys
import threading
import time
from datetime import datetime, timezone
from pathlib import Path

import httpx

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

# ── Constants ─────────────────────────────────────────────────────────────────

WORKSPACE = Path(__file__).parent.parent.parent.resolve()
PYTHON_SSE_PORT = 3002
DOTNET_SSE_PORT = 3001
QDRANT_PORT = 6333
NETWORK = "ecommerceapp_default"
PYTHON_IMAGE = "rag-tools"
DOTNET_IMAGE = "rag-dotnet"
DOTNET_COLLECTION = "ecommerceapp_docs_dotnet"
PYTHON_COLLECTION = "ecommerceapp_docs"
DOTNET_CONFIG = "/rag-config.yaml"
BANNER = "═" * 70


# ── Result tracking ───────────────────────────────────────────────────────────

class PhaseResult:
    def __init__(self, name: str):
        self.name = name
        self.start = time.monotonic()
        self.end: float | None = None
        self.ok = True
        self.items: list[tuple[str, bool, str]] = []  # (label, ok, detail)

    def record(self, label: str, ok: bool, detail: str = "") -> None:
        self.items.append((label, ok, detail))
        if not ok:
            self.ok = False
        icon = "✓" if ok else "✗"
        suffix = f"  — {detail}" if detail else ""
        print(f"    {icon}  {label}{suffix}")

    def finish(self) -> None:
        self.end = time.monotonic()
        elapsed = self.end - self.start
        status = "PASSED" if self.ok else "FAILED"
        print(f"\n  [{status}] Phase '{self.name}' in {elapsed:.1f}s")

    @property
    def elapsed(self) -> float:
        return (self.end or time.monotonic()) - self.start


ALL_RESULTS: list[PhaseResult] = []


# ── Subprocess helpers ────────────────────────────────────────────────────────

def _run(cmd: list[str], timeout: int = 60, env: dict | None = None,
         capture: bool = True) -> tuple[int, str]:
    """Run a command and return (returncode, combined_output)."""
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    result = subprocess.run(
        cmd, capture_output=capture, text=True, timeout=timeout,
        env=merged_env, encoding="utf-8", errors="replace",
    )
    out = (result.stdout or "") + (result.stderr or "")
    return result.returncode, out


def _run_stream(cmd: list[str], timeout: int = 300, env: dict | None = None, cwd: str | None = None) -> tuple[int, str]:
    """Run a command, streaming output to console, return (code, output)."""
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    buf: list[str] = []
    proc = subprocess.Popen(
        cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, env=merged_env, encoding="utf-8", errors="replace",
        cwd=cwd,
    )
    assert proc.stdout is not None
    for line in proc.stdout:
        sys.stdout.write("    " + line)
        sys.stdout.flush()
        buf.append(line)
    proc.wait(timeout=timeout)
    return proc.returncode, "".join(buf)


# ── MCP STDIO helpers (mirrors test_flows.py) ─────────────────────────────────

def _mcp_encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"


def _mcp_read(stdout, timeout: float = 60.0) -> dict:
    result: dict = {}
    err: dict = {}
    def _w():
        try:
            line = stdout.readline()
            result["v"] = json.loads(line)
        except Exception as exc:
            err["e"] = exc
    t = threading.Thread(target=_w, daemon=True)
    t.start(); t.join(timeout)
    if t.is_alive():
        raise TimeoutError(f"No MCP response after {timeout}s")
    if "e" in err:
        raise err["e"]
    return result["v"]


def _mcp_handshake(proc: subprocess.Popen) -> None:
    proc.stdin.write(_mcp_encode({  # type: ignore[union-attr]
        "jsonrpc": "2.0", "id": 0, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "pipeline-tester", "version": "1.0"}},
    }))
    proc.stdin.flush()  # type: ignore[union-attr]
    _mcp_read(proc.stdout, 30)  # type: ignore[arg-type]
    proc.stdin.write(_mcp_encode({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}))  # type: ignore[union-attr]
    proc.stdin.flush()  # type: ignore[union-attr]


_CALL_ID = [100]


def _mcp_call(proc: subprocess.Popen, tool: str, args: dict, timeout: float = 60) -> dict:
    _CALL_ID[0] += 1
    proc.stdin.write(_mcp_encode({  # type: ignore[union-attr]
        "jsonrpc": "2.0", "id": _CALL_ID[0], "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }))
    proc.stdin.flush()  # type: ignore[union-attr]
    resp = _mcp_read(proc.stdout, timeout)  # type: ignore[arg-type]
    raw = resp.get("result", {}).get("content", [{}])[0].get("text", "{}")
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        # .NET server returns plain text — wrap in a dict
        return {"text": raw}


def _mcp_call_raw(proc: subprocess.Popen, tool: str, args: dict, timeout: float = 60) -> str:
    _CALL_ID[0] += 1
    proc.stdin.write(_mcp_encode({  # type: ignore[union-attr]
        "jsonrpc": "2.0", "id": _CALL_ID[0], "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }))
    proc.stdin.flush()  # type: ignore[union-attr]
    resp = _mcp_read(proc.stdout, timeout)  # type: ignore[arg-type]
    return resp.get("result", {}).get("content", [{}])[0].get("text", "")


def _start_stdio_docker(image: str, extra_env: list[str] | None = None,
                        cmd_override: list[str] | None = None) -> tuple[subprocess.Popen, threading.Thread, list[str]]:
    """Spawn a Docker container in interactive STDIO mode, return (proc, stderr_thread, stderr_lines)."""
    docker_cmd = [
        "docker", "run", "--rm", "--interactive",
        "--network", NETWORK,
        "--volume", f"{WORKSPACE}:/workspace",
        "--env", f"RAG_WORKSPACE=/workspace",
        "--env", "PYTHONUNBUFFERED=1",
        "--env", "VECTOR_MODE=docker",
        "--env", f"QDRANT_URL=http://qdrant:{QDRANT_PORT}",
    ]
    if extra_env:
        for e in extra_env:
            docker_cmd += ["--env", e]
    docker_cmd.append(image)
    if cmd_override:
        docker_cmd += cmd_override
    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"
    stderr_lines: list[str] = []
    proc = subprocess.Popen(
        docker_cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        stderr=subprocess.PIPE, env=env,
    )
    def _drain():
        assert proc.stderr is not None
        for chunk in iter(lambda: proc.stderr.read(512), b""):
            stderr_lines.append(chunk.decode(errors="replace"))
    t = threading.Thread(target=_drain, daemon=True)
    t.start()
    return proc, t, stderr_lines


# ── SSE / HTTP helpers ────────────────────────────────────────────────────────

def _parse_sse_body(text: str) -> dict:
    for line in text.splitlines():
        if line.startswith("data:"):
            return json.loads(line[5:].strip())
    raise ValueError(f"No data: line in SSE response: {text[:200]!r}")


def _dotnet_post(client: httpx.Client, body: dict, session_id: str | None = None) -> dict:
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
    r = client.post(
        "/",
        json={"jsonrpc": "2.0", "id": 1, "method": "initialize",
              "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                         "clientInfo": {"name": "pipeline-tester", "version": "1.0"}}},
        headers={"Content-Type": "application/json", "Accept": "application/json, text/event-stream"},
        timeout=30,
    )
    r.raise_for_status()
    session_id = r.headers.get("mcp-session-id", "")
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


async def _run_sse_tool(url: str, tool: str, args: dict) -> str:
    """Call a tool on the Python SSE server and return raw text."""
    from mcp.client.sse import sse_client
    from mcp.client.session import ClientSession
    async with sse_client(f"{url}/sse", timeout=15) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            r = await session.call_tool(tool, args)
            return r.content[0].text if r.content else ""


# ── Phase helpers ─────────────────────────────────────────────────────────────

def _wait_for_port(port: int, host: str = "localhost", timeout: int = 30) -> bool:
    import socket
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with socket.create_connection((host, port), timeout=1):
                return True
        except OSError:
            time.sleep(0.5)
    return False


def _wait_for_http(url: str, timeout: int = 60) -> bool:
    """Wait until an HTTP GET to `url` returns a non-5xx response (or any response)."""
    import urllib.request, urllib.error
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=2) as resp:
                if resp.status < 500:
                    return True
        except urllib.error.HTTPError as e:
            if e.code < 500:
                return True
        except Exception:
            pass
        time.sleep(1)
    return False


def _container_running(name_substr: str) -> bool:
    rc, out = _run(["docker", "ps", "--format", "{{.Names}}"], timeout=10)
    return rc == 0 and name_substr in out


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 0: Prerequisites
# ══════════════════════════════════════════════════════════════════════════════

def phase_0_prerequisites() -> PhaseResult:
    p = PhaseResult("Prerequisites")
    print(f"\n{BANNER}\n  PHASE 0 — Prerequisites\n{BANNER}")

    # Docker daemon
    rc, _ = _run(["docker", "info"], timeout=10)
    p.record("Docker daemon reachable", rc == 0)

    # Qdrant reachable (may run under any container name)
    import socket
    try:
        with socket.create_connection(("localhost", QDRANT_PORT), timeout=2):
            qdrant_up = True
    except OSError:
        qdrant_up = False
    p.record("Qdrant reachable on port 6333", qdrant_up,
             "start with: docker compose up -d qdrant" if not qdrant_up else "")

    # Python venv
    venv_py = WORKSPACE / "tools" / "rag" / ".venv" / "Scripts" / "python.exe"
    p.record(".venv exists", venv_py.exists(), str(venv_py) if not venv_py.exists() else "")

    # mcp package importable
    rc2, _ = _run([str(venv_py), "-c", "from mcp.client.sse import sse_client"], timeout=10)
    p.record("mcp.client.sse importable", rc2 == 0)

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 1: Stop SSE containers
# ══════════════════════════════════════════════════════════════════════════════

def phase_1_stop_sse() -> PhaseResult:
    p = PhaseResult("Stop SSE containers")
    print(f"\n{BANNER}\n  PHASE 1 — Stop running SSE containers\n{BANNER}")

    # Stop ONLY the SSE containers, not base services (qdrant etc.)
    # Use 'stop' + 'rm' to avoid removing the shared network / qdrant
    for svc in ("rag-python-sse", "rag-dotnet-sse"):
        _run(["docker", "compose", "--profile", f"rag-{svc.split('-')[1]}-sse",
              "stop", svc], timeout=15)
        _run(["docker", "compose", "--profile", f"rag-{svc.split('-')[1]}-sse",
              "rm", "-f", svc], timeout=15)

    # Also kill any leftover standalone containers
    for name in ("rag-python-sse", "rag-dotnet-sse"):
        _run(["docker", "rm", "-f", name], timeout=10)

    p.record("SSE containers stopped (rag-python-sse + rag-dotnet-sse)", True)

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 2: Docker build --no-cache
# ══════════════════════════════════════════════════════════════════════════════

def phase_2_docker_build(skip: bool = False) -> PhaseResult:
    p = PhaseResult("Docker build --no-cache")
    print(f"\n{BANNER}\n  PHASE 2 — Docker build --no-cache\n{BANNER}")

    if skip:
        p.record("(skipped via --skip-build)", True)
        p.finish()
        return p

    # Python image
    print(f"\n  Building {PYTHON_IMAGE} (this downloads/installs Python deps)…")
    rc1, out1 = _run_stream(
        ["docker", "build", "--no-cache", "-t", PYTHON_IMAGE, "tools/rag/"],
        timeout=600, cwd=str(WORKSPACE),
    )
    p.record(f"docker build --no-cache {PYTHON_IMAGE}", rc1 == 0,
             f"exit={rc1}" if rc1 != 0 else "")

    # .NET image
    print(f"\n  Building {DOTNET_IMAGE} (downloads ONNX model + .NET build)…")
    rc2, out2 = _run_stream(
        ["docker", "build", "--no-cache", "-t", DOTNET_IMAGE, "tools/rag-dotnet/"],
        timeout=1200, cwd=str(WORKSPACE),
    )
    p.record(f"docker build --no-cache {DOTNET_IMAGE}", rc2 == 0,
             f"exit={rc2}" if rc2 != 0 else "")

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 3: Python STDIO — ingest + query
# ══════════════════════════════════════════════════════════════════════════════

def phase_3_python_stdio() -> PhaseResult:
    p = PhaseResult("Python STDIO — ingest + query")
    print(f"\n{BANNER}\n  PHASE 3 — Python STDIO (ingest + MCP query)\n{BANNER}")

    # 3a: Ingest via CLI
    print("\n  [3a] Running python ingest.py --mode docker --force-full …")
    rc, out = _run(
        [
            "docker", "run", "--rm",
            "--network", NETWORK,
            "--volume", f"{WORKSPACE}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "VECTOR_MODE=docker",
            "--env", f"QDRANT_URL=http://qdrant:{QDRANT_PORT}",
            "--env", "PYTHONUNBUFFERED=1",
            PYTHON_IMAGE,
            "python", "ingest.py", "--mode", "docker", "--force-full",
        ],
        timeout=300,
    )
    indexed = "indexed" in out.lower() or "upsert" in out.lower() or rc == 0
    p.record("ingest.py --mode docker --force-full", rc == 0,
             f"exit={rc}\n{out[-300:]}" if rc != 0 else "")

    # Show last few lines of ingest output
    for line in out.splitlines()[-5:]:
        if line.strip():
            print(f"    {line}")

    # 3b: STDIO MCP query
    print("\n  [3b] Spawning Docker STDIO MCP server (Python)…")
    proc, t_stderr, stderr_lines = _start_stdio_docker(
        PYTHON_IMAGE, cmd_override=["python", "mcp_server.py"]
    )
    try:
        _mcp_handshake(proc)
        p.record("MCP initialize handshake", True)

        # query_docs
        r = _mcp_call(proc, "query_docs",
                      {"question": "strongly typed entity ID TypedId domain primitives", "top_k": 3})
        hits = r.get("hits", [])
        paths = [h["rel_path"] for h in hits]
        adr6 = any("0006" in path for path in paths)
        p.record("query_docs returns ADR-0006 (TypedId)", adr6,
                 f"hits: {paths}" if not adr6 else f"{len(hits)} hits")

        # get_history (replaces get_adr_history)
        r2 = _mcp_call(proc, "get_history", {"id": "0006"})
        has_typedid = any("TypedId" in ch.get("text", "") for ch in r2.get("chunks", []))
        p.record("get_history ADR-0006 has 'TypedId' in chunks", has_typedid)

        # read_docs
        r3 = _mcp_call(proc, "read_docs",
                       {"question": "order status lifecycle state machine", "top_files": 2})
        has_files = len(r3.get("files", [])) > 0
        p.record("read_docs returns files", has_files,
                 f"{len(r3.get('files', []))} files")

        # get_history
        r4 = _mcp_call(proc, "get_history", {"id": "0006"})
        gh_count = r4.get("chunk_count", len(r4.get("chunks", [])))
        p.record("get_history ADR-0006 returns chunks", gh_count > 0,
                 f"chunk_count={gh_count}")

    except Exception as exc:
        p.record("MCP STDIO session", False, str(exc))
    finally:
        try:
            proc.stdin.close()  # type: ignore[union-attr]
        except Exception:
            pass
        try:
            proc.wait(timeout=10)
        except Exception:
            proc.kill()
        t_stderr.join(timeout=3)

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 4: .NET STDIO — ingest + query
# ══════════════════════════════════════════════════════════════════════════════

def phase_4_dotnet_stdio() -> PhaseResult:
    p = PhaseResult(".NET STDIO — ingest + query")
    print(f"\n{BANNER}\n  PHASE 4 — .NET STDIO (ingest + MCP query)\n{BANNER}")

    dotnet_mounts = [
        "--volume", f"{WORKSPACE}:/workspace",
        "--volume", f"{WORKSPACE}/tools/rag-dotnet/rag-config.yaml:{DOTNET_CONFIG}:ro",
        "--volume", f"{WORKSPACE}/tools/rag/metadata-rules.yaml:/metadata-rules.yaml:ro",
        "--volume", f"{WORKSPACE}/tools/rag/queries.yaml:/queries.yaml:ro",
    ]
    dotnet_envs = [
        f"RAG_WORKSPACE=/workspace",
        f"QDRANT_URL=http://qdrant:{QDRANT_PORT}",
        f"RAG_COLLECTION={DOTNET_COLLECTION}",
        f"RAG_CONFIG={DOTNET_CONFIG}",
        "DOTNET_CLI_TELEMETRY_OPTOUT=1",
    ]

    # 4a: Ingest via .NET CLI (dotnet /app/ingest/ingest.dll)
    print("\n  [4a] Running .NET ingest --force-full …")
    ingest_cmd = [
        "docker", "run", "--rm",
        "--network", NETWORK,
    ] + dotnet_mounts + [f"--env={e}" for e in dotnet_envs] + [
        DOTNET_IMAGE,
        "dotnet", "/app/ingest/ingest.dll", "--force-full",
    ]
    rc, out = _run(ingest_cmd, timeout=300)
    p.record(".NET ingest --force-full", rc == 0,
             f"exit={rc}\n{out[-300:]}" if rc != 0 else "")
    for line in out.splitlines()[-5:]:
        if line.strip():
            print(f"    {line}")

    # 4b: STDIO MCP query via Docker
    print("\n  [4b] Spawning Docker STDIO MCP server (.NET)…")
    docker_cmd = [
        "docker", "run", "--rm", "--interactive",
        "--network", NETWORK,
    ] + dotnet_mounts + [f"--env={e}" for e in dotnet_envs] + [
        "--env", "MCP_TRANSPORT=stdio",
        DOTNET_IMAGE,
        "dotnet", "/app/mcp/mcp_server.dll",
    ]
    env = os.environ.copy()
    stderr_lines: list[str] = []
    proc = subprocess.Popen(
        docker_cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
        stderr=subprocess.PIPE, env=env,
    )
    def _drain():
        assert proc.stderr is not None
        for chunk in iter(lambda: proc.stderr.read(512), b""):
            stderr_lines.append(chunk.decode(errors="replace"))
    t_stderr = threading.Thread(target=_drain, daemon=True)
    t_stderr.start()

    # Give server time to initialize ONNX model
    time.sleep(3)

    try:
        _mcp_handshake(proc)
        p.record("MCP initialize handshake", True)

        # query_docs (raw text response from .NET server)
        text = _mcp_call_raw(proc, "query_docs",
                             {"question": "strongly typed entity ID TypedId domain primitives",
                              "top_k": 3},
                             timeout=60)
        adr6 = "0006" in text
        typedid = "TypedId" in text
        p.record("query_docs returns ADR-0006 content", adr6, f"{len(text)} chars")
        p.record("query_docs contains 'TypedId'", typedid)

        # get_history (replaces get_adr_history)
        hist_text = _mcp_call_raw(proc, "get_history", {"id": "0006"}, timeout=60)
        has_content = len(hist_text) > 50 and "No chunks found" not in hist_text
        p.record("get_history ADR-0006 (.NET STDIO) has content", has_content,
                 f"{len(hist_text)} chars")

        # list_adrs
        list_text = _mcp_call_raw(proc, "list_adrs", {}, timeout=30)
        has_adrs = "0006" in list_text and "0014" in list_text
        p.record("list_adrs returns known ADRs", has_adrs,
                 f"{len(list_text)} chars")

        # get_history
        gh_text = _mcp_call_raw(proc, "get_history", {"id": "0006"}, timeout=60)
        gh_ok = "chunk_count" in gh_text and "\"0\"" not in gh_text[:80]
        try:
            gh_json = json.loads(gh_text)
            gh_count = gh_json.get("chunk_count", 0)
            gh_ok = gh_count > 0
        except Exception:
            gh_ok = False
        p.record(".NET get_history ADR-0006 returns chunks", gh_ok,
                 f"{len(gh_text)} chars")

    except Exception as exc:
        p.record(".NET MCP STDIO session", False, str(exc))
    finally:
        try:
            proc.stdin.close()  # type: ignore[union-attr]
        except Exception:
            pass
        try:
            proc.wait(timeout=10)
        except Exception:
            proc.kill()
        t_stderr.join(timeout=3)

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 5: SSE servers — start + ingest (HTTP) + query
# ══════════════════════════════════════════════════════════════════════════════

def phase_5_sse() -> PhaseResult:
    p = PhaseResult("SSE servers — start + HTTP ingest + query")
    print(f"\n{BANNER}\n  PHASE 5 — SSE servers (start → ingest → query)\n{BANNER}")

    # 5a: Start both SSE services
    print("\n  [5a] Starting rag-python-sse and rag-dotnet-sse …")
    rc, out = _run(
        ["docker", "compose",
         "--profile", "rag-python-sse", "--profile", "rag-dotnet-sse",
         "up", "-d", "--force-recreate", "rag-python-sse", "rag-dotnet-sse"],
        timeout=60,
    )
    p.record("docker compose up --force-recreate (both SSE)", rc == 0,
             out.strip()[-120:] if rc != 0 else "")

    # Wait for servers to be HTTP-ready (not just TCP-open)
    print(f"\n  Waiting for Python SSE (port {PYTHON_SSE_PORT}) to accept HTTP…")
    py_up = _wait_for_http(f"http://localhost:{PYTHON_SSE_PORT}/sse", timeout=60)
    p.record(f"Python SSE port {PYTHON_SSE_PORT} reachable", py_up)

    print(f"  Waiting for .NET SSE (port {DOTNET_SSE_PORT}) to accept HTTP…")
    dn_up = _wait_for_http(f"http://localhost:{DOTNET_SSE_PORT}/", timeout=60)
    p.record(f".NET SSE port {DOTNET_SSE_PORT} reachable", dn_up)

    # 5b: SSE tool calls (Python)
    if py_up:
        print(f"\n  [5b] Python SSE — MCP tool calls (port {PYTHON_SSE_PORT})…")
        try:
            raw = asyncio.run(_run_sse_tool(
                f"http://localhost:{PYTHON_SSE_PORT}",
                "query_docs",
                {"question": "coupon maximum per order business rule", "top_k": 3},
            ))
            result = json.loads(raw)
            hits = result.get("hits", [])
            paths = [h["rel_path"] for h in hits]
            adr16 = any("0016" in path for path in paths)
            p.record("Python SSE: query_docs → ADR-0016 (coupons)", adr16,
                     f"hits: {paths}")

            raw2 = asyncio.run(_run_sse_tool(
                f"http://localhost:{PYTHON_SSE_PORT}",
                "get_history",
                {"id": "0016"},
            ))
            hist = json.loads(raw2)
            has_coupon = any("coupon" in ch.get("text", "").lower() for ch in hist.get("chunks", []))
            p.record("Python SSE: get_history ADR-0016 mentions 'coupon'", has_coupon)

            raw3 = asyncio.run(_run_sse_tool(
                f"http://localhost:{PYTHON_SSE_PORT}",
                "get_history",
                {"id": "0016"},
            ))
            gh = json.loads(raw3)
            p.record("Python SSE: get_history('0016') → chunk_count > 0",
                     gh.get("chunk_count", 0) > 0,
                     f"chunk_count={gh.get('chunk_count', 0)}")

        except Exception as exc:
            p.record("Python SSE: MCP session", False, str(exc))

    # 5c: SSE tool calls (.NET Streamable HTTP)
    if dn_up:
        print(f"\n  [5c] .NET Streamable HTTP — MCP tool calls (port {DOTNET_SSE_PORT})…")
        try:
            with httpx.Client(base_url=f"http://localhost:{DOTNET_SSE_PORT}", timeout=60) as client:
                session_id = _dotnet_initialize(client)
                p.record(".NET SSE: MCP initialize handshake", bool(session_id),
                         f"session={session_id[:8]}…")

                # tools/list
                resp = _dotnet_post(client, {
                    "jsonrpc": "2.0", "id": 10, "method": "tools/list", "params": {},
                }, session_id=session_id)
                tools = [t["name"] for t in resp.get("result", {}).get("tools", [])]
                p.record(".NET SSE: tools/list", bool(tools), str(tools))

                # query_docs
                def _call_raw(tool: str, args: dict) -> str:
                    r = _dotnet_post(client, {
                        "jsonrpc": "2.0", "id": 11, "method": "tools/call",
                        "params": {"name": tool, "arguments": args},
                    }, session_id=session_id)
                    return r.get("result", {}).get("content", [{}])[0].get("text", "")

                text = _call_raw("query_docs", {
                    "question": "coupon maximum per order business rule", "top_k": 3,
                })
                adr16 = "0016" in text
                p.record(".NET SSE: query_docs → ADR-0016 (coupons)", adr16,
                         f"{len(text)} chars")

                hist_text = _call_raw("get_history", {"id": "0016"})
                has_coupon = "coupon" in hist_text.lower()
                p.record(".NET SSE: get_history ADR-0016 mentions 'coupon'", has_coupon,
                         f"{len(hist_text)} chars")

                gh_text = _call_raw("get_history", {"id": "0016"})
                try:
                    gh_json = json.loads(gh_text)
                    gh_count = gh_json.get("chunk_count", 0)
                except Exception:
                    gh_count = 0
                p.record(".NET SSE: get_history('0016') → chunk_count > 0",
                         gh_count > 0, f"chunk_count={gh_count}")

        except Exception as exc:
            p.record(".NET SSE: MCP session", False, str(exc))

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 6: Flow queries (curated subset via Docker STDIO Python)
# ══════════════════════════════════════════════════════════════════════════════

FLOW_QUESTIONS = [
    # (label, tool, args, expect_in)
    ("Coupon limit rule (ADR-0016)",
     "query_docs", {"question": "maximum coupons per order limit business rule", "top_k": 5},
     ["0016"]),
    ("Order lifecycle (ADR-0014)",
     "query_docs", {"question": "order status state machine transitions lifecycle", "top_k": 5},
     ["0014"]),
    ("Cross-BC event communication (ADR-0010)",
     "query_docs", {"question": "cross bounded context event domain message bus", "top_k": 5},
     ["0010"]),
    ("TypedId pattern (ADR-0006)",
     "get_history", {"id": "0006"},
     ["TypedId", "abstract record"]),
    ("Known .NET upgrade issues",
     "query_docs", {"question": "FluentAssertions AwesomeAssertions .NET 8 upgrade breaking change", "top_k": 5},
     ["AwesomeAssertions", "FluentAssertions"]),
    ("Saga / orchestration decision (ADR-0026)",
     "read_docs", {"question": "saga orchestration choreography distributed transaction", "top_files": 2},
     ["0026", "Saga"]),
]


def phase_6_flow_queries() -> PhaseResult:
    p = PhaseResult("Flow queries via Docker STDIO")
    print(f"\n{BANNER}\n  PHASE 6 — Flow queries (Docker STDIO Python)\n{BANNER}")

    proc, t_stderr, _ = _start_stdio_docker(
        PYTHON_IMAGE, cmd_override=["python", "mcp_server.py"]
    )
    try:
        _mcp_handshake(proc)
        p.record("MCP handshake for flow queries", True)

        for label, tool, args, expects in FLOW_QUESTIONS:
            print(f"\n  [{label}]")
            try:
                result = _mcp_call(proc, tool, args, timeout=60)
                # Check expected strings
                # For query_docs/read_docs: check rel_path hits
                # For get_history: check chunks text
                content = ""
                if tool == "query_docs":
                    content = " ".join(h.get("rel_path", "") + " " + h.get("text", "")
                                       for h in result.get("hits", []))
                elif tool == "read_docs":
                    content = " ".join(
                        f.get("rel_path", "") + " " + " ".join(
                            c.get("text", "") for c in f.get("chunks", [])
                        ) for f in result.get("files", [])
                    )
                elif tool == "get_history":
                    content = " ".join(ch.get("text", "") for ch in result.get("chunks", []))

                all_found = all(kw.lower() in content.lower() for kw in expects)
                missing = [kw for kw in expects if kw.lower() not in content.lower()]
                p.record(label, all_found,
                         f"missing: {missing}" if missing else "")
                for kw in expects:
                    found = kw.lower() in content.lower()
                    print(f"      {'✓' if found else '✗'}  '{kw}'")

            except TimeoutError as exc:
                p.record(label, False, f"timeout: {exc}")
            except Exception as exc:
                p.record(label, False, str(exc))

    except Exception as exc:
        p.record("MCP session setup", False, str(exc))
    finally:
        try:
            proc.stdin.close()  # type: ignore[union-attr]
        except Exception:
            pass
        try:
            proc.wait(timeout=10)
        except Exception:
            proc.kill()
        t_stderr.join(timeout=3)

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 7: Write report
# ══════════════════════════════════════════════════════════════════════════════

def phase_8_report(results: list[PhaseResult]) -> PhaseResult:
    p = PhaseResult("Write pipeline test report")
    print(f"\n{BANNER}\n  PHASE 8 — Writing report\n{BANNER}")

    report_path = WORKSPACE / "docs" / "rag" / "pipeline-test-report.md"
    now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")

    total_checks = sum(len(r.items) for r in results)
    total_failed = sum(sum(1 for _, ok, _ in r.items if not ok) for r in results)
    total_passed = total_checks - total_failed

    lines = [
        f"# RAG Pipeline Test Report",
        f"",
        f"Generated: {now}  ",
        f"Branch: `RAG_Improvement`",
        f"",
        f"## Summary",
        f"",
        f"| Phase | Status | Elapsed | Checks |",
        f"|---|---|---|---|",
    ]

    for r in results:
        status = "✅ PASSED" if r.ok else "❌ FAILED"
        elapsed = f"{r.elapsed:.1f}s"
        checks = f"{sum(1 for _,ok,_ in r.items if ok)}/{len(r.items)}"
        lines.append(f"| {r.name} | {status} | {elapsed} | {checks} |")

    lines += [
        f"",
        f"**Total**: {total_passed}/{total_checks} checks passed" +
        (f"  — **{total_failed} FAILED**" if total_failed else " ✅"),
        f"",
        f"## Phase Details",
        f"",
    ]

    for r in results:
        lines.append(f"### {r.name}")
        lines.append(f"")
        for label, ok, detail in r.items:
            icon = "✅" if ok else "❌"
            suffix = f" — `{detail}`" if detail else ""
            lines.append(f"- {icon} {label}{suffix}")
        lines.append(f"")

    lines += [
        f"## Notes & Improvement Suggestions",
        f"",
        f"- **Docker build time**: `.NET` image downloads ONNX model from HuggingFace (~100 MB)",
        f"  on every `--no-cache` build. Consider caching the model layer separately or",
        f"  using a private registry mirror for CI/CD.",
        f"",
        f"- **Python SSE transport**: Uses legacy `SseServerTransport` (two-endpoint SSE + POST).",
        f"  The .NET server uses the newer MCP Streamable HTTP standard. Consider migrating",
        f"  the Python server to `streamablehttp` transport when mcp-python supports it.",
        f"",
        f"- **API key enforcement**: The `.NET` SSE server enforces `X-Api-Key` via `ApiKeyMiddleware`.",
        f"  The Python SSE server has no auth guard. Add one for production use.",
        f"",
        f"- **Collection separation**: Python uses `{PYTHON_COLLECTION}`, .NET uses",
        f"  `{DOTNET_COLLECTION}`. Both are indexed independently (different embedders).",
        f"  Consider a single canonical collection if embedding parity is achieved.",
        f"",
        f"- **STDIO cold start**: .NET STDIO requires 2–3s for ONNX model load.",
        f"  Python STDIO requires 3–5s for sentence-transformers model load.",
        f"  Both are acceptable for VS Code MCP spawn (one-time cost).",
        f"",
    ]

    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text("\n".join(lines), encoding="utf-8")
    p.record(f"Report written to {report_path.relative_to(WORKSPACE)}", True)
    print(f"    Report: {report_path}")

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 7: Hosted ingest via HTTP API (no volume mounts — simulate remote host)
# ══════════════════════════════════════════════════════════════════════════════

# Synthetic document uploaded to both servers via POST /ingest/{collection}/batch
_HOSTED_DOC_REL_PATH = "docs/hosted-ingest-e2e-test.md"
_HOSTED_DOC_CONTENT = """\
# Hosted Ingest End-to-End Test Document

## Purpose

This document is uploaded via the HTTP ingest API (`POST /ingest/{collection}/batch`)
to verify that the hosted SSE server scenario works correctly without volume mounts.

## Unique Marker

HOSTED_INGEST_E2E_MARKER_42XQZ — a unique token used to verify the document
was indexed and is queryable via MCP tools.

## Scenario

When deploying the RAG MCP server to a remote host (e.g., a cloud VM or container
registry), there are no local volume mounts. Instead, documents are uploaded via
the HTTP ingest REST API. This test validates that flow end-to-end.

## Steps Validated

1. POST /ingest/{collection}/batch — upload document as a ZIP
2. GET /ingest/{collection}/operations/{opId} — poll until Completed
3. MCP query_docs — verify the unique marker is returned in results
"""

_HOSTED_INGEST_TIMEOUT = 60  # seconds to wait for ingest operation to complete


def _http_ingest_poll(base_url: str, status_url: str, timeout: int = 60,
                       api_key: str | None = None) -> dict:
    """Poll GET {status_url} until status is Completed or Failed, or timeout."""
    import urllib.request
    headers: dict[str, str] = {}
    if api_key:
        headers["X-Api-Key"] = api_key
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        req = urllib.request.Request(f"{base_url}{status_url}", headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=10) as resp:
                data = json.loads(resp.read())
                status = data.get("status", "")
                if status in ("Completed", "Failed", "completed", "failed"):
                    return data
        except Exception:
            pass
        time.sleep(2)
    return {"status": "Timeout"}


def _http_batch_upload(base_url: str, collection: str,
                       files: dict[str, str],
                       api_key: str | None = None) -> tuple[int, dict]:
    """Upload a ZIP of documents to POST /ingest/{collection}/batch.

    *files* maps relPath → text content.
    Required config files (metadata-rules.yaml, queries.yaml) are injected
    automatically if not already present in *files*.
    Returns (status_code, response_body).
    """
    _MIN_META_RULES = "doc_kind_rules:\n  - {glob: \"**\", kind: doc}\n"
    _MIN_QUERIES    = "named_queries:\n  - {name: default, question: test, top_k: 5}\n"
    all_files = {
        "metadata-rules.yaml": _MIN_META_RULES,
        "queries.yaml":        _MIN_QUERIES,
        **files,
    }
    import io, zipfile, urllib.request, urllib.error

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for rel_path, content in all_files.items():
            zf.writestr(rel_path, content)
    zip_bytes = buf.getvalue()

    headers: dict[str, str] = {"Content-Type": "application/zip"}
    if api_key:
        headers["X-Api-Key"] = api_key

    req = urllib.request.Request(
        f"{base_url}/ingest/{collection}/batch",
        data=zip_bytes, headers=headers, method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read())


def phase_7_hosted_ingest() -> PhaseResult:
    p = PhaseResult("Hosted ingest via HTTP API (no volume mounts)")
    print(f"\n{BANNER}\n  PHASE 7 — Hosted ingest via HTTP API\n{BANNER}")
    print("  Simulates remote deployment: docs uploaded via POST /ingest/{collection}/batch")
    print("  SSE servers must be running from phase 5.\n")

    py_base = f"http://localhost:{PYTHON_SSE_PORT}"
    dn_base = f"http://localhost:{DOTNET_SSE_PORT}"

    # ── Python SSE ingest upload ───────────────────────────────────────────────
    print("  [7a] Python SSE — upload doc via HTTP ingest API (batch)…")
    try:
        py_api_key: str | None = os.environ.get("RAG_API_KEY", "").strip() or None

        status_code, resp = _http_batch_upload(
            py_base, PYTHON_COLLECTION,
            {_HOSTED_DOC_REL_PATH: _HOSTED_DOC_CONTENT},
            api_key=py_api_key)
        accepted = status_code == 202
        ops = resp.get("operations", [])
        op_id = ops[0].get("operationId", "") if ops else ""
        status_url = ops[0].get("statusUrl", "") if ops else ""
        p.record("Python SSE: POST /ingest/batch → 202 Accepted", accepted,
                 f"status={status_code} opId={op_id[:40] if op_id else 'N/A'}")

        if accepted and status_url:
            print(f"    Polling operation {status_url} …")
            poll = _http_ingest_poll(py_base, status_url, timeout=_HOSTED_INGEST_TIMEOUT)
            completed = poll.get("status", "").lower() in ("completed",)
            p.record("Python SSE: ingest operation Completed", completed,
                     f"status={poll.get('status', '?')}")
        else:
            p.record("Python SSE: ingest operation Completed", False, "upload failed, skipped")

        # Query via MCP SSE to verify the unique marker is retrievable
        print("    Querying via Python MCP SSE for uploaded doc…")

        async def _query_python():
            from mcp.client.sse import sse_client
            from mcp.client.session import ClientSession
            async with sse_client(f"{py_base}/sse", timeout=15) as (read, write):
                async with ClientSession(read, write) as session:
                    await session.initialize()
                    r = await session.call_tool("query_docs", {
                        "question": "HOSTED_INGEST_E2E_MARKER_42XQZ hosted ingest test document",
                        "top_k": 5,
                    })
                    return r.content[0].text if r.content else ""

        raw = asyncio.run(_query_python())
        result = json.loads(raw)
        hits = result.get("hits", [])
        found = any(_HOSTED_DOC_REL_PATH in h.get("rel_path", "") for h in hits)
        p.record("Python SSE: uploaded doc queryable via MCP", found,
                 f"hits: {[h.get('rel_path','') for h in hits]}")

    except Exception as exc:
        p.record("Python SSE: hosted ingest", False, str(exc))

    # ── .NET SSE ingest upload ────────────────────────────────────────────────
    print("\n  [7b] .NET SSE — upload doc via HTTP ingest API…")

    # Check if .NET SSE has API key enforcement
    # The .NET server uses ApiKeyMiddleware; if RAG_API_KEY is not set, no auth needed
    dn_api_key: str | None = None  # no key set in our docker-compose

    try:
        status_code, resp = _http_batch_upload(
            dn_base, DOTNET_COLLECTION,
            {_HOSTED_DOC_REL_PATH: _HOSTED_DOC_CONTENT},
            api_key=dn_api_key)
        accepted = status_code == 202
        ops = resp.get("operations", [])
        op_id = ops[0].get("operationId", "") if ops else ""
        status_url = ops[0].get("statusUrl", "") if ops else ""
        p.record(".NET SSE: POST /ingest/batch → 202 Accepted", accepted,
                 f"status={status_code} opId={op_id[:40] if op_id else 'N/A'}")

        if accepted and status_url:
            print(f"    Polling operation {status_url} …")
            poll = _http_ingest_poll(dn_base, status_url, timeout=_HOSTED_INGEST_TIMEOUT,
                                      api_key=dn_api_key)
            completed = poll.get("status", "").lower() in ("completed",)
            p.record(".NET SSE: ingest operation Completed", completed,
                     f"status={poll.get('status', '?')}")
        else:
            p.record(".NET SSE: ingest operation Completed", False, "upload failed, skipped")

        # Query via .NET Streamable HTTP MCP
        print("    Querying via .NET MCP SSE for uploaded doc…")
        with httpx.Client(base_url=dn_base, timeout=60) as client:
            session_id = _dotnet_initialize(client)

            def _call(tool: str, args: dict) -> str:
                r = _dotnet_post(client, {
                    "jsonrpc": "2.0", "id": 20, "method": "tools/call",
                    "params": {"name": tool, "arguments": args},
                }, session_id=session_id)
                return r.get("result", {}).get("content", [{}])[0].get("text", "")

            text = _call("query_docs", {
                "question": "HOSTED_INGEST_E2E_MARKER_42XQZ hosted ingest test document",
                "top_k": 5,
            })
            found = _HOSTED_DOC_REL_PATH in text
            p.record(".NET SSE: uploaded doc queryable via MCP", found,
                     f"{len(text)} chars")

    except Exception as exc:
        p.record(".NET SSE: hosted ingest", False, str(exc))

    # ── Python SSE batch upload ───────────────────────────────────────────────
    print("\n  [7c] Python SSE — batch upload via POST /ingest/{collection}/batch…")
    _BATCH_DOC_MARKER = "BATCH_INGEST_E2E_MARKER_77KYZ"
    _BATCH_FILES = {
        "docs/batch-test-a.md": f"# Batch Test A\n\n{_BATCH_DOC_MARKER}\n",
        "docs/batch-test-b.md": "# Batch Test B\n\nSecond file in batch.\n",
    }
    try:
        py_api_key = os.environ.get("RAG_API_KEY", "").strip() or None
        status_code, resp = _http_batch_upload(
            py_base, PYTHON_COLLECTION, _BATCH_FILES, api_key=py_api_key)
        accepted = status_code == 202
        count = resp.get("count", 0)
        p.record("Python SSE: POST /ingest/batch → 202 Accepted",
                 accepted and count == len(_BATCH_FILES),
                 f"status={status_code} count={count}")

        if accepted:
            for op_entry in resp.get("operations", []):
                status_url = op_entry.get("statusUrl", "")
                if status_url:
                    print(f"    Polling {status_url} …")
                    poll = _http_ingest_poll(py_base, status_url,
                                             timeout=_HOSTED_INGEST_TIMEOUT,
                                             api_key=py_api_key)
                    completed = poll.get("status", "").lower() == "completed"
                    p.record(
                        f"Python SSE: batch op {op_entry.get('relPath','?')} Completed",
                        completed, f"status={poll.get('status','?')}")
    except Exception as exc:
        p.record("Python SSE: batch upload", False, str(exc))

    # ── .NET SSE batch upload ─────────────────────────────────────────────────
    print("\n  [7d] .NET SSE — batch upload via POST /ingest/{collection}/batch…")
    try:
        status_code, resp = _http_batch_upload(
            dn_base, DOTNET_COLLECTION, _BATCH_FILES, api_key=dn_api_key)
        accepted = status_code == 202
        count = resp.get("count", resp.get("Count", 0))
        p.record(".NET SSE: POST /ingest/batch → 202 Accepted",
                 accepted and count == len(_BATCH_FILES),
                 f"status={status_code} count={count}")

        if accepted:
            for op_entry in resp.get("operations", resp.get("Operations", [])):
                status_url = op_entry.get("statusUrl", op_entry.get("StatusUrl", ""))
                if status_url:
                    print(f"    Polling {status_url} …")
                    poll = _http_ingest_poll(dn_base, status_url,
                                             timeout=_HOSTED_INGEST_TIMEOUT,
                                             api_key=dn_api_key)
                    completed = poll.get("status", "").lower() == "completed"
                    rel = op_entry.get("relPath", op_entry.get("RelPath", "?"))
                    p.record(
                        f".NET SSE: batch op {rel} Completed",
                        completed, f"status={poll.get('status','?')}")
    except Exception as exc:
        p.record(".NET SSE: batch upload", False, str(exc))

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# PHASE 9: get_history tool — retrieve chunks by history field
# ══════════════════════════════════════════════════════════════════════════════

def phase_9_get_history() -> PhaseResult:
    p = PhaseResult("get_history tool — retrieve indexed chunks by history field")
    print(f"\n{BANNER}\n  PHASE 9 — get_history tool (Python SSE + .NET SSE)\n{BANNER}")
    print("  SSE servers must be running from phase 5.\n")

    py_base = f"http://localhost:{PYTHON_SSE_PORT}"
    dn_base = f"http://localhost:{DOTNET_SSE_PORT}"

    # ── Python SSE ────────────────────────────────────────────────────────────
    print("  [9a] Python SSE — get_history(id='0016') …")
    try:
        raw = asyncio.run(_run_sse_tool(py_base, "get_history", {"id": "0016"}))
        result = json.loads(raw)
        chunk_count = result.get("chunk_count", len(result.get("chunks", [])))
        p.record("Python SSE: get_history('0016') → chunk_count > 0",
                 chunk_count > 0,
                 f"chunk_count={chunk_count}")
        chunks_sorted = result.get("chunks", [])
        lines = [c.get("start_line", 0) for c in chunks_sorted]
        p.record("Python SSE: get_history('0016') chunks ordered by start_line",
                 lines == sorted(lines),
                 f"start_lines={lines[:6]}")
    except Exception as exc:
        p.record("Python SSE: get_history", False, str(exc))

    print("  [9b] Python SSE — get_history(id='__nonexistent__') → empty …")
    try:
        raw = asyncio.run(_run_sse_tool(py_base, "get_history", {"id": "__nonexistent_9b__"}))
        result = json.loads(raw)
        chunk_count = result.get("chunk_count", len(result.get("chunks", [])))
        p.record("Python SSE: get_history('__nonexistent_9b__') → 0 chunks",
                 chunk_count == 0,
                 f"chunk_count={chunk_count}")
    except Exception as exc:
        p.record("Python SSE: get_history (unknown id)", False, str(exc))

    # ── .NET SSE ──────────────────────────────────────────────────────────────
    print(f"\n  [9c] .NET SSE — get_history(id='0016') (port {DOTNET_SSE_PORT}) …")
    try:
        with httpx.Client(base_url=dn_base, timeout=60) as client:
            session_id = _dotnet_initialize(client)
            p.record(".NET SSE: get_history — MCP initialize", bool(session_id),
                     f"session={session_id[:8]}…" if session_id else "no session")

            def _call_raw(tool: str, args: dict) -> str:
                r = _dotnet_post(client, {
                    "jsonrpc": "2.0", "id": 20, "method": "tools/call",
                    "params": {"name": tool, "arguments": args},
                }, session_id=session_id)
                return r.get("result", {}).get("content", [{}])[0].get("text", "")

            text = _call_raw("get_history", {"id": "0016"})
            result_dn = json.loads(text) if text else {}
            chunk_count_dn = result_dn.get("chunk_count", len(result_dn.get("chunks", [])))
            p.record(".NET SSE: get_history('0016') → chunk_count > 0",
                     chunk_count_dn > 0,
                     f"chunk_count={chunk_count_dn}")

            print("  [9d] .NET SSE — get_history(id='__nonexistent__') → empty …")
            text_none = _call_raw("get_history", {"id": "__nonexistent_9d__"})
            result_none = json.loads(text_none) if text_none else {}
            chunk_count_none = result_none.get("chunk_count",
                                               len(result_none.get("chunks", [])))
            p.record(".NET SSE: get_history('__nonexistent_9d__') → 0 chunks",
                     chunk_count_none == 0,
                     f"chunk_count={chunk_count_none}")

    except Exception as exc:
        p.record(".NET SSE: get_history", False, str(exc))

    p.finish()
    return p


# ══════════════════════════════════════════════════════════════════════════════
# Main
# ══════════════════════════════════════════════════════════════════════════════

def main() -> int:
    parser = argparse.ArgumentParser(
        description="Full E2E pipeline test for Python and .NET RAG MCP servers"
    )
    parser.add_argument("--phase", type=int, metavar="N",
                        help="Run only phase N (0–9)")
    parser.add_argument("--skip-build", action="store_true",
                        help="Skip phase 2 (Docker build)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Only run phase 0 (prerequisites check)")
    args = parser.parse_args()

    print(f"\n{BANNER}")
    print(f"  RAG Full Pipeline Test  —  {datetime.now(timezone.utc).strftime('%Y-%m-%d %H:%M UTC')}")
    print(f"  Workspace: {WORKSPACE}")
    print(BANNER)

    phases = [
        (0, phase_0_prerequisites),
        (1, phase_1_stop_sse),
        (2, lambda: phase_2_docker_build(skip=args.skip_build)),
        (3, phase_3_python_stdio),
        (4, phase_4_dotnet_stdio),
        (5, phase_5_sse),
        (6, phase_6_flow_queries),
        (7, phase_7_hosted_ingest),
        (9, phase_9_get_history),
        (8, lambda: phase_8_report(ALL_RESULTS)),
    ]

    if args.dry_run:
        phases = [(0, phase_0_prerequisites)]
    elif args.phase is not None:
        phases = [(n, fn) for n, fn in phases if n == args.phase]
        if not phases:
            print(f"Unknown phase {args.phase}. Must be 0–9.")
            return 2

    for _, fn in phases:
        result = fn()
        ALL_RESULTS.append(result)
        if not result.ok and _ == 0:
            print("\n  Prerequisites failed — aborting.")
            return 1

    print(f"\n{BANNER}")
    total_checks = sum(len(r.items) for r in ALL_RESULTS)
    total_failed = sum(sum(1 for _, ok, _ in r.items if not ok) for r in ALL_RESULTS)
    if total_failed:
        print(f"  FINAL RESULT: {total_failed}/{total_checks} checks FAILED")
        for r in ALL_RESULTS:
            for label, ok, detail in r.items:
                if not ok:
                    print(f"    ✗  [{r.name}] {label}")
                    if detail:
                        print(f"         {detail[:100]}")
        return 1

    print(f"  FINAL RESULT: ALL {total_checks} CHECKS PASSED ✓")
    return 0


if __name__ == "__main__":
    sys.exit(main())
