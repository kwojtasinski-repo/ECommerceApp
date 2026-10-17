# Rejected Information (Phase 3, step 3 — consolidated)

Date: 2026-10-14 · Question: agent patterns/skills from the given sources (no recommendations) · Scope: 18 URLs + extension (13 per-pattern articles, Self-evolving/AiP series, 7 additional AutoGen pages).

## Out of scope (another module/component)

| # | Information | Source | Why rejected | Re-include if |
|---|---|---|---|---|
| R1 | Full incident-triage YAMLs repeated in AiP 3 and 4 | `28`, `29` | duplicate of the YAML from `27` (replaced by a cross-reference) | a diff between versions is needed |
| R2 | Domain details of the examples (40 MB logs, tickets, customer records) | `22`–`25` | a specific domain, not a pattern | analysis of the benchmark itself |
| R3 | PyFlink configuration (`make fresh`, venv), DGX Spark hardware | `45`, `42` | environment details, not agent patterns | deploying this infrastructure |
| R4 | Narrative openings/"Until next time", teasers | all posts | no analytical content | analysis of style/tone |
| R5 | Flattened Mermaid and sequence diagrams | sources | copying artifact, no information | access to the original diagrams |
| R6 | The output of the generated story in group-chat | `36` | generated text, not a pattern | evaluation of output quality |
| R7 | Outgoing links (arXiv 2406.11776 sparse topology, aegis repo, tool-use pages) | `39`, `03` | outside the URL list; not fetched | scope extended to external verification of debate effectiveness |

## Solution instead of problem / opinion

| # | Information | Source | Why rejected | Re-include if |
|---|---|---|---|---|
| R8 | The "Outcome" of apis-at-massive-scale ("didn't even break a sweat") | `30` | self-description without numbers | traffic data appears |
| R9 | The numbers "90% fast track", "80% of use case" | `43` | unsupported | the author provides the methodology |
| R10 | A paraphrased list of 12 patterns from a Medium mirror | gatherer `medium` | unverified domain, paraphrase | the user provides the article text |

## Gaps instead of rejections
Medium (HTTP 403), Mastra (chapter content behind an email gate), AiP Parts 5–7 — see `01-cross-source-verification.md` §3.

## Summary
- Findings in the `*-raw` files: about 490 `##` sections (some are auxiliary sections), 1,448 quoted fragments, 94.8% verbatim matches with the source files.
- Rejected groups: ~10 (in each findings file from 5 to 9 rows of the "Rejected Information" table).
- Nothing was rejected as "untrue" — the rejections concern scope and lack of support.
