"""Tests for ingest.py — pipeline utilities and incremental change detection.

Strategy: no external services (no Qdrant, no ONNX model).
Each test section targets a focused, importable function.

Fixtures and factories are named after what they build, not after "helper".
"""
from __future__ import annotations

import hashlib
import json
import textwrap
from pathlib import Path
from typing import Any
from unittest.mock import patch

import pytest

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

from ingest import (
    _build_payload,
    _file_doc_title,
    _load_file_manifest,
    _save_file_manifest,
    _sha256_file,
    _stable_id,
)


# ── Factories ─────────────────────────────────────────────────────────────────

class MinimalChunk:
    """Stand-in for a chunker.Chunk when tests need a real object shape."""

    def __init__(
        self,
        text: str = "some text",
        breadcrumb: str = "Doc > Section",
        heading_path: str = "Section",
        start_line: int = 1,
        end_line: int = 10,
        token_count: int = 5,
    ) -> None:
        self.text = text
        self.breadcrumb = breadcrumb
        self.heading_path = heading_path
        self.start_line = start_line
        self.end_line = end_line
        self.token_count = token_count


def make_chunk(**overrides: Any) -> MinimalChunk:
    """Factory for MinimalChunk with sensible defaults; accepts keyword overrides."""
    return MinimalChunk(**overrides)


def make_config_raw(
    *,
    collection: str = "test_col",
    manifest_path: str = ".rag/manifest.json",
    snapshot_path: str = ".rag/snapshot.qdrant",
    metadata_rules: dict | None = None,
) -> dict:
    """Build a minimal raw config dict accepted by Config(raw=...)."""
    return {
        "source": {"roots": ["docs"], "exclude_globs": []},
        "embedder": {"model": "test-model", "device": "cpu", "dimensions": 4, "batch_size": 4},
        "chunker": {"max_tokens": 800, "min_tokens": 1, "overlap_tokens": 80, "split_on_headings": [1, 2, 3]},
        "vector_store": {"backend": "qdrant", "mode": "memory", "collection": collection, "url": "http://localhost:6333"},
        "ranking": {"weights": []},
        "query": {"default_top_k": 5, "fetch_k": 20, "score_threshold": 0.3},
        "storage": {"manifest_path": manifest_path, "snapshot_path": snapshot_path},
        "metadata_rules": metadata_rules or {},
    }


def make_config(tmp_path: Path, **overrides) -> Any:
    """Return a Config instance pointing to tmp_path for manifest and snapshot."""
    from common import Config
    manifest_rel = ".rag/manifest.json"
    snapshot_rel = ".rag/snapshot.qdrant"
    raw = make_config_raw(manifest_path=manifest_rel, snapshot_path=snapshot_rel, **overrides)
    cfg = Config(raw=raw)
    # Patch manifest_path and snapshot_path to point inside tmp_path.
    object.__setattr__(cfg, "_tmp_root", tmp_path)
    return _ConfigWithTmpPath(cfg, tmp_path)


class _ConfigWithTmpPath:
    """Thin wrapper that redirects manifest_path/snapshot_path to a temp directory."""

    def __init__(self, cfg: Any, tmp_path: Path) -> None:
        self._cfg = cfg
        self._tmp = tmp_path

    def __getattr__(self, name: str) -> Any:
        return getattr(self._cfg, name)

    @property
    def manifest_path(self) -> Path:
        return self._tmp / ".rag" / "manifest.json"

    @property
    def snapshot_path(self) -> Path:
        return self._tmp / ".rag" / "snapshot.qdrant"


# ── _file_doc_title ───────────────────────────────────────────────────────────

