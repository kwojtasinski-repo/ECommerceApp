"""
E2E incremental conversational flow tests for the MCP RAG server.

Each flow simulates a developer discovering patterns and decisions by asking
progressively more specific questions — exactly as they would when chatting
with an AI assistant backed by this RAG index.

  Step 1 (query_docs)  — broad discovery: "what is this?"
  Step 2 (read_docs)   — first read: "show me the design"
  Step 3 (history/drill) — deep dive: "show me the exact rules / every change"

The expected text assertions verify that the actual content reaching the agent
contains the right concepts — not just that the right file was returned.

Flows (13):
  01  TypedId pattern discovery              → ADR-0006
  02  Applying a coupon to an order          → ADR-0016 (main)
  03  Coupon limit business rules            → ADR-0016 + amendment a1  (follow-up to 02)
  04  Order status lifecycle                 → ADR-0014 + all 4 amendments
  05  Cross-BC communication pattern         → ADR-0010
  06  Presale / checkout design              → ADR-0012
  07  Inventory availability                 → ADR-0011
  08  Architectural vision from event storming → ADR-0002
  09  Operator notifications when order stalls → ADR-0014/a4
  10  Refunds and fulfillment                → ADR-0017
  11  Known .NET upgrade issues              → known-issues.md
  12  Saga decision investigation            → ADR-0026
  13  EF Core per-BC DbContext pattern       → ADR-0013

Usage:
    python test_flows.py              # local .venv (query_docs times out gracefully)
    python test_flows.py --docker     # Docker + Qdrant (full test)
    python test_flows.py --docker --flow 4   # run only flow 4
"""
from __future__ import annotations

import argparse
import io
import json
import os
import subprocess
import sys
import threading
from pathlib import Path

# Ensure UTF-8 output even on Windows consoles (box-drawing characters, etc.)
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


# ── Transport helpers (NDJSON — one JSON per line) ───────────────────────────

def _encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"

def _read_response(stdout) -> dict:
    line = stdout.readline()
    if not line:
        raise EOFError("Server closed stdout")
    return json.loads(line)

def _read_with_timeout(stdout, timeout: float = 60.0) -> dict:
    result: dict = {}
    error:  dict = {}
    def _w():
        try:    result["v"] = _read_response(stdout)
        except Exception as exc: error["e"] = exc
    t = threading.Thread(target=_w, daemon=True)
    t.start(); t.join(timeout)
    if t.is_alive(): raise TimeoutError(f"No response after {timeout}s")
    if "e" in error: raise error["e"]
    return result["v"]


# ── Server management ─────────────────────────────────────────────────────────

def _build_cmd(docker: bool, workspace: Path) -> list[str]:
    if docker:
        # RAG_WORKSPACE=/workspace is the only knob — no hardcoded /workspace/ paths.
        # The image WORKDIR is /app; scripts baked there run without path qualification.
        # mcp_server.py derives config as: $RAG_WORKSPACE/tools/rag/rag-config.yaml.
        return [
            "docker", "run", "--rm", "--interactive",
            "--network", "ecommerceapp_default",
            "--volume", f"{workspace}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "--env", "VECTOR_MODE=docker",
            "--env", "QDRANT_URL=http://qdrant:6333",
            "rag-tools", "python", "mcp_server.py",
        ]
    server_py = Path(__file__).parent / "mcp_server.py"
    config_py = Path(__file__).parent / "rag-config.yaml"
    return [sys.executable, str(server_py), "--config", str(config_py)]

def _start(cmd: list[str]):
    env = os.environ.copy(); env["PYTHONUNBUFFERED"] = "1"
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, env=env)
    buf = io.BytesIO()
    def _drain():
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""): buf.write(chunk)
        except Exception: pass
    t = threading.Thread(target=_drain, daemon=True); t.start()
    return proc, buf, t

def _handshake(proc):
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "id": 0, "method": "initialize",
        "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                   "clientInfo": {"name": "flow-tester", "version": "0.1"}},
    })); proc.stdin.flush()
    _read_with_timeout(proc.stdout, 30)
    proc.stdin.write(_encode({"jsonrpc":"2.0","method":"notifications/initialized","params":{}}))
    proc.stdin.flush()

