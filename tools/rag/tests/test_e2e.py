"""End-to-end tests for the RAG pipeline: config → ingest → manifest → MCP tools.

What is tested
--------------
  Config system
    • Config.workspace is derived from --config path (parents[2]) when RAG_WORKSPACE is absent.
    • RAG_WORKSPACE env takes priority over the path-derived workspace.
    • queries.yaml companion file is loaded and exposes named_queries.
    • metadata-rules.yaml companion file drives adr_id_patterns and doc_kind_rules.
    • detect_doc_kind / detect_adr_id use the loaded rules, not the fallback defaults.
    • iter_markdown_files returns all .md files under source_roots.

  Ingest + manifest (SHA-256 tracking)
    • ingest.py --config exits 0 and creates manifest.json + snapshot.qdrant.
    • manifest.json has file_hashes matching each file's actual SHA-256.
    • Every .md file in source_roots appears in the manifest.
    • Incremental run (no --force-full) updates only the hash of the changed file.
    • --force-full regenerates the manifest with the same file count.

  Startup-sync change detection
    • No files reported as changed when nothing has been modified.
    • A modified file is detected; unchanged files keep the same hash.
    • A deleted file appears in the "changed" list.
    • ingest.py --config uses cfg.workspace, not the module-level REPO_ROOT constant
      (verified by asserting that all manifest paths exist inside the test workspace).

  MCP tools over JSON-RPC
    • list_adrs   — returns exactly the ADRs in the test workspace (count, ids, titles,
                     amendment counts, main_file paths).
    • query_docs  — returns hits with required fields; surfaces the correct ADR for
                     domain-specific questions; top_k is respected; doc_kind metadata is
                     present and correct (adr_main, adr_amendment, architecture).
    • read_docs   — returns chunks mode by default; switches to full mode on intent
                     phrases ("show me all details about", "full content of"); full mode
                     returns size_chars + content; chunks mode returns lines field.
    • get_adr_history — returns main content + amendments; returns an error dict for an
                         unknown ADR ID.

  Generic workspace isolation (the KEY requirement)
    • list_adrs returns ONLY the 2 ADRs from the test workspace, proving the server
      reads from the config-derived workspace and NOT from the ECommerceApp repo docs.

  Container mode  (requires Docker + rag-tools image, @pytest.mark.container)
    • All 4 tools work when the server is started inside a Docker container.
    • The container reads from the volume-mounted workspace, not from the image's
      internal files (verified the same way: list_adrs == 2).

Prerequisites
-------------
  sentence-transformers and qdrant-client must be installed:
      pip install -r tools/rag/requirements.txt

  The embedding model (~400 MB) is downloaded automatically on first run and cached
  in ~/.cache/huggingface/hub.

Run
---
  # All e2e tests (local mode, skips container):
  cd tools/rag
  python -m pytest tests/test_e2e.py -v

  # Fast unit tests only (skip e2e):
  python -m pytest tests/ -m "not e2e" -q

  # Container tests only (requires Docker + rag-tools image):
  python -m pytest tests/test_e2e.py -v -m container

  # Everything including container:
  python -m pytest tests/test_e2e.py -v -m "e2e or container"
"""
from __future__ import annotations

import hashlib
import io
import json
import os
import subprocess
import sys
import textwrap
import threading
from pathlib import Path
from typing import Callable

import pytest

# All tests in this file carry the e2e marker automatically.
# sentence-transformers and qdrant-client are only used INSIDE spawned subprocesses
# (ingest.py, mcp_server.py) — they are NOT imported into the test process itself.
# Run: pip install -r tools/rag/requirements.txt to ensure they are available.
# Skip e2e in fast CI: pytest tests/ -m "not e2e"
pytestmark = pytest.mark.e2e

# ── E2e infra availability guard ──────────────────────────────────────────────
# Check that the embedding backend (torch / sentence-transformers) is importable
# in the *current* Python environment. If it isn't (e.g. Python 3.14 where no
# official torch wheels exist yet, or a CI box without GPU drivers), skip the
# entire module instead of erroring with an obscure DLL / ImportError inside
# spawned subprocesses.
#
# Recommended dev environment: Python 3.13 (torch officially supports 3.8–3.13).
def _torch_available() -> bool:
    import subprocess as _sp
    result = _sp.run(
        [sys.executable, "-c", "import torch"],
        capture_output=True,
        timeout=15,
    )
    return result.returncode == 0

if not _torch_available():
    import warnings
    warnings.warn(
        "torch is not importable in this Python environment — all e2e tests will be "
        "skipped. Use Python 3.13 (official torch target) and run "
        "'pip install -r requirements.txt' to enable them.",
        stacklevel=1,
    )
    pytestmark = [pytest.mark.e2e, pytest.mark.skip(reason=(
        "torch / sentence-transformers not available in this Python environment. "
        "Recommended: Python 3.13. Run: pip install -r requirements.txt"
    ))]

_RAG_ROOT = Path(__file__).parent.parent   # tools/rag
_SERVER_PY = _RAG_ROOT / "mcp_server.py"
_INGEST_PY = _RAG_ROOT / "ingest.py"

# ---------------------------------------------------------------------------
# Fake workspace content
# A minimal repo structure that exercises all three metadata dimensions:
#   - two ADRs (0001 has an amendment, 0002 does not)
#   - one architecture file
# The metadata-rules.yaml and queries.yaml files exercise the companion-loading path.
# ---------------------------------------------------------------------------

_ADR_0001_MAIN = textwrap.dedent("""\
    # ADR-0001 — TypedId Value Objects

    ## Status
    Accepted

    ## Context
    Domain entities require strongly-typed identifiers to prevent primitive obsession
    and invalid cross-aggregate ID references (passing an OrderId where a CustomerId
    is expected compiles but is semantically wrong).

    ## Decision
    Use `TypedId<T>` as a value object wrapper around `Guid`. Every aggregate root
    and entity must expose typed IDs on public interfaces instead of raw `Guid` types.

    ## Consequences
    - Eliminates accidental cross-aggregate ID misuse at compile time.
    - EF Core requires a `ValueConverter<TypedId<T>, Guid>` for each typed ID type.
    - Domain layer becomes self-documenting about entity relationships.
    - Serialization adapters are needed at API boundaries.
""")

_ADR_0001_README = textwrap.dedent("""\
    # ADR-0001 Router

    Main decision: [0001-typed-ids.md](0001-typed-ids.md)

    Topics: TypedId, value object, strongly-typed identifiers, domain primitives.
""")

