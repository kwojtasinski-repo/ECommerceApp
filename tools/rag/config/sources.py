"""Per-collection config source abstraction.

ADR-0028 Phase 3 Python mirror. The only place that knows about mode switching
is :mod:`config.bootstrap` — every consumer accepts an :class:`IConfigSource`
and asks ``await source.get_effective(collection)``.
"""
from __future__ import annotations

import asyncio
import time
from typing import Protocol, runtime_checkable

from .payload import RagConfigPayload, merge_payloads


@runtime_checkable
class IConfigSource(Protocol):
    """Minimal surface used by every per-collection config reader."""

    async def get_effective(self, collection: str) -> RagConfigPayload: ...

    def invalidate(self, collection: str) -> None: ...


class FileConfigSource:
    """Returns the mounted-YAML default for every collection.

    Used as the default in tests, CLI ingest, and production when
    ``RAG_CONFIG_SOURCE=file`` (the safe default that keeps existing behavior).
    """

    def __init__(self, mounted_cfg: object) -> None:
        # Materialize the payload eagerly — the mounted config is process-wide
        # and immutable for the lifetime of the server.
        self._payload = RagConfigPayload.from_mounted(mounted_cfg)

    async def get_effective(self, collection: str) -> RagConfigPayload:
        return self._payload

    def invalidate(self, collection: str) -> None:
        # File-mode payload is process-lifetime constant; nothing to invalidate.
        return None


class QdrantConfigSource:
    """Reads the ``__config__`` point (id=0) per collection. No file fallback.

    Returns an empty :class:`RagConfigPayload` (all-defaults) when the
    collection has no config point yet — callers fall back to the mounted
    default via the ``None`` sentinel on each field.
    """

    def __init__(self, store: "DocumentStoreProtocol") -> None:
        self._store = store

    async def get_effective(self, collection: str) -> RagConfigPayload:
        stored = await self._store.fetch_config(collection)
        return stored if stored is not None else RagConfigPayload()

    def invalidate(self, collection: str) -> None:
        return None


class LayeredConfigSource:
    """Qdrant-stored payload overrides mounted defaults (override-wins per-field).

    Used when ``RAG_CONFIG_SOURCE=layered``. The mounted file provides safe
    defaults; per-collection writes from ingest override individual fields.
    """

    def __init__(self, file_source: IConfigSource, qdrant_source: IConfigSource) -> None:
        self._file = file_source
        self._qdrant = qdrant_source

    async def get_effective(self, collection: str) -> RagConfigPayload:
        base = await self._file.get_effective(collection)
        override = await self._qdrant.get_effective(collection)
        return merge_payloads(base, override)

    def invalidate(self, collection: str) -> None:
        self._file.invalidate(collection)
        self._qdrant.invalidate(collection)


class CachingConfigSource:
    """TTL cache wrapping any inner :class:`IConfigSource`.

    In-process, per-collection key. ``ttl_seconds`` controls the absolute
    expiration; ``max_collections`` bounds memory in multi-tenant deployments.
    Invalidation is explicit (called by ingest after persisting a new payload).
    """

    def __init__(
        self,
        inner: IConfigSource,
        ttl_seconds: float = 300.0,
        max_collections: int = 64,
    ) -> None:
        self._inner = inner
        self._ttl = float(ttl_seconds)
        self._max = int(max_collections)
        self._cache: dict[str, tuple[float, RagConfigPayload]] = {}
        self._lock = asyncio.Lock()

    async def get_effective(self, collection: str) -> RagConfigPayload:
        now = time.monotonic()
        entry = self._cache.get(collection)
        if entry is not None and (now - entry[0]) < self._ttl:
            return entry[1]
        async with self._lock:
            # Re-check under lock to avoid duplicate inner fetches on cache stampede.
            entry = self._cache.get(collection)
            if entry is not None and (time.monotonic() - entry[0]) < self._ttl:
                return entry[1]
            payload = await self._inner.get_effective(collection)
            self._cache[collection] = (time.monotonic(), payload)
            if len(self._cache) > self._max:
                # Evict the oldest entry. O(n) over a small dict (max_collections defaults to 64).
                oldest = min(self._cache.items(), key=lambda kv: kv[1][0])[0]
                self._cache.pop(oldest, None)
            return payload

    def invalidate(self, collection: str) -> None:
        self._cache.pop(collection, None)
        self._inner.invalidate(collection)


class DocumentStoreProtocol(Protocol):
    """Subset of :class:`storage.document_store.DocumentStore` needed by config sources.

    Declared here (not imported) to keep ``config`` package free of any
    Qdrant dependency at import time.
    """

    async def fetch_config(self, collection: str) -> RagConfigPayload | None: ...
