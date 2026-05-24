"""Factory function that builds the default :class:`PipelinedEmbedder` for a given config.

The stack mirrors the .NET ``AddOnnxEmbedderPipeline`` method:
  1. :class:`GlossaryExpansionPreprocessor` — multilingual query expansion (query-only)
  2. :class:`LengthTruncationPreprocessor`  — truncate to 512 words

Import this module from :class:`QueryEngine` and :class:`IngestWorker` as the
composition root for the embedding pipeline.  Tests can bypass it by injecting
a fake :class:`Embedder` directly.
"""
from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from common import Config
    from embedder import PipelinedEmbedder


def make_embedder(cfg: "Config") -> "PipelinedEmbedder":
    """Build and return a :class:`PipelinedEmbedder` for *cfg*.

    The returned embedder is cheap to construct — the underlying
    ``SentenceTransformer`` model is loaded lazily on the first embed call.
    """
    from common import _load_multilingual_glossary
    from sentence_transformer_embedder import SentenceTransformerEmbedder
    from embedder import PipelinedEmbedder
    from preprocessors import GlossaryExpansionPreprocessor, LengthTruncationPreprocessor

    inner = SentenceTransformerEmbedder(cfg.embedder_model, device=cfg.embedder_device)
    glossary = _load_multilingual_glossary(cfg.glossary_path)
    return PipelinedEmbedder(
        inner,
        preprocessors=[
            GlossaryExpansionPreprocessor(glossary),
            LengthTruncationPreprocessor(max_words=512),
        ],
    )
