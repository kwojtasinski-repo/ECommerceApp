"""Embedding execution context — carries the purpose (query vs ingest) through the pipeline."""
from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from config.payload import GlossaryEntry


class EmbedPurpose(Enum):
    QUERY  = "query"   # Single text embedded for similarity search.
    INGEST = "ingest"  # Batch of document chunks embedded during indexing.


@dataclass(frozen=True)
class EmbedContext:
    """Immutable context passed to every preprocessor and postprocessor in the pipeline.

    ``collection`` and ``glossary_entries`` are optional per-call overrides used
    by the multi-tenant HTTP path (ADR-0028 Phase 3). When ``glossary_entries``
    is ``None`` the preprocessor falls back to its constructor-supplied mounted
    glossary — preserving single-collection / STDIO behaviour.
    """

    purpose: EmbedPurpose
    collection: "str | None" = None
    glossary_entries: "tuple[GlossaryEntry, ...] | None" = None


# Module-level singletons — callers import and use these directly.
QUERY_CTX  = EmbedContext(EmbedPurpose.QUERY)
INGEST_CTX = EmbedContext(EmbedPurpose.INGEST)