_ID = [200]
def _next_id() -> int:
    _ID[0] += 1
    return _ID[0]

def _call(proc, tool: str, args: dict, timeout: float = 60) -> dict:
    proc.stdin.write(_encode({"jsonrpc":"2.0","id":_next_id(),"method":"tools/call",
                              "params":{"name":tool,"arguments":args}}))
    proc.stdin.flush()
    resp = _read_with_timeout(proc.stdout, timeout)
    raw  = resp.get("result",{}).get("content",[{}])[0].get("text","{}")
    return json.loads(raw)


# ── Output / assertion helpers ────────────────────────────────────────────────

def _ph(label: str) -> None:
    print(f"\n    [ {label} ]")

def _print_hits(r: dict) -> None:
    for h in r.get("hits", []):
        snippet = h.get("text","")[:90].replace("\n"," ")
        print(f"      [{h['score']:.3f}] {h['rel_path']}")
        print(f"             \"{snippet}...\"")

def _print_files(r: dict) -> None:
    mode = r.get("mode", "?")
    for f in r.get("files", []):
        if f.get("mode") == "full":
            lines = f["content"].count("\n")
            print(f"      [{f['score']:.3f}] {f['rel_path']}  ({f['size_chars']} chars, ~{lines} lines)")
        else:
            n = f.get("chunks_returned", len(f.get("chunks", [])))
            print(f"      [{f['score']:.3f}] {f['rel_path']}  ({n} chunk(s))")

def _had_adr(paths: list[str], adr_id: str, soft: bool = False) -> bool:
    found = any(adr_id in p for p in paths)
    icon = "✓" if found else ("⚠" if soft else "⚠")
    label = "surfaced" if found else "NOT surfaced"
    print(f"      {icon}  ADR-{adr_id} {label}")
    return found

def _file_content(result: dict, adr_id: str, exclude: str | None = "README") -> str | None:
    """Return searchable text for the first file in result whose path contains adr_id.

    Handles both response modes:
      - full mode  (f['mode'] == 'full'): uses f['content'] directly.
      - chunks mode (f['mode'] == 'chunks'): joins all chunk texts — enough for keyword assertions.
    """
    for f in result.get("files", []):
        p = f["rel_path"]
        if adr_id in p and (not exclude or exclude not in p):
            if f.get("mode") == "full":
                return f.get("content", "")
            # chunks mode — concatenate all chunk texts
            chunks = f.get("chunks", [])
            return "\n".join(c.get("text", "") for c in chunks)
    return None

def _check(content: str, label: str, keywords: list[str],
           failures: list[str], require_all: bool = True) -> None:
    """
    For each keyword, find and print the first matching line.
    Appends to failures when require_all=True and any keyword is missing.
    """
    missing = []
    for kw in keywords:
        for line in content.splitlines():
            if kw.lower() in line.lower():
                print(f"      ✓  '{kw}'")
                print(f"           → {line.strip()[:105]}")
                break
        else:
            print(f"      ✗  '{kw}'  — NOT FOUND in {label}")
            missing.append(kw)
    if missing and require_all:
        failures.append(f"[{label}] missing keywords: {missing}")


def _check_rationale(content: str, label: str, keywords: list[str],
                     failures: list[str]) -> None:
    """
    Like _check but all keywords are mandatory and prefixed with 'WHY:'.
    Used for decision-rationale / alternatives-considered assertions.
    """
    print(f"      WHY check ({label}):")
    _check(content, label, keywords, failures, require_all=True)


# ══════════════════════════════════════════════════════════════════ FLOW CASES

