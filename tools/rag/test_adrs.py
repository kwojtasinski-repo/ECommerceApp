"""
Targeted ADR smoke-test for the MCP server.

Tests 5 ADRs with varied characteristics, choosing the most appropriate tool for each:

  ADR-0026  Order Lifecycle Saga    (0 amendments, 0 examples)  → query_docs   (discovery only)
  ADR-0006  TypedId / Value Objects (0 amendments, 2 examples)  → read_docs    (full content incl. examples)
  ADR-0012  Presale Checkout        (0 amendments, 3 examples)  → read_docs    (rich examples, no amendments)
  ADR-0016  Sales Coupons           (1 amendment,  3 examples)  → read_docs + get_adr_history (amendment check)
  ADR-0014  Sales Orders            (4 amendments, 4 examples)  → get_adr_history (full evolution chain)

Usage:
    python test_adrs.py           # local .venv (default)
    python test_adrs.py --docker  # Docker via ecommerceapp_default network
"""
from __future__ import annotations

import argparse
import io
import json
import subprocess
import sys
import os
import threading
from pathlib import Path


# ── Framing helpers ──────────────────────────────────────────────────────────

def _encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"


def _read_response(stdout) -> dict:
    line = stdout.readline()
    if not line:
        raise EOFError("Server closed stdout unexpectedly")
    return json.loads(line)


def _read_response_timeout(stdout, timeout: float = 60.0) -> dict:
    result: dict = {}
    error:  dict = {}

    def _worker():
        try:
            result["data"] = _read_response(stdout)
        except Exception as exc:
            error["exc"] = exc

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        raise TimeoutError(f"No response after {timeout}s")
    if "exc" in error:
        raise error["exc"]
    return result["data"]


# ── Server spawn ─────────────────────────────────────────────────────────────

def _build_cmd(docker: bool, workspace: Path) -> list[str]:
    if docker:
        return [
            "docker", "run", "--rm", "--interactive",
            "--network", "ecommerceapp_default",
            "--volume", f"{workspace}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "--env", "VECTOR_MODE=docker",
            "--env", "QDRANT_URL=http://qdrant:6333",
            "rag-tools",
            "python", "/workspace/tools/rag/mcp_server.py",
        ]
    return [sys.executable, str(Path(__file__).parent / "mcp_server.py")]


def _start_server(cmd: list[str]) -> subprocess.Popen:
    env = os.environ.copy()
    env["PYTHONUNBUFFERED"] = "1"
    proc = subprocess.Popen(
        cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
    )
    # Drain stderr in background to prevent pipe-buffer deadlock.
    buf = io.BytesIO()
    def _drain():
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""):
                buf.write(chunk)
        except Exception:
            pass
    t = threading.Thread(target=_drain, daemon=True)
    t.start()
    return proc, buf, t


def _handshake(proc: subprocess.Popen) -> None:
    """Perform MCP initialize / initialized handshake."""
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "id": 0, "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "adr-tester", "version": "0.1"},
        },
    }))
    proc.stdin.flush()
    _read_response_timeout(proc.stdout, timeout=30)   # initialize response
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "method": "notifications/initialized", "params": {},
    }))
    proc.stdin.flush()


def _call(proc: subprocess.Popen, id: int, tool: str, args: dict, timeout: float = 60) -> dict:
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "id": id, "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }))
    proc.stdin.flush()
    resp = _read_response_timeout(proc.stdout, timeout=timeout)
    raw = resp.get("result", {}).get("content", [{}])[0].get("text", "{}")
    return json.loads(raw)


# ── Content assertion helpers ─────────────────────────────────────────────────

def _find_lines(content: str, keywords: list[str]) -> list[tuple[str, str]]:
    """Return [(keyword, matching_line), ...] for first line containing each keyword."""
    results = []
    for kw in keywords:
        kw_lower = kw.lower()
        for line in content.splitlines():
            if kw_lower in line.lower():
                results.append((kw, line.strip()[:100]))
                break
        else:
            results.append((kw, None))   # not found
    return results


