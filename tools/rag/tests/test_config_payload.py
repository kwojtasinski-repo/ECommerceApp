"""Tests for ``config.payload`` — dataclass, JSON round-trip, merge semantics."""
from __future__ import annotations

import json
from types import SimpleNamespace

import pytest

from config.payload import (
    SCHEMA_VERSION,
    GlossaryEntry,
    RagConfigPayload,
    WeightEntry,
    merge_payloads,
)


# ── Defaults ────────────────────────────────────────────────────────────────

class TestDefaults:
    def test_default_payload_is_all_none_or_empty(self) -> None:
        p = RagConfigPayload()
        assert p.schema_version == SCHEMA_VERSION
        assert p.max_tokens is None
        assert p.overlap_tokens is None
        assert p.weights == ()
        assert p.glossary_entries == ()
        assert p.history_field is None

    def test_payload_is_frozen(self) -> None:
        p = RagConfigPayload(max_tokens=10)
        with pytest.raises((AttributeError, TypeError)):
            p.max_tokens = 20  # type: ignore[misc]

    def test_payload_is_hashable(self) -> None:
        p1 = RagConfigPayload(max_tokens=10)
        p2 = RagConfigPayload(max_tokens=10)
        assert hash(p1) == hash(p2)


# ── JSON round-trip ─────────────────────────────────────────────────────────

class TestJsonRoundTrip:
    def test_empty_payload_round_trips(self) -> None:
        p = RagConfigPayload()
        assert RagConfigPayload.from_dict(p.to_dict()) == p

    def test_full_payload_round_trips(self) -> None:
        p = RagConfigPayload(
            max_tokens=512,
            overlap_tokens=64,
            weights=(WeightEntry("docs/**", 1.5), WeightEntry(".github/**", 1.2)),
            glossary_entries=(GlossaryEntry("orders", ("zamówienia", "bestellungen")),),
            history_field="adr_id",
            adr_doc_kind="adr",
            amendment_doc_kind="amendment",
        )
        assert RagConfigPayload.from_dict(p.to_dict()) == p

    def test_json_serialized_form_is_stable(self) -> None:
        """Wire format should be deterministic so identical payloads
        produce identical Qdrant payload_json strings."""
        p = RagConfigPayload(max_tokens=10, history_field="x")
        s1 = json.dumps(p.to_dict(), sort_keys=True)
        s2 = json.dumps(p.to_dict(), sort_keys=True)
        assert s1 == s2

    def test_from_dict_tolerates_missing_fields(self) -> None:
        p = RagConfigPayload.from_dict({"max_tokens": 100})
        assert p.max_tokens == 100
        assert p.overlap_tokens is None
        assert p.weights == ()

    def test_from_dict_handles_none_input(self) -> None:
        assert RagConfigPayload.from_dict(None) == RagConfigPayload()
        assert RagConfigPayload.from_dict({}) == RagConfigPayload()

    def test_from_dict_coerces_numeric_strings(self) -> None:
        p = RagConfigPayload.from_dict({"max_tokens": "42", "overlap_tokens": "8"})
        assert p.max_tokens == 42
        assert p.overlap_tokens == 8

    def test_from_dict_drops_invalid_numerics(self) -> None:
        p = RagConfigPayload.from_dict({"max_tokens": "not-a-number"})
        assert p.max_tokens is None


# ── from_mounted ────────────────────────────────────────────────────────────

