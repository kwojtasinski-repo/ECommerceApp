"""Tests for ``storage.document_store`` — config point persistence."""
from __future__ import annotations

import json
from unittest.mock import MagicMock

import pytest

from config.payload import (
    GlossaryEntry,
    RagConfigPayload,
    WeightEntry,
)
from storage.document_store import (
    CONFIG_DOC_KIND,
    CONFIG_POINT_ID,
    DocumentStore,
    _parse_payload,
)


# ── Construction ────────────────────────────────────────────────────────────

class TestConstruction:
    def test_rejects_non_positive_vector_size(self) -> None:
        with pytest.raises(ValueError):
            DocumentStore(MagicMock(), 0)
        with pytest.raises(ValueError):
            DocumentStore(MagicMock(), -1)


# ── store_config ────────────────────────────────────────────────────────────

class TestStoreConfig:
    @pytest.mark.asyncio
    async def test_upserts_point_with_zero_vector_and_correct_payload(self) -> None:
        client = MagicMock()
        store = DocumentStore(client, vector_size=4)
        payload = RagConfigPayload(
            max_tokens=256,
            overlap_tokens=32,
            weights=(WeightEntry("docs/**", 1.5),),
            history_field="adr_id",
        )

        await store.store_config("my_col", payload)

        client.upsert.assert_called_once()
        call = client.upsert.call_args
        assert call.kwargs.get("collection_name") == "my_col"
        points = call.kwargs.get("points")
        assert len(points) == 1

        pt = points[0]
        assert pt.id == CONFIG_POINT_ID == 0
        assert pt.vector == [0.0, 0.0, 0.0, 0.0]
        assert pt.payload["doc_kind"] == CONFIG_DOC_KIND
        # payload_json round-trips back to the original payload.
        round_tripped = RagConfigPayload.from_dict(json.loads(pt.payload["payload_json"]))
        assert round_tripped == payload
        # ingested_at present and ISO-ish.
        assert "T" in pt.payload["ingested_at"]

    @pytest.mark.asyncio
    async def test_serialized_payload_is_deterministic(self) -> None:
        """sort_keys=True ensures Qdrant deduplication / diffing is meaningful."""
        client = MagicMock()
        store = DocumentStore(client, vector_size=2)
        payload = RagConfigPayload(max_tokens=10, history_field="x")

        await store.store_config("c", payload)
        await store.store_config("c", payload)

        json1 = client.upsert.call_args_list[0].kwargs["points"][0].payload["payload_json"]
        json2 = client.upsert.call_args_list[1].kwargs["points"][0].payload["payload_json"]
        assert json1 == json2


# ── fetch_config ────────────────────────────────────────────────────────────

class TestFetchConfig:
    def _make_store_with_point(self, payload_dict: dict) -> tuple[DocumentStore, MagicMock]:
        client = MagicMock()
        pt = MagicMock()
        pt.payload = payload_dict
        client.retrieve.return_value = [pt]
        return DocumentStore(client, vector_size=4), client

    @pytest.mark.asyncio
    async def test_returns_payload_when_point_exists(self) -> None:
        original = RagConfigPayload(max_tokens=42, history_field="rfc_id")
        store, client = self._make_store_with_point(
            {
                "doc_kind": CONFIG_DOC_KIND,
                "payload_json": json.dumps(original.to_dict()),
            }
        )
        result = await store.fetch_config("any")
        assert result == original
        # called with id=0 + with_payload=True
        kwargs = client.retrieve.call_args.kwargs
        assert kwargs["collection_name"] == "any"
        assert kwargs["ids"] == [CONFIG_POINT_ID]
        assert kwargs["with_payload"] is True

    @pytest.mark.asyncio
    async def test_returns_none_when_retrieve_returns_empty(self) -> None:
        client = MagicMock()
        client.retrieve.return_value = []
        store = DocumentStore(client, vector_size=4)
        assert await store.fetch_config("any") is None

    @pytest.mark.asyncio
    async def test_returns_none_when_retrieve_raises(self) -> None:
        client = MagicMock()
        client.retrieve.side_effect = RuntimeError("collection missing")
        store = DocumentStore(client, vector_size=4)
        assert await store.fetch_config("any") is None

    @pytest.mark.asyncio
    async def test_returns_none_when_payload_is_none(self) -> None:
        client = MagicMock()
        pt = MagicMock()
        pt.payload = None
        client.retrieve.return_value = [pt]
        store = DocumentStore(client, vector_size=4)
        assert await store.fetch_config("any") is None

    @pytest.mark.asyncio
    async def test_returns_none_when_doc_kind_mismatch(self) -> None:
        store, _ = self._make_store_with_point(
            {"doc_kind": "chunk", "payload_json": json.dumps({})}
        )
        assert await store.fetch_config("any") is None

    @pytest.mark.asyncio
    async def test_returns_none_when_payload_json_missing(self) -> None:
        store, _ = self._make_store_with_point({"doc_kind": CONFIG_DOC_KIND})
        assert await store.fetch_config("any") is None

    @pytest.mark.asyncio
    async def test_returns_none_when_payload_json_invalid(self) -> None:
        store, _ = self._make_store_with_point(
            {"doc_kind": CONFIG_DOC_KIND, "payload_json": "not-json"}
        )
        assert await store.fetch_config("any") is None


# ── End-to-end roundtrip ────────────────────────────────────────────────────

class TestRoundTrip:
    @pytest.mark.asyncio
    async def test_store_then_fetch_through_simulated_client(self) -> None:
        """Captures upsert → returns same point on retrieve. Ensures wire
        format is symmetric between writer and reader."""
        captured: list = []

        client = MagicMock()
        client.upsert.side_effect = lambda **kw: captured.extend(kw["points"])

        def _retrieve(**kw):
            wanted = kw.get("ids", [])
            return [p for p in captured if p.id in wanted]

        client.retrieve.side_effect = _retrieve

        store = DocumentStore(client, vector_size=3)
        original = RagConfigPayload(
            max_tokens=128,
            overlap_tokens=16,
            weights=(WeightEntry("docs/**", 1.2), WeightEntry(".github/**", 1.5)),
            glossary_entries=(GlossaryEntry("orders", ("zamówienia",)),),
            history_field="adr_id",
        )
        await store.store_config("proj_alpha", original)
        round_tripped = await store.fetch_config("proj_alpha")
        assert round_tripped == original


# ── _parse_payload helper ──────────────────────────────────────────────────

class TestParsePayload:
    def test_returns_none_for_wrong_doc_kind(self) -> None:
        assert _parse_payload({"doc_kind": "chunk"}) is None

    def test_returns_none_for_non_dict_json(self) -> None:
        assert _parse_payload({"doc_kind": CONFIG_DOC_KIND, "payload_json": "[1,2,3]"}) is None
