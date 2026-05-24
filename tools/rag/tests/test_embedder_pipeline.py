"""Unit tests for the Python embedding pipeline.

No sentence-transformers or Qdrant required — all tests use fakes.

Run:
    pytest tools/rag/tests/test_embedder_pipeline.py
"""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))

from embed_context import EmbedContext, EmbedPurpose, QUERY_CTX, INGEST_CTX
from embedder import PipelinedEmbedder, Embedder, EmbedderPreprocessor, EmbedderPostprocessor
from preprocessors import GlossaryExpansionPreprocessor, LengthTruncationPreprocessor


# ── Fakes ─────────────────────────────────────────────────────────────────────

class FakeEmbedder:
    """Returns a fixed-length list of zeros — no ML required."""

    dimensions = 4

    def embed(self, text: str) -> list[float]:
        return [0.0] * 4

    def embed_batch(self, texts: list[str]) -> list[list[float]]:
        return [[0.0] * 4 for _ in texts]


class CapturingPreprocessor:
    """Records every (text, ctx) pair it is called with."""

    def __init__(self, transform: str | None = None) -> None:
        self.calls: list[tuple[str, EmbedContext]] = []
        self._transform = transform

    def process(self, text: str, ctx: EmbedContext) -> str:
        self.calls.append((text, ctx))
        return self._transform if self._transform is not None else text


class AddingPostprocessor:
    """Adds a fixed value to every element of the vector."""

    def __init__(self, add: float) -> None:
        self._add = add

    def process(self, vec: list[float], ctx: EmbedContext) -> list[float]:
        return [v + self._add for v in vec]


class OrderMarker:
    """Appends its name to a shared list on each call — used to verify ordering."""

    def __init__(self, name: str, calls: list[str]) -> None:
        self._name = name
        self._calls = calls

    def process(self, text: str, ctx: EmbedContext) -> str:
        self._calls.append(self._name)
        return text


# ── EmbedContext ──────────────────────────────────────────────────────────────

class TestEmbedContext:
    def test_query_ctx_has_query_purpose(self):
        assert QUERY_CTX.purpose == EmbedPurpose.QUERY

    def test_ingest_ctx_has_ingest_purpose(self):
        assert INGEST_CTX.purpose == EmbedPurpose.INGEST

    def test_context_is_immutable(self):
        with pytest.raises((AttributeError, TypeError)):
            QUERY_CTX.purpose = EmbedPurpose.INGEST  # type: ignore[misc]


# ── PipelinedEmbedder ─────────────────────────────────────────────────────────

class TestPipelinedEmbedder:
    def test_no_processors_passthrough(self):
        embedder = PipelinedEmbedder(FakeEmbedder())
        assert embedder.embed("hello") == [0.0, 0.0, 0.0, 0.0]

    def test_embed_uses_query_ctx_by_default(self):
        pre = CapturingPreprocessor()
        embedder = PipelinedEmbedder(FakeEmbedder(), preprocessors=[pre])
        embedder.embed("hello")
        _, ctx = pre.calls[0]
        assert ctx.purpose == EmbedPurpose.QUERY

    def test_embed_accepts_explicit_ctx(self):
        pre = CapturingPreprocessor()
        embedder = PipelinedEmbedder(FakeEmbedder(), preprocessors=[pre])
        embedder.embed("hello", INGEST_CTX)
        _, ctx = pre.calls[0]
        assert ctx.purpose == EmbedPurpose.INGEST

    def test_embed_batch_uses_ingest_ctx_by_default(self):
        pre = CapturingPreprocessor()
        embedder = PipelinedEmbedder(FakeEmbedder(), preprocessors=[pre])
        embedder.embed_batch(["a", "b"])
        _, ctx = pre.calls[0]
        assert ctx.purpose == EmbedPurpose.INGEST

    def test_preprocessors_run_in_registration_order(self):
        calls: list[str] = []
        markers = [OrderMarker("A", calls), OrderMarker("B", calls), OrderMarker("C", calls)]
        embedder = PipelinedEmbedder(FakeEmbedder(), preprocessors=markers)
        embedder.embed("x")
        assert calls == ["A", "B", "C"]

    def test_postprocessors_applied_after_embed(self):
        post = AddingPostprocessor(1.0)
        embedder = PipelinedEmbedder(FakeEmbedder(), postprocessors=[post])
        result = embedder.embed("x")
        assert result == [1.0, 1.0, 1.0, 1.0]

    def test_embed_batch_preprocesses_each_text_individually(self):
        pre = CapturingPreprocessor()
        embedder = PipelinedEmbedder(FakeEmbedder(), preprocessors=[pre])
        embedder.embed_batch(["a", "b", "c"])
        assert [t for t, _ in pre.calls] == ["a", "b", "c"]

    def test_embed_batch_postprocesses_each_vector(self):
        post = AddingPostprocessor(2.0)
        embedder = PipelinedEmbedder(FakeEmbedder(), postprocessors=[post])
        results = embedder.embed_batch(["x", "y"])
        assert all(v == 2.0 for row in results for v in row)

    def test_dimensions_delegates_to_inner(self):
        embedder = PipelinedEmbedder(FakeEmbedder())
        assert embedder.dimensions == 4

    def test_protocol_satisfied_by_fake(self):
        # FakeEmbedder has no base class — the Protocol check ensures structural typing works.
        assert isinstance(FakeEmbedder(), Embedder)

    def test_preprocessor_protocol_satisfied_by_capturing(self):
        assert isinstance(CapturingPreprocessor(), EmbedderPreprocessor)

    def test_postprocessor_protocol_satisfied_by_adding(self):
        assert isinstance(AddingPostprocessor(0.0), EmbedderPostprocessor)


