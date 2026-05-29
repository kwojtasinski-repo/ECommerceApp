"""Immutable, JSON-round-trippable per-collection configuration payload.

Mirrors the .NET ``RagConfigPayload`` (ADR-0028 amendment 005). Three rules
prevent scattered conditionals in callers:

1. Frozen dataclass — no in-place mutation, safe to share across threads.
2. ``None`` means "fall back to mounted default" — single sentinel.
3. :func:`merge_payloads` is the only place that knows override-wins semantics.
"""
from __future__ import annotations

from dataclasses import dataclass, field, replace
from typing import Any

SCHEMA_VERSION = 2


@dataclass(frozen=True, slots=True)
class GlossaryEntry:
    english: str
    patterns: tuple[str, ...] = ()

    def to_dict(self) -> dict[str, Any]:
        return {"english": self.english, "patterns": list(self.patterns)}

    @classmethod
    def from_dict(cls, raw: dict[str, Any]) -> "GlossaryEntry":
        return cls(
            english=str(raw.get("english", "")),
            patterns=tuple(str(p) for p in raw.get("patterns", []) or ()),
        )


@dataclass(frozen=True, slots=True)
class WeightEntry:
    pattern: str
    multiplier: float

    def to_dict(self) -> dict[str, Any]:
        return {"pattern": self.pattern, "multiplier": self.multiplier}

    @classmethod
    def from_dict(cls, raw: dict[str, Any]) -> "WeightEntry":
        return cls(
            pattern=str(raw.get("pattern", "")),
            multiplier=float(raw.get("multiplier", 1.0)),
        )


@dataclass(frozen=True, slots=True)
class RagConfigPayload:
    """Per-collection settings persisted at Qdrant point id=0.

    All optional fields use ``None`` (scalars) or empty tuple (collections) to
    mean "no override — keep the mounted default". Consumers fall back exactly
    once at the boundary; no scattered ``if payload else cfg`` branches.
    """

    schema_version: int = SCHEMA_VERSION
    max_tokens: int | None = None
    overlap_tokens: int | None = None
    weights: tuple[WeightEntry, ...] = ()
    glossary_entries: tuple[GlossaryEntry, ...] = ()
    history_field: str | None = None
    adr_doc_kind: str | None = None
    amendment_doc_kind: str | None = None

    # ── JSON round-trip ───────────────────────────────────────────────────

    def to_dict(self) -> dict[str, Any]:
        return {
            "schema_version": self.schema_version,
            "max_tokens": self.max_tokens,
            "overlap_tokens": self.overlap_tokens,
            "weights": [w.to_dict() for w in self.weights],
            "glossary_entries": [g.to_dict() for g in self.glossary_entries],
            "history_field": self.history_field,
            "adr_doc_kind": self.adr_doc_kind,
            "amendment_doc_kind": self.amendment_doc_kind,
        }

    @classmethod
    def from_dict(cls, raw: dict[str, Any] | None) -> "RagConfigPayload":
        if not raw:
            return cls()
        return cls(
            schema_version=int(raw.get("schema_version", SCHEMA_VERSION)),
            max_tokens=_as_optional_int(raw.get("max_tokens")),
            overlap_tokens=_as_optional_int(raw.get("overlap_tokens")),
            weights=tuple(WeightEntry.from_dict(w) for w in raw.get("weights", []) or ()),
            glossary_entries=tuple(
                GlossaryEntry.from_dict(g) for g in raw.get("glossary_entries", []) or ()
            ),
            history_field=_as_optional_str(raw.get("history_field")),
            adr_doc_kind=_as_optional_str(raw.get("adr_doc_kind")),
            amendment_doc_kind=_as_optional_str(raw.get("amendment_doc_kind")),
        )

    # ── Construction from mounted Config ─────────────────────────────────

    @classmethod
    def from_mounted(cls, cfg: Any) -> "RagConfigPayload":
        """Build the file-mode default payload from the mounted :class:`Config`.

        Reads only fields that ADR-0028 amendment 005 puts under per-collection
        scope. ``MinTokens`` / ``StubByteThreshold`` deliberately stay mounted
        (operator concerns).
        """
        chunker = cfg.chunker if cfg is not None else {}
        ranking = cfg.ranking if cfg is not None else {}
        query = cfg.query_defaults if cfg is not None else {}

        return cls(
            schema_version=SCHEMA_VERSION,
            max_tokens=_as_optional_int(chunker.get("max_tokens")),
            overlap_tokens=_as_optional_int(chunker.get("overlap_tokens")),
            weights=tuple(
                WeightEntry(pattern=str(w.get("pattern", "")), multiplier=float(w.get("multiplier", 1.0)))
                for w in ranking.get("weights", []) or ()
                if w.get("pattern")
            ),
            glossary_entries=(),  # mounted glossary loaded separately by preprocessor
            history_field=_as_optional_str(query.get("history_field")),
            adr_doc_kind=_as_optional_str(
                (cfg.raw.get("metadata_rules", {}) if cfg is not None else {}).get("adr_doc_kind")
            ),
            amendment_doc_kind=_as_optional_str(
                (cfg.raw.get("metadata_rules", {}) if cfg is not None else {}).get("amendment_doc_kind")
            ),
        )


def merge_payloads(
    base: RagConfigPayload, override: RagConfigPayload | None
) -> RagConfigPayload:
    """Override-wins per-field merge. Used by :class:`LayeredConfigSource`.

    Scalar fields: ``override`` wins unless it is ``None``.
    Tuple fields: ``override`` wins unless it is empty.
    """
    if override is None:
        return base
    return replace(
        base,
        schema_version=max(base.schema_version, override.schema_version),
        max_tokens=override.max_tokens if override.max_tokens is not None else base.max_tokens,
        overlap_tokens=override.overlap_tokens
        if override.overlap_tokens is not None
        else base.overlap_tokens,
        weights=override.weights or base.weights,
        glossary_entries=override.glossary_entries or base.glossary_entries,
        history_field=override.history_field
        if override.history_field is not None
        else base.history_field,
        adr_doc_kind=override.adr_doc_kind
        if override.adr_doc_kind is not None
        else base.adr_doc_kind,
        amendment_doc_kind=override.amendment_doc_kind
        if override.amendment_doc_kind is not None
        else base.amendment_doc_kind,
    )


def _as_optional_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _as_optional_str(value: Any) -> str | None:
    if value is None:
        return None
    s = str(value)
    return s if s else None
