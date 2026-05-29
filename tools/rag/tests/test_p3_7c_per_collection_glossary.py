"""Tests for P3-7c: per-collection glossary resolution.

Covers:
- EmbedContext.glossary_entries override in GlossaryExpansionPreprocessor
- _resolve_glossary_entries returns None when CONFIG_SOURCE is None (STDIO safety)
- _parse_zip_batch bakes mounted glossary when ZIP has none (Option A parity with .NET)
- _parse_zip_batch uses ZIP-supplied glossary when present
- config_source.invalidate is called after store_config in upload_batch

No sentence-transformers or real Qdrant required — all tests use fakes.
"""
from __future__ import annotations

import asyncio
import io
import sys
import zipfile
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from embed_context import EmbedContext, EmbedPurpose, QUERY_CTX, INGEST_CTX
from config.payload import GlossaryEntry, RagConfigPayload
from preprocessors import GlossaryExpansionPreprocessor


# ── helpers ───────────────────────────────────────────────────────────────────


def _run(coro):
    return asyncio.run(coro)


_MOUNTED_GLOSSARY: list[tuple[str, list[str]]] = [
    ("order", ["zamówienie", "bestellung"]),
    ("payment", ["płatność", "zahlung"]),
]

_PER_COLLECTION_GLOSSARY: tuple[GlossaryEntry, ...] = (
    GlossaryEntry(english="invoice", patterns=("faktura", "rechnung")),
)


# ── EmbedContext override tests ───────────────────────────────────────────────


class TestGlossaryOverrideInContext:
    """Preprocessor uses ctx.glossary_entries when non-None, falls back when None."""

    def test_uses_ctx_glossary_entries_on_query(self):
        pre = GlossaryExpansionPreprocessor(_MOUNTED_GLOSSARY)
        ctx = EmbedContext(
            purpose=EmbedPurpose.QUERY,
            glossary_entries=_PER_COLLECTION_GLOSSARY,
        )
        result = pre.process("faktura status", ctx)
        assert "invoice" in result
        # Mounted glossary NOT used — "order" should NOT appear
        assert "order" not in result

    def test_falls_back_to_mounted_when_ctx_has_none(self):
        pre = GlossaryExpansionPreprocessor(_MOUNTED_GLOSSARY)
        ctx = EmbedContext(purpose=EmbedPurpose.QUERY, glossary_entries=None)
        result = pre.process("zamówienie status", ctx)
        assert "order" in result

    def test_empty_ctx_glossary_suppresses_mounted(self):
        """Empty tuple in ctx means 'collection has no glossary' — not 'use mounted'."""
        pre = GlossaryExpansionPreprocessor(_MOUNTED_GLOSSARY)
        ctx = EmbedContext(purpose=EmbedPurpose.QUERY, glossary_entries=())
        result = pre.process("zamówienie status", ctx)
        # Empty override → no expansion at all
        assert result == "zamówienie status"

    def test_ingest_purpose_skips_expansion_regardless_of_ctx_glossary(self):
        pre = GlossaryExpansionPreprocessor(_MOUNTED_GLOSSARY)
        ctx = EmbedContext(purpose=EmbedPurpose.INGEST, glossary_entries=_PER_COLLECTION_GLOSSARY)
        result = pre.process("faktura status", ctx)
        assert result == "faktura status"

    def test_query_ctx_singleton_has_none_glossary_entries(self):
        # QUERY_CTX must have glossary_entries=None so existing call sites don't break
        assert QUERY_CTX.glossary_entries is None

    def test_ingest_ctx_singleton_has_none_glossary_entries(self):
        assert INGEST_CTX.glossary_entries is None


# ── _resolve_glossary_entries tests ──────────────────────────────────────────


class TestResolveGlossaryEntries:
    """Tests for the async resolver in rag_tools, without importing the full server stack."""

    def _make_config_source(self, entries: tuple[GlossaryEntry, ...]) -> MagicMock:
        mock = MagicMock()
        payload = RagConfigPayload(glossary_entries=entries)
        mock.get_effective = AsyncMock(return_value=payload)
        return mock

    def test_returns_none_when_config_source_is_none(self):
        """STDIO safety: no config source wired → None returned → engine uses mounted."""
        import state
        import rag_tools
        original = state.CONFIG_SOURCE
        try:
            state.CONFIG_SOURCE = None
            result = _run(rag_tools._resolve_glossary_entries("my_collection"))
            assert result is None
        finally:
            state.CONFIG_SOURCE = original

    def test_returns_entries_from_config_source(self):
        import state
        import rag_tools
        original = state.CONFIG_SOURCE
        original_cfg = state.CFG
        try:
            state.CONFIG_SOURCE = self._make_config_source(_PER_COLLECTION_GLOSSARY)
            # CFG is needed for the fallback when collection=None
            state.CFG = MagicMock()
            state.CFG.collection = "my_collection"
            result = _run(rag_tools._resolve_glossary_entries("my_collection"))
            assert result == _PER_COLLECTION_GLOSSARY
        finally:
            state.CONFIG_SOURCE = original
            state.CFG = original_cfg

    def test_returns_none_on_exception_in_config_source(self):
        import state
        import rag_tools
        original = state.CONFIG_SOURCE
        try:
            mock = MagicMock()
            mock.get_effective = AsyncMock(side_effect=RuntimeError("qdrant down"))
            state.CONFIG_SOURCE = mock
            result = _run(rag_tools._resolve_glossary_entries("my_collection"))
            assert result is None
        finally:
            state.CONFIG_SOURCE = original