_ADR_0001_AMENDMENT = textwrap.dedent("""\
    # Amendment A1 to ADR-0001 — TypedId in Collections

    ## Motivation
    The original ADR covered TypedId as primary keys only. In practice, IEnumerable
    and List parameters also need typed IDs to maintain compile-time safety throughout
    the domain layer, not just at aggregate roots.

    ## Change
    Collections and IEnumerable parameters must use TypedId instead of raw Guid,
    ensuring end-to-end type safety across the entire domain model.

    Applies to: ADR-0001
""")

_ADR_0002_MAIN = textwrap.dedent("""\
    # ADR-0002 — CQRS with ICommandHandler

    ## Status
    Accepted

    ## Context
    Service methods lack a clear contract distinguishing state-changing operations
    (commands) from data-retrieval operations (queries), leading to coupling and
    testability problems.

    ## Decision
    Use `ICommandHandler<TCommand, TResult>` for all state-changing operations.
    Commands are immutable records; results are sealed types with static factory methods.
    Queries use `IQueryHandler<TQuery, TResult>`.

    ## Consequences
    - Handlers registered via per-BC DI extension methods.
    - No direct service-to-service dependencies across bounded contexts.
    - Command records must be immutable (readonly properties or init-only setters).
    - Every handler is independently testable without a full application context.
""")

_ADR_0002_README = textwrap.dedent("""\
    # ADR-0002 Router

    Main decision: [0002-cqrs.md](0002-cqrs.md)

    Topics: CQRS, ICommandHandler, command handler, query handler, DI registration.
""")

_ARCH_OVERVIEW = textwrap.dedent("""\
    # Architecture Overview

    The system uses Clean/Onion architecture with strict bounded context separation.
    Each BC has three layers: Domain, Application, Infrastructure.

    Cross-BC communication happens exclusively through IMessage domain events to
    avoid tight coupling between bounded contexts.

    ## Principles
    - Dependency direction: Infrastructure → Application → Domain (no reverse deps).
    - No shared mutable state between BCs.
    - Each BC owns its own DbContext (per ADR-0013).
""")

_CONFIG_YAML = textwrap.dedent("""\
    source:
      roots: [docs]
      exclude_globs: []
    embedder:
      model: "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
      dimensions: 384
      device: cpu
      batch_size: 32
    chunker:
      max_tokens: 800
      min_tokens: 1
      overlap_tokens: 80
      split_on_headings: [1, 2, 3]
    vector_store:
      backend: qdrant
      mode: memory
      collection: e2e_test_col
      url: "http://localhost:6333"
    ranking:
      stub_byte_threshold: 200
      weights:
        - {pattern: "docs/adr/**/amendments/**", weight: 1.5}
        - {pattern: "docs/adr/**",               weight: 1.2}
        - {pattern: "**",                         weight: 1.0}
    query:
      default_top_k: 5
      fetch_k: 20
      score_threshold: 0.0
    config_files:
      metadata_rules: metadata-rules.yaml
      queries: queries.yaml
    storage:
      snapshot_path: .rag/snapshot.qdrant
      manifest_path: .rag/manifest.json
""")

_METADATA_RULES_YAML = textwrap.dedent("""\
    adr_id_patterns:
      - pattern: "adr/(?P<id>\\\\d{4})/"
    doc_kind_rules:
      - {glob: "**/amendments/**",     kind: adr_amendment}
      - {glob: "**/adr/**/README.md",  kind: adr_router}
      - {glob: "docs/adr/**",          kind: adr_main}
      - {glob: "docs/architecture/**", kind: architecture}
""")

_QUERIES_YAML = textwrap.dedent("""\
    named_queries:
      - name: typed-id-query
        question: "TypedId value object strongly-typed identifier pattern"
        top_k: 5
      - name: cqrs-query
        question: "CQRS command handler ICommandHandler pattern"
        top_k: 3
""")


def _build_workspace(root: Path) -> Path:
    """Create a minimal fake workspace under *root*. Returns the path to config.yaml.

    Layout (mirrors the real repo structure so config_path.parents[2] == root):
        root/
          tools/rag/
            config.yaml
            metadata-rules.yaml
            queries.yaml
          docs/
            adr/
              0001/
                README.md
                0001-typed-ids.md
                amendments/
                  a1-typed-id-collections.md
              0002/
                README.md
                0002-cqrs.md
            architecture/
              overview.md
    """
    rag_dir = root / "tools" / "rag"
    rag_dir.mkdir(parents=True)
    (rag_dir / "config.yaml").write_text(_CONFIG_YAML, encoding="utf-8")
    (rag_dir / "metadata-rules.yaml").write_text(_METADATA_RULES_YAML, encoding="utf-8")
    (rag_dir / "queries.yaml").write_text(_QUERIES_YAML, encoding="utf-8")

    adr0001 = root / "docs" / "adr" / "0001"
    adr0001.mkdir(parents=True)
    (adr0001 / "0001-typed-ids.md").write_text(_ADR_0001_MAIN, encoding="utf-8")
    (adr0001 / "README.md").write_text(_ADR_0001_README, encoding="utf-8")
    amend = adr0001 / "amendments"
    amend.mkdir()
    (amend / "a1-typed-id-collections.md").write_text(_ADR_0001_AMENDMENT, encoding="utf-8")

    adr0002 = root / "docs" / "adr" / "0002"
    adr0002.mkdir(parents=True)
    (adr0002 / "0002-cqrs.md").write_text(_ADR_0002_MAIN, encoding="utf-8")
    (adr0002 / "README.md").write_text(_ADR_0002_README, encoding="utf-8")

    arch = root / "docs" / "architecture"
    arch.mkdir(parents=True)
    (arch / "overview.md").write_text(_ARCH_OVERVIEW, encoding="utf-8")

    return rag_dir / "config.yaml"


# ---------------------------------------------------------------------------
# Subprocess helpers
# ---------------------------------------------------------------------------

def _run_ingest(config_path: Path, extra_args: list[str] | None = None) -> subprocess.CompletedProcess:
    """Run ingest.py --config <config_path> --mode memory as a blocking subprocess."""
    cmd = [
        sys.executable, str(_INGEST_PY),
        "--config", str(config_path),
        "--mode", "memory",
    ] + (extra_args or [])
    return subprocess.run(
        cmd,
        capture_output=True,
        text=True,
        timeout=300,   # model download on first run can be slow; cached runs are fast
        cwd=str(_RAG_ROOT),
        env={**os.environ, "PYTHONUNBUFFERED": "1"},
    )


