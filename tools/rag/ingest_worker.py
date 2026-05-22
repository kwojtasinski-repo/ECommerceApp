"""Background ingest worker for the Python RAG server.

Mirrors the .NET IngestWorker (BackgroundService that drains an IngestChannel<T>).
Implementation uses a single asyncio.Task consuming from an asyncio.Queue — no threads
beyond the existing sentence-transformers thread used via asyncio.to_thread.

Job lifecycle:
    Queued  (OperationStore entry created by route handler)
        └─► Processing  (worker picks job from queue)
                ├─► Completed  (embed + upsert succeeded, chunk_count stored)
                └─► Failed     (any exception, error_message stored)

The process function:
    1. Chunks the provided Markdown content.
    2. Embeds all chunk texts via the shared QueryEngine model (runs in a thread).
    3. Deletes existing Qdrant points for the same rel_path (idempotent re-ingest).
    4. Upserts new PointStructs to Qdrant.
    5. Ensures the target collection exists (creates it on first use).
"""
from __future__ import annotations

import asyncio
import hashlib
import sys
from dataclasses import dataclass
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from query import QueryEngine
    from common import Config
    from operation_store import OperationStore


@dataclass
class IngestJob:
    operation_id: str
    collection: str
    rel_path: str
    content: str
    doc_kind: str | None


# Maximum number of pending jobs.  POST /ingest returns 503 when the queue is full.
DEFAULT_CAPACITY = 100


def _stable_chunk_id(rel_path: str, breadcrumb: str, start_line: int) -> int:
    """Deterministic uint64 point ID — same formula as the .NET _StableId helper."""
    raw = f"{rel_path}|{breadcrumb}|{start_line}".encode()
    h = hashlib.blake2b(raw, digest_size=8)
    return int.from_bytes(h.digest(), "big")


def _build_process_fn(engine: "QueryEngine", cfg: "Config", store: "OperationStore"):
    """Return an async callable that processes one IngestJob end-to-end."""
    from chunker import chunk_markdown
    from common import detect_doc_kind, detect_adr_id, resolve_weight

    def _file_doc_title(rel_path: str, content: str) -> str:
        for line in content.splitlines():
            s = line.strip()
            if s.startswith("# "):
                return s[2:].strip()
            if s and not s.startswith("#") and not s.startswith("---"):
                break
        return rel_path

    def _process_sync(job: IngestJob) -> int:
        """Run inside asyncio.to_thread — does all CPU/IO-bound work synchronously."""
        from qdrant_client.models import Distance, FieldCondition, Filter, MatchValue, PointStruct, VectorParams

        engine._ensure()
        client = engine._client
        model = engine._model

        # Ensure the collection exists (important for the first upload to a new project).
        try:
            client.get_collection(job.collection)
        except Exception:
            dim = model.get_sentence_embedding_dimension()
            client.create_collection(
                job.collection,
                vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
            )

        chunks = chunk_markdown(
            job.content,
            doc_title=_file_doc_title(job.rel_path, job.content),
            chunker_cfg=cfg.chunker,
        )
        if not chunks:
            return 0

        doc_title = _file_doc_title(job.rel_path, job.content)
        doc_kind = job.doc_kind or detect_doc_kind(job.rel_path, cfg)
        adr_id = detect_adr_id(job.rel_path, cfg)
        weight = resolve_weight(job.rel_path, len(job.content.encode()), cfg.ranking)

        texts = [c.text for c in chunks]
        vectors = model.encode(texts, normalize_embeddings=True)

        # Delete existing chunks for this path (idempotent re-ingest).
        client.delete(
            collection_name=job.collection,
            points_selector=Filter(
                must=[FieldCondition(key="rel_path", match=MatchValue(value=job.rel_path))]
            ),
        )

        # Build + upsert new points.
        points = []
        for chunk, vec in zip(chunks, vectors):
            point_id = _stable_chunk_id(job.rel_path, chunk.breadcrumb, chunk.start_line)
            points.append(
                PointStruct(
                    id=point_id,
                    vector=vec.tolist(),
                    payload={
                        "rel_path": job.rel_path,
                        "doc_title": doc_title,
                        "doc_kind": doc_kind,
                        "adr_id": adr_id,
                        "breadcrumb": chunk.breadcrumb,
                        "heading_path": chunk.heading_path,
                        "start_line": chunk.start_line,
                        "end_line": chunk.end_line,
                        "token_count": chunk.token_count,
                        "weight": weight,
                        "text": chunk.text,
                    },
                )
            )

        BATCH = 64
        for i in range(0, len(points), BATCH):
            client.upsert(collection_name=job.collection, points=points[i : i + BATCH])

        return len(points)

    async def process(job: IngestJob) -> None:
        await store.mark_processing(job.operation_id)
        try:
            chunk_count = await asyncio.to_thread(_process_sync, job)
            await store.mark_completed(job.operation_id, chunk_count)
        except Exception as exc:
            print(f"[ingest-worker] ERROR processing {job.rel_path}: {exc}", file=sys.stderr)
            await store.mark_failed(job.operation_id, str(exc))

    return process


class IngestWorker:
    """Single-consumer asyncio Task draining a bounded asyncio.Queue."""

    def __init__(self, queue: asyncio.Queue, process_fn) -> None:
        self._queue = queue
        self._process_fn = process_fn
        self._task: asyncio.Task | None = None

    def start(self) -> None:
        """Schedule the background consumer task on the running event loop."""
        self._task = asyncio.create_task(self._run(), name="ingest-worker")
        print("[ingest-worker] started", file=sys.stderr)

    async def stop(self) -> None:
        """Cancel and await the consumer task (called during server shutdown)."""
        if self._task and not self._task.done():
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
        print("[ingest-worker] stopped", file=sys.stderr)

    async def _run(self) -> None:
        while True:
            job: IngestJob = await self._queue.get()
            try:
                await self._process_fn(job)
            except Exception as exc:
                # process_fn is expected to handle its own exceptions, but guard here
                print(f"[ingest-worker] unhandled exception: {exc}", file=sys.stderr)
            finally:
                self._queue.task_done()
