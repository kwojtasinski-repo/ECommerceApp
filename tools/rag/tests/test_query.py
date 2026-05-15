"""Tests for query.py — QueryEngine search logic and result formatting.

Strategy: no external services (no Qdrant, no sentence-transformers).
QueryEngine is initialized with pre-injected _client and _model attributes
to bypass lazy _ensure() and avoid real network/model calls.

Factories produce minimal fake objects that satisfy the duck-typed interfaces
used inside QueryEngine.search().
"""
from __future__ import annotations

import sys
from pathlib import Path
from typing import Any
from unittest.mock import MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from query import QueryEngine, QueryHit, _matches_bc


# ── Factories ─────────────────────────────────────────────────────────────────

def make_config_raw(
    *,
    collection: str = "test_col",
    top_k: int = 5,
    fetch_k: int = 20,
    score_threshold: float = 0.3,
) -> dict:
    """Minimal raw config accepted by Config(raw=...)."""
    return {
        "source": {"roots": ["docs"], "exclude_globs": []},
        "embedder": {"model": "test-model", "device": "cpu", "dimensions": 4, "batch_size": 4},
        "chunker": {"max_tokens": 800, "min_tokens": 1, "overlap_tokens": 80, "split_on_headings": [1, 2, 3]},
        "vector_store": {"backend": "qdrant", "mode": "memory", "collection": collection, "url": "http://localhost:6333"},
        "ranking": {"weights": []},
        "query": {"default_top_k": top_k, "fetch_k": fetch_k, "score_threshold": score_threshold},
        "storage": {"manifest_path": ".rag/manifest.json", "snapshot_path": ".rag/snapshot.qdrant"},
        "metadata_rules": {},
    }


def make_qdrant_result(
    *,
    score: float = 0.8,
    rel_path: str = "docs/adr/0001/adr.md",
    doc_title: str = "ADR 0001",
    doc_kind: str = "adr_main",
    adr_id: str | None = "0001",
    breadcrumb: str = "ADR 0001 > Decision",
    start_line: int = 1,
    end_line: int = 50,
    weight: float = 1.0,
    text: str = "Sample text.",
) -> MagicMock:
    """Factory for a fake Qdrant ScoredPoint with payload dict."""
    result = MagicMock()
    result.score = score
    result.payload = {
        "rel_path": rel_path,
        "doc_title": doc_title,
        "doc_kind": doc_kind,
        "adr_id": adr_id,
        "breadcrumb": breadcrumb,
        "start_line": start_line,
        "end_line": end_line,
        "weight": weight,
        "text": text,
    }
    return result


def make_fake_model(embedding: list[float] | None = None) -> MagicMock:
    """Factory for a fake SentenceTransformer that returns a fixed embedding."""
    if embedding is None:
        embedding = [0.1, 0.2, 0.3, 0.4]
    model = MagicMock()
    import numpy as np
    model.encode.return_value = [np.array(embedding)]
    return model


def make_engine_with_stubs(
    *,
    qdrant_results: list | None = None,
    embedding: list[float] | None = None,
    collection: str = "test_col",
    top_k: int = 5,
    fetch_k: int = 20,
) -> QueryEngine:
    """Build a QueryEngine with _client and _model pre-injected (bypasses _ensure())."""
    from common import Config
    cfg = Config(raw=make_config_raw(collection=collection, top_k=top_k, fetch_k=fetch_k))
    engine = QueryEngine.__new__(QueryEngine)
    engine.cfg = cfg
    engine._mode = "memory"

    fake_client = MagicMock()
    fake_client.search.return_value = qdrant_results or []
    engine._client = fake_client

    engine._model = make_fake_model(embedding)
    return engine


# ── _matches_bc ───────────────────────────────────────────────────────────────

class TestMatchesBc:
    def test_matches_breadcrumb_substring_case_insensitive(self):
        payload = {"breadcrumb": "Orders > Create Order", "doc_title": ""}
        assert _matches_bc(payload, "orders") is True

    def test_matches_doc_title_substring_case_insensitive(self):
        payload = {"breadcrumb": "", "doc_title": "Catalog BC Overview"}
        assert _matches_bc(payload, "catalog") is True

    def test_returns_false_when_no_match(self):
        payload = {"breadcrumb": "Payments > Refund", "doc_title": "Payments"}
        assert _matches_bc(payload, "catalog") is False

    def test_empty_bc_filter_always_matches_breadcrumb(self):
        # Empty string is a substring of everything.
        payload = {"breadcrumb": "X > Y", "doc_title": "Z"}
        assert _matches_bc(payload, "") is True

    def test_missing_breadcrumb_key_does_not_raise(self):
        payload = {"doc_title": "Something"}
        assert _matches_bc(payload, "something") is True

    def test_missing_doc_title_key_does_not_raise(self):
        payload = {"breadcrumb": "A > B"}
        assert _matches_bc(payload, "c") is False


# ── QueryEngine.search — result mapping ───────────────────────────────────────

