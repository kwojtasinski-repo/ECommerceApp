"""Unit tests for the Python ingest pipeline modules (Phase 2 of ADR-0028).

Tests are fully synchronous / in-process — no Qdrant, no real HTTP, no model loading.
Run with: pytest tools/rag/test_ingest_unit.py -v

Coverage:
  - OperationStore: enqueue, status transitions, 404 semantics, retention purge,
    list_for_collection isolation, queue_depth, admin stats fields
  - IngestWorker: happy path (mock process_fn), failure path, lifecycle start/stop
  - Ingest routes (Starlette TestClient): 202, 400, 503, GET 200/404, list, admin stats
  - ApiKeyMiddleware: 401 on missing/wrong key, pass-through on correct key, no-auth mode
"""
from __future__ import annotations

import asyncio
import json
import time
import uuid
from datetime import datetime, timezone, timedelta
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from starlette.testclient import TestClient
from starlette.applications import Starlette

from operation_store import IngestStatus, IngestOperation, OperationStore, RETENTION_HOURS
from ingest_worker import IngestJob, IngestWorker, DEFAULT_CAPACITY
from ingest_routes import build_ingest_routes
from api_key_middleware import ApiKeyMiddleware


# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

def _run(coro):
    """Run a coroutine synchronously inside a fresh event loop."""
    return asyncio.get_event_loop().run_until_complete(coro)


def _make_app(store: OperationStore, queue: asyncio.Queue, api_key: str | None = None, capacity: int = DEFAULT_CAPACITY) -> Starlette:
    routes = build_ingest_routes(store, queue, capacity=capacity)
    app = Starlette(routes=routes)
    if api_key:
        app.add_middleware(ApiKeyMiddleware, api_key=api_key)
    return app


# ─────────────────────────────────────────────────────────────────────────────
# OperationStore tests
# ─────────────────────────────────────────────────────────────────────────────

