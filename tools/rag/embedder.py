"""Embedder Protocol, pre/post-processor Protocols, and PipelinedEmbedder."""
from __future__ import annotations

from typing import Protocol, runtime_checkable

from embed_context import EmbedContext, QUERY_CTX, INGEST_CTX


@runtime_checkable
class Embedder(Protocol):
    """Minimal embedding contract — any object with these members satisfies it."""

    @property
    def dimensions(self) -> int: ...

    def embed(self, text: str) -> list[float]: ...

    def embed_batch(self, texts: list[str]) -> list[list[float]]: ...


@runtime_checkable
class EmbedderPreprocessor(Protocol):
    """Transform text before it is embedded."""

    def process(self, text: str, ctx: EmbedContext) -> str: ...


@runtime_checkable
class EmbedderPostprocessor(Protocol):
    """Transform the embedding vector after it is produced."""

    def process(self, vector: list[float], ctx: EmbedContext) -> list[float]: ...


class PipelinedEmbedder:
    """Wraps an inner :class:`Embedder` with optional pre- and post-processing steps.

    Preprocessors run per-text in registration order before the inner embedder.
    Postprocessors run per-vector in registration order after the inner embedder.
    ``embed()`` defaults to :data:`QUERY_CTX`; ``embed_batch()`` defaults to
    :data:`INGEST_CTX` — matching the pattern used by the .NET implementation.
    """

    def __init__(
        self,
        inner: Embedder,
        preprocessors: list[EmbedderPreprocessor] = (),
        postprocessors: list[EmbedderPostprocessor] = (),
    ) -> None:
        self._inner = inner
        self._preprocessors: list[EmbedderPreprocessor] = list(preprocessors)
        self._postprocessors: list[EmbedderPostprocessor] = list(postprocessors)

    # ── Public interface ──────────────────────────────────────────────────────

    @property
    def dimensions(self) -> int:
        return self._inner.dimensions

    def embed(self, text: str, ctx: EmbedContext = QUERY_CTX) -> list[float]:
        """Embed a single text (default context: QUERY)."""
        text = self._preprocess(text, ctx)
        vec = self._inner.embed(text)
        return self._postprocess(vec, ctx)

    def embed_batch(
        self,
        texts: list[str],
        ctx: EmbedContext = INGEST_CTX,
    ) -> list[list[float]]:
        """Embed a batch of texts (default context: INGEST).

        Preprocessors run per-text; the inner embedder receives the full
        preprocessed batch for efficient batch encoding; postprocessors run
        per-vector.
        """
        processed = [self._preprocess(t, ctx) for t in texts]
        vectors = self._inner.embed_batch(processed)
        return [self._postprocess(v, ctx) for v in vectors]

    # ── Private helpers ───────────────────────────────────────────────────────

    def _preprocess(self, text: str, ctx: EmbedContext) -> str:
        for pre in self._preprocessors:
            text = pre.process(text, ctx)
        return text

    def _postprocess(self, vec: list[float], ctx: EmbedContext) -> list[float]:
        for post in self._postprocessors:
            vec = post.process(vec, ctx)
        return vec
