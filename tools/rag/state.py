"""Shared deferred globals for the RAG MCP server.

Initialised in ``mcp_server.__main__`` after CLI / env config is resolved and
before the asyncio event loop starts.  Importing modules (``rag_tools``,
``mcp_server``) read these as ``state.ENGINE`` / ``state.CFG`` so there is
no circular dependency.
"""
from __future__ import annotations

import os
from contextvars import ContextVar
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from common import Config
    from query import QueryEngine

# Deferred — set by mcp_server.__main__ before the event loop starts.
ENGINE: "QueryEngine | None" = None
CFG: "Config | None" = None

# Per-SSE-session / per-HTTP-request collection override.
# Set from the ``?project=<name>`` query param on the connection URL.
# When None, tools fall back to CFG.collection (the default configured collection).
_session_collection: ContextVar[str | None] = ContextVar("_session_collection", default=None)

# Maximum seconds a single tool call may run before being cancelled.
TOOL_TIMEOUT: float = float(os.environ.get("RAG_TOOL_TIMEOUT", "60"))
