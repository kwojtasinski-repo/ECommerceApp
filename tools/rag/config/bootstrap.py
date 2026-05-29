"""Single mode-switch factory for :class:`IConfigSource`.

The *only* place in the codebase that branches on ``RAG_CONFIG_SOURCE``. Every
other module receives an already-wired :class:`IConfigSource` and is mode-blind.
"""
from __future__ import annotations

import os
from typing import Literal

from .sources import (
    CachingConfigSource,
    DocumentStoreProtocol,
    FileConfigSource,
    IConfigSource,
    LayeredConfigSource,
    QdrantConfigSource,
)

ConfigSourceMode = Literal["file", "qdrant", "layered"]

DEFAULT_TTL_SECONDS: float = 300.0
DEFAULT_MAX_COLLECTIONS: int = 64


def build_config_source(
    mounted_cfg: object,
    store: DocumentStoreProtocol | None,
    *,
    mode: str | None = None,
    ttl_seconds: float = DEFAULT_TTL_SECONDS,
    max_collections: int = DEFAULT_MAX_COLLECTIONS,
) -> IConfigSource:
    """Construct the active :class:`IConfigSource` for the process.

    Resolution: explicit ``mode`` arg wins, else ``RAG_CONFIG_SOURCE`` env var,
    else ``"file"``. Unknown modes silently fall back to ``"file"`` so a typo
    can't break server startup.
    """
    resolved = (mode or os.environ.get("RAG_CONFIG_SOURCE") or "file").strip().lower()

    file_src: IConfigSource = FileConfigSource(mounted_cfg)
    if resolved in ("qdrant", "layered") and store is None:
        # No store available (e.g. CLI / tests): degrade to file mode rather than crash.
        resolved = "file"

    inner: IConfigSource
    if resolved == "qdrant":
        assert store is not None  # narrowed above
        inner = QdrantConfigSource(store)
    elif resolved == "layered":
        assert store is not None
        inner = LayeredConfigSource(file_src, QdrantConfigSource(store))
    else:
        inner = file_src

    return CachingConfigSource(inner, ttl_seconds=ttl_seconds, max_collections=max_collections)
