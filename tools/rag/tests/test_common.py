"""Tests for common.py — config loading, metadata detection, and helper utilities.

Run with:
    cd tools/rag
    python -m pytest tests/test_common.py -v
"""
from __future__ import annotations

import fnmatch
import textwrap
from pathlib import Path

import pytest
import yaml

from common import (
    Config,
    detect_adr_id,
    detect_doc_kind,
    is_excluded,
    resolve_weight,
)


# ── is_excluded ───────────────────────────────────────────────────────────────

def test_is_excluded_matching_glob_returns_true():
    assert is_excluded("docs/draft.md", ["docs/draft*.md"]) is True


def test_is_excluded_non_matching_glob_returns_false():
    assert is_excluded("docs/adr/001.md", ["docs/draft*.md"]) is False


def test_is_excluded_empty_globs_returns_false():
    assert is_excluded("anything.md", []) is False


def test_is_excluded_wildcard_matches_all():
    assert is_excluded("any/path/file.md", ["**"]) is True


# ── detect_adr_id ─────────────────────────────────────────────────────────────

def test_detect_adr_id_standard_folder_pattern():
    assert detect_adr_id("docs/adr/0014/0014-sales.md") == "0014"


def test_detect_adr_id_nested_file():
    assert detect_adr_id("docs/adr/0003/amendments/amend-01.md") == "0003"


def test_detect_adr_id_non_adr_file_returns_none():
    assert detect_adr_id("docs/architecture/overview.md") is None


def test_detect_adr_id_uses_config_patterns_when_provided():
    class FakeCfg:
        adr_id_patterns = [r"adr/(?P<id>\d+)/"]

    assert detect_adr_id("docs/adr/0042/file.md", FakeCfg()) == "0042"


def test_detect_adr_id_config_no_match_returns_none():
    class FakeCfg:
        adr_id_patterns = [r"custom/(?P<id>ADR-\d+)/"]

    assert detect_adr_id("docs/adr/0001/file.md", FakeCfg()) is None


def test_detect_adr_id_empty_config_patterns_uses_fallback():
    class FakeCfg:
        adr_id_patterns: list = []

    assert detect_adr_id("docs/adr/0007/file.md", FakeCfg()) == "0007"


# ── detect_doc_kind ───────────────────────────────────────────────────────────

def test_detect_doc_kind_adr_main():
    assert detect_doc_kind("docs/adr/0001/0001-overview.md") == "adr_main"


def test_detect_doc_kind_adr_router_readme():
    assert detect_doc_kind("docs/adr/0001/README.md") == "adr_router"


def test_detect_doc_kind_adr_amendment():
    assert detect_doc_kind("docs/adr/0001/amendments/amend-01.md") == "adr_amendment"


def test_detect_doc_kind_adr_checklist():
    assert detect_doc_kind("docs/adr/0001/checklist.md") == "adr_checklist"


def test_detect_doc_kind_adr_migration_plan():
    assert detect_doc_kind("docs/adr/0001/migration-plan.md") == "adr_migration_plan"


def test_detect_doc_kind_context_file():
    assert detect_doc_kind(".github/context/agent-decisions.md") == "context"


def test_detect_doc_kind_architecture():
    assert detect_doc_kind("docs/architecture/bounded-context-map.md") == "architecture"


def test_detect_doc_kind_pattern():
    assert detect_doc_kind("docs/patterns/cqrs.md") == "pattern"


def test_detect_doc_kind_reference():
    assert detect_doc_kind("docs/reference/api-guide.md") == "reference"


def test_detect_doc_kind_roadmap():
    assert detect_doc_kind("docs/roadmap/q3-plan.md") == "roadmap"


def test_detect_doc_kind_unknown_returns_other():
    assert detect_doc_kind("random/file.md") == "other"


def test_detect_doc_kind_uses_config_rules_when_provided():
    class FakeCfg:
        doc_kind_rules = [{"glob": "custom/**", "kind": "custom_kind"}]

    assert detect_doc_kind("custom/something.md", FakeCfg()) == "custom_kind"


