---
applyTo: ".github/**, docs/**"
---

# Docs Index — Copilot Routing Table

> **MCP-first**: tool routing, precedence rules, and the ASCII flow live in [mcp-routing.instructions.md](mcp-routing.instructions.md) (`applyTo: **`, single source of truth). Read once per session.
>
> **RAG-first for knowledge**: before reading any file from this index, call `query_docs` or `list_adrs` via the active RAG MCP server (`ecommerceapp-rag-python` or `ecommerceapp-rag-dotnet` in VS Code; `ecommerceapp-rag` on GitHub.com). Load individual files only when RAG returns a specific path to follow.
> Human-oriented docs start at `docs/README.md`. Full routing table -> `docs-index.full.md` (read on demand only).

## When to use RAG vs. read directly

| Situation | Action |
|---|---|
| "Which ADR covers X?" | `list_adrs()` then `get_history(id)` |
| "What does the project say about Y?" | `query_docs("Y")` |
| "How did decision Z evolve?" | `get_history(id)` |
| Known file path already in hand | `read_file` directly, skip RAG |
| Known-issues / agent-decisions / project-state | `query_docs(question)` (bare — do NOT pass `bc="context"`; `bc=` is a breadcrumb/title substring filter, not a folder filter) |

## Fixed entry points (load directly, no RAG needed)

| Need | File |
|---|---|
| **MCP routing / tool precedence / ASCII flow** | `.github/instructions/mcp-routing.instructions.md` |
| BC blocked? | `.github/context/project-state.md` |
| Bug already tracked? | `.github/context/known-issues.md` |
| Prior corrections? | `.github/context/agent-decisions.md` |
| Test skip/xfail rules? | `.github/context/test-stabilization-policy.md` |
| Full routing table | `.github/instructions/docs-index.full.md` |
| Pipeline spec | `.github/AGENT-PIPELINE.md` |

## MCP tools

```
query_docs(question, bc?, top_k?)   -- semantic search across docs + .github/context
get_history(id)                     -- all indexed chunks for a history group, sorted by start_line
list_adrs()                         -- all ADR ids and titles
```

## Prompt files

| Need | File |
|---|---|
| Analyze a BC structure | `.github/prompts/bc-analysis.prompt.md` |
| Implement a BC slice | `.github/prompts/bc-implementation.prompt.md` |
| Review a PR | `.github/prompts/pr-review.prompt.md` |
| Refactor guidance | `.github/prompts/refactor.prompt.md` |
| **Analyze a user-facing flow (bidirectional)** | `.github/prompts/flow-analysis.prompt.md` |
| RAG maintenance cycle (ingest + eval + coverage) | `.github/prompts/rag-sync.prompt.md` |

## RAG maintenance skills

Use these skills when the RAG/MCP system needs attention. Read via the `read_file` tool.

| Symptom / need | Skill |
|---|---|
| MCP not starting, errors, bad results, low scores, DLL lock | `.github/skills/diagnose-rag/SKILL.md` |
| File ranks too low / too high in results | `.github/skills/tune-rag-weights/SKILL.md` |
| Polish or German query returns wrong doc (English works) | `.github/skills/expand-rag-glossary/SKILL.md` |
| New doc folder added / wrong doc_kind / query coverage gap | `.github/skills/generate-rag-rules/SKILL.md` |
| Newly indexed file has no eval query covering it | `.github/skills/generate-eval-questions/SKILL.md` |
| Cache a RAG result in context-mode FTS5 for repeated recall in the same session | `.github/skills/rag-with-memory/SKILL.md` |
| Decide whether a RAG config change needs re-indexing (none / incremental / full) | `.github/skills/rag-reindex-decision/SKILL.md` |
| Drop & rebuild a Qdrant collection (embedder.dim change, corruption, model swap) | `.github/skills/rag-collection-rebuild/SKILL.md` |
| Query returns wrong / low-quality / empty results — hypothesis-ordered debug | `.github/skills/rag-query-debug/SKILL.md` |
| Verify a new PL/DE glossary entry expands correctly on both servers | `.github/skills/rag-multilang-test/SKILL.md` |
| Find docs that lack covering eval queries (`comm -23` audit + priority + draft heuristic) | `.github/skills/rag-eval-coverage/SKILL.md` |
| Bring RAG up from scratch in a new project (compose, ingest, MCP, ADR-0028 known gap) | `.github/skills/setup-rag-new-project/SKILL.md` |

## context-mode sandbox skills

Use these when the context-mode MCP container needs verification, debugging, or pre-merge audit.

| Symptom / need | Skill |
|---|---|
| Smoke-test a freshly bootstrapped context-mode container (8 runtime checks) | `.github/skills/ctx-sandbox-bootstrap-verify/SKILL.md` |
| `ctx_doctor()` not green / `ctx_*` tool error / container failed to start | `.github/skills/ctx-doctor-playbook/SKILL.md` |
| Pre-merge compliance audit of all 22 ADR-0029 conformance items | `.github/skills/ctx-hardening-audit/SKILL.md` |
| Bring context-mode up from scratch in a new project (skeleton → ctx_doctor green) | `.github/skills/setup-context-mode-new-project/SKILL.md` |
| Stand up AdGuard DNS allowlist + strict/permissive policy for a new project | `.github/skills/setup-adguard-policy/SKILL.md` |
| Provision the AdGuard allowlist (6 required categories + forbidden defaults) | `.github/skills/ctx-bootstrap-network/SKILL.md` |
| Provision Qdrant collection + FTS5 SQLite storage for a new project | `.github/skills/ctx-bootstrap-storage/SKILL.md` |
| Add / verify sandbox runtimes beyond the default Node + shell | `.github/skills/ctx-bootstrap-runtimes/SKILL.md` |
| Configure MCP clients (VS Code, Copilot Web, Visual Studio 17.14+) | `.github/skills/setup-mcp-clients/SKILL.md` |
| Install the L3 auto-cache hook (host-side PostToolUse) in a new project | `.github/skills/setup-auto-cache-hook/SKILL.md` |

## Bootstrap playbooks

End-to-end, multi-stage walkthroughs for standing up RAG and/or context-mode in a fresh repo. Skills are atomic; playbooks compose them.

| Need | Playbook |
|---|---|
| Playbook hub (which to pick, order of operations, contribution guide) | [docs/playbooks/README.md](../../docs/playbooks/README.md) |
| Bring context-mode up end-to-end (7 stages, troubleshooting flowchart) | [docs/playbooks/context-mode-bootstrap.md](../../docs/playbooks/context-mode-bootstrap.md) |
| Bring RAG up end-to-end (7 stages, troubleshooting flowchart) | [docs/playbooks/rag-bootstrap.md](../../docs/playbooks/rag-bootstrap.md) |
| Build standalone/global multi-project RAG platform | [docs/playbooks/rag-standalone-global.md](../../docs/playbooks/rag-standalone-global.md) |

## Discovery agent

| Need | Agent |
|---|---|
| Read-only audit of a new repo: what's already set up vs. needs bootstrapping | `@setup-discovery` (`.github/agents/setup-discovery.md`) |
