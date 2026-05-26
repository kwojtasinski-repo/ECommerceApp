# MCP-first routing — migration playbook

> Goal: take a project that already has explicit "doc routing" rules
> ("for X, read `docs/foo/bar.md`") and migrate it to the
> **MCP first, docs as fallback** workflow without losing the existing
> link discipline.
>
> Audience: anyone setting this up on a second/third repo. This is a
> distilled checklist of what works and what doesn't, based on lessons
> learned wiring RAG into this project and on the user's second-project
> attempt that needed hard rules to actually take effect.

---

## 0. Why bother

Old pattern (doc routing only):

```
agent reads instructions ──► finds "for ADRs, see docs/adr/README.md"
                       └──► opens the file, scrolls, greps, hopes
                            the right ADR is referenced by name
```

Problems:
- Agent guesses from training data when the routing rule isn't explicit.
- Greps return false positives (`OrderId` in 12 files; which matters?).
- Amendment chains scatter across multiple files; routing rules can't capture them.
- Multi-language queries (PL/DE) don't grep against EN doc bodies.

New pattern (MCP first):

```
agent reads instructions ──► routing table says "ADR question → MCP"
                       └──► query_docs / read_docs / get_history
                            returns ranked chunks with file + line range
                       └──► if MCP empty: fall back to read_file/grep
                            and suggest re-ingest
```

Wins:
- Ranked, semantic results. No training-data drift.
- Amendment chains reassembled by `get_history(id)`.
- Multilingual queries handled by glossary expansion.
- File links + line numbers come back automatically — citations stay precise.

