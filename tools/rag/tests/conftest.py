"""pytest configuration for tools/rag tests.

Adds the parent directory (tools/rag) to sys.path so that test modules can import
chunker, common, ingest, mcp_server, query, etc. without requiring the caller to
set PYTHONPATH manually.

Run from anywhere:
    pytest tools/rag/tests/
Or from within tools/rag:
    pytest tests/

Marks:
    e2e        — requires sentence-transformers + qdrant-client; downloads model on first run.
                 Skip fast: pytest tests/ -m "not e2e"
    container  — additionally requires Docker and the rag-tools image to be built.
                 Run:       pytest tests/ -m container
"""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

# Insert tools/rag at position 0 so its modules take precedence.
_RAG_ROOT = Path(__file__).parent.parent
if str(_RAG_ROOT) not in sys.path:
    sys.path.insert(0, str(_RAG_ROOT))


def pytest_configure(config: pytest.Config) -> None:
    """Register project-level markers so pytest --strict-markers passes."""
    config.addinivalue_line(
        "markers",
        "e2e: full-stack end-to-end tests — require sentence-transformers, qdrant-client, "
        "and download the embedding model on first run (~400 MB, cached afterwards). "
        "Slow: skip with -m 'not e2e'.",
    )
    config.addinivalue_line(
        "markers",
        "container: additionally require Docker and the rag-tools image. "
        "Run with: pytest -m container",
    )
    config.addinivalue_line(
        "markers",
        "http_streamable: require Docker, the rag-dotnet image, and a free TCP port. "
        "Starts an ephemeral Qdrant + rag-dotnet SSE server, ingests via HTTP batch upload, "
        "and queries via MCP Streamable HTTP (e.g. http://localhost:<port>/?project=<col>). "
        "Run with: pytest -m http_streamable",
    )


# ── Shared fixtures ───────────────────────────────────────────────────────────


@pytest.fixture(scope="session")
def rag_root() -> Path:
    """Absolute path to the tools/rag directory."""
    return _RAG_ROOT


@pytest.fixture(scope="session")
def default_chunker_cfg() -> dict:
    """Minimal chunker config for unit tests.

    min_tokens=1 so short test fixtures are not dropped by the min-token guard.
    """
    return {
        "split_on_headings": [1, 2, 3],
        "max_tokens": 800,
        "min_tokens": 1,
        "overlap_tokens": 80,
    }


@pytest.fixture(scope="session")
def small_chunker_cfg() -> dict:
    """Tight chunker config that forces chunking on small inputs."""
    return {
        "split_on_headings": [1, 2, 3],
        "max_tokens": 30,
        "min_tokens": 1,
        "overlap_tokens": 8,
    }