def flow_01(proc, failures):
    """'How does the project handle entity IDs? I see Guid fields everywhere.'"""
    print("\n╔══ FLOW 01 — TypedId pattern discovery  (ADR-0006)")

    _ph("Step 1 — broad: query_docs('entity ID pattern domain strongly typed')")
    try:
        r = _call(proc, "query_docs", {"question": "entity ID pattern domain strongly typed identifiers", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0006", soft=True)
    except TimeoutError: print("      ⚠ timed out (no index?)")

    _ph("Step 2 — drill: read_docs('TypedId abstract record shared primitives')")
    try:
        r2 = _call(proc, "read_docs", {"question": "TypedId abstract record shared domain primitives ECommerceApp", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0006")
        if not content:
            failures.append("FLOW01: ADR-0006 not returned by read_docs")
            return
        print("      Expected text — core TypedId definition:")
        _check(content, "ADR-0006", [
            "TypedId",
            "abstract record",
            "ECommerceApp.Domain.Shared",
            "primitive obsession",   # always in the first context chunk
            "Value",
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW01 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0006') \u2014 alternatives section")
    try:
        hist01 = _call(proc, "get_adr_history", {"adr_id": "0006"})
        main01 = hist01.get("main", {}).get("content", "")
        if main01:
            _check_rationale(main01, "ADR-0006 rationale", [
                "Rejected",             # StronglyTypedId NuGet rejected
                "Alternatives considered",
                "Compile-time",         # key benefit stated in ADR
            ], failures)
        else:
            failures.append("FLOW01 step2b: ADR-0006 main content empty")
    except TimeoutError as e: failures.append(f"FLOW01 step2b: {e}")

    _ph("Step 3 — deep: read_docs('concrete GUID TypedId example OrderId')")
    try:
        r3 = _call(proc, "read_docs", {"question": "TypedId GuidTypedId concrete domain ID implementation example OrderId CustomerId", "top_files": 3})
        _print_files(r3)
        content = _file_content(r3, "0006")
        if content:
            print("      Expected text — Guid-based id:")
            _check(content, "ADR-0006 (examples)", [
                "TypedId",
                "Guid",
                "record",
            ], failures)
        else:
            print("      ⚠ ADR-0006 not in top files for concrete-example query (soft)")
    except TimeoutError as e: print(f"      ⚠ timed out: {e}")


def flow_02(proc, failures):
    """'I need to add a coupon to an order checkout — what service do I use?'"""
    print("\n╔══ FLOW 02 — Applying a coupon to an order  (ADR-0016 main)")

    _ph("Step 1 — broad: query_docs('apply coupon order discount')")
    try:
        r = _call(proc, "query_docs", {"question": "apply coupon order discount service method", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0016", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('ICouponService apply remove coupon order')")
    try:
        r2 = _call(proc, "read_docs", {"question": "ICouponService coupon apply remove discount order validation", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0016")
        if not content:
            failures.append("FLOW02: ADR-0016 not returned by read_docs")
            return
        print("      Expected text — coupon service contract:")
        _check(content, "ADR-0016/main", [
            "ICouponService",
            "apply",
            "Coupon",
            "discount",
            "Order",
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW02 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0016') \u2014 alternatives section")
    try:
        hist02 = _call(proc, "get_adr_history", {"adr_id": "0016"})
        main02 = hist02.get("main", {}).get("content", "")
        if main02:
            _check_rationale(main02, "ADR-0016 rationale", [
                "Alternatives considered",
                "tight coupling",       # direct synchronous call creates tight coupling
            ], failures)
        else:
            failures.append("FLOW02 step2b: ADR-0016 main content empty")
    except TimeoutError as e: failures.append(f"FLOW02 step2b: {e}")


def flow_03(proc, failures):
    """'Wait — can I apply unlimited coupons? Is there a cap?'  (follow-up to flow 02)"""
    print("\n╔══ FLOW 03 — Coupon count limit / oversize guard  (ADR-0016 a1)")

    _ph("Step 1 — broad: query_docs('coupon maximum per order limit guard')")
    try:
        r = _call(proc, "query_docs", {"question": "coupon maximum limit per order validation guard ceiling", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0016", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('MaxCouponsPerOrder ceiling CouponsOptions oversize')")
    try:
        r2 = _call(proc, "read_docs", {"question": "MaxCouponsPerOrder ceiling oversize guard CouponsOptions coupon per order", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0016")
        if not content:
            failures.append("FLOW03: ADR-0016 (amendment) not returned by read_docs")
            return
        print("      Expected text — hard limit numbers:")
        _check(content, "ADR-0016/a1", [
            "MaxCouponsPerOrder",
            "ceiling",
            "5",
            "10",
            "CouponsOptions",
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW03 step2: {e}")

    _ph("Step 3 — history: get_adr_history('0016') — confirm amendment is documented")
    try:
        r3 = _call(proc, "get_adr_history", {"adr_id": "0016"})
        count = r3.get("amendment_count", 0)
        print(f"      amendment_count: {count}")
        if count < 1:
            failures.append("FLOW03: ADR-0016 should have >= 1 amendment (oversize guard)")
        else:
            print("      ✓ amendment exists — guard is formally documented")
            a1_content = r3["amendments"][0]["content"]
            print("      Expected text — oversize guard in amendment body:")
            _check(a1_content, "ADR-0016/a1 via history", [
                "oversize",
                "MaxCouponsPerOrder",
                "EnableOversizeGuard",
            ], failures)
            _check_rationale(a1_content, "ADR-0016/a1 WHY guard exists", [
                "wasteful",             # 'prevents wasteful application of fixed-amount coupons'
            ], failures)
    except TimeoutError as e: failures.append(f"FLOW03 step3: {e}")


def flow_04(proc, failures):
    """'How does an order move through its statuses — from placed to completed?'"""
    print("\n╔══ FLOW 04 — Order status lifecycle  (ADR-0014 + all 4 amendments)")

    _ph("Step 1 — broad: query_docs('order status lifecycle column state')")
    try:
        r = _call(proc, "query_docs", {"question": "order status lifecycle column state machine payment", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0014", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — full history: get_adr_history('0014')")
    try:
        r2 = _call(proc, "get_adr_history", {"adr_id": "0014"})
        count = r2.get("amendment_count", 0)
        print(f"      amendment_count: {count}")
        for a in r2.get("amendments", []):
            print(f"      {a['rel_path']}  (~{a['content'].count(chr(10))} lines)")
        if count < 4:
            failures.append(f"FLOW04: expected >=4 amendments, got {count}")

        main = r2.get("main", {}).get("content", "")
        print("      Expected text — main ADR covers sales orders:")
        _check(main, "ADR-0014/main", ["Order", "Status", "sales"], failures)

        amds = r2.get("amendments", [])
        if len(amds) >= 1:
            print("      Expected text — a1 OrderStatus lifecycle column:")
            _check(amds[0]["content"], "ADR-0014/a1", ["OrderStatus", "lifecycle", "column"], failures)
        if len(amds) >= 2:
            print("      Expected text — a2 event payload records:")
            _check(amds[1]["content"], "ADR-0014/a2", ["payload", "record", "event"], failures)
        if len(amds) >= 3:
            print("      Expected text — a3 integration flow decisions:")
            _check(amds[2]["content"], "ADR-0014/a3", ["OrderPlaced", "integration", "flow"], failures)
        if len(amds) >= 4:
            print("      Expected text — a4 operator notifications:")
            _check(amds[3]["content"], "ADR-0014/a4", ["OrderRequiresAttention", "notification", "operator"], failures)

        # Rationale check on the main ADR — why UnitCost VO instead of plain decimal:
        _check_rationale(main, "ADR-0014/main WHY VO", [
            "Why a VO instead of plain",   # exact phrase from ADR §14
            "compile-time type safety",
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW04 step2: {e}")


def flow_05(proc, failures):
    """'How do bounded contexts communicate with each other — events, direct calls?'"""
    print("\n╔══ FLOW 05 — Cross-BC communication pattern  (ADR-0010)")

    _ph("Step 1 — broad: query_docs('bounded context communication events message')")
    try:
        r = _call(proc, "query_docs", {"question": "cross bounded context communication events message bus publish", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0010", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('IMessage IMessageHandler in-memory broker')")
    try:
        r2 = _call(proc, "read_docs", {"question": "IMessage IMessageHandler in-memory broker publish subscribe cross BC events", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0010")
        if not content:
            failures.append("FLOW05: ADR-0010 not returned by read_docs")
            return
        print("      Expected text — messaging contract:")
        _check(content, "ADR-0010", [
            "IMessage",
            "IMessageHandler",
            "broker",
        ], failures)
        print("      Expected text — cross-BC scoping (optional):")
        _check(content, "ADR-0010 (optional)", [
            "cross",
            "publish",
        ], failures, require_all=False)
    except TimeoutError as e: failures.append(f"FLOW05 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0010') \u2014 alternatives section")
    try:
        hist05 = _call(proc, "get_adr_history", {"adr_id": "0010"})
        main05 = hist05.get("main", {}).get("content", "")
        if main05:
            _check_rationale(main05, "ADR-0010 WHY not MediatR/direct injection", [
                "rejected",             # direct injection rejected, MediatR rejected
                "Alternatives considered",
            ], failures)
        else:
            failures.append("FLOW05 step2b: ADR-0010 main content empty")
    except TimeoutError as e: failures.append(f"FLOW05 step2b: {e}")


def flow_06(proc, failures):
    """'I'm implementing checkout — how does the presale/basket reservation flow work?'"""
    print("\n╔══ FLOW 06 — Presale / checkout design  (ADR-0012)")

    _ph("Step 1 — broad: query_docs('checkout basket reservation presale')")
    try:
        r = _call(proc, "query_docs", {"question": "checkout cart reservation presale flow CartLine", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0012", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('Presale CartLine SoftReservation StockSnapshot ACL')")
    try:
        r2 = _call(proc, "read_docs", {"question": "Presale checkout CartLine SoftReservation StockSnapshot ACL anti-corruption", "top_files": 3})
        _print_files(r2)
        content = _file_content(r2, "0012")
        if not content:
            failures.append("FLOW06: ADR-0012 not returned by read_docs")
            return
        print("      Expected text — core presale model:")
        _check(content, "ADR-0012/main", [
            "CartLine",
            "SoftReservation",
            "StockSnapshot",
            "Presale",
            "checkout",
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW06 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0012') \u2014 alternatives section")
    try:
        hist06 = _call(proc, "get_adr_history", {"adr_id": "0012"})
        main06 = hist06.get("main", {}).get("content", "")
        if main06:
            _check_rationale(main06, "ADR-0012 WHY not aggregate/polling", [
                "rejected",             # polling loop rejected; denormalized read model rejected
                "Alternatives considered",
            ], failures)
        else:
            failures.append("FLOW06 step2b: ADR-0012 main content empty")
    except TimeoutError as e: failures.append(f"FLOW06 step2b: {e}")

    _ph("Step 3 — deeper: read_docs('presale ACL IPresaleReadModel stock snapshot')")
    try:
        r3 = _call(proc, "read_docs", {"question": "presale ACL IPresaleReadModel context mapping anti-corruption layer stock", "top_files": 3})
        _print_files(r3)
        content = _file_content(r3, "0012")
        if content:
            print("      Expected text — ACL / context mapping (optional):")
            _check(content, "ADR-0012/main (ACL)", [
                "ACL",
                "Anti-Corruption",
                "context",
            ], failures, require_all=False)
        else:
            print("      ⚠ ADR-0012 not in top-3 for ACL-focused query (soft)")
    except TimeoutError as e: print(f"      ⚠ timed out: {e}")


def flow_07(proc, failures):
    """'Before building checkout I need to understand inventory — how does it work?'"""
    print("\n╔══ FLOW 07 — Inventory availability  (ADR-0011)")

    _ph("Step 1 — broad: query_docs('inventory stock availability check product')")
    try:
        r = _call(proc, "query_docs", {"question": "inventory stock availability check product unit", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0011", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('inventory availability InventoryItem StockLevel')")
    try:
        r2 = _call(proc, "read_docs", {"question": "inventory availability BC design InventoryItem stock level unit in-stock", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0011")
        if not content:
            failures.append("FLOW07: ADR-0011 not returned by read_docs")
            return
        print("      Expected text — inventory model essentials:")
        _check(content, "ADR-0011/main", [
            "inventory",
            "availability",
            "stock",
        ], failures)
        print("      Expected text — quantity / reservation concepts (optional):")
        _check(content, "ADR-0011/main (optional)", [
            "Quantity",
            "reserve",
        ], failures, require_all=False)
    except TimeoutError as e: failures.append(f"FLOW07 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0011') \u2014 alternatives section")
    try:
        hist07 = _call(proc, "get_adr_history", {"adr_id": "0011"})
        main07 = hist07.get("main", {}).get("content", "")
        if main07:
            _check_rationale(main07, "ADR-0011 WHY design choice", [
                "Alternatives considered",
                "Counter pattern",      # rejected alternative: reservations in aggregate
            ], failures)
        else:
            failures.append("FLOW07 step2b: ADR-0011 main content empty")
    except TimeoutError as e: failures.append(f"FLOW07 step2b: {e}")


def flow_08(proc, failures):
    """'What was the original architectural vision? Was there a big design session?'"""
    print("\n╔══ FLOW 08 — Architectural vision from event storming  (ADR-0002)")

    _ph("Step 1 — broad: query_docs('event storming architecture strategy BC')")
    try:
        r = _call(proc, "query_docs", {"question": "event storming architectural evolution strategy domain BC map", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0002", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('event storming process manager saga BC strategy')")
    try:
        r2 = _call(proc, "read_docs", {"question": "event storming post evolutionary strategy process manager Saga bounded context", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0002")
        if not content:
            failures.append("FLOW08: ADR-0002 not returned by read_docs")
            return
        print("      Expected text — architectural strategy essentials:")
        _check(content, "ADR-0002", [
            "Process Manager",
            "Order",
            "BC",
        ], failures)
        print("      Expected text — saga / orchestrator (optional — may use synonyms):")
        _check(content, "ADR-0002 (optional)", [
            "Saga",
            "Orchestrator",
        ], failures, require_all=False)
    except TimeoutError as e: failures.append(f"FLOW08 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0002') \u2014 alternatives section")
    try:
        hist08 = _call(proc, "get_adr_history", {"adr_id": "0002"})
        main08 = hist08.get("main", {}).get("content", "")
        if main08:
            _check_rationale(main08, "ADR-0002 WHY not monolith/microservices", [
                "rejected",             # monolith / microservices / full ES all rejected
                "Alternatives considered",
            ], failures)
        else:
            failures.append("FLOW08 step2b: ADR-0002 main content empty")
    except TimeoutError as e: failures.append(f"FLOW08 step2b: {e}")


def flow_09(proc, failures):
    """'What happens when payment fails or an order needs human intervention?'"""
    print("\n╔══ FLOW 09 — Operator notifications when order needs attention  (ADR-0014/a4)")

    _ph("Step 1 — broad: query_docs('order requires attention operator notification')")
    try:
        r = _call(proc, "query_docs", {"question": "order requires attention operator notification alert manual", "top_k": 5})
        _print_hits(r)
        paths = [h["rel_path"] for h in r.get("hits",[])]
        hit = any("0014" in p for p in paths)
        print(f"      {'✓' if hit else '⚠'}  ADR-0014 (or amendment) {'in' if hit else 'NOT in'} results")
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — history: get_adr_history('0014') → navigate to a4")
    try:
        r2 = _call(proc, "get_adr_history", {"adr_id": "0014"})
        amds = r2.get("amendments", [])
        if len(amds) < 4:
            failures.append(f"FLOW09: expected >=4 amendments, got {len(amds)}")
            return
        a4 = amds[3]
        print(f"      a4 path: {a4['rel_path']}")
        print("      Expected text — notification message contract:")
        _check(a4["content"], "ADR-0014/a4", [
            "OrderRequiresAttention",
            "notification",
            "operator",
        ], failures)
        print("      Expected text — event enrichment context (optional):")
        _check(a4["content"], "ADR-0014/a4 (optional)", [
            "payload",
            "enrichment",
        ], failures, require_all=False)
        # WHY a new message vs. existing one — the amendment should explain motivation:
        _check_rationale(a4["content"], "ADR-0014/a4 WHY message added", [
            "problematic state",    # 'signals operators when an order enters a problematic state'
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW09 step2: {e}")


def flow_10(proc, failures):
    """'How do refunds and shipping work after a payment succeeds?'"""
    print("\n╔══ FLOW 10 — Refunds and fulfillment design  (ADR-0017)")

    _ph("Step 1 — broad: query_docs('refund shipment fulfillment post-payment')")
    try:
        r = _call(proc, "query_docs", {"question": "refund shipment fulfillment post-payment order lifecycle return", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0017", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('fulfillment refund shipment BC design')")
    try:
        r2 = _call(proc, "read_docs", {"question": "fulfillment refund shipment sales BC design post-payment order return", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0017")
        if not content:
            failures.append("FLOW10: ADR-0017 not returned by read_docs")
            return
        print("      Expected text — fulfillment domain model:")
        _check(content, "ADR-0017", [
            "refund",
            "shipment",
            "fulfillment",
        ], failures)
        print("      Expected text — BC placement context:")
        _check(content, "ADR-0017 (optional)", [
            "post-payment",
            "BC",
        ], failures, require_all=False)
        _check_rationale(content, "ADR-0017 WHY separate fulfillment BC", [
            "spread across the wrong BCs",  # exact problem statement from ADR-0017 opener
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW10 step2: {e}")


def flow_11(proc, failures):
    """'Are there any gotchas with FluentAssertions or the .NET version?'"""
    print("\n╔══ FLOW 11 — Known .NET upgrade issues  (known-issues.md)")

    _ph("Step 1 — broad: query_docs('known issues upgrade FluentAssertions NET')")
    try:
        r = _call(proc, "query_docs", {"question": "known issues upgrade problems FluentAssertions dotnet version", "top_k": 5})
        _print_hits(r)
        paths = [h["rel_path"] for h in r.get("hits",[])]
        hit = any("known-issues" in p for p in paths)
        print(f"      {'✓' if hit else '⚠'}  known-issues.md {'surfaced' if hit else 'NOT surfaced'}")
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('FluentAssertions AwesomeAssertions NET 8 upgrade')")
    try:
        r2 = _call(proc, "read_docs", {"question": "FluentAssertions AwesomeAssertions NET 8 upgrade replacement drop-in", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "known-issues")
        if not content:
            failures.append("FLOW11: known-issues.md not returned by read_docs")
            return
        print("      Expected text — testing library replacement rule:")
        _check(content, "known-issues.md", [
            "AwesomeAssertions",
            "FluentAssertions",
        ], failures)
        print("      Expected text — version trigger (optional):")
        _check(content, "known-issues.md (optional)", [
            ".NET 8",
            "KI-",
        ], failures, require_all=False)
        # WHY AwesomeAssertions: the known-issues file explains the migration context:
        _check_rationale(content, "known-issues WHY switch from FluentAssertions", [
            "Migration executed",    # 'Migration executed ahead of .NET 8+ upgrade'
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW11 step2: {e}")


def flow_12(proc, failures):
    """'Is there a saga or process manager coordinating the order flow?'"""
    print("\n╔══ FLOW 12 — Saga decision investigation  (ADR-0026)")

    _ph("Step 1 — broad: query_docs('saga process manager order lifecycle orchestrator')")
    try:
        r = _call(proc, "query_docs", {"question": "saga process manager order lifecycle orchestrator decision deferred", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0026", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — history: get_adr_history('0026') — read the full saga decision record")
    try:
        r2 = _call(proc, "get_adr_history", {"adr_id": "0026"})
        main = r2.get("main", {})
        amds = r2.get("amendments", [])
        print(f"      main: {main.get('rel_path','—')}  (~{main.get('content','').count(chr(10))} lines)")
        print(f"      amendment_count: {r2.get('amendment_count', 0)}")
        content = main.get("content", "")
        if not content:
            failures.append("FLOW12: ADR-0026 main content empty from get_adr_history")
            return
        print("      Expected text — architectural decision (Option A chosen, Option B deferred):")
        _check(content, "ADR-0026", [
            "deferred",
            "Option",
            "Order",
            "Choreography",
        ], failures)
        print("      Expected text — saga mechanism details (optional):")
        _check(content, "ADR-0026 (optional)", [
            "Saga",
            "Compensation",
        ], failures, require_all=False)
        _check_rationale(content, "ADR-0026 WHY Option A over Option B", [
            "Option B",             # Option B explicitly deferred with explanation
            "deferred",
            "consistent with the existing choreography",  # stated reason for Option A
        ], failures)
    except TimeoutError as e: failures.append(f"FLOW12 step2: {e}")


def flow_13(proc, failures):
    """'I'm adding a new BC — what EF Core DbContext pattern should I follow?'"""
    print("\n╔══ FLOW 13 — EF Core per-BC DbContext pattern  (ADR-0013)")

    _ph("Step 1 — broad: query_docs('EF Core DbContext per bounded context schema')")
    try:
        r = _call(proc, "query_docs", {"question": "EF Core DbContext per bounded context schema constants design pattern", "top_k": 5})
        _print_hits(r)
        _had_adr([h["rel_path"] for h in r.get("hits",[])], "0013", soft=True)
    except TimeoutError: print("      ⚠ timed out")

    _ph("Step 2 — read: read_docs('per-BC DbContext schema design-time factory')")
    try:
        r2 = _call(proc, "read_docs", {"question": "per BC DbContext schema constants design-time factory IEntityTypeConfiguration DI", "top_files": 2})
        _print_files(r2)
        content = _file_content(r2, "0013")
        if not content:
            failures.append("FLOW13: ADR-0013 not returned by read_docs")
            return
        print("      Expected text — EF pattern essentials:")
        _check(content, "ADR-0013", [
            "DbContext",
            "interface",
            "per BC",
        ], failures)
        print("      Expected text — per-BC isolation details (optional):")
        _check(content, "ADR-0013 (optional)", [
            "IEntityTypeConfiguration",
            "design-time",
            "AddDbContext",
        ], failures, require_all=False)
    except TimeoutError as e: failures.append(f"FLOW13 step2: {e}")

    _ph("Step 2b \u2014 rationale: get_adr_history('0013') \u2014 alternatives section")
    try:
        hist13 = _call(proc, "get_adr_history", {"adr_id": "0013"})
        main13 = hist13.get("main", {}).get("content", "")
        if main13:
            _check_rationale(main13, "ADR-0013 WHY interfaces in Application not Domain", [
                "rejected",             # Domain / Application placements both rejected
                "Alternatives considered",
                "EF Core type",         # exact reason: DbSet<T> is EF Core, not domain
            ], failures)
        else:
            failures.append("FLOW13 step2b: ADR-0013 main content empty")
    except TimeoutError as e: failures.append(f"FLOW13 step2b: {e}")


# ── Runner ────────────────────────────────────────────────────────────────────

ALL_FLOWS = [
    flow_01, flow_02, flow_03, flow_04, flow_05, flow_06, flow_07,
    flow_08, flow_09, flow_10, flow_11, flow_12, flow_13,
]

def main() -> int:
    parser = argparse.ArgumentParser(description="E2E conversational flow tests for MCP RAG server")
    grp = parser.add_mutually_exclusive_group()
    grp.add_argument("--local",  dest="docker", action="store_false", default=False,
                     help="Use local .venv Python (default)")
    grp.add_argument("--docker", dest="docker", action="store_true",
                     help="Use Docker + Qdrant via ecommerceapp_default network")
    parser.add_argument("--flow", type=int, metavar="N",
                        help=f"Run only flow N (1-{len(ALL_FLOWS)})")
    args = parser.parse_args()

    flows = ALL_FLOWS
    if args.flow:
        idx = args.flow - 1
        if not (0 <= idx < len(ALL_FLOWS)):
            print(f"--flow must be 1-{len(ALL_FLOWS)}")
            return 2
        flows = [ALL_FLOWS[idx]]

    workspace = Path(__file__).parent.parent.parent
    cmd = _build_cmd(args.docker, workspace)
    mode = "Docker + Qdrant" if args.docker else "local (no index)"

    print(f"[flow-test] Starting MCP server ({mode})…")
    print(f"[flow-test] Running {len(flows)} flow(s)")
    proc, stderr_buf, stderr_t = _start(cmd)
    failures: list[str] = []

    try:
        _handshake(proc)
        print("[flow-test] Handshake OK")
        for fn in flows:
            fn(proc, failures)
    finally:
        proc.stdin.close()
        try: proc.wait(timeout=10)
        except subprocess.TimeoutExpired: proc.kill()
        stderr_t.join(timeout=5)
        stderr = stderr_buf.getvalue().decode(errors="replace")
        meaningful = [l for l in stderr.splitlines()
                      if l.strip() and "up to date" not in l and "running incremental" not in l]
        if meaningful:
            print("\n[server stderr]")
            for line in meaningful: print(" ", line)

    n = len(flows)
    print(f"\n{'═' * 70}")
    if failures:
        print(f"  RESULT: {len(failures)} FAILURE(S) across {n} flow(s)")
        for f in failures: print(f"    ✗ {f}")
        return 1
    print(f"  RESULT: ALL {n} FLOW(S) PASSED ✓")
    return 0


if __name__ == "__main__":
    sys.exit(main())