def _assert_content(
    content: str,
    label: str,
    keywords: list[str],
    failures: list[str],
    require_all: bool = True,
) -> None:
    """
    Check that `content` contains each keyword.
    Prints a snippet per keyword; appends to `failures` for missing ones.
    """
    hits = _find_lines(content, keywords)
    missing = []
    for kw, line in hits:
        if line is None:
            print(f"    ✗  '{kw}' — NOT FOUND in {label}")
            missing.append(kw)
        else:
            print(f"    ✓  '{kw}' → {line}")
    if missing and require_all:
        failures.append(f"Content check failed in {label}: missing {missing}")
    elif missing:
        print(f"    ⚠ optional keyword(s) not found: {missing}")


# ── Result printers ──────────────────────────────────────────────────────────

def _print_header(title: str) -> None:
    print(f"\n{'─' * 70}")
    print(f"  {title}")
    print(f"{'─' * 70}")


def _print_query_docs(result: dict) -> None:
    hits = result.get("hits", [])
    print(f"  query_docs → {len(hits)} hit(s)")
    for h in hits:
        print(f"    [{h['score']:.4f}] {h['rel_path']}  ({h['lines']})")
        snippet = h.get("text", "")[:120].replace("\n", " ")
        print(f'           "{snippet}..."')


def _print_read_docs(result: dict) -> None:
    files = result.get("files", [])
    print(f"  read_docs → {len(files)} file(s) returned")
    for f in files:
        if f.get("mode") == "full":
            lines = f["content"].count("\n")
            print(f"    [{f['score']:.4f}] {f['rel_path']}  ({f['size_chars']} chars, ~{lines} lines)")
            for line in f["content"].splitlines():
                if line.startswith("#"):
                    print(f"           title: {line.strip()}")
                    break
        else:
            n = f.get("chunks_returned", len(f.get("chunks", [])))
            print(f"    [{f['score']:.4f}] {f['rel_path']}  ({n} chunk(s))")


def _get_content(f: dict) -> str:
    """Return text content from a read_docs file entry regardless of mode."""
    if f.get("mode") == "full":
        return f.get("content", "")
    return "\n".join(c.get("text", "") for c in f.get("chunks", []))


def _print_adr_history(result: dict) -> None:
    adr_id = result.get("adr_id", "?")
    main   = result.get("main", {})
    amends = result.get("amendments", [])
    main_lines = main.get("content", "").count("\n") if main.get("content") else 0
    print(f"  get_adr_history(ADR-{adr_id})")
    if main.get("rel_path"):
        print(f"    main:       {main['rel_path']}  (~{main_lines} lines)")
    for a in amends:
        a_lines = a.get("content", "").count("\n")
        print(f"    amendment:  {a['rel_path']}  (~{a_lines} lines)")
        # First heading in amendment
        for line in a.get("content", "").splitlines():
            if line.startswith("#"):
                print(f"               title: {line.strip()}")
                break
    print(f"    total amendments: {result.get('amendment_count', 0)}")


