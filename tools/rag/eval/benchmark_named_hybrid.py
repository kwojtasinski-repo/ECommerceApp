from __future__ import annotations

import argparse
import json
import re
import time
from dataclasses import dataclass
from pathlib import Path

import yaml

# Keep imports local to the repo layout.
import sys

RAG_DIR = Path(__file__).resolve().parents[1]
if str(RAG_DIR) not in sys.path:
    sys.path.insert(0, str(RAG_DIR))

from query import QueryEngine, QueryHit  # noqa: E402


@dataclass
class NamedProfile:
    name: str
    question: str
    doc_kind: str | None = None
    adr_id: str | None = None
    top_k: int | None = None


def _tokenize(text: str) -> set[str]:
    return set(re.findall(r"[a-z0-9_]+", text.lower()))


def _match_score(question: str, profile: NamedProfile) -> float:
    q = _tokenize(question)
    p = _tokenize(profile.question)
    if not q or not p:
        return 0.0
    overlap = len(q & p)
    return overlap / max(1, len(p))


def _first_relevant_rank(hits: list[QueryHit], expect_any: list[str]) -> int | None:
    needles = [s.lower() for s in expect_any]
    for i, h in enumerate(hits, start=1):
        rp = h.rel_path.lower()
        if any(n in rp for n in needles):
            return i
    return None


def _rrf_fuse(result_lists: list[list[QueryHit]], top_k: int, k: int = 60) -> list[QueryHit]:
    scores: dict[str, float] = {}
    keep: dict[str, QueryHit] = {}

    for hits in result_lists:
        for rank, h in enumerate(hits, start=1):
            key = f"{h.rel_path}:{h.start_line}:{h.end_line}"
            scores[key] = scores.get(key, 0.0) + 1.0 / (k + rank)
            if key not in keep:
                keep[key] = h

    ordered = sorted(scores.items(), key=lambda kv: kv[1], reverse=True)
    return [keep[key] for key, _ in ordered[:top_k]]


