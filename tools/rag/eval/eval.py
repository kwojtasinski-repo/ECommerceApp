"""Evaluate RAG retrieval quality.

Loads tools/rag/eval/questions.json and reports:
- recall@5  — share of questions whose top-5 contains at least one expected substring
- recall@8
- mean rank of the first expected hit (lower is better)
- per-question breakdown of any failures (no expected hit in top-8)

Usage:
    python tools/rag/eval/eval.py
    python tools/rag/eval/eval.py --top-k 5
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

# Allow `import query` etc. when this script runs directly.
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from query import QueryEngine  # noqa: E402

QUESTIONS_PATH = Path(__file__).resolve().parent / "questions.json"


def _first_match_rank(hits, expected_any: list[str]) -> int | None:
    for i, h in enumerate(hits, 1):
        for needle in expected_any:
            if needle in h.rel_path:
                return i
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--top-k", type=int, default=8, help="Fetch this many hits per query")
    args = parser.parse_args()

    data = json.loads(QUESTIONS_PATH.read_text(encoding="utf-8"))
    all_questions = data["questions"]
    # Skip auto-generated questions not yet confirmed by a human.
    questions = [q for q in all_questions if q.get("reviewed", True) is not False]
    skipped = len(all_questions) - len(questions)
    if skipped:
        print(f"[eval] Skipping {skipped} unreviewed auto-generated question(s)")

    engine = QueryEngine()
    hits_at_k = []
    failures = []
    for entry in questions:
        q = entry["q"]
        expected = entry["expect_any"]
        hits = engine.search(q, top_k=args.top_k, fetch_k=max(args.top_k, 20))
        rank = _first_match_rank(hits, expected)
        hits_at_k.append(rank)
        if rank is None:
            failures.append((q, expected, [(h.rel_path, h.final_score) for h in hits[:3]]))

    n = len(questions)
    r5 = sum(1 for r in hits_at_k if r is not None and r <= 5) / n
    r8 = sum(1 for r in hits_at_k if r is not None and r <= 8) / n
    mean_rank = sum(r for r in hits_at_k if r is not None) / max(1, sum(1 for r in hits_at_k if r is not None))

    print(f"Questions: {n}")
    print(f"recall@5:  {r5:.2%}")
    print(f"recall@8:  {r8:.2%}")
    print(f"mean rank of first expected hit: {mean_rank:.2f}")
    if failures:
        print(f"\n{len(failures)} failure(s):")
        for q, expected, top3 in failures:
            print(f"\n  Q: {q}")
            print(f"     expected one of: {expected}")
            print(f"     top-3 actually returned:")
            for path, score in top3:
                print(f"       {score:.3f}  {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
