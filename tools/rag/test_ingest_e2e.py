"""E2E integration tests for the Python ingest HTTP API (ADR-0028 Phase 2).

These tests start a real Starlette/uvicorn HTTP server in-process and hit it
with real HTTP requests. They do NOT require a running Qdrant instance; the
ingest worker's actual chunking/embedding is patched to a no-op so tests stay
fast and hermetic.

For "full pipeline to Qdrant" scenarios, set QDRANT_URL env var. Tests that
need Qdrant are skipped automatically when QDRANT_URL is not set.

Run:
    .venv\\Scripts\\pytest.exe test_ingest_e2e.py -v
"""
from __future__ import annotations

import asyncio
import json
import os
import threading
import time
import urllib.request
import urllib.error
from contextlib import asynccontextmanager
from unittest.mock import AsyncMock, patch

import pytest
import uvicorn
from starlette.applications import Starlette

from api_key_middleware import ApiKeyMiddleware
from ingest_routes import build_ingest_routes
from ingest_worker import DEFAULT_CAPACITY, IngestJob, IngestWorker
from operation_store import IngestStatus, OperationStore


# ─────────────────────────────────────────────────────────────────────────────
# Fixture helpers
# ─────────────────────────────────────────────────────────────────────────────

def _find_free_port() -> int:
    import socket

    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


class _ServerHandle:
    """Starts uvicorn in a background thread; exposes base_url."""

    def __init__(self, app, port: int) -> None:
        self.base_url = f"http://127.0.0.1:{port}"
        cfg = uvicorn.Config(app, host="127.0.0.1", port=port, log_level="warning")
        self._server = uvicorn.Server(cfg)
        self._thread = threading.Thread(target=self._server.run, daemon=True)

    def start(self) -> None:
        self._thread.start()
        # Wait until the server is actually serving.
        deadline = time.monotonic() + 10
        while not self._server.started:
            if time.monotonic() > deadline:
                raise TimeoutError("uvicorn did not start within 10s")
            time.sleep(0.05)

    def stop(self) -> None:
        self._server.should_exit = True
        self._thread.join(timeout=5)


def _build_test_app(
    process_fn=None,
    api_key: str | None = None,
) -> tuple[Starlette, OperationStore, asyncio.Queue]:
    """Build a Starlette app backed by real OperationStore + IngestWorker.

    ``process_fn`` is the async callable invoked by IngestWorker for each job.
    If None, uses an AsyncMock (no-op, instantly completes).
    """
    store = OperationStore()
    queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)

    if process_fn is None:
        # Default no-op: mark job completed with 3 fake chunks.
        async def _noop(job: IngestJob) -> None:
            await store.mark_processing(job.operation_id)
            await asyncio.sleep(0)
            await store.mark_completed(job.operation_id, chunk_count=3)

        process_fn = _noop

    worker = IngestWorker(queue, process_fn)

    @asynccontextmanager
    async def lifespan(_app):
        worker.start()
        yield
        await worker.stop()

    routes = build_ingest_routes(store, queue, DEFAULT_CAPACITY)
    app = Starlette(lifespan=lifespan, routes=routes)
    if api_key:
        app.add_middleware(ApiKeyMiddleware, api_key=api_key)
    return app, store, queue


def _get(url: str, headers: dict | None = None) -> tuple[int, dict]:
    req = urllib.request.Request(url, headers=headers or {})
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def _post(url: str, body: dict, headers: dict | None = None) -> tuple[int, dict]:
    data = json.dumps(body).encode()
    req = urllib.request.Request(
        url,
        data=data,
        headers={"Content-Type": "application/json", **(headers or {})},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read())
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def _poll_until_done(base_url: str, collection: str, op_id: str, timeout: float = 10.0) -> dict:
    """Poll GET /ingest/{collection}/operations/{op_id} until not Queued or Processing."""
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        status_code, body = _get(f"{base_url}/ingest/{collection}/operations/{op_id}")
        if status_code == 200 and body.get("status") not in ("Queued", "Processing"):
            return body
        time.sleep(0.1)
    raise TimeoutError(f"Operation {op_id} did not complete within {timeout}s")


