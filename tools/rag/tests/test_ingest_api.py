"""Tests for the Python ingest pipeline: operation_store, ingest_worker, ingest_routes, api_key_middleware.

Strategy: no external services (no Qdrant, no model loading).
Tests use asyncio.run(), Starlette TestClient, and mocks to verify behavior
in isolation.

All JSON response fields use snake_case (project-wide convention).

Coverage:
  OperationStore         — enqueue, transitions, list, retention purge, to_dict shape
  IngestWorker           — happy path, failure path, empty-chunks path (bug #1), lifecycle
  IngestRoutes           — POST 202/400/413/415/503, GET 200/404, list, admin
  ApiKeyMiddleware       — 401 / pass-through
"""
from __future__ import annotations

import asyncio
import io
import json
import time
import uuid
import zipfile
from datetime import datetime, timezone, timedelta
from pathlib import Path
from typing import Any
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from starlette.applications import Starlette
from starlette.testclient import TestClient

import sys
sys.path.insert(0, str(Path(__file__).parent.parent))

from api_key_middleware import ApiKeyMiddleware
from ingest_routes import build_ingest_routes
from ingest_worker import DEFAULT_CAPACITY, IngestJob, IngestWorker, _stable_chunk_id
from operation_store import RETENTION_HOURS, IngestOperation, IngestStatus, OperationStore


# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

def _run(coro):
    """Run a coroutine inside a fresh event loop."""
    return asyncio.run(coro)


def _make_app(
    store: OperationStore,
    queue: asyncio.Queue,
    api_key: str | None = None,
    capacity: int = DEFAULT_CAPACITY,
) -> Starlette:
    routes = build_ingest_routes(store, queue, capacity=capacity)
    app = Starlette(routes=routes)
    if api_key:
        app.add_middleware(ApiKeyMiddleware, api_key=api_key)
    return app


def _make_zip(files: dict[str, str]) -> bytes:
    """Build an in-memory ZIP from {filename: content} mapping."""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        for name, content in files.items():
            zf.writestr(name, content)
    return buf.getvalue()


_MINIMAL_META = """\
doc_kind_rules:
  - glob: "**/*.md"
    kind: "other"
"""

_MINIMAL_RAG_CONFIG = """\
chunker: { max_tokens: 512 }
ranking: { weights: [ { pattern: "docs/**", weight: 1.0 } ] }
"""

_MINIMAL_RAG_CONFIG_INLINE = """\
chunker: { max_tokens: 512 }
ranking: { weights: [ { pattern: "docs/**", weight: 1.0 } ] }
metadata_rules:
  doc_kind_rules:
    - glob: "**/*.md"
      kind: "other"
named_queries:
  - name: "general"
    query: "documentation"
    doc_kind: "other"
"""

_MINIMAL_QUERIES = """\
named_queries:
  - name: "general"
    query: "documentation"
    doc_kind: "other"
"""

_MINIMAL_DOC = "# Test Document\n\nSome content here for testing.\n"


def _batch_zip(extra: dict[str, str] | None = None) -> bytes:
    """Minimal valid ZIP for batch ingest."""
    files = {
        "rag-config.yaml": _MINIMAL_RAG_CONFIG,
        "metadata-rules.yaml": _MINIMAL_META,
        "queries.yaml": _MINIMAL_QUERIES,
        "docs/test.md": _MINIMAL_DOC,
    }
    if extra:
        files.update(extra)
    return _make_zip(files)


# ─────────────────────────────────────────────────────────────────────────────
# OperationStore
# ─────────────────────────────────────────────────────────────────────────────

