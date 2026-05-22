"""API key authentication middleware for the ingest HTTP API.

Mirrors the .NET ApiKeyMiddleware (ADR-0028):
  - Reads X-Api-Key header on all /ingest/* and /admin/* requests.
  - Returns 401 if the key is missing or wrong.
  - When RAG_API_KEY env var is NOT set (or empty), all requests pass through
    (useful for local dev without authentication).
  - MCP SSE/messages paths are NOT protected — they use their own VS Code
    connection security (tunnelled through Copilot).

Usage:
    from api_key_middleware import ApiKeyMiddleware
    app = Starlette(...)
    app.add_middleware(ApiKeyMiddleware)
"""
from __future__ import annotations

import json
import os

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import Response

# Paths that require authentication.
_PROTECTED_PREFIXES = ("/ingest/", "/ingest", "/admin/")


class ApiKeyMiddleware(BaseHTTPMiddleware):
    """Starlette ASGI middleware that enforces X-Api-Key on protected paths."""

    def __init__(self, app, api_key: str | None = None) -> None:
        super().__init__(app)
        # Explicit key wins; fall back to env var.  Empty string means "no auth".
        raw = api_key if api_key is not None else os.environ.get("RAG_API_KEY", "")
        self._api_key: str = raw.strip()

    async def dispatch(self, request: Request, call_next) -> Response:
        if self._api_key and self._is_protected(request.url.path):
            provided = request.headers.get("X-Api-Key", "")
            if provided != self._api_key:
                return Response(
                    content=json.dumps({"error": "Unauthorized — invalid or missing X-Api-Key"}),
                    status_code=401,
                    media_type="application/json",
                )
        return await call_next(request)

    @staticmethod
    def _is_protected(path: str) -> bool:
        return any(path.startswith(p) for p in _PROTECTED_PREFIXES)