def _load_questions(path: Path, limit: int) -> list[dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    qs = data.get("questions", [])
    return qs[:limit] if limit > 0 else qs


def _load_profiles(path: Path) -> list[NamedProfile]:
    raw = yaml.safe_load(path.read_text(encoding="utf-8")) or {}
    out: list[NamedProfile] = []
    for q in raw.get("named_queries", []):
        out.append(
            NamedProfile(
                name=q.get("name", ""),
                question=q.get("question", ""),
                doc_kind=q.get("doc_kind"),
                adr_id=q.get("adr_id"),
                top_k=q.get("top_k"),
            )
        )
    return out


def _summarize(name: str, rows: list[dict]) -> dict:
    n = max(1, len(rows))
    hit1 = sum(1 for r in rows if r["rank"] == 1) / n
    recall5 = sum(1 for r in rows if r["rank"] is not None and r["rank"] <= 5) / n
    mrr = sum((1.0 / r["rank"]) if r["rank"] else 0.0 for r in rows) / n
    latencies = sorted(r["latency_ms"] for r in rows)
    p50 = latencies[int(0.50 * (len(latencies) - 1))]
    p95 = latencies[int(0.95 * (len(latencies) - 1))]
    return {
        "mode": name,
        "n": n,
        "hit_at_1": hit1,
        "recall_at_5": recall5,
        "mrr": mrr,
        "p50_ms": p50,
        "p95_ms": p95,
        "total_ms": sum(r["latency_ms"] for r in rows),
    }


def run(limit: int, top_k: int, fetch_k: int, profile_top_n: int, min_confidence: float) -> int:
    questions_path = RAG_DIR / "eval" / "questions.json"
    profiles_path = RAG_DIR / "queries.yaml"

    questions = _load_questions(questions_path, limit)
    profiles = _load_profiles(profiles_path)
    engine = QueryEngine()

    # One warm-up call to avoid counting lazy model/client init in only the first mode.
    _ = engine.search("warmup", top_k=1, fetch_k=max(5, fetch_k))

    baseline_rows: list[dict] = []
    hybrid_rows: list[dict] = []
    fallback_count = 0
    profile_used_total = 0

    for item in questions:
        q = item["q"]
        expect_any = item.get("expect_any", [])

        t0 = time.perf_counter()
        baseline_hits = engine.search(q, top_k=top_k, fetch_k=fetch_k)
        t1 = time.perf_counter()
        baseline_rank = _first_relevant_rank(baseline_hits, expect_any)
        baseline_rows.append({"rank": baseline_rank, "latency_ms": (t1 - t0) * 1000.0})

        scored = sorted(
            ((p, _match_score(q, p)) for p in profiles),
            key=lambda x: x[1],
            reverse=True,
        )
        selected = [p for p, s in scored[:profile_top_n] if s >= min_confidence]

        t2 = time.perf_counter()
        hybrid_free_hits = engine.search(q, top_k=top_k, fetch_k=fetch_k)
        if not selected:
            fused = hybrid_free_hits
            fallback_count += 1
        else:
            result_lists: list[list[QueryHit]] = [hybrid_free_hits]
            for p in selected:
                filt = None
                if p.adr_id:
                    filt = ("adr_id", p.adr_id)
                elif p.doc_kind:
                    filt = ("doc_kind", p.doc_kind)
                profile_k = p.top_k if isinstance(p.top_k, int) and p.top_k > 0 else fetch_k
                ph = engine.search(
                    p.question,
                    top_k=min(max(profile_k, top_k), max(fetch_k, profile_k)),
                    fetch_k=max(fetch_k, profile_k),
                    field_filter=filt,
                )
                result_lists.append(ph)
            fused = _rrf_fuse(result_lists, top_k=top_k)
            profile_used_total += len(selected)
        t3 = time.perf_counter()

        hybrid_rank = _first_relevant_rank(fused, expect_any)
        hybrid_rows.append({"rank": hybrid_rank, "latency_ms": (t3 - t2) * 1000.0})

    b = _summarize("baseline", baseline_rows)
    h = _summarize("hybrid_named", hybrid_rows)

    print("BENCHMARK SUMMARY")
    print(f"questions={len(questions)} top_k={top_k} fetch_k={fetch_k}")
    print(f"profile_top_n={profile_top_n} min_confidence={min_confidence}")
    print("")
    print(
        "baseline "
        f"recall@5={b['recall_at_5']:.3f} hit@1={b['hit_at_1']:.3f} mrr={b['mrr']:.3f} "
        f"p50={b['p50_ms']:.1f}ms p95={b['p95_ms']:.1f}ms total={b['total_ms']:.0f}ms"
    )
    print(
        "hybrid   "
        f"recall@5={h['recall_at_5']:.3f} hit@1={h['hit_at_1']:.3f} mrr={h['mrr']:.3f} "
        f"p50={h['p50_ms']:.1f}ms p95={h['p95_ms']:.1f}ms total={h['total_ms']:.0f}ms"
    )
    print(
        "delta    "
        f"recall@5={(h['recall_at_5'] - b['recall_at_5']):+.3f} "
        f"hit@1={(h['hit_at_1'] - b['hit_at_1']):+.3f} "
        f"mrr={(h['mrr'] - b['mrr']):+.3f}"
    )
    print("")
    print(
        f"hybrid_stats fallback_questions={fallback_count}/{len(questions)} "
        f"avg_profiles_when_used={(profile_used_total / max(1, len(questions) - fallback_count)) if (len(questions) - fallback_count) else 0:.2f}"
    )

    return 0


def main() -> int:
    p = argparse.ArgumentParser(description="Benchmark baseline vs named-query-assisted hybrid retrieval")
    p.add_argument("--limit", type=int, default=30, help="How many eval questions to run (default: 30)")
    p.add_argument("--top-k", type=int, default=5)
    p.add_argument("--fetch-k", type=int, default=20)
    p.add_argument("--profile-top-n", type=int, default=2)
    p.add_argument("--min-confidence", type=float, default=0.15)
    args = p.parse_args()
    return run(
        limit=args.limit,
        top_k=args.top_k,
        fetch_k=args.fetch_k,
        profile_top_n=args.profile_top_n,
        min_confidence=args.min_confidence,
    )


if __name__ == "__main__":
    raise SystemExit(main())
