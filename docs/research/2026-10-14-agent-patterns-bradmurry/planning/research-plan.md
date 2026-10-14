# Research Plan

## Overview
Collect and cross-verify the content of 18 given URLs (14 bradmurry.com/software posts, 1 Medium article, 1 Mastra book, 2 AutoGen pages) on AI-agent design patterns and production use. Downstream (out of scope here): synthesis and a multi-agent debate.

Type: **external**. Self-check against the question text: it requires only knowledge from outside the organisation (the listed URLs). There is no internal source (code, ticket or transcript) to query, so the classification holds. ECommerceApp is context for a later step, not a source for this phase.

## Scope and Success Criteria
Taken from the brief (`research-brief.md`).

- In scope: agent patterns, architectures and skills in the 18 URLs. Specifically: reactive reducer, FSM+LLM, HITL, self-evolving agents, agents in production, AutoGen multi-agent debate mechanics, and production guidance (reliability, contracts, configuration, scale).
- Out of scope: synthesis, recommendation, the final report, running the debate, and sources outside the list (except a basic-fact check).
- Deliverables: per-source patterns/concepts/skills with quotes and confidence; a deduplicated pattern catalog; debate mechanics (roles, rounds, aggregation); production-suitable patterns with justification; contradictions and gaps.
- Quality: every finding has a URL and a quote. No facts from training memory. Unavailable pages are logged as gaps.

## Actors
None — research is actor-agnostic.

## Information Layers
- Big Picture: which problem classes the patterns solve, and why agents fail in production.
- Modeling: FSM, reducer, states, contracts, DSL/AI partitioning.
- Implementation: AutoGen, Mastra, Flink, sparkctl, configuration, APIs.

Each gatherer should tag findings with a layer.

## Methodology
- Primary: fetch each URL and extract patterns, concepts and skills as verbatim quotes with a confidence level.
- Each finding follows the Findings File shape: Source (URL + quote), Evidence, Confidence, then a Rejected Information section. No Declarative Conclusions section (external sources only).
- Confidence is based on how explicit the quote is. Single-source claims are at most Medium until the verification phase.
- Fallback for unreachable pages:
  - Medium may block automated fetches (HTTP 403/paywall/login wall). Try the URL once, then at most one non-fabricating alternative (for example the same URL without tracking parameters, or the page's own raw or "friend" link if the page links to one).
  - If still blocked, log the page as a **gap** in the findings file: URL, error or symptom, and attempts made. Do not reconstruct content from memory or from search-snippet paraphrase. Any partial text actually retrieved is quoted as such and marked Low confidence.
  - The same rule applies to any other URL that fails (redirect, 404, JS-only render).
- Cross-links between pages: record them for the verification phase, but do not follow links outside the 18 URLs.

## Gathering Strategy

### Instances: 5 (max 8; the default codebase/documentation/configuration/external split does not apply because there are no internal sources)

| # | Category ID | Focus Area | Tools | Output Prefix |
|---|---|---|---|---|
| 1 | bm-agents-fsm | bradmurry posts on agents, FSM and reactive reducer | WebFetch | `bm-agents-fsm` |
| 2 | bm-data-infra-ops | bradmurry posts on data, infra, contracts, DSL/AI partition and the index | WebFetch | `bm-data-infra-ops` |
| 3 | medium-12-patterns | Medium "12 design patterns" article | WebFetch (expect blocking) | `medium-12-patterns` |
| 4 | mastra-book | Mastra "Patterns of Building AI Agents" book | WebFetch | `mastra-book` |
| 5 | autogen-patterns | AutoGen design-patterns intro and multi-agent-debate pages | WebFetch | `autogen-patterns` |

Findings output dir for all instances: `C:\Projekty\ECommerceApp\docs\research\2026-10-14-agent-patterns-bradmurry\analysis\findings\`. Each instance writes `<prefix>-findings.md` there.

### Instance source URLs

**1. bm-agents-fsm** (6 URLs, base `https://bradmurry.com/software/`)
- https://bradmurry.com/software/agents-in-production-intro/
- https://bradmurry.com/software/self-evolving-agents-intro/
- https://bradmurry.com/software/reactive-reducer-patterns/
- https://bradmurry.com/software/reactive-reducer-patterns-intro/
- https://bradmurry.com/software/walking-a-reentrant-human-in-the-loop-fsm-step-by-step/
- https://bradmurry.com/software/binding-llms-to-finite-state-machines-using-the-reactive-reducer-pattern/

**2. bm-data-infra-ops** (8 URLs)
- https://bradmurry.com/software/ (index; use it to confirm the post list, not as a pattern source)
- https://bradmurry.com/software/sparkctl/
- https://bradmurry.com/software/dsl-ai-partition/
- https://bradmurry.com/software/ai-annotations/
- https://bradmurry.com/software/pyflink-intro/
- https://bradmurry.com/software/apis-at-massive-scale/
- https://bradmurry.com/software/dev-ops-contract/
- https://bradmurry.com/software/config-driven-transforms/

**3. medium-12-patterns** (1 URL)
- https://medium.com/@mohsho10/every-ai-agent-needs-these-12-design-patterns-or-it-will-eventually-fail-4970ebd4cb89

**4. mastra-book** (1 URL)
- https://mastra.ai/books/patterns-of-building-ai-agents (if the book is multi-chapter, fetch every chapter reachable from this page and list each chapter URL in the findings file)

**5. autogen-patterns** (2 URLs)
- https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/intro.html
- https://microsoft.github.io/autogen/stable//user-guide/core-user-guide/design-patterns/multi-agent-debate.html
- Required capture for the debate page: agent roles, number of rounds, how answers are aggregated, topology (for example sparse communication), and the code-level message and topic structure.

### Rationale
- Sources are all external, so the split follows the source host and content theme, giving one distinct focus per instance and no overlap. 5 instances is within the cap.
- bradmurry is split into two because 14 posts is too many for one context, and the themes separate cleanly. Agents/FSM/reducer is the core of the research question. Data/infra/ops, DSL/AI partition and annotations are the production-guidance side.
- Medium is isolated so that a probable fetch block affects only that instance and the fallback is simple.
- Mastra (a book, potentially multi-page) and AutoGen (the debate mechanics the downstream step depends on) each get their own instance for depth.

## Sources

### Codebase Sources
None — external research; no codebase category.

### Documentation Sources
None (internal).

### Configuration Sources
None.

### External Sources
See `sources.md` for the full manifest (18 URLs, 5 categories).
