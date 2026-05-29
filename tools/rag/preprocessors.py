"""Built-in embedding preprocessors.

Each class satisfies the :class:`EmbedderPreprocessor` Protocol — no inheritance
required.  Register them via :class:`PipelinedEmbedder` or
:func:`make_embedder`.
"""
from __future__ import annotations

import re

from embed_context import EmbedContext, EmbedPurpose


class GlossaryExpansionPreprocessor:
    """Appends English synonym terms for non-English patterns found in the query.

    Only runs when ``ctx.purpose == EmbedPurpose.QUERY`` — ingest texts are
    returned unchanged because adding synonyms to document chunks would distort
    the embedding and waste token budget.
    """

    def __init__(
        self,
        glossary: list[tuple[str, list[str]]],
        repeat: int = 3,
    ) -> None:
        """
        Parameters
        ----------
        glossary:
            List of ``(english_term, [pattern, ...])`` pairs.  Loaded from
            ``multilingual-glossary.yaml`` by :func:`make_embedder`.
        repeat:
            Number of times the English expansion is repeated so it outweighs
            the non-English source tokens in mean pooling.
        """
        self._glossary = glossary
        self._repeat = repeat

    def process(self, text: str, ctx: EmbedContext) -> str:
        if ctx.purpose != EmbedPurpose.QUERY:
            return text
        # Per-collection override from EmbedContext — used by the HTTP multi-tenant path.
        # When glossary_entries is present (even if empty) it supersedes the mounted default.
        if ctx.glossary_entries is not None:
            effective: list[tuple[str, list[str]]] = [
                (e.english, list(e.patterns)) for e in ctx.glossary_entries
            ]
            return _expand(text, effective, self._repeat)
        return _expand(text, self._glossary, self._repeat)


class LengthTruncationPreprocessor:
    """Hard-truncates text to a maximum word count.

    Applies to both ``QUERY`` and ``INGEST`` purposes.  Protects the model's
    token budget when the input exceeds the context window.
    """

    def __init__(self, max_words: int = 512) -> None:
        self._max_words = max_words

    def process(self, text: str, ctx: EmbedContext) -> str:
        words = text.split()
        if len(words) <= self._max_words:
            return text
        return " ".join(words[: self._max_words])


# ── Private helper ────────────────────────────────────────────────────────────

def _expand(query: str, glossary: list[tuple[str, list[str]]], repeat: int) -> str:
    """Append English synonym groups for any non-English patterns found in *query*.

    Uses whole-word-ish boundary matching (same logic as the original
    ``_expand_query`` in ``query.py``) to avoid false positives on substrings.
    """
    lower = query.lower()
    additions: list[str] = []
    for english, patterns in glossary:
        for pattern in patterns:
            if re.search(r"(?<![a-z])" + re.escape(pattern) + r"(?![a-z])", lower):
                additions.append(english)
                break  # one match per glossary entry is enough
    if not additions:
        return query
    expansion = " ".join(additions)
    return query + (" " + expansion) * repeat