def _start_server(config_path: Path) -> tuple[subprocess.Popen, io.BytesIO, threading.Thread]:
    """Spawn the MCP server. Returns (proc, stderr_buffer, drain_thread)."""
    proc = subprocess.Popen(
        [sys.executable, str(_SERVER_PY), "--config", str(config_path)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=str(_RAG_ROOT),
        env={**os.environ, "PYTHONUNBUFFERED": "1"},
    )
    buf = io.BytesIO()

    def _drain() -> None:
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""):
                buf.write(chunk)
        except Exception:
            pass

    t = threading.Thread(target=_drain, daemon=True)
    t.start()
    return proc, buf, t


def _kill_server(
    proc: subprocess.Popen,
    buf: io.BytesIO,
    drain_thread: threading.Thread,
) -> str:
    """Terminate the server and return collected stderr text."""
    try:
        proc.stdin.close()
    except Exception:
        pass
    try:
        proc.wait(timeout=10)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait(timeout=5)
    drain_thread.join(timeout=3)
    return buf.getvalue().decode("utf-8", errors="replace")


# ---------------------------------------------------------------------------
# Tiny MCP client (NDJSON over stdio — one JSON object per line)
# ---------------------------------------------------------------------------

def _encode(msg: dict) -> bytes:
    return json.dumps(msg).encode() + b"\n"


def _read_response(stdout, timeout: float = 90.0) -> dict:
    """Read one JSON-RPC line from stdout with a hard deadline."""
    result: dict = {}
    err: dict = {}

    def _worker() -> None:
        try:
            line = stdout.readline()
            if not line:
                raise EOFError("Server stdout closed unexpectedly")
            result["v"] = json.loads(line)
        except Exception as exc:
            err["e"] = exc

    t = threading.Thread(target=_worker, daemon=True)
    t.start()
    t.join(timeout)
    if t.is_alive():
        raise TimeoutError(
            f"MCP server did not respond within {timeout}s — "
            "check that the embedding model is cached and Qdrant snapshot exists."
        )
    if "e" in err:
        raise err["e"]
    return result["v"]


def _handshake(proc: subprocess.Popen) -> None:
    """Send MCP initialize + initialized notification and wait for the response."""
    proc.stdin.write(_encode({
        "jsonrpc": "2.0", "id": 0, "method": "initialize",
        "params": {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "e2e-test", "version": "0.1"},
        },
    }))
    proc.stdin.flush()
    _read_response(proc.stdout, timeout=90)   # model load on first use can be slow
    proc.stdin.write(_encode({
        "jsonrpc": "2.0",
        "method": "notifications/initialized",
        "params": {},
    }))
    proc.stdin.flush()


def _call_tool(
    proc: subprocess.Popen,
    call_id: int,
    tool: str,
    args: dict,
    timeout: float = 90.0,
) -> dict:
    """Send tools/call and return the parsed payload dict."""
    proc.stdin.write(_encode({
        "jsonrpc": "2.0",
        "id": call_id,
        "method": "tools/call",
        "params": {"name": tool, "arguments": args},
    }))
    proc.stdin.flush()
    resp = _read_response(proc.stdout, timeout=timeout)
    raw = resp.get("result", {}).get("content", [{}])[0].get("text", "{}")
    return json.loads(raw)


# ---------------------------------------------------------------------------
# Docker helpers (container tests)
# ---------------------------------------------------------------------------

def _docker_available() -> bool:
    try:
        r = subprocess.run(["docker", "info"], capture_output=True, timeout=5)
        return r.returncode == 0
    except Exception:
        return False


def _rag_image_exists() -> bool:
    try:
        r = subprocess.run(
            ["docker", "image", "inspect", "rag-tools"],
            capture_output=True,
            timeout=5,
        )
        return r.returncode == 0
    except Exception:
        return False


# ---------------------------------------------------------------------------
# Session-scoped fixtures (shared by all non-modifying tests)
# ---------------------------------------------------------------------------

@pytest.fixture(scope="session")
def e2e_workspace(tmp_path_factory: pytest.TempPathFactory) -> Path:
    """Create a minimal fake workspace. Returns path to config.yaml.

    Session-scoped: created once, shared across TestConfigResolution,
    TestManifest (read-only tests), and TestMcpTools.
    """
    root = tmp_path_factory.mktemp("e2e_workspace")
    return _build_workspace(root)


@pytest.fixture(scope="session")
def ingested_workspace(e2e_workspace: Path) -> Path:
    """Run ingest once on e2e_workspace. Returns config.yaml path.

    Session-scoped: ingest only runs once per test session.
    On the very first run in a clean environment the embedding model is downloaded
    (~400 MB); subsequent runs use the HuggingFace cache (~3–5 seconds).
    """
    result = _run_ingest(e2e_workspace)
    assert result.returncode == 0, (
        f"ingest.py exited {result.returncode}.\n"
        f"stdout: {result.stdout}\nstderr: {result.stderr}"
    )
    return e2e_workspace


# Type alias for the callable yielded by mcp_session / container_session
ToolCaller = Callable[..., dict]


@pytest.fixture(scope="session")
def mcp_session(ingested_workspace: Path) -> ToolCaller:
    """Start the MCP server once for the session. Yields a tool-call helper.

    Usage in tests::

        def test_something(mcp_session):
            result = mcp_session("list_adrs", {})

    The server process is terminated automatically after the test session.
    """
    config_path = ingested_workspace
    proc, buf, drain_t = _start_server(config_path)
    _call_id = [0]

    try:
        _handshake(proc)
    except Exception as exc:
        stderr = _kill_server(proc, buf, drain_t)
        pytest.fail(
            f"MCP server handshake failed: {type(exc).__name__}: {exc}\n"
            f"Server stderr:\n{stderr}"
        )

    def _call(tool: str, args: dict, *, timeout: float = 90.0) -> dict:
        _call_id[0] += 1
        return _call_tool(proc, _call_id[0], tool, args, timeout=timeout)

    yield _call

    _kill_server(proc, buf, drain_t)


# ---------------------------------------------------------------------------
# Container session fixture (Docker)
# ---------------------------------------------------------------------------