# ─────────────────────────────────────────────────────────────────────────────
# E2E tests — full HTTP server (no Qdrant, worker is a no-op)
# ─────────────────────────────────────────────────────────────────────────────

@pytest.fixture(scope="module")
def server() -> _ServerHandle:
    port = _find_free_port()
    app, _store, _queue = _build_test_app()
    handle = _ServerHandle(app, port)
    handle.start()
    yield handle
    handle.stop()


class TestE2EUpload:
    def test_post_returns_202_and_operation_id(self, server):
        code, body = _post(
            f"{server.base_url}/ingest/docs",
            {"relPath": "adr/0001.md", "content": "# ADR-0001"},
        )
        assert code == 202
        assert "operationId" in body
        assert body["status"] == "Queued"

    def test_post_missing_rel_path_returns_400(self, server):
        code, _ = _post(f"{server.base_url}/ingest/docs", {"content": "# ADR"})
        assert code == 400

    def test_post_missing_content_returns_400(self, server):
        code, _ = _post(f"{server.base_url}/ingest/docs", {"relPath": "x.md"})
        assert code == 400


class TestE2EStatusPolling:
    def test_operation_reaches_completed_status(self, server):
        code, body = _post(
            f"{server.base_url}/ingest/docs",
            {"relPath": "e2e/polling.md", "content": "# Polling test"},
        )
        assert code == 202
        op_id = body["operationId"]
        final = _poll_until_done(server.base_url, "docs", op_id)
        assert final["status"] == "Completed"
        assert final["chunkCount"] == 3  # from our no-op process_fn

    def test_get_operation_returns_404_for_unknown_id(self, server):
        code, _ = _get(f"{server.base_url}/ingest/docs/operations/nonexistent-id")
        assert code == 404

    def test_get_operation_returns_404_for_wrong_collection(self, server):
        code, body = _post(
            f"{server.base_url}/ingest/col-a",
            {"relPath": "f.md", "content": "# Hello"},
        )
        assert code == 202
        op_id = body["operationId"]
        # Look up with a different collection name.
        code2, _ = _get(f"{server.base_url}/ingest/col-b/operations/{op_id}")
        assert code2 == 404

    def test_location_header_leads_to_operation(self, server):
        code, body = _post(
            f"{server.base_url}/ingest/docs",
            {"relPath": "e2e/location.md", "content": "# Location"},
        )
        assert code == 202
        location = body.get("location")
        assert location
        full_url = f"{server.base_url}{location}"
        _poll_until_done(server.base_url, "docs", body["operationId"])
        status_code, op_body = _get(full_url)
        assert status_code == 200
        assert op_body["operationId"] == body["operationId"]


class TestE2EListOperations:
    def test_list_returns_uploaded_operations(self, server):
        # Upload two docs to a unique collection.
        col = "e2e-list-test"
        _post(f"{server.base_url}/ingest/{col}", {"relPath": "a.md", "content": "# A"})
        _post(f"{server.base_url}/ingest/{col}", {"relPath": "b.md", "content": "# B"})
        time.sleep(0.5)
        code, body = _get(f"{server.base_url}/ingest/{col}/operations")
        assert code == 200
        assert body["count"] >= 2

    def test_list_empty_for_unused_collection(self, server):
        code, body = _get(f"{server.base_url}/ingest/collection-that-does-not-exist/operations")
        assert code == 200
        assert body["count"] == 0


class TestE2EAdminStats:
    def test_admin_stats_returns_retention_hours_one(self, server):
        code, body = _get(f"{server.base_url}/admin/stats")
        assert code == 200
        assert body["retention_hours"] == 1  # both Python and .NET use 1h

    def test_admin_stats_has_all_expected_fields(self, server):
        code, body = _get(f"{server.base_url}/admin/stats")
        assert code == 200
        assert "queue_depth" in body
        assert "retention_hours" in body
        assert "total_operations" in body


