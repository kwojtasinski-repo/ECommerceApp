"""E2E tests for split_on_headings auto mode — three levels.

Level A  (chunker integration, no infrastructure required)
    Loads fixture docs from tools/rag-testdata/, runs them through chunk_markdown()
    with both auto and explicit configs, and asserts on the produced Chunk list.

Level B  (ingest pipeline → Qdrant, requires QDRANT_URL env var)
    Creates a temporary Qdrant collection, chunks + embeds the fixture with the ONNX
    model, upserts into Qdrant, scrolls the collection, and asserts on the stored
    breadcrumb payloads. Skipped automatically when QDRANT_URL is not set.

Level C  (semantic search, requires QDRANT_URL + ONNX model)
    Same ingest as Level B, then queries Qdrant with text that exists only inside
    H4 sections (auto mode) or inside a short dropped section (explicit mode) and
    verifies that auto mode returns at least one result with a meaningful score.
    Skipped automatically when QDRANT_URL is not set or the model is absent.

Run:
    # Level A only (fast, no infra)
    .venv\\Scripts\\pytest.exe test_chunker_e2e.py -k "LevelA" -v

    # All levels (Qdrant must be running)
    QDRANT_URL=http://localhost:6333 .venv\\Scripts\\pytest.exe test_chunker_e2e.py -v
"""
from __future__ import annotations

import os
import uuid
from pathlib import Path

import pytest

from chunker import Chunk, chunk_markdown, count_tokens

# ─────────────────────────────────────────────────────────────────────────────
# Paths
# ─────────────────────────────────────────────────────────────────────────────

# Resolve fixtures relative to this file so tests work regardless of cwd.
_FIXTURES = Path(__file__).resolve().parent.parent / "rag-testdata"
_H4_FIXTURE   = _FIXTURES / "auto-mode-h4-sections.md"
_SHORT_FIXTURE = _FIXTURES / "auto-mode-short-sections.md"
_H5_FIXTURE   = _FIXTURES / "auto-mode-h5-sections.md"


# ─────────────────────────────────────────────────────────────────────────────
# Config helpers
# ─────────────────────────────────────────────────────────────────────────────

def _auto_cfg(min_tokens: int = 40, max_tokens: int = 800) -> dict:
    return {
        "split_on_headings": "auto",
        "max_tokens": max_tokens,
        "min_tokens": min_tokens,
        "overlap_tokens": 0,
    }


def _explicit_cfg(min_tokens: int = 40, max_tokens: int = 800) -> dict:
    return {
        "split_on_headings": [1, 2, 3],
        "max_tokens": max_tokens,
        "min_tokens": min_tokens,
        "overlap_tokens": 0,
    }


def _explicit_h4_cfg(min_tokens: int = 40, max_tokens: int = 800) -> dict:
    return {
        "split_on_headings": [1, 2, 3, 4],
        "max_tokens": max_tokens,
        "min_tokens": min_tokens,
        "overlap_tokens": 0,
    }


# ─────────────────────────────────────────────────────────────────────────────
# Skip guards
# ─────────────────────────────────────────────────────────────────────────────

def _qdrant_url() -> str | None:
    return os.environ.get("QDRANT_URL")


def _model_dir() -> Path | None:
    """Return model directory when the ONNX model is present, else None."""
    env = os.environ.get("RAG_MODEL_DIR")
    candidates = [
        Path(env) if env else None,
        Path(__file__).resolve().parent.parent.parent / "rag-dotnet" / "model",
    ]
    for p in candidates:
        if p and (p / "model.onnx").exists():
            return p
    return None


# ═════════════════════════════════════════════════════════════════════════════
# LEVEL A — Chunker integration (no infrastructure)
# ═════════════════════════════════════════════════════════════════════════════