class TestFileDocTitle:
    def test_returns_first_h1(self):
        text = "# My Document\n\nSome paragraph."
        assert _file_doc_title("docs/foo.md", text) == "My Document"

    def test_returns_rel_path_when_no_h1(self):
        text = "Just a paragraph.\n\nNo heading here."
        assert _file_doc_title("docs/foo.md", text) == "docs/foo.md"

    def test_ignores_frontmatter_dash_lines(self):
        # The function skips lines starting with '---' but stops at other content.
        # A bare '---\n# Title' (no body between dashes) does reach the H1.
        text = "---\n# Real Title"
        assert _file_doc_title("docs/foo.md", text) == "Real Title"

    def test_strips_extra_whitespace_from_h1(self):
        text = "#   Padded Title   \n\nBody."
        assert _file_doc_title("docs/foo.md", text) == "Padded Title"

    def test_ignores_h2_as_title(self):
        text = "## Not A Title\n\nBody."
        # No H1 → falls back to rel_path
        assert _file_doc_title("docs/bar.md", text) == "docs/bar.md"

    def test_empty_file_returns_rel_path(self):
        assert _file_doc_title("docs/empty.md", "") == "docs/empty.md"


# ── _stable_id ────────────────────────────────────────────────────────────────

class TestStableId:
    def test_same_inputs_produce_same_id(self):
        a = _stable_id("docs/adr/0001/adr.md", "ADR > Decision", 42)
        b = _stable_id("docs/adr/0001/adr.md", "ADR > Decision", 42)
        assert a == b

    def test_different_paths_produce_different_ids(self):
        a = _stable_id("docs/a.md", "Section", 1)
        b = _stable_id("docs/b.md", "Section", 1)
        assert a != b

    def test_different_start_lines_produce_different_ids(self):
        a = _stable_id("docs/a.md", "Section", 1)
        b = _stable_id("docs/a.md", "Section", 2)
        assert a != b

    def test_returns_integer(self):
        result = _stable_id("docs/a.md", "X", 0)
        assert isinstance(result, int)

    def test_id_is_positive(self):
        # blake2b digest interpreted as unsigned big-endian int is always >= 0
        result = _stable_id("docs/a.md", "X", 0)
        assert result >= 0


# ── _sha256_file ──────────────────────────────────────────────────────────────

class TestSha256File:
    def test_returns_hex_string(self, tmp_path):
        f = tmp_path / "doc.md"
        f.write_bytes(b"hello world")
        result = _sha256_file(f)
        assert isinstance(result, str)
        assert len(result) == 64  # SHA-256 hex = 64 chars

    def test_matches_expected_hash(self, tmp_path):
        content = b"deterministic content"
        f = tmp_path / "doc.md"
        f.write_bytes(content)
        expected = hashlib.sha256(content).hexdigest()
        assert _sha256_file(f) == expected

    def test_different_content_different_hash(self, tmp_path):
        f1 = tmp_path / "a.md"
        f2 = tmp_path / "b.md"
        f1.write_bytes(b"aaa")
        f2.write_bytes(b"bbb")
        assert _sha256_file(f1) != _sha256_file(f2)

    def test_empty_file_has_known_hash(self, tmp_path):
        f = tmp_path / "empty.md"
        f.write_bytes(b"")
        expected = hashlib.sha256(b"").hexdigest()
        assert _sha256_file(f) == expected


# ── _build_payload ────────────────────────────────────────────────────────────

class TestBuildPayload:
    def test_rel_path_in_payload(self):
        chunk = make_chunk()
        payload = _build_payload("docs/adr/0001/adr.md", "ADR Title", chunk, 1.0)
        assert payload["rel_path"] == "docs/adr/0001/adr.md"

    def test_doc_title_in_payload(self):
        chunk = make_chunk()
        payload = _build_payload("docs/x.md", "My Doc", chunk, 1.0)
        assert payload["doc_title"] == "My Doc"

    def test_weight_in_payload(self):
        chunk = make_chunk()
        payload = _build_payload("docs/x.md", "T", chunk, 1.25)
        assert payload["weight"] == 1.25

    def test_chunk_fields_forwarded(self):
        chunk = make_chunk(text="chunk text", breadcrumb="A > B", start_line=5, end_line=20, token_count=12)
        payload = _build_payload("docs/x.md", "T", chunk, 1.0)
        assert payload["text"] == "chunk text"
        assert payload["breadcrumb"] == "A > B"
        assert payload["start_line"] == 5
        assert payload["end_line"] == 20
        assert payload["token_count"] == 12

    def test_doc_kind_present(self):
        chunk = make_chunk()
        payload = _build_payload("docs/x.md", "T", chunk, 1.0)
        assert "doc_kind" in payload

    def test_adr_id_present(self):
        chunk = make_chunk()
        payload = _build_payload("docs/adr/0001/adr.md", "T", chunk, 1.0)
        assert "adr_id" in payload


