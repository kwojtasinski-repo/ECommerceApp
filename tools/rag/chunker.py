"""Heading-aware markdown chunker with size guard, overlap, and breadcrumb prepending.

Chunking strategy (matches docs/rag/README.md):
1. Split the document at H1/H2/H3 boundaries (configurable via chunker.split_on_headings).
2. Build a breadcrumb of all ancestor headings for each chunk
   (e.g. "ADR-0014 Sales/Orders > §3 Order aggregate > Status transitions").
3. If a chunk exceeds chunker.max_tokens, split it on paragraph boundaries with rolling overlap.
4. Drop chunks below chunker.min_tokens (too generic for a useful embedding).
5. Prepend the breadcrumb to the embed_text so semantic similarity captures the section context.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Iterable

import tiktoken

# Use the cl100k_base encoder as a portable token approximation. This matches OpenAI tokenizers
# closely enough for size accounting; the actual embedder has its own (smaller) limit which we
# stay safely under via max_tokens.
_ENC = tiktoken.get_encoding("cl100k_base")

_HEADING_RE = re.compile(r"^(#{1,6})\s+(.+?)\s*$")
_FENCE_RE = re.compile(r"^```")


def count_tokens(text: str) -> int:
    return len(_ENC.encode(text))


@dataclass
class Chunk:
    text: str                  # raw markdown content of the chunk (without breadcrumb)
    embed_text: str            # text fed to the embedder (breadcrumb prepended)
    breadcrumb: str            # "Doc title > H2 > H3"
    heading_path: list[str] = field(default_factory=list)
    start_line: int = 0
    end_line: int = 0
    token_count: int = 0


@dataclass
class _Section:
    """An intermediate representation: one heading + its body text until the next same-or-higher heading."""
    level: int                 # 0 = document root, 1..6 = heading level
    title: str                 # heading text (or document title for root)
    start_line: int
    body_lines: list[str] = field(default_factory=list)
    children: list["_Section"] = field(default_factory=list)


def _strip_frontmatter(text: str) -> tuple[str, int]:
    """Strip a leading YAML front-matter block. Returns (text_without_fm, lines_skipped)."""
    if not text.startswith("---\n"):
        return text, 0
    end = text.find("\n---\n", 4)
    if end == -1:
        return text, 0
    skipped = text.count("\n", 0, end + 5)
    return text[end + 5 :], skipped


def _parse_sections(text: str, split_levels: list[int]) -> _Section:
    root = _Section(level=0, title="", start_line=1)
    stack: list[_Section] = [root]
    in_fence = False
    for idx, line in enumerate(text.splitlines(), start=1):
        if _FENCE_RE.match(line):
            in_fence = not in_fence
            stack[-1].body_lines.append(line)
            continue
        if not in_fence:
            m = _HEADING_RE.match(line)
            if m and len(m.group(1)) in split_levels:
                level = len(m.group(1))
                title = m.group(2).strip()
                while stack and stack[-1].level >= level:
                    stack.pop()
                node = _Section(level=level, title=title, start_line=idx)
                if not stack:
                    stack.append(root)
                stack[-1].children.append(node)
                stack.append(node)
                continue
        stack[-1].body_lines.append(line)
    return root


def _walk(section: _Section, ancestors: list[str]) -> Iterable[tuple[list[str], _Section]]:
    path = ancestors + ([section.title] if section.title else [])
    yield path, section
    for child in section.children:
        yield from _walk(child, path)


def _split_oversized(body: str, start_line: int, max_tokens: int, overlap_tokens: int) -> list[tuple[str, int, int]]:
    """Split a body that is too long into smaller pieces on paragraph boundaries with rolling overlap.

    Returns a list of (piece_text, start_line_offset_within_body, end_line_offset_within_body).
    """
    paragraphs = re.split(r"\n\s*\n", body)
    pieces: list[tuple[str, int, int]] = []
    current: list[str] = []
    current_tokens = 0
    line_cursor = 0   # 0-based offset within the body
    piece_start = 0

    def flush() -> None:
        nonlocal current, current_tokens, piece_start, line_cursor
        if not current:
            return
        joined = "\n\n".join(current)
        end = line_cursor
        pieces.append((joined, piece_start, end))
        # Build overlap: keep trailing paragraphs whose combined tokens are <= overlap_tokens.
        if overlap_tokens > 0:
            tail: list[str] = []
            tail_tokens = 0
            for para in reversed(current):
                t = count_tokens(para)
                if tail_tokens + t > overlap_tokens:
                    break
                tail.insert(0, para)
                tail_tokens += t
            current = tail
            current_tokens = tail_tokens
            piece_start = end - sum(p.count("\n") + 2 for p in current)
        else:
            current = []
            current_tokens = 0
            piece_start = end

    for para in paragraphs:
        para_lines = para.count("\n") + 1
        ptokens = count_tokens(para)
        if current_tokens + ptokens > max_tokens and current:
            flush()
        current.append(para)
        current_tokens += ptokens
        line_cursor += para_lines + 1   # +1 for the blank separator line
    if current:
        joined = "\n\n".join(current)
        pieces.append((joined, piece_start, line_cursor))
    # Convert offsets to absolute line numbers
    return [(t, start_line + s, start_line + e) for (t, s, e) in pieces]


def chunk_markdown(text: str, doc_title: str, chunker_cfg: dict) -> list[Chunk]:
    text, fm_lines = _strip_frontmatter(text)
    split_levels: list[int] = chunker_cfg.get("split_on_headings", [1, 2, 3])
    max_tokens: int = int(chunker_cfg.get("max_tokens", 800))
    min_tokens: int = int(chunker_cfg.get("min_tokens", 40))
    overlap_tokens: int = int(chunker_cfg.get("overlap_tokens", 80))

    root = _parse_sections(text, split_levels)
    chunks: list[Chunk] = []
    for path, section in _walk(root, [doc_title] if doc_title else []):
        body = "\n".join(section.body_lines).strip()
        if not body:
            continue
        breadcrumb = " > ".join(p for p in path if p)
        # Account for the leading frontmatter so start_line maps to the actual file line.
        start_line = section.start_line + fm_lines
        token_count = count_tokens(body)
        if token_count <= max_tokens:
            if token_count < min_tokens:
                continue
            embed_text = f"{breadcrumb}\n\n{body}" if breadcrumb else body
            chunks.append(
                Chunk(
                    text=body,
                    embed_text=embed_text,
                    breadcrumb=breadcrumb,
                    heading_path=list(path),
                    start_line=start_line,
                    end_line=start_line + body.count("\n"),
                    token_count=token_count,
                )
            )
        else:
            for piece, sl, el in _split_oversized(body, start_line, max_tokens, overlap_tokens):
                t = count_tokens(piece)
                if t < min_tokens:
                    continue
                embed_text = f"{breadcrumb}\n\n{piece}" if breadcrumb else piece
                chunks.append(
                    Chunk(
                        text=piece,
                        embed_text=embed_text,
                        breadcrumb=breadcrumb,
                        heading_path=list(path),
                        start_line=sl,
                        end_line=el,
                        token_count=t,
                    )
                )
    return chunks
