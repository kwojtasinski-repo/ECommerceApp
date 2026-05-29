"""Storage facade for the RAG server.

ADR-0028 Phase 3 Python mirror. Owns every Qdrant operation that touches the
``__config__`` point so callers never reach for raw clients. Chunk / search
operations remain on the existing :class:`QueryEngine` and ingest worker for
now; later slices will fold them in.
"""
from __future__ import annotations
