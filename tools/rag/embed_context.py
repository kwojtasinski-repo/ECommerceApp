"""Embedding execution context — carries the purpose (query vs ingest) through the pipeline."""
from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class EmbedPurpose(Enum):
    QUERY  = "query"   # Single text embedded for similarity search.
    INGEST = "ingest"  # Batch of document chunks embedded during indexing.


@dataclass(frozen=True)
class EmbedContext:
    """Immutable context passed to every preprocessor and postprocessor in the pipeline."""

    purpose: EmbedPurpose


# Module-level singletons — callers import and use these directly.
QUERY_CTX  = EmbedContext(EmbedPurpose.QUERY)
INGEST_CTX = EmbedContext(EmbedPurpose.INGEST)