def test_detect_doc_kind_config_no_match_returns_other():
    class FakeCfg:
        doc_kind_rules = [{"glob": "custom/**", "kind": "custom_kind"}]

    assert detect_doc_kind("other/path.md", FakeCfg()) == "other"


# ── resolve_weight ────────────────────────────────────────────────────────────

RANKING_CFG = {
    "stub_byte_threshold": 400,
    "weights": [
        {"pattern": "docs/adr/*/README.md", "weight": 0.3},
        {"pattern": "docs/adr/**", "weight": 1.5},
        {"pattern": "**", "weight": 1.0},
    ],
}


def test_resolve_weight_adr_main_file_gets_adr_weight():
    w = resolve_weight("docs/adr/0001/0001-overview.md", 1000, RANKING_CFG)
    assert w == 1.5


def test_resolve_weight_adr_readme_gets_lower_weight():
    w = resolve_weight("docs/adr/0001/README.md", 1000, RANKING_CFG)
    assert w == 0.3  # First matching pattern wins


def test_resolve_weight_other_file_uses_wildcard_weight():
    w = resolve_weight("docs/architecture/overview.md", 1000, RANKING_CFG)
    assert w == 1.0


def test_resolve_weight_stub_example_implementation_buried():
    # Tiny file in example-implementation → very low weight regardless of pattern.
    w = resolve_weight(
        "docs/adr/0001/example-implementation/service.md",
        50,  # < 400 byte threshold
        RANKING_CFG,
    )
    assert w == pytest.approx(0.05)


def test_resolve_weight_stub_non_example_file_not_buried():
    # Tiny file that is NOT in example-implementation → goes through normal matching.
    w = resolve_weight(
        "docs/adr/0001/0001-overview.md",
        50,  # < 400 bytes but not an example-implementation file
        RANKING_CFG,
    )
    assert w == 1.5  # Matched by docs/adr/** pattern


def test_resolve_weight_no_patterns_returns_one():
    w = resolve_weight("anything.md", 1000, {"stub_byte_threshold": 400, "weights": []})
    assert w == 1.0


# ── Config property delegation (smoke tests via raw dict) ─────────────────────

def test_config_collection_uses_env_override(monkeypatch):
    cfg = Config(raw={
        "vector_store": {"collection": "default_collection"},
        "source": {"roots": [], "exclude_globs": []},
        "embedder": {"model": "m"},
        "chunker": {},
        "ranking": {},
        "query": {},
        "storage": {"snapshot_path": "snap", "manifest_path": "manifest.json"},
    })
    monkeypatch.setenv("RAG_COLLECTION", "override_collection")
    assert cfg.collection == "override_collection"


def test_config_collection_uses_yaml_when_no_env(monkeypatch):
    monkeypatch.delenv("RAG_COLLECTION", raising=False)
    cfg = Config(raw={
        "vector_store": {"collection": "yaml_collection"},
        "source": {"roots": [], "exclude_globs": []},
        "embedder": {"model": "m"},
        "chunker": {},
        "ranking": {},
        "query": {},
        "storage": {"snapshot_path": "snap", "manifest_path": "manifest.json"},
    })
    assert cfg.collection == "yaml_collection"


def test_config_exclude_globs_returns_list():
    cfg = Config(raw={
        "source": {"roots": [], "exclude_globs": ["*.draft.md", "tmp/**"]},
        "vector_store": {"collection": "c"},
        "embedder": {"model": "m"},
        "chunker": {},
        "ranking": {},
        "query": {},
        "storage": {"snapshot_path": "snap", "manifest_path": "manifest.json"},
    })
    assert cfg.exclude_globs == ["*.draft.md", "tmp/**"]


def test_config_chunker_returns_dict():
    cfg = Config(raw={
        "source": {"roots": [], "exclude_globs": []},
        "vector_store": {"collection": "c"},
        "embedder": {"model": "m"},
        "chunker": {"max_tokens": 800, "min_tokens": 40},
        "ranking": {},
        "query": {},
        "storage": {"snapshot_path": "snap", "manifest_path": "manifest.json"},
    })
    assert cfg.chunker["max_tokens"] == 800