class TestE2EApiKeyAuth:
    def test_protected_endpoint_returns_401_without_key(self):
        port = _find_free_port()
        app, _, _ = _build_test_app(api_key="test-secret")
        handle = _ServerHandle(app, port)
        handle.start()
        try:
            code, _ = _post(
                f"http://127.0.0.1:{port}/ingest/c",
                {"relPath": "f.md", "content": "# Hi"},
            )
            assert code == 401
        finally:
            handle.stop()

    def test_protected_endpoint_passes_with_correct_key(self):
        port = _find_free_port()
        app, _, _ = _build_test_app(api_key="test-secret")
        handle = _ServerHandle(app, port)
        handle.start()
        try:
            code, _ = _post(
                f"http://127.0.0.1:{port}/ingest/c",
                {"relPath": "f.md", "content": "# Hi"},
                headers={"X-Api-Key": "test-secret"},
            )
            assert code == 202
        finally:
            handle.stop()

    def test_wrong_key_returns_401(self):
        port = _find_free_port()
        app, _, _ = _build_test_app(api_key="test-secret")
        handle = _ServerHandle(app, port)
        handle.start()
        try:
            code, _ = _post(
                f"http://127.0.0.1:{port}/ingest/c",
                {"relPath": "f.md", "content": "# Hi"},
                headers={"X-Api-Key": "wrong"},
            )
            assert code == 401
        finally:
            handle.stop()


class TestE2EWorkerFailurePropagation:
    def test_failed_job_is_reflected_in_status(self):
        """Verify that a process_fn that raises causes the operation to reach Failed status.

        The process_fn must call mark_processing / mark_failed itself (same contract
        as _build_process_fn in ingest_worker.py). This mirrors IngestWorker._run's
        design: the worker catches unhandled exceptions but does not touch the store.
        """

        async def _failing(job: IngestJob) -> None:
            await store.mark_processing(job.operation_id)
            await asyncio.sleep(0)
            try:
                raise RuntimeError("intentional test failure")
            except Exception as exc:
                await store.mark_failed(job.operation_id, str(exc))
                raise  # re-raise so the worker logs it

        port = _find_free_port()
        # Build store separately so we can watch it.
        store = OperationStore()
        queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)
        worker = IngestWorker(queue, _failing)

        @asynccontextmanager
        async def lifespan(_app):
            worker.start()
            yield
            await worker.stop()

        routes = build_ingest_routes(store, queue, DEFAULT_CAPACITY)
        app = Starlette(lifespan=lifespan, routes=routes)
        handle = _ServerHandle(app, port)
        handle.start()
        try:
            code, body = _post(
                f"http://127.0.0.1:{port}/ingest/c",
                {"relPath": "bad.md", "content": "# Will fail"},
            )
            assert code == 202
            op_id = body["operationId"]
            final = _poll_until_done(f"http://127.0.0.1:{port}", "c", op_id)
            assert final["status"] == "Failed"
            assert final["errorMessage"]  # non-empty error message
        finally:
            handle.stop()


# ─────────────────────────────────────────────────────────────────────────────
# Full-pipeline E2E (requires real Qdrant via QDRANT_URL env var)
# ─────────────────────────────────────────────────────────────────────────────

_DEFAULT_QDRANT_URL = os.environ.get("QDRANT_URL", "http://localhost:6333")


def _qdrant_reachable(url: str = _DEFAULT_QDRANT_URL) -> bool:
    """Return True if Qdrant HTTP API is responding at *url*."""
    try:
        with urllib.request.urlopen(f"{url}/healthz", timeout=3) as resp:
            return resp.status == 200
    except Exception:
        return False