class TestLevelA_AutoModeH4Fixture:
    """H4 headings become chunk boundaries in auto mode but not in explicit mode."""

    def _load(self) -> str:
        return _H4_FIXTURE.read_text(encoding="utf-8")

    # ── H4 chunk boundaries ───────────────────────────────────────────────

    def test_auto_h4_appears_in_breadcrumbs(self):
        chunks = chunk_markdown(self._load(), "Service Design", _auto_cfg())
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert any("Versioning Strategy" in b for b in breadcrumbs), (
            "H4 'Versioning Strategy' should become a chunk boundary in auto mode. "
            f"Got breadcrumbs: {breadcrumbs}"
        )
        assert any("Status Codes" in b for b in breadcrumbs), (
            "H4 'Status Codes' should become a chunk boundary in auto mode. "
            f"Got breadcrumbs: {breadcrumbs}"
        )
        assert any("Unit of Work" in b for b in breadcrumbs), (
            "H4 'Unit of Work' should become a chunk boundary in auto mode."
        )
        # "Connection Management" is a short H4 (< min_tokens) so it is merged BACKWARD
        # into the preceding "Unit of Work" chunk (trailing-section fix) rather than dropped.
        combined = " ".join(c.text for c in chunks)
        assert "Connection pooling is handled exclusively" in combined, (
            "Connection Management body text must be backward-merged into the adjacent chunk, "
            "not dropped, even though it is the last section and below min_tokens."
        )

    def test_explicit_h4_not_in_breadcrumbs(self):
        chunks = chunk_markdown(self._load(), "Service Design", _explicit_cfg())
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert not any("Versioning Strategy" in b for b in breadcrumbs), (
            "H4 should NOT be a chunk boundary in explicit [1,2,3] mode."
        )
        assert not any("Status Codes" in b for b in breadcrumbs), (
            "H4 should NOT be a chunk boundary in explicit [1,2,3] mode."
        )

    def test_auto_produces_more_chunks_than_explicit(self):
        text = self._load()
        auto_n = len(chunk_markdown(text, "Service Design", _auto_cfg()))
        explicit_n = len(chunk_markdown(text, "Service Design", _explicit_cfg()))
        assert auto_n > explicit_n, (
            f"Auto mode should produce more chunks due to H4 boundaries: "
            f"auto={auto_n}, explicit={explicit_n}"
        )

    # ── Content completeness ──────────────────────────────────────────────

    def test_auto_h4_content_not_lost(self):
        """Text that sits exclusively under H4 headings must appear in auto-mode chunks."""
        chunks = chunk_markdown(self._load(), "Service Design", _auto_cfg())
        combined = " ".join(c.text for c in chunks)
        # These phrases appear only inside the H4 "Versioning Strategy" section.
        assert "Never break existing versions" in combined
        assert "Deprecation requires a minimum six-month notice period" in combined
        # These phrases appear only inside the H4 "Status Codes" section.
        assert "Use 201 for successful POST" in combined
        assert "Use 409 for conflicts" in combined

    def test_explicit_h4_content_present_inside_parent_chunk(self):
        """In explicit mode H4 content is NOT a separate chunk — it's merged into the parent."""
        chunks = chunk_markdown(self._load(), "Service Design", _explicit_cfg())
        combined = " ".join(c.text for c in chunks)
        # Content still exists (it wasn't dropped), just bundled with the parent H3 chunk.
        assert "Never break existing versions" in combined
        assert "Use 201 for successful POST" in combined


class TestLevelA_AutoModeShortSectionsFixture:
    """Short sections are merged in auto mode instead of being silently dropped."""

    def _load(self) -> str:
        return _SHORT_FIXTURE.read_text(encoding="utf-8")

    # The "See Also" section body is a single line: "See MIGRATION.md for step-by-step upgrade instructions."
    # With min_tokens=40 that is well below the threshold → dropped in explicit, merged in auto.

    def test_explicit_drops_short_section(self):
        chunks = chunk_markdown(self._load(), "Release Notes", _explicit_cfg(min_tokens=40))
        combined = " ".join(c.text for c in chunks)
        assert "See MIGRATION.md" not in combined, (
            "Explicit mode should drop the short 'See Also' section (< min_tokens=40). "
            f"Got combined text: {combined[:300]}"
        )

    def test_auto_preserves_short_section_via_merge(self):
        chunks = chunk_markdown(self._load(), "Release Notes", _auto_cfg(min_tokens=40))
        combined = " ".join(c.text for c in chunks)
        assert "See MIGRATION.md" in combined, (
            "Auto mode must merge the short 'See Also' section into an adjacent chunk "
            "rather than dropping it. Combined text does not contain the expected phrase."
        )

    def test_auto_no_content_loss(self):
        """Every section's key phrase must be present in the auto-mode output."""
        chunks = chunk_markdown(self._load(), "Release Notes", _auto_cfg(min_tokens=40))
        combined = " ".join(c.text for c in chunks)
        # Version 2.0 section
        assert "Major rewrite of the order processing subsystem" in combined
        # See Also (short, must be merged)
        assert "See MIGRATION.md" in combined
        # Breaking Changes section
        assert "The CreateOrder method now requires a UserId parameter" in combined
        # Deprecated APIs section
        assert "LegacyOrderService" in combined

    def test_auto_vs_explicit_chunk_count(self):
        """Auto mode produces the same or more chunks than explicit when min_tokens causes drops."""
        text = self._load()
        auto_n   = len(chunk_markdown(text, "Release Notes", _auto_cfg(min_tokens=40)))
        explicit_n = len(chunk_markdown(text, "Release Notes", _explicit_cfg(min_tokens=40)))
        # Explicit drops the short section, auto merges it — auto should have at least as many.
        assert auto_n >= explicit_n, (
            f"Auto should not lose chunks vs explicit: auto={auto_n}, explicit={explicit_n}"
        )