class TestOperationStoreEnqueue:
    def test_enqueue_returns_queued_operation(self):
        store = OperationStore()
        op = _run(store.enqueue("col-a", "docs/intro.md"))
        assert op.status == IngestStatus.Queued
        assert op.collection == "col-a"
        assert op.rel_path == "docs/intro.md"
        assert op.operation_id  # non-empty

    def test_enqueue_assigns_unique_ids(self):
        store = OperationStore()
        ids = {_run(store.enqueue("c", "f.md")).operation_id for _ in range(5)}
        assert len(ids) == 5

    def test_get_returns_none_for_unknown_id(self):
        store = OperationStore()
        assert _run(store.get(str(uuid.uuid4()))) is None

    def test_get_returns_enqueued_operation(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        retrieved = _run(store.get(op.operation_id))
        assert retrieved is not None
        assert retrieved.operation_id == op.operation_id


class TestOperationStoreTransitions:
    def test_mark_processing_updates_status_and_started_at(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        _run(store.mark_processing(op.operation_id))
        updated = _run(store.get(op.operation_id))
        assert updated.status == IngestStatus.Processing
        assert updated.started_at is not None

    def test_mark_completed_sets_chunk_count_and_doc_kind(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        _run(store.mark_completed(op.operation_id, chunk_count=7, doc_kind="adr"))
        updated = _run(store.get(op.operation_id))
        assert updated.status == IngestStatus.Completed
        assert updated.chunk_count == 7
        assert updated.doc_kind == "adr"
        assert updated.completed_at is not None

    def test_mark_failed_sets_error_message(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        _run(store.mark_failed(op.operation_id, error="boom"))
        updated = _run(store.get(op.operation_id))
        assert updated.status == IngestStatus.Failed
        assert updated.error_message == "boom"

    def test_mark_unknown_id_is_silent_noop(self):
        store = OperationStore()
        _run(store.mark_processing(str(uuid.uuid4())))  # must not raise


class TestOperationStoreListAndStats:
    def test_list_for_collection_returns_only_matching(self):
        store = OperationStore()
        _run(store.enqueue("col-a", "a.md"))
        _run(store.enqueue("col-b", "b.md"))
        _run(store.enqueue("col-a", "c.md"))
        ops = _run(store.list_for_collection("col-a"))
        assert len(ops) == 2
        assert all(op.collection == "col-a" for op in ops)

    def test_queue_depth_counts_only_queued(self):
        store = OperationStore()
        op1 = _run(store.enqueue("c", "a.md"))
        op2 = _run(store.enqueue("c", "b.md"))
        _run(store.mark_completed(op2.operation_id, 3))
        assert store.queue_depth() == 1

    def test_total_count_includes_all_statuses(self):
        store = OperationStore()
        for i in range(3):
            _run(store.enqueue("c", f"{i}.md"))
        assert store.total_count() == 3

    def test_retention_hours_constant_is_one(self):
        """RETENTION_HOURS must match /admin/stats (1 hour — mirrors .NET)."""
        assert RETENTION_HOURS == 1


class TestOperationStoreRetentionPurge:
    def test_old_operations_purged_on_next_enqueue(self):
        store = OperationStore()
        expired = IngestOperation(
            operation_id=str(uuid.uuid4()),
            collection="c",
            rel_path="old.md",
            enqueued_at=datetime.now(timezone.utc) - timedelta(hours=2),
        )
        store._ops[expired.operation_id] = expired
        _run(store.enqueue("c", "new.md"))
        assert _run(store.get(expired.operation_id)) is None

    def test_recent_operations_survive_purge(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        _run(store.enqueue("c", "g.md"))
        assert _run(store.get(op.operation_id)) is not None


# ─────────────────────────────────────────────────────────────────────────────
# IngestOperation.to_dict — snake_case contract
# ─────────────────────────────────────────────────────────────────────────────

class TestIngestOperationToDict:
    """to_dict() must return snake_case keys — project-wide API convention."""

    def test_to_dict_uses_snake_case_keys(self):
        store = OperationStore()
        op = _run(store.enqueue("my-col", "docs/adr/0001.md"))
        d = op.to_dict()
        assert "operation_id" in d
        assert "rel_path" in d
        assert "enqueued_at" in d
        assert "error_message" in d
        # camelCase must NOT appear
        assert "operationId" not in d
        assert "relPath" not in d

    def test_to_dict_completed_manifest_uses_snake_case(self):
        store = OperationStore()
        op = _run(store.enqueue("my-col", "docs/adr/0001.md"))
        _run(store.mark_completed(op.operation_id, chunk_count=12, doc_kind="adr_main"))
        d = _run(store.get(op.operation_id)).to_dict()
        assert "manifest" in d
        assert d["manifest"]["indexed_chunks"] == 12
        assert d["manifest"]["doc_kind"] == "adr_main"

    def test_to_dict_non_completed_has_no_manifest(self):
        store = OperationStore()
        op = _run(store.enqueue("my-col", "f.md"))
        assert "manifest" not in op.to_dict()

    def test_to_dict_status_is_string(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        assert op.to_dict()["status"] == "Queued"


# ─────────────────────────────────────────────────────────────────────────────
# IngestWorker
# ─────────────────────────────────────────────────────────────────────────────

class TestIngestWorker:
    def test_worker_calls_process_fn_and_stops(self):
        queue: asyncio.Queue = asyncio.Queue()
        called_with = []

        async def fake_process(job):
            called_with.append(job)

        async def run():
            worker = IngestWorker(queue, fake_process)
            worker.start()
            job = IngestJob("op-1", "col", "a.md", "# Hi", "other")
            await queue.put(job)
            await asyncio.sleep(0.05)
            await worker.stop()

        asyncio.run(run())
        assert len(called_with) == 1
        assert called_with[0].operation_id == "op-1"

    def test_worker_catches_process_fn_exception_without_crashing(self):
        queue: asyncio.Queue = asyncio.Queue()

        async def failing_process(job):
            raise RuntimeError("intentional test failure")

        async def run():
            worker = IngestWorker(queue, failing_process)
            worker.start()
            await queue.put(IngestJob("op-2", "col", "b.md", "content", "other"))
            await asyncio.sleep(0.05)
            await worker.stop()

        asyncio.run(run())  # must not raise

    def test_worker_stop_before_start_is_safe(self):
        queue: asyncio.Queue = asyncio.Queue()
        worker = IngestWorker(queue, AsyncMock())
        asyncio.run(worker.stop())  # must not raise


class TestStableChunkId:
    def test_same_inputs_produce_same_id(self):
        a = _stable_chunk_id("docs/adr.md", "ADR > Decision", 42)
        b = _stable_chunk_id("docs/adr.md", "ADR > Decision", 42)
        assert a == b

    def test_different_paths_produce_different_ids(self):
        a = _stable_chunk_id("docs/a.md", "Section", 1)
        b = _stable_chunk_id("docs/b.md", "Section", 1)
        assert a != b

    def test_returns_non_negative_integer(self):
        result = _stable_chunk_id("docs/a.md", "X", 0)
        assert isinstance(result, int)
        assert result >= 0


# ─────────────────────────────────────────────────────────────────────────────
# Bug #1 regression: _process_sync must not raise NameError on empty-chunk path
# ─────────────────────────────────────────────────────────────────────────────

class TestProcessSyncEmptyChunks:
    """When chunk_markdown returns [], _process_sync must return (0, kind) cleanly."""

    def test_empty_chunks_returns_zero_without_name_error(self):
        """Regression for: doc_kind used before assignment when chunks == []."""
        from unittest.mock import patch
        from common import Config

        raw_cfg = {
            "source": {"roots": ["docs"], "exclude_globs": []},
            "embedder": {"model": "test-model", "device": "cpu", "dimensions": 4, "batch_size": 4},
            "chunker": {"max_tokens": 800, "min_tokens": 10, "overlap_tokens": 80, "split_on_headings": [1, 2, 3]},
            "vector_store": {"backend": "qdrant", "mode": "docker", "collection": "test", "url": "http://localhost:6333"},
            "ranking": {"weights": []},
            "query": {"default_top_k": 5, "fetch_k": 20, "score_threshold": 0.3},
            "storage": {"manifest_path": ".rag/manifest.json", "snapshot_path": ".rag/snapshot.qdrant"},
            "metadata_rules": {"doc_kind_rules": [{"glob": "**/*.md", "kind": "other"}]},
        }
        cfg = Config(raw=raw_cfg)
        store = OperationStore()

        fake_engine = MagicMock()
        fake_engine._ensure = MagicMock()
        fake_engine._client = MagicMock()
        fake_engine.embedder = MagicMock()
        fake_engine.embedder.dimensions = 4
        fake_engine.embedder.embed_batch.return_value = []

        # Patch chunk_markdown to return an empty list (simulates below-min-token doc).
        with patch("ingest_worker.chunk_markdown", return_value=[]):
            from ingest_worker import _build_process_fn
            process_fn = _build_process_fn(fake_engine, cfg, store)

        op = _run(store.enqueue("col", "tiny.md"))
        job = IngestJob(
            operation_id=op.operation_id,
            collection="col",
            rel_path="docs/tiny.md",
            content="# T\n\nX",
            doc_kind="other",
            adr_id=None,
        )

        # Should complete as (0, "other") without NameError
        with patch("ingest_worker.chunk_markdown", return_value=[]):
            asyncio.run(process_fn(job))

        completed = _run(store.get(op.operation_id))
        assert completed.status == IngestStatus.Completed
        assert completed.chunk_count == 0

    def test_doc_kind_pre_computed_before_chunk_call(self):
        """doc_kind must be resolved from the job BEFORE chunk_markdown is called."""
        from ingest_worker import _build_process_fn
        from common import Config

        call_order: list[str] = []

        raw_cfg = {
            "source": {"roots": ["docs"], "exclude_globs": []},
            "embedder": {"model": "x", "device": "cpu", "dimensions": 4, "batch_size": 4},
            "chunker": {"max_tokens": 800, "min_tokens": 1, "overlap_tokens": 0, "split_on_headings": [1, 2, 3]},
            "vector_store": {"backend": "qdrant", "mode": "docker", "collection": "test", "url": "http://localhost:6333"},
            "ranking": {"weights": []},
            "query": {"default_top_k": 5, "fetch_k": 20, "score_threshold": 0.0},
            "storage": {"manifest_path": ".rag/m.json", "snapshot_path": ".rag/s.qdrant"},
            "metadata_rules": {"doc_kind_rules": [{"glob": "**/*.md", "kind": "other"}]},
        }
        cfg = Config(raw=raw_cfg)
        store = OperationStore()

        fake_engine = MagicMock()
        fake_engine._ensure = MagicMock()
        fake_engine._client = MagicMock()
        fake_engine._client.get_collection.side_effect = Exception("not found")
        fake_engine.embedder = MagicMock()
        fake_engine.embedder.dimensions = 4
        fake_engine.embedder.embed_batch.return_value = []

        original_chunk = None

        def chunk_spy(*args, **kwargs):
            call_order.append("chunk_markdown_called")
            return []  # empty → triggers early return

        with patch("ingest_worker.chunk_markdown", side_effect=chunk_spy):
            process_fn = _build_process_fn(fake_engine, cfg, store)

        op = _run(store.enqueue("col", "tiny.md"))
        job = IngestJob(
            operation_id=op.operation_id,
            collection="col",
            rel_path="docs/tiny.md",
            content="# Title\n\nBody",
            doc_kind="adr_main",
            adr_id="0001",
        )
        with patch("ingest_worker.chunk_markdown", side_effect=chunk_spy):
            asyncio.run(process_fn(job))

        # Process completed without error
        result = _run(store.get(op.operation_id))
        assert result.status == IngestStatus.Completed


# ─────────────────────────────────────────────────────────────────────────────
# Ingest routes — HTTP API (snake_case response contract)
# ─────────────────────────────────────────────────────────────────────────────

class TestBatchIngestRoute:
    def _client_and_store(self, capacity=DEFAULT_CAPACITY):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=capacity)
        app = _make_app(store, queue, capacity=capacity)
        return TestClient(app, raise_server_exceptions=False), store, queue

    def test_post_returns_202_with_valid_zip(self):
        client, _, _ = self._client_and_store()
        r = client.post(
            "/ingest/my-col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202

    def test_post_body_has_snake_case_keys(self):
        client, _, _ = self._client_and_store()
        r = client.post(
            "/ingest/my-col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        body = r.json()
        # snake_case at root
        assert "batch_id" in body
        assert "count" in body
        assert "operations" in body
        # camelCase must NOT appear
        assert "batchId" not in body

    def test_post_operations_have_snake_case_keys(self):
        client, _, _ = self._client_and_store()
        r = client.post(
            "/ingest/my-col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        op = r.json()["operations"][0]
        assert "operation_id" in op
        assert "rel_path" in op
        assert "status_url" in op
        # camelCase must NOT appear
        assert "operationId" not in op
        assert "relPath" not in op

    def test_post_count_matches_file_count(self):
        client, _, _ = self._client_and_store()
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": "# A\n\nContent A.",
            "docs/b.md": "# B\n\nContent B.",
        })
        r = client.post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202
        body = r.json()
        assert body["count"] == 2
        assert len(body["operations"]) == 2

    def test_post_each_file_gets_unique_operation_id(self):
        client, _, _ = self._client_and_store()
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": "# A\n\nContent.",
            "docs/b.md": "# B\n\nContent.",
        })
        r = client.post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        ops = r.json()["operations"]
        ids = {op["operation_id"] for op in ops}
        assert len(ids) == 2

    def test_post_operations_registered_in_store(self):
        client, store, _ = self._client_and_store()
        r = client.post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        op_id = r.json()["operations"][0]["operation_id"]
        retrieved = _run(store.get(op_id))
        assert retrieved is not None
        assert retrieved.collection == "col"

    def test_post_skips_directory_entries_in_zip(self):
        """Directory entries inside the ZIP must not be enqueued as jobs."""
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as zf:
            zf.mkdir("docs/")  # directory entry
            zf.writestr("rag-config.yaml", _MINIMAL_RAG_CONFIG)
            zf.writestr("metadata-rules.yaml", _MINIMAL_META)
            zf.writestr("queries.yaml", _MINIMAL_QUERIES)
            zf.writestr("docs/real.md", _MINIMAL_DOC)
        client, _, _ = self._client_and_store()
        r = client.post(
            "/ingest/col/batch",
            content=buf.getvalue(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202
        body = r.json()
        rel_paths = [op["rel_path"] for op in body["operations"]]
        assert rel_paths == ["docs/real.md"]

    def test_post_config_files_not_enqueued_as_documents(self):
        client, _, _ = self._client_and_store()
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/doc.md": _MINIMAL_DOC,
        })
        r = client.post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        body = r.json()
        rel_paths = [op["rel_path"] for op in body["operations"]]
        assert "metadata-rules.yaml" not in rel_paths
        assert "queries.yaml" not in rel_paths


class TestBatchValidation:
    def _client(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        return TestClient(_make_app(store, queue), raise_server_exceptions=False)

    def test_wrong_content_type_returns_415(self):
        r = self._client().post(
            "/ingest/col/batch",
            content=b"data",
            headers={"Content-Type": "text/plain"},
        )
        assert r.status_code == 415

    def test_empty_body_returns_400(self):
        r = self._client().post(
            "/ingest/col/batch",
            content=b"",
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400

    def test_invalid_zip_returns_400(self):
        r = self._client().post(
            "/ingest/col/batch",
            content=b"not a zip at all",
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400

    def test_missing_metadata_rules_companion_uses_inline_rules(self):
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG_INLINE,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202

    def test_missing_queries_companion_uses_inline_queries(self):
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG_INLINE,
            "metadata-rules.yaml": _MINIMAL_META,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202

    def test_missing_metadata_rules_everywhere_returns_400(self):
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400
        assert "doc_kind_rules" in r.json()["error"]

    def test_missing_rag_config_returns_400(self):
        zip_data = _make_zip({
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400
        assert "rag-config.yaml" in r.json()["error"]

    def test_no_md_files_returns_400(self):
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400

    def test_queue_full_returns_503(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=1)
        app = _make_app(store, queue, capacity=1)
        client = TestClient(app, raise_server_exceptions=False)
        # Pre-fill the queue to trigger 503
        zip_data = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
            "docs/b.md": "# B\n\nContent B.",
        })
        r = client.post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 503

    def test_invalid_collection_name_returns_400(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)
        r = client.post(
            "/ingest/INVALID COLLECTION/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code in (400, 404)  # routing or validation error

    def test_path_traversal_in_zip_returns_400(self):
        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as zf:
            zf.writestr("rag-config.yaml", _MINIMAL_RAG_CONFIG)
            zf.writestr("metadata-rules.yaml", _MINIMAL_META)
            zf.writestr("queries.yaml", _MINIMAL_QUERIES)
            zf.writestr("../etc/passwd", "root:x:0:0")
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)
        r = client.post(
            "/ingest/col/batch",
            content=buf.getvalue(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 400
        assert "traversal" in r.json()["error"].lower()

    def test_missing_ranking_weights_adds_warning(self):
        zip_data = _make_zip({
            "rag-config.yaml": "chunker:\n  max_tokens: 512\n",
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202
        assert any("ranking.weights" in w for w in r.json().get("warnings", []))

    def test_missing_chunker_max_tokens_adds_warning(self):
        zip_data = _make_zip({
            "rag-config.yaml": "ranking:\n  weights:\n    - pattern: 'docs/**'\n      weight: 1.0\n",
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
        })
        r = self._client().post(
            "/ingest/col/batch",
            content=zip_data,
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202
        assert any("chunker.max_tokens" in w for w in r.json().get("warnings", []))

    def test_weights_and_max_tokens_present_do_not_add_warning(self):
        r = self._client().post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202
        warnings = r.json().get("warnings", [])
        assert not any("ranking.weights" in w for w in warnings)
        assert not any("chunker.max_tokens" in w for w in warnings)


class TestGetOperationRoute:
    def test_get_returns_200_with_snake_case_body(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)

        r = client.post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        op_id = r.json()["operations"][0]["operation_id"]

        r2 = client.get(f"/ingest/col/operations/{op_id}")
        assert r2.status_code == 200
        body = r2.json()
        assert body["operation_id"] == op_id
        assert body["status"] == "Queued"
        assert "rel_path" in body

    def test_get_wrong_collection_returns_404(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)

        r = client.post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        op_id = r.json()["operations"][0]["operation_id"]
        r2 = client.get(f"/ingest/other-col/operations/{op_id}")
        assert r2.status_code == 404

    def test_get_unknown_id_returns_404(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)
        r = client.get(f"/ingest/col/operations/{uuid.uuid4()}")
        assert r.status_code == 404


class TestListOperationsRoute:
    def test_list_returns_operations_for_collection(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)

        zip_two = _make_zip({
            "rag-config.yaml": _MINIMAL_RAG_CONFIG,
            "metadata-rules.yaml": _MINIMAL_META,
            "queries.yaml": _MINIMAL_QUERIES,
            "docs/a.md": _MINIMAL_DOC,
            "docs/b.md": "# B\n\nContent.",
        })
        client.post("/ingest/col/batch", content=zip_two, headers={"Content-Type": "application/zip"})

        r = client.get("/ingest/col/operations")
        assert r.status_code == 200
        body = r.json()
        assert body["count"] == 2
        assert len(body["operations"]) == 2

    def test_list_isolates_by_collection(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)

        client.post("/ingest/col-a/batch", content=_batch_zip(), headers={"Content-Type": "application/zip"})
        client.post("/ingest/col-b/batch", content=_batch_zip(), headers={"Content-Type": "application/zip"})

        r = client.get("/ingest/col-a/operations")
        assert r.json()["count"] == 1


class TestAdminStatsRoute:
    def test_admin_stats_returns_expected_fields(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)
        r = client.get("/admin/stats")
        assert r.status_code == 200
        body = r.json()
        assert "queue_depth" in body
        assert "retention_hours" in body
        assert "total_operations" in body
        assert body["retention_hours"] == RETENTION_HOURS


# ─────────────────────────────────────────────────────────────────────────────
# ApiKeyMiddleware
# ─────────────────────────────────────────────────────────────────────────────

class TestApiKeyMiddleware:
    def _secured_client(self, api_key: str = "secret-key") -> TestClient:
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        return TestClient(_make_app(store, queue, api_key=api_key), raise_server_exceptions=False)

    def test_missing_key_returns_401(self):
        r = self._secured_client().post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 401

    def test_wrong_key_returns_401(self):
        r = self._secured_client().post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip", "X-Api-Key": "wrong"},
        )
        assert r.status_code == 401

    def test_correct_key_passes_through(self):
        r = self._secured_client("my-key").post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip", "X-Api-Key": "my-key"},
        )
        assert r.status_code == 202

    def test_no_key_configured_allows_all(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        # No api_key arg → no auth
        client = TestClient(_make_app(store, queue), raise_server_exceptions=False)
        r = client.post(
            "/ingest/col/batch",
            content=_batch_zip(),
            headers={"Content-Type": "application/zip"},
        )
        assert r.status_code == 202

    def test_admin_stats_also_protected(self):
        r = self._secured_client().get("/admin/stats")
        assert r.status_code == 401