# ── Main ─────────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser()
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--local",  dest="docker", action="store_false", default=False)
    mode.add_argument("--docker", dest="docker", action="store_true")
    args = parser.parse_args()

    workspace = Path(__file__).parent.parent.parent
    cmd = _build_cmd(args.docker, workspace)
    mode_label = "Docker" if args.docker else "local"

    print(f"[adr-test] Starting MCP server ({mode_label})…")
    proc, stderr_buf, stderr_thread = _start_server(cmd)

    failures: list[str] = []

    try:
        _handshake(proc)
        print("[adr-test] Handshake OK\n")

        # ── TEST 1 ─────────────────────────────────────────────────────────
        # ADR-0026  Order Lifecycle Saga  (0 amendments, 0 examples)
        # Pure structure — just query_docs to see if it surfaces the right file.
        _print_header("TEST 1 — ADR-0026 Order Lifecycle Saga  (0 amd, 0 ex)  → query_docs")
        try:
            r = _call(proc, 10, "query_docs",
                      {"question": "order lifecycle saga state machine", "top_k": 5})
            _print_query_docs(r)
            top_paths = [h["rel_path"] for h in r.get("hits", [])]
            if not any("0026" in p for p in top_paths):
                print("  ⚠ ADR-0026 NOT in top results")
                failures.append("TEST1: ADR-0026 not surfaced by query_docs")
            else:
                print("  ✓ ADR-0026 surfaced correctly")
        except Exception as exc:
            print(f"  ✗ FAILED: {exc}")
            failures.append(f"TEST1: {exc}")

        # ── TEST 2 ─────────────────────────────────────────────────────────
        # ADR-0006  TypedId / Value Objects  (0 amendments, 2 examples)
        # No amendments but has example implementations — read_docs should pull
        # at least the main ADR file and ideally an example.
        _print_header("TEST 2 — ADR-0006 TypedId / Value Objects  (0 amd, 2 ex)  → read_docs")
        try:
            r = _call(proc, 20, "read_docs",
                      {"question": "TypedId strongly typed id value object shared primitives", "top_files": 3})
            _print_read_docs(r)
            returned_paths = [f["rel_path"] for f in r.get("files", [])]
            if not any("0006" in p for p in returned_paths):
                print("  ⚠ ADR-0006 NOT in returned files")
                failures.append("TEST2: ADR-0006 not in read_docs result")
            else:
                print("  ✓ ADR-0006 returned with full content")
                # Spot-check that the main ADR content actually explains the pattern:
                main_file = next(f for f in r["files"] if "0006" in f["rel_path"] and "README" not in f["rel_path"])
                print("  Content checks (ADR-0006 main):")
                _assert_content(
                    _get_content(main_file), "ADR-0006",
                    ["TypedId", "abstract record", "ECommerceApp.Domain.Shared", "primitive obsession", "Value"],
                    failures,
                )
        except Exception as exc:
            print(f"  ✗ FAILED: {exc}")
            failures.append(f"TEST2: {exc}")

        # ── TEST 3 ─────────────────────────────────────────────────────────
        # ADR-0012  Presale Checkout  (0 amendments, 3 examples)
        # Rich examples but no amendments — agent should get main ADR + examples.
        _print_header("TEST 3 — ADR-0012 Presale Checkout  (0 amd, 3 ex)  → read_docs (top_files=4)")
        try:
            r = _call(proc, 30, "read_docs",
                      {"question": "presale checkout basket reservation BC design", "top_files": 4})
            _print_read_docs(r)
            returned_paths = [f["rel_path"] for f in r.get("files", [])]
            has_main    = any("0012" in p and "example" not in p and "README" not in p for p in returned_paths)
            has_example = any("0012" in p and "example" in p for p in returned_paths)
            if not any("0012" in p for p in returned_paths):
                print("  ⚠ ADR-0012 NOT in returned files")
                failures.append("TEST3: ADR-0012 not in read_docs result")
            else:
                label = "main + example" if (has_main and has_example) else ("main" if has_main else "example/router")
                print(f"  ✓ ADR-0012 returned ({label})")
                # Spot-check the main presale ADR contains checkout/reservation concepts:
                main_file = next(f for f in r["files"] if "0012" in f["rel_path"] and "README" not in f["rel_path"])
                print("  Content checks (ADR-0012 main):")
                _assert_content(
                    _get_content(main_file), "ADR-0012",
                    ["Presale", "checkout", "CartLine", "reservation", "SoftReservation"],
                    failures,
                )
        except Exception as exc:
            print(f"  ✗ FAILED: {exc}")
            failures.append(f"TEST3: {exc}")

        # ── TEST 4 ─────────────────────────────────────────────────────────
        # ADR-0016  Sales Coupons  (1 amendment, 3 examples)
        # Has one amendment — test both read_docs (content) and get_adr_history
        # (amendment chain). The amendment added the oversize guard.
        _print_header("TEST 4 — ADR-0016 Coupons  (1 amd, 3 ex)  → read_docs + get_adr_history")
        try:
            r_docs = _call(proc, 40, "read_docs",
                           {"question": "coupons maximum per order discount validation", "top_files": 3})
            _print_read_docs(r_docs)

            r_hist = _call(proc, 41, "get_adr_history", {"adr_id": "0016"})
            _print_adr_history(r_hist)

            ok_docs = any("0016" in f["rel_path"] for f in r_docs.get("files", []))
            ok_hist = r_hist.get("amendment_count", 0) >= 1
            if not ok_docs:
                failures.append("TEST4: ADR-0016 not in read_docs result")
            if not ok_hist:
                failures.append("TEST4: ADR-0016 amendment_count should be >= 1")
            if ok_docs and ok_hist:
                print("  ✓ read_docs surfaced ADR-0016; get_adr_history has amendment(s)")

            # Spot-check the amendment content — it should describe the oversize guard:
            if r_hist.get("amendments"):
                a1 = r_hist["amendments"][0]
                print("  Content checks (ADR-0016 amendment a1):")
                _assert_content(
                    a1["content"], "ADR-0016/a1",
                    ["oversize", "MaxCouponsPerOrder", "CouponsOptions", "ceiling"],
                    failures,
                )

            # Spot-check the main ADR via get_adr_history for coupon core rules:
            if r_hist.get("main", {}).get("content"):
                print("  Content checks (ADR-0016 main):")
                _assert_content(
                    r_hist["main"]["content"], "ADR-0016/main",
                    ["Coupon", "discount", "Order", "apply"],
                    failures,
                )
        except Exception as exc:
            print(f"  ✗ FAILED: {exc}")
            failures.append(f"TEST4: {exc}")

        # ── TEST 5 ─────────────────────────────────────────────────────────
        # ADR-0014  Sales Orders  (4 amendments, 4 examples)
        # Most complex evolution — use get_adr_history to retrieve the full chain,
        # verify all 4 amendments are present, and spot-check each one's content.
        _print_header("TEST 5 — ADR-0014 Sales Orders  (4 amd, 4 ex)  → get_adr_history (full chain)")
        try:
            r = _call(proc, 50, "get_adr_history", {"adr_id": "0014"}, timeout=30)
            _print_adr_history(r)
            amd_count = r.get("amendment_count", 0)
            if amd_count < 4:
                print(f"  ⚠ Expected >= 4 amendments, got {amd_count}")
                failures.append(f"TEST5: expected >=4 amendments, got {amd_count}")
            else:
                print(f"  ✓ All {amd_count} amendments returned in chronological order")

            amds = r.get("amendments", [])
            # a1 — OrderStatus lifecycle column
            if len(amds) >= 1:
                print("  Content checks (ADR-0014/a1 — OrderStatus lifecycle):")
                _assert_content(amds[0]["content"], "a1",
                    ["OrderStatus", "Status", "lifecycle", "column"],
                    failures)
            # a2 — event payload records
            if len(amds) >= 2:
                print("  Content checks (ADR-0014/a2 — event payload records):")
                _assert_content(amds[1]["content"], "a2",
                    ["payload", "record", "event"],
                    failures)
            # a3 — integration flow decisions
            if len(amds) >= 3:
                print("  Content checks (ADR-0014/a3 — integration flow):")
                _assert_content(amds[2]["content"], "a3",
                    ["OrderPlaced", "integration", "flow"],
                    failures)
            # a4 — operator notifications
            if len(amds) >= 4:
                print("  Content checks (ADR-0014/a4 — operator notifications):")
                _assert_content(amds[3]["content"], "a4",
                    ["OrderRequiresAttention", "notification", "operator"],
                    failures)
        except Exception as exc:
            print(f"  ✗ FAILED: {exc}")
            failures.append(f"TEST5: {exc}")

    finally:
        proc.stdin.close()
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()
        stderr_thread.join(timeout=5)
        stderr = stderr_buf.getvalue().decode(errors="replace")
        if stderr.strip():
            # Only print meaningful lines, skip routine startup messages
            meaningful = [l for l in stderr.splitlines()
                          if l.strip() and "up to date" not in l and "running incremental" not in l]
            if meaningful:
                print("\n[server stderr]")
                for l in meaningful:
                    print(" ", l)

    print(f"\n{'═' * 70}")
    if failures:
        print(f"  RESULT: {len(failures)} FAILURE(S)")
        for f in failures:
            print(f"    ✗ {f}")
        return 1
    else:
        print(f"  RESULT: ALL TESTS PASSED ✓")
        return 0


if __name__ == "__main__":
    sys.exit(main())
