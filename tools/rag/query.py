"""Query the RAG index. Library + CLI.

Two backends:
- local  → connects to an embedded Qdrant at local_path (file-based, no server)
- docker → connects to a running Qdrant server at vector_store.url

Public API:
    QueryEngine.search(query, top_k=5, fetch_k=20, bc_filter=None) -> list[QueryHit]
"""
from __future__ import annotations

import argparse
import json
import sys
from dataclasses import asdict, dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any

# Force UTF-8 output on Windows to avoid UnicodeEncodeError with special chars
if sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

from common import Config, load_config



@dataclass
class QueryHit:
    rel_path: str
    doc_title: str
    doc_kind: str
    adr_id: str | None
    breadcrumb: str
    start_line: int
    end_line: int
    raw_score: float
    weight: float
    final_score: float
    text: str

    def as_dict(self) -> dict[str, Any]:
        return asdict(self)


class QueryEngine:
    def __init__(self, cfg: Config | None = None, embedder=None) -> None:
        self.cfg = cfg or load_config()
        self._client = None
        self._embedder = embedder  # injected or lazy-created on first _ensure() call
        self._mode = self.cfg.vector_mode

    @property
    def embedder(self):
        """The active embedding pipeline.  Creates one lazily if not injected."""
        if self._embedder is None:
            from make_embedder import make_embedder
            self._embedder = make_embedder(self.cfg)
        return self._embedder

    def _ensure(self) -> None:
        if self._client is not None:
            return
        # Accessing .dimensions triggers the actual ML model load inside the embedder.
        # This is intentionally done synchronously here so the model is ready before
        # the asyncio event loop starts (mcp_server.py calls _ensure() in __main__).
        _ = self.embedder.dimensions
        # Qdrant client setup.
        from qdrant_client import QdrantClient
        from qdrant_client.models import Distance, PointStruct, VectorParams
        if self._mode == "memory":
            self._client = QdrantClient(":memory:")
            snapshot_path = self.cfg.snapshot_path
            if not snapshot_path.exists():
                raise FileNotFoundError(
                    f"Snapshot not found at {snapshot_path}. Run `python tools/rag/ingest.py` first."
                )
            with snapshot_path.open("r", encoding="utf-8") as fh:
                snap = json.load(fh)
            self._client.create_collection(
                collection_name=snap["collection"],
                vectors_config=VectorParams(size=snap["dim"], distance=Distance.COSINE),
            )
            BATCH = 256
            points = [
                PointStruct(id=p["id"], vector=p["vector"], payload=p["payload"])
                for p in snap["points"]
            ]
            for i in range(0, len(points), BATCH):
                self._client.upsert(collection_name=snap["collection"], points=points[i : i + BATCH])
        elif self._mode == "local":
            self._client = QdrantClient(path=self.cfg.vector_local_path)
        else:  # docker
            self._client = QdrantClient(url=self.cfg.vector_url)

    def get_collection_config(self, collection: "str | None" = None) -> dict:
        """Retrieve the __config__ point (id=0) payload for *collection*.

        Returns an empty dict if the collection has no config point yet (e.g.
        collections ingested before the config-point convention was introduced).
        Callers should not pierce the private ``_client`` attribute — use this
        method instead.
        """
        self._ensure()
        target = collection or self.cfg.collection
        try:
            pts = self._client.retrieve(
                collection_name=target,
                ids=[0],
                with_payload=True,
            )
            if pts and pts[0].payload:
                return dict(pts[0].payload)
        except Exception:
            pass
        return {}

    def search(
        self,
        query: str,
        top_k: int | None = None,
        fetch_k: int | None = None,
        bc_filter: str | None = None,
        field_filter: tuple[str, str] | None = None,
        collection: str | None = None,
    ) -> list[QueryHit]:
        """Search the Qdrant index.

        *collection* overrides cfg.collection — used by the SSE multi-tenant path
        where ?project= sets a per-session collection name via ContextVar.

        *field_filter* is an optional (field_name, value) pair applied as a Qdrant
        MUST exact-match filter.  Used by get_history to filter by adr_id (or any
        collection-configured history field) without a score threshold.
        """
        self._ensure()
        defaults = self.cfg.query_defaults
        top_k = top_k or int(defaults["default_top_k"])
        fetch_k = fetch_k or int(defaults["fetch_k"])

        from qdrant_client.models import Filter, FieldCondition, MatchValue

        qvec = self._embedder.embed(query)
        qfilter = None
        if field_filter:
            field_name, field_value = field_filter
            qfilter = Filter(must=[FieldCondition(key=field_name, match=MatchValue(value=field_value))])
        if bc_filter:
            # bc_filter is matched against breadcrumb / heading_path as a substring (case-insensitive).
            # Qdrant has no native ICONTAINS on arrays, so we post-filter after the search.
            pass

        results = self._client.search(
            collection_name=collection or self.cfg.collection,
            query_vector=qvec,
            query_filter=qfilter,
            limit=fetch_k,
        )

        hits: list[QueryHit] = []
        for r in results:
            payload = r.payload or {}
            if bc_filter and not _matches_bc(payload, bc_filter):
                continue
            weight = float(payload.get("weight", 1.0))
            final = float(r.score) * weight
            hits.append(
                QueryHit(
                    rel_path=payload.get("rel_path", ""),
                    doc_title=payload.get("doc_title", ""),
                    doc_kind=payload.get("doc_kind", ""),
                    adr_id=payload.get("adr_id"),
                    breadcrumb=payload.get("breadcrumb", ""),
                    start_line=int(payload.get("start_line", 0)),
                    end_line=int(payload.get("end_line", 0)),
                    raw_score=float(r.score),
                    weight=weight,
                    final_score=final,
                    text=payload.get("text", ""),
                )
            )
        # Skip score threshold when field_filter is set — exact metadata lookups
        # (e.g. get_history) must return ALL matching chunks regardless of vector
        # similarity.  The query string is only used to rank results, not filter them.
        if not field_filter:
            threshold = float(self.cfg.query_defaults.get("score_threshold", 0.0))
            if threshold > 0:
                hits = [h for h in hits if h.raw_score >= threshold]
        hits.sort(key=lambda h: h.final_score, reverse=True)
        return hits[:top_k]


def _matches_bc(payload: dict, bc_filter: str) -> bool:
    needle = bc_filter.lower()
    bread = (payload.get("breadcrumb") or "").lower()
    if needle in bread:
        return True
    title = (payload.get("doc_title") or "").lower()
    return needle in title


@lru_cache(maxsize=1)
def get_engine() -> QueryEngine:
    return QueryEngine()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("query", help="Natural-language query")
    parser.add_argument("--top-k", type=int, default=None)
    parser.add_argument("--fetch-k", type=int, default=None)
    parser.add_argument("--bc", type=str, default=None, help="Substring filter on breadcrumb / title")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON")
    args = parser.parse_args()

    engine = QueryEngine()
    hits = engine.search(args.query, top_k=args.top_k, fetch_k=args.fetch_k, bc_filter=args.bc)
    if args.json:
        print(json.dumps([h.as_dict() for h in hits], indent=2))
        return 0

    for i, h in enumerate(hits, 1):
        print(f"#{i}  score={h.final_score:.3f}  (raw={h.raw_score:.3f} × w={h.weight:.2f})")
        print(f"     {h.rel_path}:{h.start_line}-{h.end_line}")
        print(f"     {h.breadcrumb}")
        snippet = h.text.strip().splitlines()
        preview = " ".join(snippet)[:240]
        print(f"     > {preview}...")
        print()
    return 0


if __name__ == "__main__":
    sys.exit(main())