class TestLevelA_ExplicitH4Mode:
    """Explicit split_on_headings=[1,2,3,4] splits at H4 headings (like auto mode does
    for H4-depth docs), but does NOT split at H5."""

    def _load_h4(self) -> str:
        return _H4_FIXTURE.read_text(encoding="utf-8")

    def test_explicit_1234_h4_appears_in_breadcrumbs(self):
        chunks = chunk_markdown(self._load_h4(), "Service Design", _explicit_h4_cfg())
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert any(
            "Versioning Strategy" in b or "Status Codes" in b or "Unit of Work" in b
            for b in breadcrumbs
        ), (
            "Explicit [1,2,3,4] should split at H4 headings and produce H4-level breadcrumbs. "
            f"Got: {breadcrumbs}"
        )

    def test_explicit_1234_produces_more_chunks_than_123(self):
        text = self._load_h4()
        h4_n = len(chunk_markdown(text, "Service Design", _explicit_h4_cfg()))
        h3_n = len(chunk_markdown(text, "Service Design", _explicit_cfg()))
        assert h4_n > h3_n, (
            f"Explicit [1,2,3,4] should produce more chunks than [1,2,3]: h4={h4_n}, h3={h3_n}"
        )

    def test_explicit_1234_h4_content_not_lost(self):
        chunks = chunk_markdown(self._load_h4(), "Service Design", _explicit_h4_cfg())
        combined = " ".join(c.text for c in chunks)
        assert "Never break existing versions" in combined
        assert "Deprecation requires a minimum six-month notice period" in combined
        assert "Use 201 for successful POST" in combined
        assert "Use 409 for conflicts" in combined
        # Note: "Connection Management" is a small trailing H4 (< min_tokens=40 in
        # tiktoken cl100k_base units). Explicit mode drops small chunks rather than
        # merging them — this is the expected behavior. Auto mode would preserve it.

    def test_explicit_1234_drops_small_trailing_section_but_auto_preserves_it(self):
        """Explicit mode drops sub-threshold trailing sections; auto mode backward-merges them."""
        explicit_combined = " ".join(
            c.text for c in chunk_markdown(self._load_h4(), "Service Design", _explicit_h4_cfg())
        )
        auto_combined = " ".join(
            c.text for c in chunk_markdown(self._load_h4(), "Service Design", _auto_cfg())
        )
        # Auto mode must preserve the trailing "Connection Management" content.
        assert "Connection pooling is handled exclusively" in auto_combined
        # Explicit mode drops it (< min_tokens=40).
        assert "Connection pooling is handled exclusively" not in explicit_combined