@pytest.fixture(scope="session")
def container_session(tmp_path_factory: pytest.TempPathFactory) -> ToolCaller:
    """Start the MCP server in a Docker container. Yields a tool-call helper.

    Skipped automatically if Docker is not available or the rag-tools image is
    not built. Build it with::

        docker compose build rag-tools
    """
    if not _docker_available():
        pytest.skip("Docker not available — skipping container tests")
    if not _rag_image_exists():
        pytest.skip(
            "rag-tools image not found — build it with: docker compose build rag-tools"
        )

    # Fresh isolated workspace for the container session.
    root = tmp_path_factory.mktemp("container_workspace")
    config_path = _build_workspace(root)

    # Step 1: run ingest inside the container against the mounted workspace.
    # RAG_WORKSPACE=/workspace is the only path knob — ingest.py derives
    # config as $RAG_WORKSPACE/tools/rag/config.yaml. No hardcoded container paths.
    ingest_r = subprocess.run(
        [
            "docker", "run", "--rm",
            "--volume", f"{root}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "rag-tools",
            "python", "ingest.py",   # WORKDIR /app — uses baked scripts
            "--mode", "memory",
        ],
        capture_output=True,
        text=True,
        timeout=300,
    )
    assert ingest_r.returncode == 0, (
        f"Container ingest failed:\nstdout: {ingest_r.stdout}\nstderr: {ingest_r.stderr}"
    )

    # Step 2: start a long-lived MCP server container reading from the same volume.
    # Again: RAG_WORKSPACE is the only knob, no --config or /workspace/ paths.
    proc = subprocess.Popen(
        [
            "docker", "run", "--rm", "--interactive",
            "--volume", f"{root}:/workspace",
            "--env", "RAG_WORKSPACE=/workspace",
            "--env", "PYTHONUNBUFFERED=1",
            "rag-tools",
            "python", "mcp_server.py",  # WORKDIR /app — uses baked scripts
        ],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env={**os.environ, "PYTHONUNBUFFERED": "1"},
    )
    buf = io.BytesIO()

    def _drain() -> None:
        try:
            for chunk in iter(lambda: proc.stderr.read(1024), b""):
                buf.write(chunk)
        except Exception:
            pass

    drain_t = threading.Thread(target=_drain, daemon=True)
    drain_t.start()
    _call_id = [0]

    try:
        _handshake(proc)
    except Exception as exc:
        stderr = _kill_server(proc, buf, drain_t)
        pytest.fail(
            f"Container MCP server handshake failed: {type(exc).__name__}: {exc}\n"
            f"Container stderr:\n{stderr}"
        )

    def _call(tool: str, args: dict, *, timeout: float = 90.0) -> dict:
        _call_id[0] += 1
        return _call_tool(proc, _call_id[0], tool, args, timeout=timeout)

    yield _call

    _kill_server(proc, buf, drain_t)


# ---------------------------------------------------------------------------
# ── Group 1: Config resolution (no ingest needed) ──────────────────────────
# ---------------------------------------------------------------------------

