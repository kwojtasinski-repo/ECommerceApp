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
    load_config,
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


# ── load_config with explicit path ────────────────────────────────────────────

_MINIMAL_CONFIG = textwrap.dedent("""\
    source:
      roots: [docs]
      exclude_globs: []
    embedder: {model: test-model}
    chunker: {}
    ranking: {weights: []}
    query: {default_top_k: 5, fetch_k: 10, score_threshold: 0.0}
    vector_store: {mode: memory, collection: test_col, url: "http://localhost:6333"}
    storage:
      snapshot_path: .rag/snapshot
      manifest_path: .rag/manifest.json
""")


def _write_companion_files(tmp_path: Path) -> Path:
    """Write config.yaml + companion queries.yaml + metadata-rules.yaml to tmp_path."""
    config_yaml = tmp_path / "config.yaml"
    config_yaml.write_text(
        _MINIMAL_CONFIG + textwrap.dedent("""\
            config_files:
              metadata_rules: metadata-rules.yaml
              queries: queries.yaml
        """),
        encoding="utf-8",
    )
    (tmp_path / "queries.yaml").write_text(
        textwrap.dedent("""\
            named_queries:
              - {name: q-one, question: "What is a domain event?"}
              - {name: q-two, question: "Explain CQRS"}
        """),
        encoding="utf-8",
    )
    (tmp_path / "metadata-rules.yaml").write_text(
        textwrap.dedent("""\
            adr_id_patterns:
              - {pattern: "adr/(?P<id>\\\\d{4})/"}
            doc_kind_rules:
              - {glob: "docs/adr/*/README.md", kind: adr_router}
              - {glob: "docs/adr/**", kind: adr_main}
        """),
        encoding="utf-8",
    )
    return config_yaml


def test_load_config_explicit_path_returns_config(tmp_path):
    config_yaml = _write_companion_files(tmp_path)
    cfg = load_config(config_yaml)
    assert isinstance(cfg, Config)


def test_load_config_loads_named_queries_from_companion(tmp_path):
    config_yaml = _write_companion_files(tmp_path)
    cfg = load_config(config_yaml)
    assert len(cfg.named_queries) == 2
    assert cfg.named_queries[0]["name"] == "q-one"
    assert cfg.named_queries[1]["name"] == "q-two"


def test_load_config_loads_adr_id_patterns_from_companion(tmp_path):
    config_yaml = _write_companion_files(tmp_path)
    cfg = load_config(config_yaml)
    assert len(cfg.adr_id_patterns) == 1
    assert "id" in cfg.adr_id_patterns[0]  # named group present


def test_load_config_loads_doc_kind_rules_from_companion(tmp_path):
    config_yaml = _write_companion_files(tmp_path)
    cfg = load_config(config_yaml)
    assert len(cfg.doc_kind_rules) == 2
    assert cfg.doc_kind_rules[0]["kind"] == "adr_router"


def test_load_config_missing_companion_files_gives_empty_metadata(tmp_path):
    """No companion files → empty named_queries, empty adr_id_patterns, empty doc_kind_rules."""
    config_yaml = tmp_path / "config.yaml"
    config_yaml.write_text(_MINIMAL_CONFIG, encoding="utf-8")

    cfg = load_config(config_yaml)

    assert cfg.named_queries == []
    assert cfg.adr_id_patterns == []
    assert cfg.doc_kind_rules == []


def test_load_config_companion_resolved_relative_to_config_dir(tmp_path):
    """Companion files are resolved from the config.yaml directory, not CWD."""
    sub = tmp_path / "project" / "rag"
    sub.mkdir(parents=True)
    config_yaml = sub / "config.yaml"
    config_yaml.write_text(
        _MINIMAL_CONFIG + "config_files:\n  queries: queries.yaml\n",
        encoding="utf-8",
    )
    (sub / "queries.yaml").write_text(
        "named_queries:\n  - {name: subdir-q, question: Test}\n",
        encoding="utf-8",
    )

    cfg = load_config(config_yaml)
    assert len(cfg.named_queries) == 1
    assert cfg.named_queries[0]["name"] == "subdir-q"
