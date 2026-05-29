"""Tests for ``config.bootstrap.build_config_source``.

The single mode-switch in the codebase. Every other module is mode-blind.
"""
from __future__ import annotations

import os
from types import SimpleNamespace
from unittest.mock import patch

import pytest

from config.bootstrap import build_config_source
from config.payload import RagConfigPayload
from config.sources import (
    CachingConfigSource,
    FileConfigSource,
    LayeredConfigSource,
    QdrantConfigSource,
)


def _cfg():
    return SimpleNamespace(
        chunker={"max_tokens": 100},
        ranking={},
        query_defaults={},
        raw={"metadata_rules": {}},
    )


class _NullStore:
    async def fetch_config(self, collection: str) -> RagConfigPayload | None:
        return None


class TestBuildConfigSource:
    def test_default_mode_is_file(self) -> None:
        src = build_config_source(_cfg(), store=None)
        assert isinstance(src, CachingConfigSource)
        assert isinstance(src._inner, FileConfigSource)

    def test_explicit_file_mode(self) -> None:
        src = build_config_source(_cfg(), store=None, mode="file")
        assert isinstance(src._inner, FileConfigSource)

    def test_qdrant_mode_with_store(self) -> None:
        src = build_config_source(_cfg(), store=_NullStore(), mode="qdrant")
        assert isinstance(src._inner, QdrantConfigSource)

    def test_layered_mode_with_store(self) -> None:
        src = build_config_source(_cfg(), store=_NullStore(), mode="layered")
        assert isinstance(src._inner, LayeredConfigSource)

    def test_qdrant_mode_without_store_degrades_to_file(self) -> None:
        """Safety net: never crash startup if store wiring is incomplete."""
        src = build_config_source(_cfg(), store=None, mode="qdrant")
        assert isinstance(src._inner, FileConfigSource)

    def test_layered_mode_without_store_degrades_to_file(self) -> None:
        src = build_config_source(_cfg(), store=None, mode="layered")
        assert isinstance(src._inner, FileConfigSource)

    def test_unknown_mode_falls_back_to_file(self) -> None:
        src = build_config_source(_cfg(), store=_NullStore(), mode="banana")
        assert isinstance(src._inner, FileConfigSource)

    def test_env_var_drives_mode(self) -> None:
        with patch.dict(os.environ, {"RAG_CONFIG_SOURCE": "layered"}, clear=False):
            src = build_config_source(_cfg(), store=_NullStore())
            assert isinstance(src._inner, LayeredConfigSource)

    def test_explicit_mode_overrides_env(self) -> None:
        with patch.dict(os.environ, {"RAG_CONFIG_SOURCE": "layered"}, clear=False):
            src = build_config_source(_cfg(), store=_NullStore(), mode="file")
            assert isinstance(src._inner, FileConfigSource)

    def test_caching_wraps_outermost(self) -> None:
        src = build_config_source(_cfg(), store=_NullStore(), mode="qdrant", ttl_seconds=42.0)
        assert isinstance(src, CachingConfigSource)
        assert src._ttl == 42.0

    @pytest.mark.asyncio
    async def test_returns_working_config_source(self) -> None:
        """End-to-end smoke: build → get_effective returns mounted defaults."""
        src = build_config_source(_cfg(), store=None)
        payload = await src.get_effective("any_collection")
        assert payload.max_tokens == 100