class TestConfigResolution:
    """Verify that Config.workspace is derived correctly from --config path
    and that companion files (metadata-rules.yaml, queries.yaml) are loaded.

    These tests do not require ingest — they only load config.yaml from disk.
    """

    def test_workspace_derived_from_config_path(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        """config_path.parents[2] == workspace root when RAG_WORKSPACE is not set."""
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        # e2e_workspace is at <root>/tools/rag/config.yaml → parents[2] is <root>
        expected_root = e2e_workspace.parents[2]
        assert cfg.workspace == expected_root

    def test_config_path_wins_over_env_workspace(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch, tmp_path: Path
    ) -> None:
        """config_path.parents[2] takes priority over RAG_WORKSPACE.

        When --config is passed explicitly, the workspace is derived from the
        config file path (assumes <root>/tools/rag/config.yaml layout), even if
        RAG_WORKSPACE is set to a different directory in the environment.
        RAG_WORKSPACE is only used as a fallback when the config path is too
        shallow to derive a workspace (e.g. /app/config.yaml in a container).
        """
        monkeypatch.setenv("RAG_WORKSPACE", str(tmp_path))
        from common import load_config
        cfg = load_config(e2e_workspace)
        # e2e_workspace is <tmpdir>/tools/rag/config.yaml → parents[2] == <tmpdir root>
        expected = e2e_workspace.parents[2]
        assert cfg.workspace == expected, (
            f"Expected config_path-derived workspace {expected}, "
            f"got {cfg.workspace}. RAG_WORKSPACE should not override an explicit config path."
        )

    def test_named_queries_loaded_from_queries_yaml(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        """queries.yaml companion file is loaded and exposes named_queries."""
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        assert len(cfg.named_queries) == 2
        names = {q["name"] for q in cfg.named_queries}
        assert "typed-id-query" in names
        assert "cqrs-query" in names

    def test_named_query_has_question_field(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        for q in cfg.named_queries:
            assert "question" in q, f"Named query {q.get('name')!r} is missing 'question' field"

    def test_adr_id_patterns_loaded_from_metadata_rules(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        """metadata-rules.yaml provides adr_id_patterns with a named 'id' group."""
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        assert len(cfg.adr_id_patterns) >= 1
        # Every pattern must have the named capture group 'id'.
        for pattern in cfg.adr_id_patterns:
            assert "(?P<id>" in pattern, f"Pattern {pattern!r} is missing named group 'id'"

    def test_doc_kind_rules_loaded_from_metadata_rules(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        """metadata-rules.yaml provides doc_kind_rules with glob + kind pairs."""
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        assert len(cfg.doc_kind_rules) >= 3
        kinds = {r["kind"] for r in cfg.doc_kind_rules}
        assert "adr_main" in kinds
        assert "adr_amendment" in kinds
        assert "adr_router" in kinds

    def test_metadata_rules_classify_adr_main(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_doc_kind
        cfg = load_config(e2e_workspace)
        assert detect_doc_kind("docs/adr/0001/0001-typed-ids.md", cfg) == "adr_main"

    def test_metadata_rules_classify_adr_amendment(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_doc_kind
        cfg = load_config(e2e_workspace)
        assert detect_doc_kind("docs/adr/0001/amendments/a1.md", cfg) == "adr_amendment"

    def test_metadata_rules_classify_adr_router(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_doc_kind
        cfg = load_config(e2e_workspace)
        assert detect_doc_kind("docs/adr/0001/README.md", cfg) == "adr_router"

    def test_metadata_rules_classify_architecture(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_doc_kind
        cfg = load_config(e2e_workspace)
        assert detect_doc_kind("docs/architecture/overview.md", cfg) == "architecture"

    def test_metadata_rules_extract_adr_id_from_0001(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_adr_id
        cfg = load_config(e2e_workspace)
        assert detect_adr_id("docs/adr/0001/0001-typed-ids.md", cfg) == "0001"

    def test_metadata_rules_extract_adr_id_from_0002(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_adr_id
        cfg = load_config(e2e_workspace)
        assert detect_adr_id("docs/adr/0002/0002-cqrs.md", cfg) == "0002"

    def test_metadata_rules_no_adr_id_for_non_adr(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, detect_adr_id
        cfg = load_config(e2e_workspace)
        assert detect_adr_id("docs/architecture/overview.md", cfg) is None

    def test_source_roots_are_inside_workspace(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        for root in cfg.source_roots:
            assert str(root).startswith(str(cfg.workspace)), (
                f"source_root {root} is not inside workspace {cfg.workspace}"
            )

    def test_iter_markdown_files_finds_all_docs(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config, iter_markdown_files
        cfg = load_config(e2e_workspace)
        files = iter_markdown_files(cfg)
        rel_paths = [f.relative_to(cfg.workspace).as_posix() for f in files]
        assert any("0001-typed-ids" in p for p in rel_paths), rel_paths
        assert any("0002-cqrs" in p for p in rel_paths), rel_paths
        assert any("a1-" in p for p in rel_paths), rel_paths
        assert any("overview" in p for p in rel_paths), rel_paths

    def test_manifest_path_is_inside_workspace(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        assert str(cfg.manifest_path).startswith(str(cfg.workspace))

    def test_snapshot_path_is_inside_workspace(
        self, e2e_workspace: Path, monkeypatch: pytest.MonkeyPatch
    ) -> None:
        monkeypatch.delenv("RAG_WORKSPACE", raising=False)
        from common import load_config
        cfg = load_config(e2e_workspace)
        assert str(cfg.snapshot_path).startswith(str(cfg.workspace))


# ---------------------------------------------------------------------------
# ── Group 2: Ingest + manifest (requires model load) ──────────────────────
# ---------------------------------------------------------------------------

class TestManifest:
    """Verify manifest.json creation, SHA-256 accuracy, and incremental behaviour.

    Tests that use ingested_workspace share the session-level ingest run.
    Tests that need to modify files create their own isolated workspace
    (via tmp_path_factory) to avoid polluting the shared session workspace.
    """

    def test_ingest_creates_manifest_file(self, ingested_workspace: Path) -> None:
        from common import load_config
        cfg = load_config(ingested_workspace)
        assert cfg.manifest_path.exists(), f"manifest.json not found at {cfg.manifest_path}"

    def test_manifest_has_expected_fields(self, ingested_workspace: Path) -> None:
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            data = json.load(fh)
        for field in ("last_indexed", "file_hashes", "files", "chunks", "model"):
            assert field in data, f"Expected field {field!r} in manifest.json"

    def test_manifest_file_count_is_positive(self, ingested_workspace: Path) -> None:
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            data = json.load(fh)
        assert data["files"] > 0, "Expected at least one file in manifest"
        assert data["chunks"] > 0, "Expected at least one chunk in manifest"

    def test_manifest_hashes_match_actual_files(self, ingested_workspace: Path) -> None:
        """Every SHA-256 in file_hashes matches the file's actual bytes on disk."""
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            data = json.load(fh)
        file_hashes: dict[str, str] = data["file_hashes"]
        assert len(file_hashes) > 0
        for rel_path, stored_hash in file_hashes.items():
            abs_path = cfg.workspace / rel_path
            assert abs_path.exists(), f"File in manifest not found: {abs_path}"
            actual_hash = hashlib.sha256(abs_path.read_bytes()).hexdigest()
            assert actual_hash == stored_hash, (
                f"SHA-256 mismatch for {rel_path}: "
                f"stored={stored_hash[:12]}…, actual={actual_hash[:12]}…"
            )

    def test_manifest_covers_all_markdown_files(self, ingested_workspace: Path) -> None:
        """Every .md file discovered by iter_markdown_files appears in the manifest."""
        from common import load_config, iter_markdown_files
        cfg = load_config(ingested_workspace)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            data = json.load(fh)
        file_hashes: dict[str, str] = data["file_hashes"]
        for path in iter_markdown_files(cfg):
            rel = path.relative_to(cfg.workspace).as_posix()
            assert rel in file_hashes, f"File missing from manifest: {rel}"

    def test_snapshot_created_for_memory_mode(self, ingested_workspace: Path) -> None:
        from common import load_config
        cfg = load_config(ingested_workspace)
        assert cfg.snapshot_path.exists(), (
            f"Memory-mode snapshot not found at {cfg.snapshot_path}"
        )

    def test_snapshot_is_valid_json(self, ingested_workspace: Path) -> None:
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.snapshot_path.open(encoding="utf-8") as fh:
            snap = json.load(fh)
        assert "collection" in snap
        assert "points" in snap
        assert isinstance(snap["points"], list)
        assert len(snap["points"]) > 0

    def test_snapshot_points_have_payload(self, ingested_workspace: Path) -> None:
        """Every point in the snapshot carries the expected payload fields."""
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.snapshot_path.open(encoding="utf-8") as fh:
            snap = json.load(fh)
        for pt in snap["points"]:
            payload = pt.get("payload", {})
            for key in ("rel_path", "doc_kind", "breadcrumb", "start_line", "end_line", "text"):
                assert key in payload, (
                    f"Snapshot point missing payload field {key!r}: {payload}"
                )

    def test_snapshot_doc_kind_from_metadata_rules(self, ingested_workspace: Path) -> None:
        """Snapshot points reflect doc_kind values from metadata-rules.yaml."""
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.snapshot_path.open(encoding="utf-8") as fh:
            snap = json.load(fh)
        kinds_present = {pt["payload"].get("doc_kind") for pt in snap["points"]}
        # Our test workspace has all three: adr_main, adr_router, adr_amendment, architecture
        assert "adr_main" in kinds_present, f"Expected adr_main in kinds: {kinds_present}"
        assert "adr_amendment" in kinds_present, f"Expected adr_amendment in kinds: {kinds_present}"
        assert "architecture" in kinds_present, f"Expected architecture in kinds: {kinds_present}"

    def test_snapshot_adr_id_metadata_extracted(self, ingested_workspace: Path) -> None:
        """ADR-main chunks carry the adr_id extracted by metadata-rules regex."""
        from common import load_config
        cfg = load_config(ingested_workspace)
        with cfg.snapshot_path.open(encoding="utf-8") as fh:
            snap = json.load(fh)
        adr_main_points = [
            pt for pt in snap["points"]
            if pt["payload"].get("doc_kind") == "adr_main"
        ]
        assert len(adr_main_points) > 0
        adr_ids = {pt["payload"].get("adr_id") for pt in adr_main_points}
        assert "0001" in adr_ids, f"Expected '0001' in adr_ids: {adr_ids}"
        assert "0002" in adr_ids, f"Expected '0002' in adr_ids: {adr_ids}"

    def test_incremental_ingest_updates_only_changed_file(
        self, tmp_path_factory: pytest.TempPathFactory
    ) -> None:
        """After modifying one file, only its hash changes; all others stay the same."""
        root = tmp_path_factory.mktemp("incr_test")
        config_path = _build_workspace(root)

        r1 = _run_ingest(config_path)
        assert r1.returncode == 0, f"First ingest failed:\n{r1.stderr}"

        from common import load_config
        cfg = load_config(config_path)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            manifest1: dict = json.load(fh)

        # Modify one file.
        target = root / "docs" / "adr" / "0001" / "0001-typed-ids.md"
        original_rel = target.relative_to(root).as_posix()
        target.write_text(
            target.read_text(encoding="utf-8") + "\n\nExtra paragraph for change detection.",
            encoding="utf-8",
        )

        r2 = _run_ingest(config_path)
        assert r2.returncode == 0, f"Incremental ingest failed:\n{r2.stderr}"

        with cfg.manifest_path.open(encoding="utf-8") as fh:
            manifest2: dict = json.load(fh)

        # The altered file must have a new hash.
        assert manifest1["file_hashes"][original_rel] != manifest2["file_hashes"][original_rel], (
            "Hash for modified file should have changed"
        )

        # An unmodified file must keep the same hash.
        unchanged_rel = "docs/adr/0002/0002-cqrs.md"
        assert manifest1["file_hashes"][unchanged_rel] == manifest2["file_hashes"][unchanged_rel], (
            "Hash for unmodified file should be unchanged"
        )

    def test_force_full_regenerates_manifest_with_same_file_count(
        self, tmp_path_factory: pytest.TempPathFactory
    ) -> None:
        root = tmp_path_factory.mktemp("force_full_test")
        config_path = _build_workspace(root)

        r1 = _run_ingest(config_path)
        assert r1.returncode == 0

        from common import load_config
        cfg = load_config(config_path)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            m1: dict = json.load(fh)

        r2 = _run_ingest(config_path, extra_args=["--force-full"])
        assert r2.returncode == 0

        with cfg.manifest_path.open(encoding="utf-8") as fh:
            m2: dict = json.load(fh)

        assert m2["files"] == m1["files"], (
            f"File count changed after --force-full: {m1['files']} → {m2['files']}"
        )
        assert m2["last_indexed"] >= m1["last_indexed"]


# ---------------------------------------------------------------------------
# ── Group 3: Startup-sync change detection ─────────────────────────────────
# ---------------------------------------------------------------------------

class TestStartupSync:
    """Verify the SHA-256 change-detection logic used by _startup_check.

    We exercise the detection algorithm directly (without spawning the MCP server)
    since _startup_check in memory mode returns early (no persistent index to sync).
    """

    def test_unchanged_files_produce_no_changes(self, ingested_workspace: Path) -> None:
        """Re-computing hashes on freshly ingested files detects zero changes."""
        from common import load_config, iter_markdown_files
        cfg = load_config(ingested_workspace)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            stored_hashes: dict[str, str] = json.load(fh)["file_hashes"]

        changed = [
            rel
            for path in iter_markdown_files(cfg)
            for rel in [path.relative_to(cfg.workspace).as_posix()]
            if stored_hashes.get(rel) != hashlib.sha256(path.read_bytes()).hexdigest()
        ]
        assert changed == [], (
            f"Expected no changes on an unmodified workspace, got: {changed}"
        )

    def test_modified_file_detected_as_changed(
        self, tmp_path_factory: pytest.TempPathFactory
    ) -> None:
        """After modifying a file, exactly that file appears in the changed list."""
        root = tmp_path_factory.mktemp("sync_modify")
        config_path = _build_workspace(root)

        r = _run_ingest(config_path)
        assert r.returncode == 0

        from common import load_config, iter_markdown_files
        cfg = load_config(config_path)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            stored_hashes: dict[str, str] = json.load(fh)["file_hashes"]

        target = root / "docs" / "adr" / "0002" / "0002-cqrs.md"
        target.write_text(target.read_text(encoding="utf-8") + "\n\nModified.", encoding="utf-8")

        changed = [
            rel
            for path in iter_markdown_files(cfg)
            for rel in [path.relative_to(cfg.workspace).as_posix()]
            if stored_hashes.get(rel) != hashlib.sha256(path.read_bytes()).hexdigest()
        ]
        assert len(changed) == 1, f"Expected 1 changed file, got: {changed}"
        assert "0002-cqrs" in changed[0]

    def test_deleted_file_detected_as_changed(
        self, tmp_path_factory: pytest.TempPathFactory
    ) -> None:
        """A file that was ingested but then deleted appears in the changed list."""
        root = tmp_path_factory.mktemp("sync_delete")
        config_path = _build_workspace(root)

        r = _run_ingest(config_path)
        assert r.returncode == 0

        from common import load_config, iter_markdown_files
        cfg = load_config(config_path)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            stored_hashes: dict[str, str] = json.load(fh)["file_hashes"]

        # Delete one file.
        target = root / "docs" / "architecture" / "overview.md"
        target.unlink()

        # Mimic _detect_changed_files from mcp_server.py.
        current_rels: set[str] = set()
        changed: list[str] = []
        for path in iter_markdown_files(cfg):
            rel = path.relative_to(cfg.workspace).as_posix()
            current_rels.add(rel)
            if stored_hashes.get(rel) != hashlib.sha256(path.read_bytes()).hexdigest():
                changed.append(rel)
        for rel in stored_hashes:
            if rel not in current_rels:
                changed.append(rel)

        assert any("overview" in c for c in changed), (
            f"Deleted file 'overview.md' not in changed list: {changed}"
        )

    def test_ingest_uses_config_workspace_not_repo_root(
        self, tmp_path_factory: pytest.TempPathFactory
    ) -> None:
        """All manifest file_hashes resolve to paths within the test workspace.

        This test fails if ingest.py uses the module-level REPO_ROOT constant
        instead of cfg.workspace — in that case, paths would point into the
        real ECommerceApp repo rather than the temp workspace.
        """
        root = tmp_path_factory.mktemp("workspace_isolation")
        config_path = _build_workspace(root)

        r = _run_ingest(config_path)
        assert r.returncode == 0, f"ingest failed:\n{r.stderr}"

        from common import load_config
        cfg = load_config(config_path)
        with cfg.manifest_path.open(encoding="utf-8") as fh:
            data = json.load(fh)

        for rel_path in data["file_hashes"]:
            abs_path = root / rel_path
            assert abs_path.exists(), (
                f"Manifest entry {rel_path!r} resolves to {abs_path} — does not exist. "
                "This indicates ingest.py used REPO_ROOT instead of cfg.workspace."
            )


# ---------------------------------------------------------------------------
# ── Group 4: MCP tools (requires session-level server) ────────────────────
# ---------------------------------------------------------------------------

class TestMcpTools:
    """End-to-end tests for all 4 MCP tools via the JSON-RPC protocol.

    All tests share the session-level MCP server (started in mcp_session).
    The server is connected to the test workspace containing exactly 2 ADRs.
    """

    # ── list_adrs ──────────────────────────────────────────────────────────

    def test_list_adrs_count_matches_workspace(self, mcp_session: ToolCaller) -> None:
        """list_adrs returns exactly 2 ADRs — the test workspace, not the real repo."""
        result = mcp_session("list_adrs", {})
        assert result["count"] == 2, (
            f"Expected 2 ADRs (test workspace), got {result['count']}. "
            "If this is larger the server may be reading from the wrong workspace."
        )

    def test_list_adrs_includes_correct_ids(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        ids = {adr["id"] for adr in result["adrs"]}
        assert "0001" in ids
        assert "0002" in ids

    def test_list_adrs_titles_are_non_empty(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        for adr in result["adrs"]:
            assert adr["title"], f"ADR {adr['id']} has empty title"

    def test_list_adrs_0001_title_contains_typedid(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        adr0001 = next(a for a in result["adrs"] if a["id"] == "0001")
        assert "typedid" in adr0001["title"].lower() or "typed" in adr0001["title"].lower()

    def test_list_adrs_0001_has_one_amendment(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        adr0001 = next(a for a in result["adrs"] if a["id"] == "0001")
        assert adr0001["amendments"] == 1

    def test_list_adrs_0002_has_no_amendments(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        adr0002 = next(a for a in result["adrs"] if a["id"] == "0002")
        assert adr0002["amendments"] == 0

    def test_list_adrs_each_has_main_file(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("list_adrs", {})
        for adr in result["adrs"]:
            assert adr["main_file"] is not None
            assert adr["main_file"].endswith(".md")

    # ── query_docs ─────────────────────────────────────────────────────────

    def test_query_docs_returns_hits_for_typedid(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "TypedId value object identifier"})
        assert len(result["hits"]) > 0

    def test_query_docs_hit_has_required_fields(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "CQRS command handler"})
        for hit in result["hits"]:
            for field in ("rel_path", "score", "text", "doc_kind", "breadcrumb", "lines"):
                assert field in hit, f"Hit is missing required field {field!r}: {hit}"

    def test_query_docs_typedid_surfaces_adr_0001(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "TypedId strongly typed identifier", "top_k": 5})
        rel_paths = [h["rel_path"] for h in result["hits"]]
        assert any("0001" in p for p in rel_paths), (
            f"Expected ADR-0001 in hits for TypedId query. Got: {rel_paths}"
        )

    def test_query_docs_cqrs_surfaces_adr_0002(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "CQRS ICommandHandler immutable command", "top_k": 5})
        rel_paths = [h["rel_path"] for h in result["hits"]]
        assert any("0002" in p for p in rel_paths), (
            f"Expected ADR-0002 in hits for CQRS query. Got: {rel_paths}"
        )

    def test_query_docs_top_k_limits_results(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "architecture decisions", "top_k": 2})
        assert len(result["hits"]) <= 2

    def test_query_docs_adr_main_doc_kind_present(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "TypedId value object", "top_k": 5})
        adr_main_hits = [h for h in result["hits"] if h.get("doc_kind") == "adr_main"]
        assert len(adr_main_hits) > 0, (
            f"Expected hits with doc_kind=adr_main. "
            f"Got doc_kinds: {[h.get('doc_kind') for h in result['hits']]}"
        )

    def test_query_docs_amendment_has_correct_doc_kind(self, mcp_session: ToolCaller) -> None:
        """A query for amendment-specific content surfaces the amendment with the right doc_kind."""
        result = mcp_session(
            "query_docs",
            {"question": "TypedId amendment collections IEnumerable typed safety", "top_k": 10},
        )
        amendment_hits = [h for h in result["hits"] if h.get("doc_kind") == "adr_amendment"]
        assert len(amendment_hits) > 0, (
            f"Expected hits with doc_kind=adr_amendment. "
            f"Got doc_kinds: {[h.get('doc_kind') for h in result['hits']]}"
        )

    def test_query_docs_adr_id_metadata_in_adr_hits(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "TypedId", "top_k": 5})
        adr_hits = [h for h in result["hits"] if h.get("adr_id") is not None]
        assert len(adr_hits) > 0, "Expected at least one hit with adr_id set"
        adr_ids = {h["adr_id"] for h in adr_hits}
        assert "0001" in adr_ids, f"Expected adr_id=0001 in hits: {adr_ids}"

    def test_query_docs_score_is_float_in_range(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("query_docs", {"question": "software architecture"})
        for hit in result["hits"]:
            assert isinstance(hit["score"], float)
            assert 0.0 <= hit["score"], "Score must be non-negative"

    # ── read_docs ──────────────────────────────────────────────────────────

    def test_read_docs_defaults_to_chunks_mode(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("read_docs", {"question": "TypedId pattern"})
        assert result["mode"] == "chunks"

    def test_read_docs_chunks_mode_returns_files(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("read_docs", {"question": "TypedId pattern"})
        assert len(result["files"]) > 0

    def test_read_docs_chunks_have_line_numbers(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("read_docs", {"question": "TypedId value object"})
        for f in result["files"]:
            if f.get("mode") == "chunks":
                for chunk in f.get("chunks", []):
                    assert "lines" in chunk, f"Chunk missing 'lines' field: {chunk}"
                    assert "-" in chunk["lines"], (
                        f"'lines' should be in 'start-end' format: {chunk['lines']!r}"
                    )

    def test_read_docs_full_mode_triggered_by_all_details(self, mcp_session: ToolCaller) -> None:
        result = mcp_session(
            "read_docs",
            {"question": "show me all details about TypedId value objects"},
        )
        assert result["mode"] == "full"

    def test_read_docs_full_mode_triggered_by_full_content(self, mcp_session: ToolCaller) -> None:
        result = mcp_session(
            "read_docs",
            {"question": "full content of the TypedId ADR please"},
        )
        assert result["mode"] == "full"

    def test_read_docs_full_mode_has_content_and_size(self, mcp_session: ToolCaller) -> None:
        result = mcp_session(
            "read_docs",
            {"question": "show me all details about CQRS command handler"},
        )
        full_files = [f for f in result["files"] if f.get("mode") == "full"]
        assert len(full_files) > 0, "Expected at least one file in full mode"
        for f in full_files:
            assert f["size_chars"] > 0
            assert "content" in f
            assert len(f["content"]) > 0

    def test_read_docs_respects_top_files_limit(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("read_docs", {"question": "architecture decisions", "top_files": 1})
        assert result["files_returned"] <= 1

    def test_read_docs_chunk_mode_does_not_include_full_content_key(
        self, mcp_session: ToolCaller
    ) -> None:
        result = mcp_session("read_docs", {"question": "TypedId"})
        for f in result["files"]:
            if f.get("mode") == "chunks":
                assert "content" not in f, "Chunk mode should NOT return 'content' field"

    def test_read_docs_cqrs_returns_relevant_file(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("read_docs", {"question": "CQRS ICommandHandler pattern", "top_files": 2})
        rel_paths = [f["rel_path"] for f in result["files"]]
        assert any("0002" in p for p in rel_paths), (
            f"Expected ADR-0002 in read_docs files for CQRS query. Got: {rel_paths}"
        )

    # ── get_adr_history ────────────────────────────────────────────────────

    def test_get_adr_history_returns_main_for_0001(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0001"})
        assert result["main"]["content"] is not None
        assert "TypedId" in result["main"]["content"]

    def test_get_adr_history_0001_includes_amendment(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0001"})
        assert result["amendment_count"] == 1
        assert len(result["amendments"]) == 1

    def test_get_adr_history_amendment_has_content(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0001"})
        amend = result["amendments"][0]
        assert "content" in amend
        assert len(amend["content"]) > 0
        assert "collections" in amend["content"].lower() or "typed" in amend["content"].lower()

    def test_get_adr_history_0002_has_no_amendments(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0002"})
        assert result["amendment_count"] == 0
        assert result["amendments"] == []

    def test_get_adr_history_main_rel_path_contains_id(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0001"})
        assert result["main"]["rel_path"] is not None
        assert "0001" in result["main"]["rel_path"]

    def test_get_adr_history_unknown_adr_returns_error_dict(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "9999"})
        assert "error" in result

    def test_get_adr_history_adr_id_in_response(self, mcp_session: ToolCaller) -> None:
        result = mcp_session("get_adr_history", {"adr_id": "0002"})
        assert result["adr_id"] == "0002"

    # ── Generic workspace isolation (KEY requirement) ──────────────────────

    def test_server_reads_from_config_workspace_not_module_repo_root(
        self, mcp_session: ToolCaller
    ) -> None:
        """The server must read from the test workspace, not from the real ECommerceApp repo.

        If REPO_ROOT leaks through, list_adrs would return 20+ ADRs from the real
        project. Returning exactly 2 proves workspace isolation is working.
        """
        result = mcp_session("list_adrs", {})
        assert result["count"] == 2, (
            f"Expected exactly 2 ADRs from the isolated test workspace, "
            f"but got {result['count']}. "
            "The server may be deriving workspace from the module-level REPO_ROOT constant "
            "rather than from the --config path. Check Config.workspace priority logic."
        )


# ---------------------------------------------------------------------------
# ── Group 5: Container tests (Docker required) ─────────────────────────────
# ---------------------------------------------------------------------------

@pytest.mark.container
class TestContainerMode:
    """Run ingest + MCP server inside Docker. Requires the rag-tools image.

    Build the image first:
        docker compose build rag-tools

    These tests mirror TestMcpTools but prove the containerized path works.
    The KEY test is workspace isolation: the container must read from the
    volume-mounted test workspace (2 ADRs), not from the image's own files.
    """

    def test_container_list_adrs_returns_two_adrs(self, container_session: ToolCaller) -> None:
        result = container_session("list_adrs", {})
        assert result["count"] == 2, (
            f"Expected 2 ADRs (test workspace via volume mount), got {result['count']}. "
            "Container may be reading from the image's internal docs."
        )

    def test_container_list_adrs_has_correct_ids(self, container_session: ToolCaller) -> None:
        result = container_session("list_adrs", {})
        ids = {adr["id"] for adr in result["adrs"]}
        assert "0001" in ids
        assert "0002" in ids

    def test_container_query_docs_returns_hits(self, container_session: ToolCaller) -> None:
        result = container_session("query_docs", {"question": "TypedId value object", "top_k": 3})
        assert len(result["hits"]) > 0

    def test_container_query_docs_hit_has_doc_kind(self, container_session: ToolCaller) -> None:
        result = container_session("query_docs", {"question": "TypedId value object", "top_k": 5})
        assert all(h.get("doc_kind") for h in result["hits"]), (
            "All hits should have a non-empty doc_kind"
        )

    def test_container_read_docs_chunks_mode(self, container_session: ToolCaller) -> None:
        result = container_session("read_docs", {"question": "CQRS pattern"})
        assert result["mode"] == "chunks"
        assert len(result["files"]) > 0

    def test_container_read_docs_full_mode(self, container_session: ToolCaller) -> None:
        result = container_session(
            "read_docs",
            {"question": "show me all details about TypedId"},
        )
        assert result["mode"] == "full"

    def test_container_get_adr_history_returns_amendment(self, container_session: ToolCaller) -> None:
        result = container_session("get_adr_history", {"adr_id": "0001"})
        assert result["amendment_count"] == 1
        assert result["main"]["content"] is not None

    def test_container_workspace_from_volume_not_image_files(
        self, container_session: ToolCaller
    ) -> None:
        """Proves the container reads from --config volume-mounted workspace, not image files."""
        result = container_session("list_adrs", {})
        assert result["count"] == 2, (
            f"Container returned {result['count']} ADRs. "
            "Expected 2 (from volume-mounted test workspace). "
            "This means --config or RAG_WORKSPACE is not respected inside the container."
        )