# ── _load_file_manifest ───────────────────────────────────────────────────────

class TestLoadFileManifest:
    def test_returns_empty_structure_when_no_manifest(self, tmp_path):
        cfg = make_config(tmp_path)
        result = _load_file_manifest(cfg)
        assert result == {"last_indexed": None, "file_hashes": {}}

    def test_loads_existing_manifest(self, tmp_path):
        cfg = make_config(tmp_path)
        manifest_path = cfg.manifest_path
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_data = {"last_indexed": "2024-01-01T00:00:00Z", "file_hashes": {"docs/a.md": "abc123"}}
        manifest_path.write_text(json.dumps(manifest_data), encoding="utf-8")

        result = _load_file_manifest(cfg)
        assert result["file_hashes"]["docs/a.md"] == "abc123"

    def test_returns_empty_when_manifest_has_no_file_hashes_key(self, tmp_path):
        cfg = make_config(tmp_path)
        manifest_path = cfg.manifest_path
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps({"legacy_key": "value"}), encoding="utf-8")

        result = _load_file_manifest(cfg)
        assert result == {"last_indexed": None, "file_hashes": {}}


# ── _save_file_manifest ───────────────────────────────────────────────────────

class TestSaveFileManifest:
    def test_creates_manifest_file(self, tmp_path):
        cfg = make_config(tmp_path)
        _save_file_manifest(cfg, {"docs/a.md": "abc"}, total_files=1, total_chunks=3, dim=384)
        assert cfg.manifest_path.exists()

    def test_manifest_contains_file_hashes(self, tmp_path):
        cfg = make_config(tmp_path)
        hashes = {"docs/a.md": "hash1", "docs/b.md": "hash2"}
        _save_file_manifest(cfg, hashes, total_files=2, total_chunks=5, dim=384)
        saved = json.loads(cfg.manifest_path.read_text(encoding="utf-8"))
        assert saved["file_hashes"] == hashes

    def test_manifest_contains_model_name(self, tmp_path):
        cfg = make_config(tmp_path)
        _save_file_manifest(cfg, {}, total_files=0, total_chunks=0, dim=384)
        saved = json.loads(cfg.manifest_path.read_text(encoding="utf-8"))
        assert saved["model"] == "test-model"

    def test_manifest_contains_dim(self, tmp_path):
        cfg = make_config(tmp_path)
        _save_file_manifest(cfg, {}, total_files=0, total_chunks=0, dim=384)
        saved = json.loads(cfg.manifest_path.read_text(encoding="utf-8"))
        assert saved["dim"] == 384

    def test_manifest_directory_created_automatically(self, tmp_path):
        cfg = make_config(tmp_path)
        assert not cfg.manifest_path.parent.exists()
        _save_file_manifest(cfg, {}, total_files=0, total_chunks=0, dim=4)
        assert cfg.manifest_path.parent.exists()

    def test_roundtrip_save_then_load(self, tmp_path):
        cfg = make_config(tmp_path)
        hashes = {"docs/a.md": "abc123"}
        _save_file_manifest(cfg, hashes, total_files=1, total_chunks=2, dim=4)
        loaded = _load_file_manifest(cfg)
        assert loaded["file_hashes"] == hashes