**Lesson learned (from second-project attempt):** soft rules
("prefer MCP", "consider using the tool") get ignored. The agent will
fall back to grep almost every time. You need **hard rules** ("MUST call
MCP first", "NEVER answer from training data for X") and a
**trigger-phrase table** that names the exact intents that route to MCP.

---

## 1. Pre-flight

You can only migrate once the RAG side is actually working:

- [ ] Qdrant container is up (`docker compose up -d qdrant`).
- [ ] At least one ingest run has populated the collection.
- [ ] An MCP server variant is registered and shows green in the
  VS Code MCP panel (stdio or HTTP).
- [ ] All 4 tools are listable: `list_adrs`, `query_docs`, `read_docs`,
  `get_history`.
- [ ] You can call each tool once from chat and get a sensible answer.

If any box is unchecked, fix that first. The migration is wasted effort
on a non-functional index.

---

## 2. Audit existing doc-routing rules

Make a flat list of every routing rule in your current instructions.
Look in `.github/copilot-instructions.md`, every `*.instructions.md`,
every `*.prompt.md`, every agent file. Grep for phrases like:

- `see docs/...`
- `read .../README.md`
- `look up ... in ...`
- `before editing ... read ...`
- `consult ...`

For each rule, write down four things in a table:

| Rule text (short) | Domain it answers | Current target | Indexed in RAG? |
|---|---|---|---|

Example row (from this project's audit):

| "before bug fix, read known-issues.md" | bug fixes | `.github/context/known-issues.md` | yes |

If "Indexed in RAG?" is **no** for a file the agent must read, decide:
add it to the ingest scope, or keep the hard-rule doc reference.
The migration only applies to rules whose target file is in the index.

---

## 3. Classify each rule (Keep / Replace / Strengthen)

For every audited rule, pick exactly one bucket:

- **Keep as-is** — the rule names a single, small, frequently-edited
  file that's faster to read directly than to query
  (e.g. `bounded-context-map.md`, `project-state.md`'s blocked-BC list).
  Doc routing wins on cost; don't route through MCP.

- **Replace with MCP route** — the rule points at a discoverable knowledge
  base (ADRs, architecture docs, roadmap, design notes). Replace the
  "read file X" instruction with a "use tool Y for intent Z" entry in
  the trigger table.

- **Strengthen + keep + route** — the rule is critical (safety,
  blockers) AND the docs are in RAG. Keep the hard rule, *and* add a
  routing entry so agents who skip the read still hit MCP. Belt + braces.

Rule of thumb: anything that changes more than once a quarter and is
larger than ~200 lines → Replace. Anything tiny and high-frequency →
Keep.

---

## 4. Write the global RAG instruction (one file, `applyTo: **`)

Create a single instruction file that every agent inherits. In this
repo it's [.github/instructions/rag.instructions.md](../../.github/instructions/rag.instructions.md). Template:

```markdown
---
applyTo: "**"
---

# RAG — when to use the MCP tools

The repo ships an MCP server backed by a local Qdrant index over `docs/`.
It exposes 4 tools:

| Tool                                    | When to use                                                                 |
| --------------------------------------- | --------------------------------------------------------------------------- |
| `list_adrs()`                           | Orientation: "what ADRs exist?", "is there an ADR about X?"                 |
| `query_docs(question, bc?, top_k?)`     | Discovery: which files score highest? Returns ranked chunks + file + line.  |
| `read_docs(question, bc?, top_files?)`  | **Preferred for reasoning.** Full content of top unique files.              |
| `get_history(id)`                       | Evolution: ADR + all amendments, sorted by start_line.                      |

## Recommended flow

list_adrs → query_docs → read_docs → get_history

Prefer `read_docs` over `query_docs` when you need to quote rules,
check conformance, or understand rationale.

## Refresh policy

If the user reports stale answers, suggest re-running `ingest.py`.
```

Keep it short. Long files get skimmed. The tool table + the flow line
are what the agent actually internalises.

---

## 5. Write the trigger-phrase routing table (hard rules)

This is the part the second project was missing. Add a numbered section
to `.github/copilot-instructions.md` (or equivalent) with two things:

1. A table mapping **user intent / trigger phrase → MCP tool**.
2. **Hard rules** stating MCP must be called before answering, with
   the word **always** or **never**.

Template (matches §12 in [.github/copilot-instructions.md](../../.github/copilot-instructions.md)):

```markdown
## NN. RAG / MCP tool routing

| Trigger phrase / intent                              | Tool to call             | When to use                                  |
|------------------------------------------------------|--------------------------|----------------------------------------------|
| "list ADRs", "what ADRs exist", "show all decisions" | `list_adrs`              | Enumerate all indexed ADRs                   |
| "ADR-NNNN", "ADR about X", "decision on X"           | `get_history(id="NNNN")` | All indexed chunks for a specific ADR        |
| General architecture / pattern / "how does X work?"  | `query_docs(query="...")`| Semantic search over all docs                |
| "full content of X", "show me everything about X"    | `read_docs(query="...")` | Returns top files in full                    |
| Known issues, bug fixes, blocked BCs, project state  | `query_docs` → target `.github/context/` chunks | High-relevance context files are weighted up |

**Rules:**
- Always use a MCP tool **before answering** questions about ADRs, project
  state, known issues, or roadmap — **never** guess from training data.
- If the tool returns "No chunks found", fall back to saying so and
  suggest re-running ingest.
- Prefer `get_history` over `query_docs` when the user mentions a
  specific ADR number or title.
```

The wording **always / never / before answering** is what makes the rule
stick. Soft phrasing like "consider using" gets ignored — that's the
behaviour the user hit on the second project.

---

## 6. Migrate individual routing rules (concrete examples)

For each rule classified as **Replace** in §3, rewrite it in place.
Two before/after patterns cover most cases.

### Pattern A — "read file X for topic Y" → routing entry

Before (somewhere in `copilot-instructions.md`):

```markdown
- For coupon rules and limits, read `docs/adr/0016/0016-coupons.md`.
```

After (delete the line, add a row to the routing table):

```markdown
| "coupon", "discount stacking", "ADR-0016"            | `get_history(id="0016")` | Coupon rules + amendments     |
```

The rule is now data, not prose. Agents that don't read the prose still
hit MCP because the trigger words are in the table.

### Pattern B — "before doing X, read Y" → hard rule + route

Before:

```markdown
- Before any bug fix, MUST read `known-issues.md`.
```

After (keep the hard rule, *also* add a route for queries that don't
trigger a bug-fix workflow):

```markdown
- Before any bug fix, MUST read `known-issues.md` (use `read_docs` or
  direct `read_file`; both are valid).
- Questions about a specific KI-NNN code route to:
  `query_docs(query="KI-NNN <symptom>")`.
```

The MUST-read stays — it's a workflow gate, not a knowledge query. The
route catches the case where the user asks "what's KI-007?" without
starting a bug-fix flow.

---

## 7. Keep direct file reads as the **fallback path**

Do not delete `read_file` / `grep_search` / `semantic_search` from the
agent's toolbox. The routing rule is:

```
1. MCP first (semantic, ranked, citation-friendly).
2. If MCP returns empty or low-confidence (all scores < ~0.25):
   a. Tell the user the index looks stale.
   b. Suggest re-running ingest.
   c. THEN fall back to direct read_file / grep_search.
3. NEVER answer from training data for indexed domains.
```

Encode (2c) in the global instruction so the fallback is explicit, not
inferred. Without it, the agent either loops on MCP or invents an
answer when MCP comes back empty.

---

## 8. Stale-index detection — make it automatic

Three signals tell the agent the index is stale:

1. MCP returns `"No chunks found"` for a query the user phrased
   reasonably.
2. All top results score below ~0.25.
3. The user says "the doc says X but the tool returned Y".

For each signal, the agent should respond with the same canned action:

> The RAG index looks stale or doesn't cover this. Re-ingest with
> `python tools/rag/ingest.py` (or `docker compose --profile rag run
> --rm rag-tools python ingest.py`), then re-ask. Meanwhile I'll fall
> back to direct file reads.

Put that exact phrasing in the global instruction. Saves three turns
of "are you sure?" every time the index drifts.

---

## 9. Verification — concrete test prompts

After the migration, run these prompts and confirm the agent calls the
expected tool **before** writing any answer. If it answers from training
data, the rules aren't strong enough yet — go back and tighten the
"always" / "never" wording.

| Prompt                                              | Expected tool call                  |
|-----------------------------------------------------|-------------------------------------|
| "List the ADRs in this repo."                       | `list_adrs()`                       |
| "What does ADR-0016 say?"                           | `get_history(id="0016")`            |
| "How does ingest detect changed files?"             | `query_docs(...)` then `read_docs`  |
| "Show me everything about the error envelope."      | `read_docs(query="error envelope")` |
| "Is there a known issue with NBP currency import?"  | `query_docs(query="NBP currency")`  |
| "Which BCs are currently blocked?"                  | `read_docs` or direct read of `project-state.md` (per Keep rule) |

If any prompt skipped MCP, locate the missing trigger phrase and add it
to the routing table. Re-test.

---

## 10. Anti-patterns to avoid

- **Spreading rules across N agent files.** Put the RAG rules in ONE
  global instruction (`applyTo: **`) so every agent inherits them.
  Per-agent overrides only when truly necessary.
- **Soft verbs.** "Consider", "prefer", "you may" → ignored. Use
  "MUST", "always", "never", "before answering".
- **No trigger table.** Pure prose rules don't survive long instruction
  files. The table is what the agent grep-skims first.
- **No fallback path.** If you forbid training-data answers but don't
  permit `read_file` after MCP empty, the agent loops or invents.
- **Forgetting amendments.** ADRs amend each other; route to
  `get_history(id)` for any "ADR-NNNN" intent — not `query_docs`.
- **One MCP variant only.** Register both stdio (for offline) and HTTP
  (for shared/long-lived sessions). If only stdio is wired, agents on
  GitHub.com Copilot have no MCP at all.
- **Skipping the audit (§2).** Migrating without a list means duplicate
  rules, contradictory routes, and a routing table that doesn't match
  the prose.

---

## 11. Rollback

The migration is reversible by design — the original doc files are
untouched, only the instruction files changed.

- `git revert` the commit that introduced the routing table → agents
  go back to direct file reads.
- The MCP servers stay registered; they just don't get called as often.
- The Qdrant index stays valid; nothing to clean up.

Cost of rollback is one commit. Cost of keeping the migration even
partially is roughly N fewer wrong-doc reads per session.

---

## 12. Suggested order on a fresh project

1. Get RAG indexed and one MCP variant green (§1).
2. Audit existing rules into the four-column table (§2).
3. Classify each row (§3).
4. Write the global instruction (§4).
5. Add the §NN routing table to `copilot-instructions.md` (§5).
6. Migrate rules one Pattern A / Pattern B at a time (§6).
7. Add the fallback paragraph (§7) and stale-detection canned reply (§8).
8. Run the verification prompts (§9). Loop §5–§8 until they all pass.

Do not skip §9. The whole reason the second project needed "hard rules"
is that nobody ran the verification prompts after the initial wording,
and the agent silently kept reading files instead of querying MCP.

---

## Cross-references

- Tool surface: [rag-architecture.md §8 MCP tools](rag-architecture.md#8-mcp-tools)
- Global instruction example: [.github/instructions/rag.instructions.md](../../.github/instructions/rag.instructions.md)
- Trigger table example: [.github/copilot-instructions.md §12](../../.github/copilot-instructions.md)
- Error envelope contract: [rag-architecture.md §14](rag-architecture.md#14-error-handling-sanitisation-and-middleware)
- Maintenance prompt: [.github/prompts/rag-sync.prompt.md](../../.github/prompts/rag-sync.prompt.md)