class TestLevelA_H5Fixture:
    """H5 headings: explicit [1,2,3,4] ignores H5; auto mode splits at H5."""

    def _load(self) -> str:
        return _H5_FIXTURE.read_text(encoding="utf-8")

    def test_explicit_1234_h5_not_in_breadcrumbs(self):
        """[1,2,3,4] must NOT create H5-level split boundaries."""
        chunks = chunk_markdown(self._load(), "Architecture Guide", _explicit_h4_cfg())
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert not any(
            "Synchronous Path" in b or "Asynchronous Path" in b
            for b in breadcrumbs
        ), (
            "Explicit [1,2,3,4] must not split at H5 headings. "
            f"Got breadcrumbs: {breadcrumbs}"
        )

    def test_explicit_1234_h5_content_preserved_inside_h4_chunk(self):
        chunks = chunk_markdown(self._load(), "Architecture Guide", _explicit_h4_cfg())
        combined = " ".join(c.text for c in chunks)
        assert "synchronous request path is the primary processing route" in combined.lower()
        assert "asynchronous request path queues work items" in combined.lower()

    def test_auto_h5_appears_in_breadcrumbs(self):
        """Auto mode detects H5 as the deepest heading level and splits there."""
        chunks = chunk_markdown(self._load(), "Architecture Guide", _auto_cfg())
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert any(
            "Synchronous Path" in b or "Asynchronous Path" in b
            for b in breadcrumbs
        ), (
            "Auto mode should split at H5 and produce H5-level breadcrumbs. "
            f"Got: {breadcrumbs}"
        )

    def test_auto_produces_more_chunks_than_explicit_1234_for_h5_doc(self):
        text = self._load()
        auto_n     = len(chunk_markdown(text, "Architecture Guide", _auto_cfg()))
        explicit_n = len(chunk_markdown(text, "Architecture Guide", _explicit_h4_cfg()))
        assert auto_n > explicit_n, (
            f"Auto mode should produce more chunks than explicit [1,2,3,4] for H5 doc: "
            f"auto={auto_n}, explicit={explicit_n}"
        )


# ═════════════════════════════════════════════════════════════════════════════
# LEVEL B — Ingest pipeline → Qdrant
# Requires: QDRANT_URL env var
# Uses the ONNX model for real embeddings; skips when model is absent.
# ═════════════════════════════════════════════════════════════════════════════

def _make_qdrant_client(url: str):
    from qdrant_client import QdrantClient
    return QdrantClient(url=url)


def _ingest_fixture_to_qdrant(
    text: str,
    rel_path: str,
    collection: str,
    qdrant_url: str,
    model_dir: Path,
) -> int:
    """Chunk, embed (real model), and upsert one fixture doc. Returns the number of points upserted."""
    import hashlib
    from qdrant_client.models import Distance, PointStruct, VectorParams
    from sentence_transformers import SentenceTransformer

    chunks = chunk_markdown(text, _file_title(text, rel_path), _auto_cfg())
    if not chunks:
        return 0

    model_path = str(model_dir)
    model = SentenceTransformer(model_path, device="cpu")
    client = _make_qdrant_client(qdrant_url)
    dim = model.get_sentence_embedding_dimension()

    client.recreate_collection(
        collection_name=collection,
        vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
    )

    vectors = model.encode(
        [c.embed_text for c in chunks],
        normalize_embeddings=True,
        show_progress_bar=False,
    )

    def _id(bc: str, line: int) -> int:
        h = hashlib.blake2b(f"{rel_path}|{bc}|{line}".encode(), digest_size=8)
        return int.from_bytes(h.digest(), "big")

    points = [
        PointStruct(
            id=_id(c.breadcrumb, c.start_line),
            vector=v.tolist(),
            payload={
                "rel_path": rel_path,
                "breadcrumb": c.breadcrumb,
                "text": c.text,
                "start_line": c.start_line,
                "token_count": c.token_count,
            },
        )
        for c, v in zip(chunks, vectors)
    ]
    client.upsert(collection_name=collection, points=points)
    return len(points)


def _file_title(text: str, fallback: str) -> str:
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("# "):
            return s[2:].strip()
    return fallback


def _scroll_all(client, collection: str) -> list[dict]:
    """Return all payload dicts from the collection via scroll."""
    results, _ = client.scroll(collection_name=collection, limit=500, with_payload=True)
    return [r.payload or {} for r in results]


