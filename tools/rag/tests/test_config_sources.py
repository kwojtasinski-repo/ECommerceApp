"""Tests for ``config.sources`` — IConfigSource implementations."""
from __future__ import annotations

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

from config.payload import RagConfigPayload, WeightEntry
from config.sources import (
    CachingConfigSource,
    FileConfigSource,
    IConfigSource,
    LayeredConfigSource,
    QdrantConfigSource,
)


# ── FileConfigSource ────────────────────────────────────────────────────────

class TestFileConfigSource:
    def _cfg(self):
        return SimpleNamespace(
            chunker={"max_tokens": 512, "overlap_tokens": 64},
            ranking={"weights": []},
            query_defaults={"history_field": "adr_id"},
            raw={"metadata_rules": {}},
        )

    @pytest.mark.asyncio
    async def test_returns_mounted_payload_for_any_collection(self) -> None:
        src = FileConfigSource(self._cfg())
        a = await src.get_effective("col_a")
        b = await src.get_effective("col_b")
        assert a is b  # eagerly materialized → same instance every call
        assert a.max_tokens == 512
        assert a.history_field == "adr_id"

    def test_invalidate_is_noop(self) -> None:
        src = FileConfigSource(self._cfg())
        src.invalidate("anything")  # must not raise

    def test_satisfies_iconfig_source_protocol(self) -> None:
        src = FileConfigSource(self._cfg())
        assert isinstance(src, IConfigSource)


# ── QdrantConfigSource ──────────────────────────────────────────────────────

class _FakeStore:
    def __init__(self, payload: RagConfigPayload | None = None, *, raises: Exception | None = None):
        self.payload = payload
        self.raises = raises
        self.calls: list[str] = []

    async def fetch_config(self, collection: str) -> RagConfigPayload | None:
        self.calls.append(collection)
        if self.raises is not None:
            raise self.raises
        return self.payload


class TestQdrantConfigSource:
    @pytest.mark.asyncio
    async def test_returns_stored_payload(self) -> None:
        stored = RagConfigPayload(max_tokens=123, history_field="rfc_id")
        src = QdrantConfigSource(_FakeStore(stored))
        result = await src.get_effective("project_x")
        assert result == stored

    @pytest.mark.asyncio
    async def test_returns_default_when_store_returns_none(self) -> None:
        src = QdrantConfigSource(_FakeStore(None))
        result = await src.get_effective("project_x")
        assert result == RagConfigPayload()  # all-None / empty sentinel

    @pytest.mark.asyncio
    async def test_passes_collection_through_to_store(self) -> None:
        store = _FakeStore(None)
        src = QdrantConfigSource(store)
        await src.get_effective("col_a")
        await src.get_effective("col_b")
        assert store.calls == ["col_a", "col_b"]


# ── LayeredConfigSource ─────────────────────────────────────────────────────

class _StubSource:
    def __init__(self, payload: RagConfigPayload):
        self.payload = payload
        self.invalidated: list[str] = []

    async def get_effective(self, collection: str) -> RagConfigPayload:
        return self.payload

    def invalidate(self, collection: str) -> None:
        self.invalidated.append(collection)


class TestLayeredConfigSource:
    @pytest.mark.asyncio
    async def test_qdrant_overrides_file(self) -> None:
        file = _StubSource(RagConfigPayload(max_tokens=10, history_field="adr_id"))
        qdrant = _StubSource(RagConfigPayload(max_tokens=99))  # only max_tokens overridden
        merged = await LayeredConfigSource(file, qdrant).get_effective("col")
        assert merged.max_tokens == 99
        assert merged.history_field == "adr_id"  # base wins where override is None

    @pytest.mark.asyncio
    async def test_empty_qdrant_payload_is_pure_fallback(self) -> None:
        file = _StubSource(RagConfigPayload(max_tokens=10, weights=(WeightEntry("a", 1.0),)))
        qdrant = _StubSource(RagConfigPayload())  # all-None
        merged = await LayeredConfigSource(file, qdrant).get_effective("col")
        assert merged.max_tokens == 10
        assert merged.weights == (WeightEntry("a", 1.0),)

    def test_invalidate_propagates_to_both(self) -> None:
        file = _StubSource(RagConfigPayload())
        qdrant = _StubSource(RagConfigPayload())
        LayeredConfigSource(file, qdrant).invalidate("col_x")
        assert file.invalidated == ["col_x"]
        assert qdrant.invalidated == ["col_x"]


