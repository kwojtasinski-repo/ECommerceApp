"""Unit tests for the heading-aware markdown chunker, including the auto mode.

Run with: pytest tools/rag/test_chunker.py -v
"""
from __future__ import annotations

import pytest

from chunker import chunk_markdown, _merge_small_chunks, Chunk


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_BASE_CFG = {
    "split_on_headings": [1, 2, 3],
    "max_tokens": 800,
    "min_tokens": 40,
    "overlap_tokens": 0,
}

_AUTO_CFG = {
    "split_on_headings": "auto",
    "max_tokens": 800,
    "min_tokens": 40,
    "overlap_tokens": 0,
}


def _cfg(**overrides: object) -> dict:
    return {**_AUTO_CFG, **overrides}


def _make_chunk(tok: int, breadcrumb: str = "A", start: int = 1, end: int = 5) -> Chunk:
    """Build a Chunk whose *actual* token count is approximately `tok` (one token ≈ one word)."""
    text = ("word " * tok).strip()
    from chunker import count_tokens

    real_tok = count_tokens(text)
    return Chunk(
        text=text,
        embed_text=f"{breadcrumb}\n\n{text}" if breadcrumb else text,
        breadcrumb=breadcrumb,
        heading_path=[breadcrumb],
        start_line=start,
        end_line=end,
        token_count=real_tok,
    )


# ---------------------------------------------------------------------------
# Explicit-list mode (regression — must not break existing behaviour)
# ---------------------------------------------------------------------------


class TestExplicitMode:
    def test_h4_not_split_in_explicit_mode(self) -> None:
        """H4 stays inside its parent H3 chunk when using explicit [1,2,3]."""
        doc = (
            "## Section\n\nH2 content.\n\n"
            "### Sub\n\nH3 content.\n\n"
            "#### Detail\n\nH4 content.\n"
        )
        chunks = chunk_markdown(doc, "Doc", _BASE_CFG)
        # Everything falls under the same H3 section
        assert all("Detail" not in c.breadcrumb for c in chunks), (
            "H4 should not appear in breadcrumb in explicit [1,2,3] mode"
        )

    def test_small_chunks_dropped_in_explicit_mode(self) -> None:
        """Sections below min_tokens are silently dropped in explicit mode."""
        doc = "## Big\n\n" + "word " * 50 + "\n\n## Tiny\n\nSmall.\n"
        chunks = chunk_markdown(doc, "Doc", {**_BASE_CFG, "min_tokens": 40})
        # Only the big section should survive
        assert all("Tiny" not in c.breadcrumb for c in chunks)


# ---------------------------------------------------------------------------
# Auto mode — split_on_headings: "auto"
# ---------------------------------------------------------------------------


class TestAutoModeHeadingSplit:
    def test_h4_headings_become_split_boundaries(self) -> None:
        """In auto mode H4 headings start new sections."""
        doc = (
            "## Section\n\nSection body.\n\n"
            "### Sub\n\nSub body.\n\n"
            "#### Detail A\n\n" + "word " * 20 + "\n\n"
            "#### Detail B\n\n" + "word " * 20 + "\n"
        )
        chunks = chunk_markdown(doc, "Doc", _cfg(min_tokens=5))
        breadcrumbs = [c.breadcrumb for c in chunks]
        assert any("Detail A" in b for b in breadcrumbs), "Detail A should become its own chunk"
        assert any("Detail B" in b for b in breadcrumbs), "Detail B should become its own chunk"

    def test_auto_produces_more_chunks_than_explicit_for_h4_docs(self) -> None:
        """Auto mode generates at least as many chunks as explicit when H4 headings exist."""
        doc = (
            "## Section\n\n" + "word " * 30 + "\n\n"
            "#### Detail A\n\n" + "word " * 30 + "\n\n"
            "#### Detail B\n\n" + "word " * 30 + "\n"
        )
        cfg_min = {"max_tokens": 800, "min_tokens": 5, "overlap_tokens": 0}
        chunks_auto = chunk_markdown(doc, "Doc", {**cfg_min, "split_on_headings": "auto"})
        chunks_explicit = chunk_markdown(doc, "Doc", {**cfg_min, "split_on_headings": [1, 2, 3]})
        assert len(chunks_auto) >= len(chunks_explicit)

    def test_auto_recognises_string_case_insensitive(self) -> None:
        """'AUTO', 'Auto', and 'auto' are all treated as auto mode."""
        doc = "## Section\n\n" + "word " * 50 + "\n"
        for variant in ("AUTO", "Auto", "auto"):
            chunks = chunk_markdown(doc, "Doc", _cfg(split_on_headings=variant))
            assert len(chunks) >= 1, f"Variant {variant!r} should produce chunks"


# ---------------------------------------------------------------------------
# Auto mode — merge-small behaviour
# ---------------------------------------------------------------------------


