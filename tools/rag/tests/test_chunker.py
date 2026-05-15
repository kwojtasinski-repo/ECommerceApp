"""Tests for chunker.py — heading-aware markdown chunker.

Run with:
    cd tools/rag
    python -m pytest tests/test_chunker.py -v

No external dependencies beyond tiktoken (already in requirements.txt).
"""
import pytest

# The chunker module uses tiktoken at import time. Make sure PYTHONPATH includes tools/rag.
from chunker import chunk_markdown, Chunk, count_tokens

# ── Shared config fixture ─────────────────────────────────────────────────────

DEFAULT_CFG = {
    "split_on_headings": [1, 2, 3],
    "max_tokens": 800,
    "min_tokens": 1,   # very low so short test fixtures are not dropped
    "overlap_tokens": 80,
}

SMALL_CFG = {
    "split_on_headings": [1, 2, 3],
    "max_tokens": 30,
    "min_tokens": 1,
    "overlap_tokens": 8,
}


def chunk(text: str, title: str = "doc", cfg: dict = None) -> list[Chunk]:
    return chunk_markdown(text, title, cfg or DEFAULT_CFG)


# ── Basic splitting ───────────────────────────────────────────────────────────

def test_empty_document_returns_no_chunks():
    assert chunk("") == []


def test_whitespace_only_returns_no_chunks():
    assert chunk("   \n\n\t  ") == []


def test_single_section_returns_one_chunk():
    md = "# Title\n\nThis is a paragraph with some content."
    result = chunk(md)
    assert len(result) == 1


def test_two_h2_sections_return_two_chunks():
    md = "## Section A\n\nFirst section body.\n\n## Section B\n\nSecond section body."
    result = chunk(md)
    assert len(result) == 2


def test_h3_is_a_split_boundary():
    md = "## Parent\n\nParent body.\n\n### Child\n\nChild body."
    result = chunk(md)
    assert len(result) == 2


def test_h4_is_not_a_split_boundary_by_default():
    """H4 is not in split_on_headings=[1,2,3]; body stays in same section."""
    md = "## Section\n\nBody text.\n\n#### Not a boundary\n\nMore text."
    result = chunk(md)
    # Everything folds into one section.
    assert len(result) == 1


# ── Breadcrumb ────────────────────────────────────────────────────────────────

def test_breadcrumb_contains_doc_title():
    md = "## Section\n\nSome body text here."
    result = chunk(md, title="My Document")
    assert all("My Document" in c.breadcrumb for c in result)


def test_breadcrumb_contains_heading_title():
    md = "## Feature Alpha\n\nSome body text here."
    result = chunk(md)
    assert any("Feature Alpha" in c.breadcrumb for c in result)


def test_breadcrumb_chains_h1_h2_with_arrow():
    md = "# Parent\n\nIntro.\n\n## Child\n\nChild body text."
    result = chunk(md)
    child_chunk = result[-1]
    assert " > " in child_chunk.breadcrumb
    assert "Child" in child_chunk.breadcrumb


def test_embed_text_prepends_breadcrumb():
    md = "## Section\n\nSome body text here for testing."
    result = chunk(md)
    for c in result:
        assert c.embed_text.startswith(c.breadcrumb)


# ── Heading path ──────────────────────────────────────────────────────────────

def test_heading_path_matches_path_list():
    md = "## Section\n\nBody here."
    result = chunk(md, title="doc")
    c = result[0]
    assert c.heading_path == ["doc", "Section"]


# ── min_tokens guard ──────────────────────────────────────────────────────────

def test_section_below_min_tokens_is_dropped():
    cfg = {**DEFAULT_CFG, "min_tokens": 100}
    md = "## Tiny\n\nHi."
    result = chunk(md, cfg=cfg)
    assert result == []


def test_section_above_min_tokens_is_kept():
    md = "## Section\n\nHello world foo bar baz."
    result = chunk(md)  # min_tokens = 1
    assert len(result) == 1


# ── Code-fence handling ───────────────────────────────────────────────────────