class TestOperationStoreEnqueue:
    def test_enqueue_returns_queued_operation(self):
        store = OperationStore()
        op = _run(store.enqueue("col-a", "docs/intro.md"))
        assert op.status == IngestStatus.Queued
        assert op.collection == "col-a"
        assert op.rel_path == "docs/intro.md"
        assert op.operation_id  # non-empty UUID

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

    def test_mark_completed_sets_chunk_count(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        _run(store.mark_completed(op.operation_id, chunk_count=7))
        updated = _run(store.get(op.operation_id))
        assert updated.status == IngestStatus.Completed
        assert updated.chunk_count == 7
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

    def test_retention_hours_constant_matches_admin_value(self):
        """RETENTION_HOURS must match what /admin/stats reports (1 hour, both .NET and Python)."""
        assert RETENTION_HOURS == 1


class TestOperationStoreRetentionPurge:
    def test_old_operations_are_purged_on_next_enqueue(self):
        store = OperationStore()
        # Manually insert an expired operation.
        expired = IngestOperation(
            operation_id=str(uuid.uuid4()),
            collection="c",
            rel_path="old.md",
            enqueued_at=datetime.now(timezone.utc) - timedelta(hours=2),
        )
        store._ops[expired.operation_id] = expired
        # Trigger purge by enqueueing a new operation.
        _run(store.enqueue("c", "new.md"))
        assert _run(store.get(expired.operation_id)) is None

    def test_recent_operations_survive_purge(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        # Trigger purge again.
        _run(store.enqueue("c", "g.md"))
        assert _run(store.get(op.operation_id)) is not None


class TestIngestOperationToDict:
    def test_to_dict_keys_are_camel_case(self):
        store = OperationStore()
        op = _run(store.enqueue("my-col", "docs/adr/0001.md"))
        d = op.to_dict()
        assert "operationId" in d
        assert "relPath" in d
        assert "enqueuedAt" in d
        assert "chunkCount" in d
        assert "errorMessage" in d

    def test_to_dict_status_is_string(self):
        store = OperationStore()
        op = _run(store.enqueue("c", "f.md"))
        assert op.to_dict()["status"] == "Queued"


# ─────────────────────────────────────────────────────────────────────────────
# IngestWorker tests
# ─────────────────────────────────────────────────────────────────────────────

class TestIngestWorker:
    def test_worker_calls_process_fn_and_stops(self):
        processed: list[IngestJob] = []

        async def fake_process(job: IngestJob) -> None:
            processed.append(job)

        queue: asyncio.Queue = asyncio.Queue()
        worker = IngestWorker(queue, fake_process)

        async def _run_test():
            worker.start()
            await queue.put(IngestJob("op1", "col", "f.md", "# Content", None))
            await queue.join()  # wait until task_done()
            await worker.stop()
            assert len(processed) == 1
            assert processed[0].operation_id == "op1"

        _run(_run_test())

    def test_worker_continues_after_process_fn_raises(self):
        succeeded: list[str] = []
        call_count = 0

        async def flaky_process(job: IngestJob) -> None:
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                raise RuntimeError("first call fails")
            succeeded.append(job.operation_id)

        queue: asyncio.Queue = asyncio.Queue()
        worker = IngestWorker(queue, flaky_process)

        async def _run_test():
            worker.start()
            await queue.put(IngestJob("bad", "c", "a.md", "", None))
            await queue.put(IngestJob("good", "c", "b.md", "", None))
            await queue.join()
            await worker.stop()
            assert "good" in succeeded

        _run(_run_test())

    def test_worker_stop_is_idempotent(self):
        queue: asyncio.Queue = asyncio.Queue()
        worker = IngestWorker(queue, AsyncMock())

        async def _run_test():
            worker.start()
            await worker.stop()
            await worker.stop()  # must not raise

        _run(_run_test())


# ─────────────────────────────────────────────────────────────────────────────
# Ingest routes (HTTP) tests — uses Starlette TestClient (synchronous)
# ─────────────────────────────────────────────────────────────────────────────

class TestGetOperationRoute:
    def _setup(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        app = _make_app(store, queue)
        client = TestClient(app, raise_server_exceptions=False)
        return store, client

    def test_get_operation_returns_200_for_valid_id(self):
        store, client = self._setup()
        op = _run(store.enqueue("col", "f.md"))
        resp = client.get(f"/ingest/col/operations/{op.operation_id}")
        assert resp.status_code == 200
        body = resp.json()
        assert body["operationId"] == op.operation_id

    def test_get_operation_returns_404_for_unknown_id(self):
        _, client = self._setup()
        resp = client.get(f"/ingest/col/operations/{uuid.uuid4()}")
        assert resp.status_code == 404

    def test_get_operation_returns_404_for_wrong_collection(self):
        store, client = self._setup()
        op = _run(store.enqueue("col-a", "f.md"))
        # Look up with wrong collection name.
        resp = client.get(f"/ingest/col-b/operations/{op.operation_id}")
        assert resp.status_code == 404


class TestListOperationsRoute:
    def test_list_returns_only_matching_collection(self):
        store = OperationStore()
        _run(store.enqueue("col-a", "a.md"))
        _run(store.enqueue("col-a", "b.md"))
        _run(store.enqueue("col-b", "c.md"))
        queue: asyncio.Queue = asyncio.Queue()
        app = _make_app(store, queue)
        client = TestClient(app)
        resp = client.get("/ingest/col-a/operations")
        assert resp.status_code == 200
        body = resp.json()
        assert body["count"] == 2

    def test_list_returns_empty_for_unknown_collection(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        app = _make_app(store, queue)
        client = TestClient(app)
        resp = client.get("/ingest/unknown-col/operations")
        assert resp.status_code == 200
        assert resp.json()["count"] == 0


class TestAdminStatsRoute:
    def test_admin_stats_returns_expected_fields(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=50)
        app = _make_app(store, queue)
        client = TestClient(app)
        resp = client.get("/admin/stats")
        assert resp.status_code == 200
        body = resp.json()
        assert "queue_depth" in body
        assert "retention_hours" in body
        assert "total_operations" in body

    def test_admin_stats_retention_hours_is_one(self):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        app = _make_app(store, queue)
        client = TestClient(app)
        resp = client.get("/admin/stats")
        assert resp.json()["retention_hours"] == 1  # same as .NET RetentionPeriod (1h)


# ─────────────────────────────────────────────────────────────────────────────
# ApiKeyMiddleware tests
# ─────────────────────────────────────────────────────────────────────────────

class TestApiKeyMiddleware:
    def _make_client(self, api_key: str | None = "secret") -> TestClient:
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue()
        return TestClient(_make_app(store, queue, api_key=api_key), raise_server_exceptions=False)

    def test_correct_key_passes_through(self):
        client = self._make_client("my-key")
        resp = client.post(
            "/ingest/c",
            json={"relPath": "f.md", "content": "# Hi"},
            headers={"X-Api-Key": "my-key"},
        )
        assert resp.status_code == 202

    def test_missing_key_returns_401(self):
        client = self._make_client("my-key")
        resp = client.post("/ingest/c", json={"relPath": "f.md", "content": "# Hi"})
        assert resp.status_code == 401

    def test_wrong_key_returns_401(self):
        client = self._make_client("my-key")
        resp = client.post(
            "/ingest/c",
            json={"relPath": "f.md", "content": "# Hi"},
            headers={"X-Api-Key": "wrong"},
        )
        assert resp.status_code == 401

    def test_no_auth_configured_passes_all_requests(self):
        client = self._make_client(api_key=None)  # middleware not added
        resp = client.post("/ingest/c", json={"relPath": "f.md", "content": "# Hi"})
        assert resp.status_code == 202

    def test_admin_stats_protected_when_key_set(self):
        client = self._make_client("secret")
        resp = client.get("/admin/stats")  # no key
        assert resp.status_code == 401

    def test_admin_stats_passes_with_correct_key(self):
        client = self._make_client("secret")
        resp = client.get("/admin/stats", headers={"X-Api-Key": "secret"})
        assert resp.status_code == 200


# ─────────────────────────────────────────────────────────────────────────────
# POST /ingest/{collection}/batch  (P2-2)
# ─────────────────────────────────────────────────────────────────────────────

def _make_zip(files: dict[str, str]) -> bytes:
    """Build an in-memory ZIP archive from {relPath: content} mapping."""
    import io
    import zipfile

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for rel_path, content in files.items():
            zf.writestr(rel_path, content)
    return buf.getvalue()


class TestBatchIngestRoute:
    def _setup(self, capacity: int = DEFAULT_CAPACITY):
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=capacity)
        app = _make_app(store, queue, capacity=capacity)
        return store, queue, TestClient(app, raise_server_exceptions=False)

    # ── happy path ────────────────────────────────────────────────────────────

    def test_batch_single_file_returns_202(self):
        _, _, client = self._setup()
        zb = _make_zip({"docs/intro.md": "# Intro\nHello"})
        resp = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 202

    def test_batch_single_file_body_has_count_and_operations(self):
        _, _, client = self._setup()
        zb = _make_zip({"docs/intro.md": "# Intro"})
        body = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        ).json()
        assert body["count"] == 1
        assert len(body["operations"]) == 1
        op = body["operations"][0]
        assert op["relPath"] == "docs/intro.md"
        assert op["operationId"]
        assert op["statusUrl"].startswith("/ingest/col/operations/")

    def test_batch_multiple_files_enqueues_all(self):
        store, queue, client = self._setup()
        files = {
            "docs/adr/0001.md": "# ADR-0001",
            "docs/adr/0002.md": "# ADR-0002",
            "docs/concepts/ddd.md": "# DDD",
        }
        zb = _make_zip(files)
        resp = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 202
        body = resp.json()
        assert body["count"] == 3
        assert len(body["operations"]) == 3
        rel_paths = {op["relPath"] for op in body["operations"]}
        assert rel_paths == set(files.keys())
        # All three jobs must have been placed on the queue.
        assert queue.qsize() == 3

    def test_batch_each_file_gets_unique_operation_id(self):
        _, _, client = self._setup()
        zb = _make_zip({"a.md": "A", "b.md": "B"})
        body = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        ).json()
        ids = [op["operationId"] for op in body["operations"]]
        assert len(ids) == len(set(ids)), "operation IDs must be unique"

    def test_batch_operations_are_registered_in_store(self):
        store, _, client = self._setup()
        zb = _make_zip({"f.md": "# F"})
        body = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        ).json()
        op_id = body["operations"][0]["operationId"]
        op = _run(store.get(op_id))
        assert op is not None
        assert op.status == IngestStatus.Queued

    def test_batch_body_contains_batch_id(self):
        _, _, client = self._setup()
        zb = _make_zip({"x.md": "# X"})
        body = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        ).json()
        assert "batchId" in body

    # ── error paths ───────────────────────────────────────────────────────────

    def test_batch_invalid_zip_returns_400(self):
        _, _, client = self._setup()
        resp = client.post(
            "/ingest/col/batch",
            content=b"not a zip file",
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 400
        assert "error" in resp.json()

    def test_batch_empty_zip_returns_400(self):
        _, _, client = self._setup()
        zb = _make_zip({})  # ZIP with no files
        resp = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 400
        assert "error" in resp.json()

    def test_batch_queue_full_returns_503(self):
        # Capacity=1, already filled → 503
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=1)
        queue.put_nowait(object())  # fill the one slot
        app = _make_app(store, queue, capacity=1)
        client = TestClient(app, raise_server_exceptions=False)

        zb = _make_zip({"a.md": "A", "b.md": "B"})
        resp = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 503

    def test_batch_skips_directory_entries(self):
        """ZIP directory entries (names ending '/') must not produce operations."""
        import io, zipfile

        buf = io.BytesIO()
        with zipfile.ZipFile(buf, "w") as zf:
            zf.mkdir("docs/")  # directory entry
            zf.writestr("docs/real.md", "# Real")
        zb = buf.getvalue()

        _, _, client = self._setup()
        resp = client.post(
            "/ingest/col/batch",
            content=zb,
            headers={"Content-Type": "application/zip"},
        )
        assert resp.status_code == 202
        body = resp.json()
        assert body["count"] == 1
        assert body["operations"][0]["relPath"] == "docs/real.md"