class TestQueryEngineSearch:
    def test_returns_list_of_query_hits(self):
        engine = make_engine_with_stubs(qdrant_results=[make_qdrant_result()])
        hits = engine.search("test query")
        assert isinstance(hits, list)
        assert all(isinstance(h, QueryHit) for h in hits)

    def test_empty_results_when_no_qdrant_hits(self):
        engine = make_engine_with_stubs(qdrant_results=[])
        hits = engine.search("test query")
        assert hits == []

    def test_hit_fields_populated_from_payload(self):
        result = make_qdrant_result(
            rel_path="docs/adr/0016/adr.md",
            doc_title="Coupons",
            doc_kind="adr_main",
            adr_id="0016",
            breadcrumb="Coupons > Decision",
            start_line=10,
            end_line=40,
            text="Coupon content.",
            score=0.9,
            weight=1.2,
        )
        engine = make_engine_with_stubs(qdrant_results=[result])
        hit = engine.search("coupons")[0]

        assert hit.rel_path == "docs/adr/0016/adr.md"
        assert hit.doc_title == "Coupons"
        assert hit.doc_kind == "adr_main"
        assert hit.adr_id == "0016"
        assert hit.breadcrumb == "Coupons > Decision"
        assert hit.start_line == 10
        assert hit.end_line == 40
        assert hit.text == "Coupon content."

    def test_final_score_is_raw_score_times_weight(self):
        result = make_qdrant_result(score=0.8, weight=1.25)
        engine = make_engine_with_stubs(qdrant_results=[result])
        hit = engine.search("q")[0]
        assert abs(hit.final_score - 0.8 * 1.25) < 1e-6

    def test_results_sorted_by_final_score_descending(self):
        results = [
            make_qdrant_result(score=0.5, weight=1.0, rel_path="docs/a.md"),
            make_qdrant_result(score=0.9, weight=1.0, rel_path="docs/b.md"),
            make_qdrant_result(score=0.7, weight=1.0, rel_path="docs/c.md"),
        ]
        engine = make_engine_with_stubs(qdrant_results=results)
        hits = engine.search("q")
        scores = [h.final_score for h in hits]
        assert scores == sorted(scores, reverse=True)

    def test_top_k_limits_returned_hits(self):
        results = [make_qdrant_result(score=0.9 - i * 0.1, rel_path=f"docs/{i}.md") for i in range(6)]
        engine = make_engine_with_stubs(qdrant_results=results, top_k=3)
        hits = engine.search("q", top_k=3)
        assert len(hits) <= 3

    def test_weight_multiplier_from_ranking(self):
        # Weight 2.0 should double the score.
        result = make_qdrant_result(score=0.5, weight=2.0)
        engine = make_engine_with_stubs(qdrant_results=[result])
        hit = engine.search("q")[0]
        assert abs(hit.final_score - 1.0) < 1e-6

    def test_model_encode_called_with_query(self):
        engine = make_engine_with_stubs(qdrant_results=[])
        engine.search("find something")
        engine._model.encode.assert_called_once()
        call_args = engine._model.encode.call_args
        assert "find something" in call_args[0][0]

    def test_qdrant_search_called_with_correct_collection(self):
        engine = make_engine_with_stubs(qdrant_results=[], collection="my_col")
        engine.search("q")
        engine._client.search.assert_called_once()
        kwargs = engine._client.search.call_args[1]
        assert kwargs.get("collection_name") == "my_col"

    def test_fetch_k_passed_to_qdrant(self):
        engine = make_engine_with_stubs(qdrant_results=[], fetch_k=15)
        engine.search("q", fetch_k=15)
        kwargs = engine._client.search.call_args[1]
        assert kwargs.get("limit") == 15


# ── QueryEngine.search — bc_filter ────────────────────────────────────────────

class TestQueryEngineSearchBcFilter:
    def test_bc_filter_excludes_non_matching_hits(self):
        results = [
            make_qdrant_result(breadcrumb="Orders > Create", doc_title="Orders", rel_path="docs/a.md"),
            make_qdrant_result(breadcrumb="Payments > Refund", doc_title="Payments", rel_path="docs/b.md"),
        ]
        engine = make_engine_with_stubs(qdrant_results=results)
        hits = engine.search("q", bc_filter="orders")
        paths = [h.rel_path for h in hits]
        assert "docs/a.md" in paths
        assert "docs/b.md" not in paths

    def test_bc_filter_case_insensitive(self):
        results = [make_qdrant_result(breadcrumb="CATALOG > Overview", doc_title="")]
        engine = make_engine_with_stubs(qdrant_results=results)
        hits = engine.search("q", bc_filter="catalog")
        assert len(hits) == 1

    def test_no_bc_filter_returns_all_hits(self):
        results = [
            make_qdrant_result(rel_path="docs/a.md"),
            make_qdrant_result(rel_path="docs/b.md"),
        ]
        engine = make_engine_with_stubs(qdrant_results=results)
        hits = engine.search("q", bc_filter=None)
        assert len(hits) == 2


# ── QueryHit.as_dict ──────────────────────────────────────────────────────────

class TestQueryHitAsDict:
    def test_as_dict_contains_rel_path(self):
        result = make_qdrant_result(rel_path="docs/adr/0001/adr.md")
        engine = make_engine_with_stubs(qdrant_results=[result])
        hit = engine.search("q")[0]
        d = hit.as_dict()
        assert d["rel_path"] == "docs/adr/0001/adr.md"

    def test_as_dict_contains_all_fields(self):
        result = make_qdrant_result()
        engine = make_engine_with_stubs(qdrant_results=[result])
        hit = engine.search("q")[0]
        d = hit.as_dict()
        expected_keys = {
            "rel_path", "doc_title", "doc_kind", "adr_id", "breadcrumb",
            "start_line", "end_line", "raw_score", "weight", "final_score", "text",
        }
        assert expected_keys.issubset(d.keys())
