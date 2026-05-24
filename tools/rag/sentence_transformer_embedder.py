"""Embedder implementation backed by sentence-transformers (ONNX / PyTorch).

The model is lazy-loaded on the first call so construction is always cheap.
"""
from __future__ import annotations


class SentenceTransformerEmbedder:
    """Wraps a ``SentenceTransformer`` model to satisfy the :class:`Embedder` Protocol.

    The underlying model is loaded on the first call to :meth:`embed` or
    :meth:`embed_batch`.  This keeps container startup fast and avoids downloading
    the model at import time.
    """

    def __init__(self, model_name: str, device: str = "cpu") -> None:
        self._model_name = model_name
        self._device = device
        self._model = None  # lazy

    # ── Embedder Protocol ────────────────────────────────────────────────────

    @property
    def dimensions(self) -> int:
        self._load()
        return self._model.get_sentence_embedding_dimension()

    def embed(self, text: str) -> list[float]:
        self._load()
        return self._model.encode([text], normalize_embeddings=True)[0].tolist()

    def embed_batch(self, texts: list[str]) -> list[list[float]]:
        self._load()
        return self._model.encode(texts, normalize_embeddings=True).tolist()

    # ── Internal ─────────────────────────────────────────────────────────────

    def _load(self) -> None:
        if self._model is None:
            from sentence_transformers import SentenceTransformer
            self._model = SentenceTransformer(self._model_name, device=self._device)