class TestE2EFullPipelineQdrant:
    """Full-pipeline tests that spin up a real uvicorn server + real Qdrant.

    The test probes Qdrant at startup and fails immediately with a clear message
    if Qdrant is not reachable — no silent skips.
    Override the URL via ``QDRANT_URL`` env var (default: http://localhost:6333).
    Start Qdrant with: ``docker compose up -d qdrant``
    """

    def test_uploaded_document_appears_in_qdrant(self):
        """Upload a doc, wait for Completed, verify at least 1 point exists in Qdrant."""
        qdrant_url = _DEFAULT_QDRANT_URL
        if not _qdrant_reachable(qdrant_url):
            pytest.fail(
                f"Qdrant is not reachable at {qdrant_url}. "
                "Start it with: docker compose up -d qdrant  "
                "(or set QDRANT_URL to a running instance)"
            )

        import os
        from pathlib import Path

        from common import load_config
        from ingest_worker import _build_process_fn
        from query import QueryEngine
        from qdrant_client import QdrantClient

        cfg_path = Path(__file__).parent / "config.yaml"
        if not cfg_path.exists():
            pytest.fail("config.yaml not found next to test file — cannot build QueryEngine")

        # Force HTTP mode so the engine connects to the running Qdrant container.
        # The config may default to 'local' (embedded Qdrant), which writes to a local
        # file and is a completely different store from the HTTP container.
        old_mode = os.environ.get("VECTOR_MODE")
        old_url = os.environ.get("QDRANT_URL")
        os.environ["VECTOR_MODE"] = "qdrant"
        os.environ["QDRANT_URL"] = qdrant_url

        handle = None
        try:
            cfg = load_config(cfg_path)
            engine = QueryEngine(cfg)
            store = OperationStore()
            queue: asyncio.Queue = asyncio.Queue(maxsize=DEFAULT_CAPACITY)
            process_fn = _build_process_fn(engine, cfg, store)
            worker = IngestWorker(queue, process_fn)

            @asynccontextmanager
            async def lifespan(_app):
                worker.start()
                yield
                await worker.stop()

            port = _find_free_port()
            routes = build_ingest_routes(store, queue, DEFAULT_CAPACITY)
            app = Starlette(lifespan=lifespan, routes=routes)
            handle = _ServerHandle(app, port)
            handle.start()

            collection = "e2e-qdrant-test"
            base = f"http://127.0.0.1:{port}"
            code, body = _post(
                f"{base}/ingest/{collection}",
                {
                    "relPath": "e2e/qdrant-test.md",
                    "content": (
                        "# E2E Qdrant Integration Test Document\n\n"
                        "This document is used by the Python ingest E2E integration test "
                        "to verify that the full pipeline — HTTP POST, asyncio worker, "
                        "SentenceTransformer embedding, and Qdrant upsert — all operate "
                        "correctly end-to-end. The text is deliberately verbose so that "
                        "the content exceeds the chunker min_tokens threshold (default 40) "
                        "and at least one chunk is produced and indexed into Qdrant. "
                        "The test then queries Qdrant directly to assert that at least "
                        "one point exists for the uploaded rel_path, confirming that the "
                        "worker completed the ingest pipeline successfully.\n"
                    ),
                    "docKind": "adr",
                },
            )
            assert code == 202, f"Expected 202 but got {code}: {body}"
            final = _poll_until_done(base, collection, body["operationId"], timeout=60)
            assert final["status"] == "Completed", f"Expected Completed: {final}"
            assert final["chunkCount"] > 0, "Expected at least 1 chunk to be indexed"

            # Verify points exist in Qdrant directly.
            client = QdrantClient(url=qdrant_url)
            result = client.scroll(
                collection_name=collection,
                scroll_filter={
                    "must": [{"key": "rel_path", "match": {"value": "e2e/qdrant-test.md"}}]
                },
                limit=10,
            )
            points, _ = result
            assert len(points) > 0, "Expected at least 1 point in Qdrant after ingest"
        finally:
            if handle:
                handle.stop()
            # Restore original env vars.
            if old_mode is None:
                os.environ.pop("VECTOR_MODE", None)
            else:
                os.environ["VECTOR_MODE"] = old_mode
            if old_url is None:
                os.environ.pop("QDRANT_URL", None)
            else:
                os.environ["QDRANT_URL"] = old_url
