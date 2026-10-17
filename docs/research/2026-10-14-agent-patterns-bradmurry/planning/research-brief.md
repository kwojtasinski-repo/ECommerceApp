# Research Brief: AI agent building patterns (Brad Murry + Medium/Mastra/AutoGen) and their use in production

## Research Question
Collect and cross-verify the content of 18 given sources (14 posts from bradmurry.com/software, the Medium article
"12 design patterns", Mastra "Patterns of Building AI Agents", 2 AutoGen pages: multi-agent-debate and
design-patterns intro). The ultimate goal (outside this workflow): based on the collected facts, run a
multi-agent debate pattern with several agents — which skills/patterns can be used in production AI.

## Type
external
Self-check performed: the question requires only knowledge from outside the organization (the given URLs), there are no
internal sources (code/ticket/transcript) to query. The reference to ECommerceApp is only the context of the
later step, not a source for this phase. Simple classification.

## Scope

### In Scope
- Agent patterns, architectures and skills described in the 18 given URLs
- Reactive reducer pattern, FSM + LLM, human-in-the-loop, self-evolving agents, agents in production
- Multi-agent debate (AutoGen) — the mechanics of the pattern
- Production guidance (reliability, contracts, configuration, scale)

### Out of Scope
- Synthesis, recommendation and the final report (a separate step after this workflow)
- Actually running the agent debate (a separate step after this workflow)
- Sources outside the given list (except possible verification of basic facts)

### Source Restrictions
- Only the 18 given URLs; every fact must have a URL + quote

## Success Criteria

### Concrete Deliverables
- [ ] Per source: a list of patterns/concepts/skills with quotes and confidence
- [ ] A pattern catalog with deduplication across sources (e.g. HITL, FSM, reflection)
- [ ] A description of the multi-agent debate mechanics (roles, rounds, aggregation) from AutoGen
- [ ] A list of patterns marked as suitable for production (with justification from the sources)
- [ ] Contradictions and gaps between sources

### Information Quality Requirements
- Every finding with a URL and a quote; no facts from training memory; unavailable pages logged as a gap

## Actors
None — research is actor-agnostic

## Information Layers

### Big Picture
- What classes of problems these patterns solve, why agents fail in production

### Modeling
- FSM, reducer, states, contracts, DSL/AI partitioning

### Implementation
- Specific frameworks (AutoGen, Mastra, Flink, sparkctl), configuration, API

## Output Location
docs/research/2026-10-14-agent-patterns-bradmurry/ — confirmed by the user (AskUserQuestion, 2026-10-14)
