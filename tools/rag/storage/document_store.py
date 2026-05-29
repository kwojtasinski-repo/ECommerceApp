"""Qdrant-backed storage facade for the per-collection config point.

Owns the wire format of the ``__config__`` point so callers depend only on
:class:`RagConfigPayload`. Mirrors .NET ``QdrantDocumentStore`` config methods.

Wire format (point id=0, doc_kind="__config__"):
    {
      "id": 0,
      "vector": [0.0, ...],            # zero vector — not indexable
      "payload": {
        "doc_kind": "__config__",
        "schema_version": 2,
        "payload_json": "<JSON-RagConfigPayload>",
        "ingested_at": "<ISO8601 UTC>"
      }
    }

The zero-vector keeps ``__config__`` invisible to similarity search but
retrievable by id. ``payload_json`` keeps the schema forward-compatible —
unknown fields survive a server downgrade.
"""
from __future__ import annotations

import json
from datetime import datetime, timezone
from typing import Any

from config.payload import RagConfigPayload, SCHEMA_VERSION

CONFIG_POINT_ID: int = 0
CONFIG_DOC_KIND: str = "__config__"


class DocumentStore:
    """Per-collection config persistence over a :class:`qdrant_client.QdrantClient`.

    The vector dimension is taken from the active embedder so the zero-vector
    matches the collection schema. The store does not create collections — that
    remains the ingest pipeline's job.
    """

    def __init__(self, client: Any, vector_size: int) -> None:
        self._client = client
        self._vector_size = int(vector_size)
        if self._vector_size <= 0:
            raise ValueError(f"vector_size must be positive, got {vector_size!r}")

    # ── Public API ────────────────────────────────────────────────────────

    async def fetch_config(self, collection: str) -> RagConfigPayload | None:
        """Return the persisted payload, or ``None`` if the collection has none."""
        raw = self._retrieve_config_payload(collection)
        if not raw:
            return None
        return _parse_payload(raw)

    async def store_config(self, collection: str, payload: RagConfigPayload) -> None:
        """Upsert the ``__config__`` point. Atomic from Qdrant's perspective."""
        point = self._build_point(payload)
        self._client.upsert(collection_name=collection, points=[point])

    # ── Internals (sync — Qdrant client is sync) ──────────────────────────

    def _retrieve_config_payload(self, collection: str) -> dict[str, Any] | None:
        try:
            pts = self._client.retrieve(
                collection_name=collection,
                ids=[CONFIG_POINT_ID],
                with_payload=True,
            )
        except Exception:
            # Missing collection / network error / no permission — treat as
            # "no config persisted". Callers fall back to mounted defaults.
            return None
        if not pts:
            return None
        payload = getattr(pts[0], "payload", None)
        if not payload:
            return None
        return dict(payload)

    def _build_point(self, payload: RagConfigPayload) -> Any:
        from qdrant_client.models import PointStruct  # local import keeps test imports cheap

        wire = {
            "doc_kind": CONFIG_DOC_KIND,
            "schema_version": payload.schema_version or SCHEMA_VERSION,
            "payload_json": json.dumps(payload.to_dict(), separators=(",", ":"), sort_keys=True),
            "ingested_at": datetime.now(timezone.utc).isoformat(),
        }
        return PointStruct(
            id=CONFIG_POINT_ID,
            vector=[0.0] * self._vector_size,
            payload=wire,
        )


def _parse_payload(raw: dict[str, Any]) -> RagConfigPayload | None:
    """Deserialize the wire payload. Returns ``None`` if the point isn't a config point."""
    if raw.get("doc_kind") != CONFIG_DOC_KIND:
        return None
    payload_json = raw.get("payload_json")
    if not payload_json:
        # Legacy / hand-written config point — treat as "no payload" so callers
        # fall back to mounted defaults cleanly.
        return None
    try:
        parsed = json.loads(payload_json)
    except (TypeError, ValueError):
        return None
    if not isinstance(parsed, dict):
        return None
    return RagConfigPayload.from_dict(parsed)
