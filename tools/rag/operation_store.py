"""In-memory operation store for async ingest operations.

Mirrors the .NET OperationStore (Phase 1 of ADR-0028):
  - Each POST /ingest/{collection} creates an IngestOperation entry.
  - The background worker transitions status: Queued → Processing → Completed | Failed.
  - Entries older than RETENTION_HOURS are pruned on every write.
  - Concurrency: asyncio.Lock protects the shared dict (single-threaded asyncio loop).
"""
from __future__ import annotations

import asyncio
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone, timedelta
from enum import Enum

RETENTION_HOURS = 1
_RETENTION = timedelta(hours=RETENTION_HOURS)


class IngestStatus(str, Enum):
    Queued = "Queued"
    Processing = "Processing"
    Completed = "Completed"
    Failed = "Failed"


@dataclass
class IngestOperation:
    operation_id: str
    collection: str
    rel_path: str
    status: IngestStatus = IngestStatus.Queued
    enqueued_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    started_at: datetime | None = None
    completed_at: datetime | None = None
    chunk_count: int = 0
    doc_kind: str = ""
    error_message: str | None = None

    def to_dict(self) -> dict:
        d: dict = {
            "operationId": self.operation_id,
            "status": self.status.value,
            "collection": self.collection,
            "relPath": self.rel_path,
            "enqueuedAt": self.enqueued_at.isoformat(),
            "startedAt": self.started_at.isoformat() if self.started_at else None,
            "completedAt": self.completed_at.isoformat() if self.completed_at else None,
            "errorMessage": self.error_message,
        }
        if self.status == IngestStatus.Completed:
            d["manifest"] = {
                "indexedChunks": self.chunk_count,
                "docKind": self.doc_kind,
            }
        return d


class OperationStore:
    """Thread-safe (asyncio) in-memory store for IngestOperation records."""

    def __init__(self) -> None:
        self._ops: dict[str, IngestOperation] = {}
        self._lock = asyncio.Lock()

    async def enqueue(self, collection: str, rel_path: str) -> IngestOperation:
        """Create a new Queued operation and return it."""
        op = IngestOperation(
            operation_id=str(uuid.uuid4()),
            collection=collection,
            rel_path=rel_path,
        )
        async with self._lock:
            self._purge_locked()
            self._ops[op.operation_id] = op
        return op

    async def mark_processing(self, operation_id: str) -> None:
        async with self._lock:
            if op := self._ops.get(operation_id):
                op.status = IngestStatus.Processing
                op.started_at = datetime.now(timezone.utc)

    async def mark_completed(self, operation_id: str, chunk_count: int, doc_kind: str = "") -> None:
        async with self._lock:
            if op := self._ops.get(operation_id):
                op.status = IngestStatus.Completed
                op.completed_at = datetime.now(timezone.utc)
                op.chunk_count = chunk_count
                op.doc_kind = doc_kind

    async def mark_failed(self, operation_id: str, error: str) -> None:
        async with self._lock:
            if op := self._ops.get(operation_id):
                op.status = IngestStatus.Failed
                op.completed_at = datetime.now(timezone.utc)
                op.error_message = error

    async def get(self, operation_id: str) -> IngestOperation | None:
        async with self._lock:
            return self._ops.get(operation_id)

    async def list_for_collection(self, collection: str) -> list[IngestOperation]:
        async with self._lock:
            return [op for op in self._ops.values() if op.collection == collection]

    def queue_depth(self) -> int:
        """Number of operations currently in Queued state (no lock — best-effort read)."""
        return sum(1 for op in self._ops.values() if op.status == IngestStatus.Queued)

    def total_count(self) -> int:
        """Total operations in store (no lock — best-effort read)."""
        return len(self._ops)

    def _purge_locked(self) -> None:
        """Remove operations older than RETENTION_HOURS. Must be called inside lock."""
        cutoff = datetime.now(timezone.utc) - _RETENTION
        expired = [k for k, v in self._ops.items() if v.enqueued_at < cutoff]
        for k in expired:
            del self._ops[k]