# ── CachingConfigSource ─────────────────────────────────────────────────────

class _CountingSource:
    def __init__(self, payload_by_collection: dict[str, RagConfigPayload]):
        self._by_col = dict(payload_by_collection)
        self.calls: list[str] = []
        self.invalidated: list[str] = []

    async def get_effective(self, collection: str) -> RagConfigPayload:
        self.calls.append(collection)
        return self._by_col.get(collection, RagConfigPayload())

    def invalidate(self, collection: str) -> None:
        self.invalidated.append(collection)


class TestCachingConfigSource:
    @pytest.mark.asyncio
    async def test_caches_subsequent_lookups(self) -> None:
        inner = _CountingSource({"a": RagConfigPayload(max_tokens=1)})
        src = CachingConfigSource(inner, ttl_seconds=300)
        await src.get_effective("a")
        await src.get_effective("a")
        await src.get_effective("a")
        assert inner.calls == ["a"]  # only one inner call

    @pytest.mark.asyncio
    async def test_separate_collections_cached_independently(self) -> None:
        inner = _CountingSource(
            {"a": RagConfigPayload(max_tokens=1), "b": RagConfigPayload(max_tokens=2)}
        )
        src = CachingConfigSource(inner, ttl_seconds=300)
        a1 = await src.get_effective("a")
        b1 = await src.get_effective("b")
        a2 = await src.get_effective("a")
        assert a1.max_tokens == 1
        assert b1.max_tokens == 2
        assert a2 is a1
        assert inner.calls == ["a", "b"]

    @pytest.mark.asyncio
    async def test_invalidate_forces_refresh(self) -> None:
        inner = _CountingSource({"a": RagConfigPayload(max_tokens=1)})
        src = CachingConfigSource(inner, ttl_seconds=300)
        await src.get_effective("a")
        src.invalidate("a")
        await src.get_effective("a")
        assert inner.calls == ["a", "a"]
        assert inner.invalidated == ["a"]  # also propagates downward

    @pytest.mark.asyncio
    async def test_ttl_expiry_refreshes(self) -> None:
        inner = _CountingSource({"a": RagConfigPayload(max_tokens=1)})
        src = CachingConfigSource(inner, ttl_seconds=0.01)
        await src.get_effective("a")
        await asyncio.sleep(0.02)
        await src.get_effective("a")
        assert len(inner.calls) == 2

    @pytest.mark.asyncio
    async def test_lru_eviction_when_over_capacity(self) -> None:
        inner = _CountingSource({})
        src = CachingConfigSource(inner, ttl_seconds=300, max_collections=2)
        await src.get_effective("a")
        await asyncio.sleep(0.001)  # ensure monotonic timestamps differ
        await src.get_effective("b")
        await asyncio.sleep(0.001)
        await src.get_effective("c")  # triggers eviction of "a" (oldest)
        await src.get_effective("a")  # cache miss, refetches
        assert inner.calls.count("a") == 2

    @pytest.mark.asyncio
    async def test_concurrent_first_lookup_only_calls_inner_once(self) -> None:
        """Stampede protection — many awaiters during the first miss must
        produce exactly one inner call."""
        slow_payload = RagConfigPayload(max_tokens=42)

        class _SlowInner:
            def __init__(self):
                self.calls = 0
                self.invalidated: list[str] = []

            async def get_effective(self, collection: str) -> RagConfigPayload:
                self.calls += 1
                await asyncio.sleep(0.05)
                return slow_payload

            def invalidate(self, collection: str) -> None:
                self.invalidated.append(collection)

        inner = _SlowInner()
        src = CachingConfigSource(inner, ttl_seconds=300)
        results = await asyncio.gather(*(src.get_effective("a") for _ in range(10)))
        assert all(r is slow_payload for r in results)
        assert inner.calls == 1