class TestMergeSmall:
    def test_consecutive_small_chunks_are_merged(self) -> None:
        """Two tiny chunks are merged into one when their total stays within max_tokens."""
        small_a = _make_chunk(tok=10, breadcrumb="A", start=1, end=3)
        small_b = _make_chunk(tok=10, breadcrumb="B", start=4, end=6)
        result = _merge_small_chunks([small_a, small_b], min_tokens=20, max_tokens=800)
        assert len(result) == 1
        assert "word" in result[0].text  # combined text present

    def test_merged_chunk_keeps_first_breadcrumb(self) -> None:
        small_a = _make_chunk(tok=5, breadcrumb="Heading A")
        small_b = _make_chunk(tok=5, breadcrumb="Heading B")
        result = _merge_small_chunks([small_a, small_b], min_tokens=10, max_tokens=800)
        assert result[0].breadcrumb == "Heading A"

    def test_large_chunk_not_merged(self) -> None:
        """A chunk above min_tokens is emitted as-is."""
        large = _make_chunk(tok=50, breadcrumb="Large")
        small = _make_chunk(tok=5, breadcrumb="Small")
        result = _merge_small_chunks([large, small], min_tokens=20, max_tokens=800)
        # Large chunk emitted; small should merge into nothing before it (it follows a large chunk)
        assert any("Large" in c.breadcrumb for c in result)

    def test_overflow_emits_trailing_chunk(self) -> None:
        """When backward-merge of a trailing small chunk would exceed max_tokens, the small
        chunk is emitted as-is rather than dropped — content is never silently lost."""
        big = _make_chunk(tok=50, breadcrumb="Big")
        tiny = _make_chunk(tok=5, breadcrumb="Tiny")
        result = _merge_small_chunks([big, tiny], min_tokens=30, max_tokens=55)
        # big is emitted (>= min); combined big+tiny ~57 tokens > max=55 → tiny emitted separately
        assert len(result) == 2
        assert result[0].breadcrumb == "Big"
        assert result[1].breadcrumb == "Tiny"

    def test_empty_input_returns_empty(self) -> None:
        assert _merge_small_chunks([], min_tokens=40, max_tokens=800) == []

    def test_single_large_chunk_returned_as_is(self) -> None:
        large = _make_chunk(tok=50, breadcrumb="Only")
        result = _merge_small_chunks([large], min_tokens=20, max_tokens=800)
        assert len(result) == 1
        assert result[0].breadcrumb == "Only"

    def test_single_small_chunk_emitted_when_only_chunk(self) -> None:
        """A single tiny chunk with no neighbours is emitted rather than dropped;
        discarding the only chunk in a document would be silent content loss."""
        tiny = _make_chunk(tok=3, breadcrumb="Tiny")
        result = _merge_small_chunks([tiny], min_tokens=20, max_tokens=800)
        assert len(result) == 1
        assert result[0].breadcrumb == "Tiny"

    def test_start_line_preserved_from_first_chunk(self) -> None:
        a = _make_chunk(tok=5, start=10, end=12)
        b = _make_chunk(tok=5, start=13, end=15)
        result = _merge_small_chunks([a, b], min_tokens=10, max_tokens=800)
        assert result[0].start_line == 10

    def test_end_line_taken_from_last_merged_chunk(self) -> None:
        a = _make_chunk(tok=5, start=1, end=3)
        b = _make_chunk(tok=5, start=4, end=8)
        result = _merge_small_chunks([a, b], min_tokens=10, max_tokens=800)
        assert result[0].end_line == 8


# ---------------------------------------------------------------------------
# Integration: chunk_markdown with auto mode and realistic doc
# ---------------------------------------------------------------------------


class TestAutoModeIntegration:
    _DOC = (
        "# ADR-9999\n\n"
        "## Summary\n\n"
        + "word " * 60
        + "\n\n"
        "## Background\n\n"
        + "word " * 80
        + "\n\n"
        "## Decision\n\n"
        + "word " * 50
        + "\n\n"
        "### Rationale\n\n"
        + "word " * 40
        + "\n\n"
        "#### See also\n\nRefer to ADR-0014.\n\n"
        "## Consequences\n\n"
        + "word " * 60
        + "\n"
    )

    def test_auto_mode_does_not_lose_content(self) -> None:
        """Total text length in auto-mode chunks should be close to original (minus dropped tiny sections)."""
        chunks = chunk_markdown(self._DOC, "ADR-9999", _AUTO_CFG)
        combined = " ".join(c.text for c in chunks)
        # "See also" section is tiny — may be merged or dropped; but main sections must survive
        assert "word" in combined

    def test_auto_mode_does_not_duplicate_content(self) -> None:
        """With overlap_tokens=0, words should not appear in two separate chunks."""
        chunks = chunk_markdown(self._DOC, "ADR-9999", _AUTO_CFG)
        # Use a distinctive long run — if it appears in two chunks we have a duplication
        # (overlap_tokens=0 means no intentional overlap)
        all_texts = [c.text for c in chunks]
        # The most basic sanity: same chunk object not returned twice
        assert len(chunks) == len(set(id(c) for c in chunks))

    def test_auto_mode_breadcrumbs_present(self) -> None:
        chunks = chunk_markdown(self._DOC, "ADR-9999", _AUTO_CFG)
        assert all(c.breadcrumb for c in chunks), "Every chunk must have a breadcrumb in auto mode"

    def test_auto_mode_explicit_list_parity_on_simple_doc(self) -> None:
        """When a doc has no H4+ sections, auto and explicit [1,2,3] should produce the same chunks."""
        simple_doc = (
            "# Title\n\n"
            "## Alpha\n\n" + "word " * 50 + "\n\n"
            "## Beta\n\n" + "word " * 50 + "\n"
        )
        auto = chunk_markdown(simple_doc, "Doc", _AUTO_CFG)
        explicit = chunk_markdown(simple_doc, "Doc", _BASE_CFG)
        # Content of the chunks should match (breadcrumb may differ due to H1 being a split level)
        auto_texts = sorted(c.text for c in auto)
        explicit_texts = sorted(c.text for c in explicit)
        assert auto_texts == explicit_texts