@pytest.mark.skipif(not _qdrant_url(), reason="QDRANT_URL not set — Level B skipped")
class TestLevelB_IngestPipeline:
    """Level B: chunk + embed + upsert a fixture file, assert on stored Qdrant payloads."""

    @pytest.fixture(autouse=True)
    def _require_model(self):
        if _model_dir() is None:
            pytest.skip("ONNX model not found — Level B skipped. "
                        "Set RAG_MODEL_DIR or place model.onnx in tools/rag-dotnet/model/")

    # ── H4 fixture ────────────────────────────────────────────────────────

    def test_h4_chunks_have_h4_breadcrumbs_in_qdrant(self):
        """After ingesting the H4 fixture, Qdrant must contain points whose breadcrumbs
        include H4 heading names — proving the H4 sections were split by the chunker."""
        url = _qdrant_url()
        collection = f"e2e_lv_b_h4_{uuid.uuid4().hex[:8]}"
        text = _H4_FIXTURE.read_text(encoding="utf-8")

        count = _ingest_fixture_to_qdrant(text, "auto-mode-h4-sections.md", collection, url, _model_dir())
        assert count > 0, "Expected at least one point to be upserted"

        client = _make_qdrant_client(url)
        payloads = _scroll_all(client, collection)
        breadcrumbs = [p.get("breadcrumb", "") for p in payloads]

        assert any("Versioning Strategy" in b for b in breadcrumbs), (
            "Qdrant should contain a point with 'Versioning Strategy' in its breadcrumb. "
            f"Stored breadcrumbs: {breadcrumbs}"
        )
        assert any("Status Codes" in b for b in breadcrumbs), (
            "Qdrant should contain a point with 'Status Codes' in its breadcrumb."
        )
        assert any("Unit of Work" in b for b in breadcrumbs), (
            "Qdrant should contain a point with 'Unit of Work' in its breadcrumb."
        )

        # Clean up.
        client.delete_collection(collection)

    def test_h4_chunk_count_exceeds_explicit_mode(self):
        """Auto-mode ingest stores more chunks than explicit-mode ingest for the same doc."""
        url = _qdrant_url()
        model = _model_dir()
        text = _H4_FIXTURE.read_text(encoding="utf-8")
        rel_path = "auto-mode-h4-sections.md"

        auto_col     = f"e2e_lv_b_auto_{uuid.uuid4().hex[:8]}"
        explicit_col = f"e2e_lv_b_expl_{uuid.uuid4().hex[:8]}"

        # Ingest with auto mode.
        auto_n = _ingest_fixture_to_qdrant(text, rel_path, auto_col, url, model)

        # Ingest with explicit mode (monkey-patch config in chunk_markdown via direct call).
        from sentence_transformers import SentenceTransformer
        from qdrant_client.models import Distance, PointStruct, VectorParams
        import hashlib

        explicit_chunks = chunk_markdown(text, _file_title(text, rel_path), _explicit_cfg())
        mod = SentenceTransformer(str(model), device="cpu")
        dim = mod.get_sentence_embedding_dimension()
        client = _make_qdrant_client(url)
        client.recreate_collection(
            collection_name=explicit_col,
            vectors_config=VectorParams(size=dim, distance=Distance.COSINE),
        )
        vecs = mod.encode([c.embed_text for c in explicit_chunks], normalize_embeddings=True,
                          show_progress_bar=False)

        def _eid(bc: str, line: int) -> int:
            h = hashlib.blake2b(f"{rel_path}|{bc}|{line}".encode(), digest_size=8)
            return int.from_bytes(h.digest(), "big")

        client.upsert(collection_name=explicit_col, points=[
            PointStruct(id=_eid(c.breadcrumb, c.start_line), vector=v.tolist(),
                        payload={"breadcrumb": c.breadcrumb})
            for c, v in zip(explicit_chunks, vecs)
        ])
        explicit_n = len(explicit_chunks)

        assert auto_n > explicit_n, (
            f"Auto mode should store more chunks than explicit due to H4 splits: "
            f"auto={auto_n}, explicit={explicit_n}"
        )

        client.delete_collection(auto_col)
        client.delete_collection(explicit_col)

    # ── Short-sections fixture ─────────────────────────────────────────────

    def test_short_section_text_present_in_qdrant_payload(self):
        """Auto mode must merge the short 'See Also' section — its text must appear in Qdrant."""
        url = _qdrant_url()
        collection = f"e2e_lv_b_short_{uuid.uuid4().hex[:8]}"
        text = _SHORT_FIXTURE.read_text(encoding="utf-8")

        _ingest_fixture_to_qdrant(text, "auto-mode-short-sections.md", collection, url, _model_dir())

        client = _make_qdrant_client(url)
        payloads = _scroll_all(client, collection)
        all_text = " ".join(p.get("text", "") for p in payloads)

        assert "See MIGRATION.md" in all_text, (
            "The short 'See Also' section body should be preserved in Qdrant payload "
            "via auto-mode merging. It was not found in any stored chunk text."
        )

        client.delete_collection(collection)


# ═════════════════════════════════════════════════════════════════════════════
# LEVEL C — Semantic query level
# Requires: QDRANT_URL env var + ONNX model
# ═════════════════════════════════════════════════════════════════════════════