# ── GlossaryExpansionPreprocessor ─────────────────────────────────────────────

_GLOSSARY: list[tuple[str, list[str]]] = [
    ("order", ["zamówienie", "bestellung"]),
    ("payment", ["płatność", "zahlung"]),
]


class TestGlossaryExpansionPreprocessor:
    def test_expands_non_english_on_query(self):
        pre = GlossaryExpansionPreprocessor(_GLOSSARY)
        result = pre.process("zamówienie status", QUERY_CTX)
        assert "order" in result

    def test_skips_expansion_on_ingest(self):
        pre = GlossaryExpansionPreprocessor(_GLOSSARY)
        result = pre.process("zamówienie status", INGEST_CTX)
        assert result == "zamówienie status"

    def test_leaves_english_query_unchanged(self):
        pre = GlossaryExpansionPreprocessor(_GLOSSARY)
        result = pre.process("order status", QUERY_CTX)
        assert result == "order status"

    def test_matches_multiple_glossary_entries(self):
        pre = GlossaryExpansionPreprocessor(_GLOSSARY)
        result = pre.process("zamówienie płatność", QUERY_CTX)
        assert "order" in result
        assert "payment" in result

    def test_empty_glossary_returns_text_unchanged(self):
        pre = GlossaryExpansionPreprocessor([])
        result = pre.process("zamówienie", QUERY_CTX)
        assert result == "zamówienie"

    def test_repeat_controls_expansion_count(self):
        pre = GlossaryExpansionPreprocessor(_GLOSSARY, repeat=2)
        result = pre.process("zamówienie", QUERY_CTX)
        assert result.count("order") == 2


# ── LengthTruncationPreprocessor ──────────────────────────────────────────────

class TestLengthTruncationPreprocessor:
    def test_short_text_returned_unchanged(self):
        pre = LengthTruncationPreprocessor(max_words=5)
        assert pre.process("one two three", QUERY_CTX) == "one two three"

    def test_exactly_max_words_returned_unchanged(self):
        pre = LengthTruncationPreprocessor(max_words=3)
        assert pre.process("one two three", QUERY_CTX) == "one two three"

    def test_long_text_truncated_to_max_words(self):
        pre = LengthTruncationPreprocessor(max_words=3)
        result = pre.process("one two three four five", QUERY_CTX)
        assert result == "one two three"

    def test_applies_on_ingest_too(self):
        pre = LengthTruncationPreprocessor(max_words=2)
        result = pre.process("a b c d", INGEST_CTX)
        assert result == "a b"

    def test_empty_text_returned_unchanged(self):
        pre = LengthTruncationPreprocessor(max_words=5)
        assert pre.process("", QUERY_CTX) == ""