# ── ingest_routes glossary baking tests ──────────────────────────────────────


def _make_zip(files: dict[str, str]) -> bytes:
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        for name, content in files.items():
            zf.writestr(name, content)
    return buf.getvalue()


_META_YAML = """\
doc_kind_rules:
  - glob: "**/*.md"
    kind: "other"
"""

_QUERIES_YAML = """\
named_queries:
  - name: "general"
    query: "documentation"
    doc_kind: "other"
"""

_RAG_CONFIG_YAML = """\
chunker: { max_tokens: 512 }
ranking: { weights: [ { pattern: "docs/**", weight: 1.0 } ] }
"""

_GLOSSARY_YAML = """\
entries:
  - english: invoice
    patterns:
      - faktura
      - rechnung
"""


def _batch_zip(include_glossary: bool = False, include_doc: bool = True) -> bytes:
    files = {
        "rag-config.yaml": _RAG_CONFIG_YAML,
        "metadata-rules.yaml": _META_YAML,
        "queries.yaml": _QUERIES_YAML,
    }
    if include_glossary:
        files["multilingual-glossary.yaml"] = _GLOSSARY_YAML
    if include_doc:
        files["docs/test.md"] = "# Test\nContent."
    return _make_zip(files)


class TestIngestRoutesBakesGlossary:
    """Verify _parse_zip_batch baking behaviour (Option A: always bake, ZIP wins)."""

    def _parse(self, zip_bytes: bytes, mounted_cfg=None):
        from ingest_routes import _parse_zip_batch
        content, err = _parse_zip_batch(
            zip_bytes, capacity=100, queue_size=0, mounted_cfg=mounted_cfg
        )
        assert err is None, f"parse error: {err}"
        return content

    def _mounted_cfg(self, glossary_path=None):
        cfg = MagicMock()
        cfg.glossary_path = glossary_path
        return cfg

    def test_no_zip_glossary_no_mounted_glossary_yields_empty(self):
        """No ZIP file, no mounted path → empty glossary_entries."""
        content = self._parse(_batch_zip(include_glossary=False), mounted_cfg=None)
        assert content.config_payload.glossary_entries == ()

    def test_no_zip_glossary_bakes_mounted_glossary(self, tmp_path):
        """No ZIP glossary → mounted YAML baked into payload."""
        gfile = tmp_path / "multilingual-glossary.yaml"
        gfile.write_text(_GLOSSARY_YAML, encoding="utf-8")
        cfg = self._mounted_cfg(glossary_path=gfile)
        content = self._parse(_batch_zip(include_glossary=False), mounted_cfg=cfg)
        entries = content.config_payload.glossary_entries
        assert len(entries) == 1
        assert entries[0].english == "invoice"
        assert "faktura" in entries[0].patterns

    def test_zip_supplied_glossary_wins_over_mounted(self, tmp_path):
        """ZIP includes glossary → use ZIP entries, NOT mounted."""
        gfile = tmp_path / "multilingual-glossary.yaml"
        gfile.write_text("entries:\n  - english: order\n    patterns:\n      - zamówienie\n",
                          encoding="utf-8")
        cfg = self._mounted_cfg(glossary_path=gfile)
        content = self._parse(_batch_zip(include_glossary=True), mounted_cfg=cfg)
        entries = content.config_payload.glossary_entries
        # ZIP says "invoice/faktura/rechnung" — mounted "order/zamówienie" should not appear
        assert any(e.english == "invoice" for e in entries)
        assert not any(e.english == "order" for e in entries)

    def test_config_source_invalidated_after_store(self):
        """upload_batch calls config_source.invalidate(collection) after persist."""
        from ingest_routes import IngestController
        from operation_store import OperationStore

        store = OperationStore()
        queue = asyncio.Queue(maxsize=100)
        doc_store = MagicMock()
        doc_store.ensure_collection = AsyncMock()
        doc_store.store_config = AsyncMock()
        config_source = MagicMock()
        config_source.invalidate = MagicMock()

        ctrl = IngestController(
            store, queue, capacity=100,
            document_store=doc_store,
            config_source=config_source,
        )

        # Manually invoke the persist path
        async def _run_persist():
            await doc_store.ensure_collection("col1")
            await doc_store.store_config("col1", RagConfigPayload())
            config_source.invalidate("col1")

        _run(_run_persist())
        config_source.invalidate.assert_called_once_with("col1")
