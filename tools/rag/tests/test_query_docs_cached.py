"""Unit tests for the query_docs_cached helpers in rag_tools.

Cover only pure helpers (no Qdrant required):
- _derive_source_label  — deterministic, label shape, ADR / scope_attrs / fallback routing
- _format_chunks_to_markdown — header, breadcrumb, path-with-line-range, separators

scope_attrs is a dict[str, str] — any keys the caller cares about.
The first entry's key+value are used for the slug in the source label.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from rag_tools import _derive_source_label, _format_chunks_to_markdown  # noqa: E402


# ── _derive_source_label ─────────────────────────────────────────────────────

def test_source_label_extracts_adr_id_from_question() -> None:
    assert _derive_source_label("How does ADR-0029 work?", None).startswith("rag-cache-adr0029-")
    # 3-digit form also matches.
    assert _derive_source_label("Tell me about adr 016 pls", None).startswith("rag-cache-adr0016-")
    # Bare 4-digit ID is treated as ADR id.
    assert _derive_source_label("0016 coupons limits", None).startswith("rag-cache-adr0016-")


def test_source_label_uses_scope_attrs_first_entry() -> None:
    # bc key → rag-cache-bc-<slug>-<hash8>
    label = _derive_source_label("Tell me about checkout flow", {"bc": "Sales/Orders"})
    assert label.startswith("rag-cache-bc-sales-orders-")


def test_source_label_uses_arbitrary_scope_key() -> None:
    label = _derive_source_label("catalog docs", {"topic": "Catalog"})
    assert label.startswith("rag-cache-topic-catalog-")


def test_source_label_uses_generic_scope_key() -> None:
    label = _derive_source_label("catalog docs", {"scope": "Catalog"})
    assert label.startswith("rag-cache-scope-catalog-")


def test_source_label_uses_first_entry_only() -> None:
    # dict with multiple keys — only the first key-value pair is used for the slug
    label = _derive_source_label("some question", {"region": "PL", "bc": "Orders"})
    assert label.startswith("rag-cache-region-pl-")


def test_source_label_falls_back_to_q_prefix() -> None:
    label = _derive_source_label("What is FluentValidation convention?", None)
    assert label.startswith("rag-cache-q-")


def test_source_label_falls_back_to_q_prefix_for_empty_dict() -> None:
    label = _derive_source_label("some question", {})
    assert label.startswith("rag-cache-q-")


def test_source_label_is_deterministic() -> None:
    a = _derive_source_label("How does ADR-0029 work?", None)
    b = _derive_source_label("How does ADR-0029 work?", None)
    assert a == b


def test_source_label_deterministic_with_scope_attrs() -> None:
    a = _derive_source_label("catalog flow", {"bc": "Catalog"})
    b = _derive_source_label("catalog flow", {"bc": "Catalog"})
    assert a == b


def test_source_label_differs_for_different_questions() -> None:
    a = _derive_source_label("How does ADR-0029 work?", None)
    b = _derive_source_label("How does ADR-0030 work?", None)
    assert a != b


def test_source_label_lowercase_ascii_kebab_only() -> None:
    label = _derive_source_label("Q with weird CHARS!!", {"BC Key": "Value With Spaces"})
    assert label == label.lower()
    assert all(c.isalnum() or c == "-" for c in label)
    assert label.startswith("rag-cache-")


def test_source_label_adr_wins_over_scope_attrs() -> None:
    # ADR detection has priority 1 even when scope_attrs is provided
    label = _derive_source_label("ADR-0028 overview", {"bc": "Catalog"})
    assert label.startswith("rag-cache-adr0028-")


# ── _format_chunks_to_markdown ───────────────────────────────────────────────

def test_format_markdown_contains_header_and_metadata() -> None:
    files = [{
        "rel_path": "docs/adr/0029/file.md",
        "score": 0.95,
        "chunks": [{
            "lines": "10-50",
            "score": 0.95,
            "breadcrumb": "ADR-0029 > Decision",
            "text": "Sample chunk text.",
        }],
    }]
    md = _format_chunks_to_markdown("How does ADR-0029 work?", None, files)
    assert "# How does ADR-0029 work" in md  # heading without trailing '?'
    assert "Cached from RAG on" in md
    assert "query_docs_cached(" in md
    assert "## file.md" in md
    assert "**Path**: `docs/adr/0029/file.md#L10-L50`" in md
    assert "**Breadcrumb**: ADR-0029 > Decision" in md
    assert "Sample chunk text." in md
    assert md.endswith("\n")


def test_format_markdown_includes_scope_attrs_in_source_line() -> None:
    md = _format_chunks_to_markdown("checkout flow", {"bc": "Sales/Orders"}, [])
    assert 'scope_attrs={"bc": "Sales/Orders"}' in md


def test_format_markdown_includes_multi_key_scope_attrs() -> None:
    md = _format_chunks_to_markdown("q", {"region": "PL", "bc": "Orders"}, [])
    assert "scope_attrs=" in md
    assert "region" in md
    assert "PL" in md


def test_format_markdown_no_scope_arg_when_none() -> None:
    md = _format_chunks_to_markdown("q", None, [])
    assert "scope_attrs=" not in md


def test_format_markdown_handles_multiple_files() -> None:
    files = [
        {
            "rel_path": "docs/a.md",
            "score": 0.9,
            "chunks": [{"lines": "1-10", "score": 0.9, "breadcrumb": "A > x", "text": "AAA"}],
        },
        {
            "rel_path": "docs/b.md",
            "score": 0.8,
            "chunks": [{"lines": "20-30", "score": 0.8, "breadcrumb": "B > y", "text": "BBB"}],
        },
    ]
    md = _format_chunks_to_markdown("q", None, files)
    assert md.count("## ") == 2
    assert "AAA" in md and "BBB" in md
