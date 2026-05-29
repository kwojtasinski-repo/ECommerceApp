"""Per-collection config persistence layer (ADR-0028 Phase 3 Python mirror).

Public entry point is :func:`config.bootstrap.build_config_source`. Everything
downstream depends only on the :class:`IConfigSource` Protocol.
"""
from __future__ import annotations

from .bootstrap import (
    DEFAULT_MAX_COLLECTIONS,
    DEFAULT_TTL_SECONDS,
    ConfigSourceMode,
    build_config_source,
)
from .payload import (
    SCHEMA_VERSION,
    GlossaryEntry,
    RagConfigPayload,
    WeightEntry,
    merge_payloads,
)
from .sources import (
    CachingConfigSource,
    FileConfigSource,
    IConfigSource,
    LayeredConfigSource,
    QdrantConfigSource,
)

__all__ = [
    "ConfigSourceMode",
    "DEFAULT_MAX_COLLECTIONS",
    "DEFAULT_TTL_SECONDS",
    "SCHEMA_VERSION",
    "CachingConfigSource",
    "FileConfigSource",
    "GlossaryEntry",
    "IConfigSource",
    "LayeredConfigSource",
    "QdrantConfigSource",
    "RagConfigPayload",
    "WeightEntry",
    "build_config_source",
    "merge_payloads",
]