@pytest.mark.skipif(not _qdrant_url(), reason="QDRANT_URL not set — Level C skipped")
class TestLevelC_QueryLevel:
    """Level C: ingest fixture → semantic query → verify H4 content is findable."""

    @pytest.fixture(autouse=True)
    def _require_model(self):
        if _model_dir() is None:
            pytest.skip("ONNX model not found — Level C skipped.")

    def _query(self, query_text: str, collection: str, qdrant_url: str, model_dir: Path,
               top_k: int = 5) -> list[dict]:
        """Embed query + vector-search Qdrant, return payloads of top hits."""
        from sentence_transformers import SentenceTransformer
        model = SentenceTransformer(str(model_dir), device="cpu")
        qvec = model.encode([query_text], normalize_embeddings=True)[0].tolist()
        client = _make_qdrant_client(qdrant_url)
        results = client.search(collection_name=collection, query_vector=qvec, limit=top_k)
        return [{"payload": r.payload or {}, "score": r.score} for r in results]

    # ── H4 fixture ────────────────────────────────────────────────────────

    def test_h4_content_findable_by_semantic_search(self):
        """Text that lives only inside an H4 section should be retrievable by semantic search
        after auto-mode ingest — verifying end-to-end chunker → embed → query correctness."""
        url = _qdrant_url()
        model = _model_dir()
        collection = f"e2e_lv_c_h4_{uuid.uuid4().hex[:8]}"
        text = _H4_FIXTURE.read_text(encoding="utf-8")

        _ingest_fixture_to_qdrant(text, "auto-mode-h4-sections.md", collection, url, model)

        # Query with a phrase about API versioning (lives inside H4 "Versioning Strategy").
        hits = self._query("API versioning breaking changes deprecation policy", collection, url, model)

        assert len(hits) > 0, "Expected at least one search result for H4 content query."
        top_payload = hits[0]["payload"]
        assert hits[0]["score"] > 0.1, (
            f"Expected a meaningful similarity score, got {hits[0]['score']:.3f}"
        )
        # The top result should be from the Versioning Strategy H4 chunk.
        top_text = top_payload.get("text", "") + " " + top_payload.get("breadcrumb", "")
        assert any(phrase in top_text for phrase in
                   ["Versioning Strategy", "version", "deprecat", "breaking"]), (
            f"Top hit does not seem related to the H4 versioning section. "
            f"Breadcrumb: {top_payload.get('breadcrumb')}, text[:150]: {top_payload.get('text', '')[:150]}"
        )

        _make_qdrant_client(url).delete_collection(collection)

    def test_h4_status_codes_findable(self):
        """HTTP status codes section (H4) should be retrievable by a status-code query."""
        url = _qdrant_url()
        model = _model_dir()
        collection = f"e2e_lv_c_sc_{uuid.uuid4().hex[:8]}"
        text = _H4_FIXTURE.read_text(encoding="utf-8")

        _ingest_fixture_to_qdrant(text, "auto-mode-h4-sections.md", collection, url, model)

        hits = self._query("HTTP response status codes 200 201 404 409", collection, url, model)

        assert len(hits) > 0
        found = any(
            "Status Codes" in (h["payload"].get("breadcrumb") or "")
            or "201" in (h["payload"].get("text") or "")
            for h in hits
        )
        assert found, (
            "Expected at least one hit to come from the 'Status Codes' H4 chunk. "
            f"Got: {[(h['payload'].get('breadcrumb'), h['score']) for h in hits]}"
        )

        _make_qdrant_client(url).delete_collection(collection)

    # ── Short-sections fixture ─────────────────────────────────────────────

    def test_merged_short_section_content_is_searchable(self):
        """The merged 'See Also' content should be discoverable via semantic search
        — proving that merging preserved the text in the vector index."""
        url = _qdrant_url()
        model = _model_dir()
        collection = f"e2e_lv_c_short_{uuid.uuid4().hex[:8]}"
        text = _SHORT_FIXTURE.read_text(encoding="utf-8")

        _ingest_fixture_to_qdrant(text, "auto-mode-short-sections.md", collection, url, model)

        # Query about the short "See Also" content — migration instructions.
        hits = self._query("migration upgrade instructions guide", collection, url, model)

        assert len(hits) > 0
        combined = " ".join(h["payload"].get("text", "") for h in hits)
        assert "MIGRATION.md" in combined or "upgrade" in combined.lower(), (
            "Expected the merged short section content to appear in search results. "
            f"Got texts: {[h['payload'].get('text', '')[:80] for h in hits]}"
        )

        _make_qdrant_client(url).delete_collection(collection)