def test_heading_inside_code_fence_is_not_a_split():
    md = (
        "## Real Section\n\n"
        "Some body text.\n\n"
        "```markdown\n"
        "## Fake heading inside fence\n"
        "```\n\n"
        "More body text after the fence."
    )
    result = chunk(md)
    # Everything is one section — fenced heading must NOT split.
    assert len(result) == 1


def test_fence_toggle_is_tracked_correctly():
    """Opening and closing fence toggles the fence state; a second heading after the fence DOES split."""
    md = (
        "## Section A\n\n"
        "```\n"
        "code block\n"
        "```\n\n"
        "## Section B\n\n"
        "Body of B."
    )
    result = chunk(md)
    assert len(result) == 2


# ── YAML front-matter stripping ───────────────────────────────────────────────

def test_frontmatter_is_stripped():
    md = "---\ntitle: Test\n---\n## Section\n\nBody text here."
    result = chunk(md)
    assert len(result) == 1
    # Body text must not include front-matter content.
    assert "title: Test" not in result[0].text


def test_frontmatter_line_offset_applied_to_start_line():
    md = "---\ntitle: Test\ndate: 2024\n---\n## Section\n\nBody text here."
    result = chunk(md)
    c = result[0]
    # 4 lines of front-matter → start_line should be > 1.
    assert c.start_line > 1


# ── Token count metadata ──────────────────────────────────────────────────────

def test_token_count_is_positive():
    md = "## Section\n\nSome body text."
    result = chunk(md)
    assert all(c.token_count > 0 for c in result)


def test_token_count_matches_count_tokens():
    md = "## Section\n\nSome body text with several words."
    result = chunk(md)
    for c in result:
        assert c.token_count == count_tokens(c.text)


# ── Oversized sections → paragraph splitting with overlap ────────────────────

def test_oversized_section_is_split_into_multiple_chunks():
    """A section that exceeds max_tokens is split at paragraph boundaries."""
    # Each paragraph ~8 tokens; 10 paragraphs > max_tokens=30.
    paragraphs = [f"Paragraph {i} has some words." for i in range(10)]
    md = "## Big Section\n\n" + "\n\n".join(paragraphs)
    result = chunk(md, cfg=SMALL_CFG)
    assert len(result) > 1


def test_all_chunks_respect_max_tokens():
    paragraphs = [f"Paragraph {i} has some words." for i in range(15)]
    md = "## Section\n\n" + "\n\n".join(paragraphs)
    result = chunk(md, cfg=SMALL_CFG)
    assert all(c.token_count <= SMALL_CFG["max_tokens"] for c in result)


def test_overlap_produces_repeated_content_in_adjacent_chunks():
    """With overlap > 0 the last paragraph of chunk N should appear in chunk N+1."""
    cfg = {**SMALL_CFG, "overlap_tokens": 10}
    paragraphs = [f"Paragraph {i} is long enough." for i in range(20)]
    md = "## Section\n\n" + "\n\n".join(paragraphs)
    result = chunk(md, cfg=cfg)
    if len(result) >= 2:
        # The last paragraph of the first chunk must appear somewhere in the second.
        last_para_of_first = result[0].text.split("\n\n")[-1]
        assert last_para_of_first in result[1].text


# ── Line numbers ──────────────────────────────────────────────────────────────

def test_start_line_is_positive():
    md = "## Section\n\nBody text here."
    result = chunk(md)
    assert all(c.start_line >= 1 for c in result)


def test_end_line_gte_start_line():
    md = "## A\n\nBody A.\n\n## B\n\nBody B."
    result = chunk(md)
    assert all(c.end_line >= c.start_line for c in result)


# ── count_tokens helper ───────────────────────────────────────────────────────

def test_count_tokens_empty_returns_zero():
    assert count_tokens("") == 0


def test_count_tokens_is_positive_for_nonempty():
    assert count_tokens("hello world") > 0


def test_count_tokens_longer_text_returns_more():
    short = count_tokens("hello")
    long = count_tokens("hello world foo bar baz qux quux")
    assert long > short
