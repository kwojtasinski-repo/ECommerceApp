---
applyTo: "**"
---

# Agent Workflow Hub

> **First read for any non-trivial task.** This is the short front door that tells the agent what to do first.
> Detailed rules still live in the canonical files linked below.

## Fast routing

- Do not wait for the user to name a tool. Infer the path from task shape and target files.
- One atomic intent gets one MCP path.
- Docs / ADRs / known issues / project state / roadmap / config meaning -> RAG first.
- Logs / local code / analysis / derivation / transformation -> context-mode first.
- Implementation work -> bounded context-mode probe on the smallest relevant files, then exact patching with classic tools only if needed.
- Research / evidence gathering / source verification -> `research-gatherer` skill first, with human approval gates unless `--yolo` is explicit.

## What to read next

- MCP routing detail -> [mcp-routing.instructions.md](mcp-routing.instructions.md)
- Docs lookup map -> [docs-index.instructions.md](docs-index.instructions.md)
- Pipeline flow -> [../AGENT-PIPELINE.md](../AGENT-PIPELINE.md)
- Research workflow -> [../skills/research-gatherer/SKILL.md](../skills/research-gatherer/SKILL.md)
- Prior corrections -> [../context/agent-decisions.md](../context/agent-decisions.md)
- BC block status -> [../context/project-state.md](../context/project-state.md)
- Confirmed bugs -> [../context/known-issues.md](../context/known-issues.md)

## When editing

- If the task touches docs, `.github/`, or workflow meaning, keep the hub and the canonical detail files aligned in the same change.
- If the task needs code changes, continue from the routing decision above instead of re-reading the whole tree.