class TestFromMounted:
    def _stub_cfg(self, *, chunker=None, ranking=None, query=None, metadata_rules=None):
        return SimpleNamespace(
            chunker=chunker or {},
            ranking=ranking or {},
            query_defaults=query or {},
            raw={"metadata_rules": metadata_rules or {}},
        )

    def test_returns_default_when_cfg_is_empty(self) -> None:
        p = RagConfigPayload.from_mounted(self._stub_cfg())
        assert p == RagConfigPayload()

    def test_reads_chunker_token_settings(self) -> None:
        p = RagConfigPayload.from_mounted(
            self._stub_cfg(chunker={"max_tokens": 256, "overlap_tokens": 32})
        )
        assert p.max_tokens == 256
        assert p.overlap_tokens == 32

    def test_reads_ranking_weights(self) -> None:
        p = RagConfigPayload.from_mounted(
            self._stub_cfg(
                ranking={
                    "weights": [
                        {"pattern": "docs/**", "multiplier": 1.5},
                        {"pattern": ".github/**", "multiplier": 1.2},
                    ]
                }
            )
        )
        assert p.weights == (
            WeightEntry("docs/**", 1.5),
            WeightEntry(".github/**", 1.2),
        )

    def test_skips_weight_entries_without_pattern(self) -> None:
        p = RagConfigPayload.from_mounted(
            self._stub_cfg(ranking={"weights": [{"pattern": "", "multiplier": 1.5}]})
        )
        assert p.weights == ()

    def test_reads_history_field_and_doc_kinds(self) -> None:
        p = RagConfigPayload.from_mounted(
            self._stub_cfg(
                query={"history_field": "rfc_id"},
                metadata_rules={"adr_doc_kind": "rfc", "amendment_doc_kind": "rfc-amend"},
            )
        )
        assert p.history_field == "rfc_id"
        assert p.adr_doc_kind == "rfc"
        assert p.amendment_doc_kind == "rfc-amend"

    def test_handles_none_cfg(self) -> None:
        """Defensive: a callable passing ``None`` should not crash."""
        p = RagConfigPayload.from_mounted(None)
        assert p == RagConfigPayload()


# ── merge_payloads ─────────────────────────────────────────────────────────

class TestMergePayloads:
    def test_override_is_none_returns_base(self) -> None:
        base = RagConfigPayload(max_tokens=10)
        assert merge_payloads(base, None) is base

    def test_override_wins_when_scalar_set(self) -> None:
        base = RagConfigPayload(max_tokens=10, overlap_tokens=2)
        override = RagConfigPayload(max_tokens=99)
        merged = merge_payloads(base, override)
        assert merged.max_tokens == 99
        assert merged.overlap_tokens == 2  # base wins because override is None

    def test_override_none_scalar_keeps_base(self) -> None:
        base = RagConfigPayload(history_field="adr_id")
        override = RagConfigPayload(history_field=None)
        assert merge_payloads(base, override).history_field == "adr_id"

    def test_override_wins_when_tuple_non_empty(self) -> None:
        base = RagConfigPayload(weights=(WeightEntry("a", 1.0),))
        override = RagConfigPayload(weights=(WeightEntry("b", 2.0),))
        merged = merge_payloads(base, override)
        assert merged.weights == (WeightEntry("b", 2.0),)

    def test_override_empty_tuple_keeps_base(self) -> None:
        base = RagConfigPayload(weights=(WeightEntry("a", 1.0),))
        override = RagConfigPayload(weights=())
        merged = merge_payloads(base, override)
        assert merged.weights == (WeightEntry("a", 1.0),)

    def test_glossary_override_replaces_base(self) -> None:
        base = RagConfigPayload(glossary_entries=(GlossaryEntry("x", ("p",)),))
        override = RagConfigPayload(glossary_entries=(GlossaryEntry("y", ("q",)),))
        merged = merge_payloads(base, override)
        assert merged.glossary_entries == (GlossaryEntry("y", ("q",)),)

    def test_schema_version_uses_higher(self) -> None:
        base = RagConfigPayload(schema_version=1)
        override = RagConfigPayload(schema_version=2)
        assert merge_payloads(base, override).schema_version == 2


# ── GlossaryEntry / WeightEntry ────────────────────────────────────────────

class TestGlossaryEntry:
    def test_round_trip(self) -> None:
        g = GlossaryEntry("orders", ("zamówienia", "bestellungen"))
        assert GlossaryEntry.from_dict(g.to_dict()) == g

    def test_from_dict_handles_missing_patterns(self) -> None:
        g = GlossaryEntry.from_dict({"english": "x"})
        assert g.patterns == ()


class TestWeightEntry:
    def test_round_trip(self) -> None:
        w = WeightEntry("docs/**", 1.25)
        assert WeightEntry.from_dict(w.to_dict()) == w

    def test_from_dict_defaults_multiplier_to_1(self) -> None:
        w = WeightEntry.from_dict({"pattern": "x"})
        assert w.multiplier == 1.0
