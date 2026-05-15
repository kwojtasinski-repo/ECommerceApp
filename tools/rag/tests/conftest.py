"""pytest configuration for tools/rag tests.

Adds the parent directory (tools/rag) to sys.path so that test modules can import
chunker, common, ingest, mcp_server, query, etc. without requiring the caller to
set PYTHONPATH manually.

Run from anywhere:
    pytest tools/rag/tests/
Or from within tools/rag:
    pytest tests/
"""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

# Insert tools/rag at position 0 so its modules take precedence.
_RAG_ROOT = Path(__file__).parent.parent
if str(_RAG_ROOT) not in sys.path:
    sys.path.insert(0, str(_RAG_ROOT))


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
