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

## 13. Coexistence with a second MCP server (worked example: context-mode)

Running the container is one thing. Wiring it into the **flow** so the
agent actually picks the right MCP for the right intent is the part
that breaks projects.

This section assumes the RAG MCP is already wired per §1–§9 and you
are adding a second MCP server alongside it (here: `context-mode` per
[ADR-0029](../adr/0029/0029-context-mode-mcp-sandbox.md) and the
[context-mode roadmap](../roadmap/context-mode-integration.md)). The
same recipe applies to any second MCP (Playwright, GitHub Issues,
filesystem sandbox, etc.).

### 13.0 The whole picture on one screen

```text
  +-----------------------------------------------------------------------------+
  |                              USER PROMPT                                    |
  +------------------------------------+----------------------------------------+
                                       |
                                       v
  +-----------------------------------------------------------------------------+
  |                AGENT  --  reads global instruction once                     |
  |   (one file, two tool tables + numbered precedence block, see 13.3)         |
  +------------------------------------+----------------------------------------+
                                       |
                                       v
                         INTENT  CLASSIFICATION
              +------------------------+------------------------+
              |                        |                        |
              v                        v                        v
     "how does X work?"        "run / save / index"     pure code edit
     "what does ADR-N say?"    "summarise file"         (no knowledge,
     "is BC blocked?"          "fetch this URL"          no fetch)
     "any known issue?"        "store this for later"
     "show me the rule"
              |                        |                        |
              v                        v                        v
  +----------------------+  +----------------------+  +----------------------+
  |  MCP #1 -- RAG       |  |  MCP #2 -- CONTEXT-  |  |   no MCP needed      |
  |  ecommerceapp-rag-*  |  |  MODE                |  |   (direct tools)     |
  |                      |  |  ecommerceapp-       |  |                      |
  |  KNOWLEDGE           |  |  context-mode        |  |  read_file           |
  |                      |  |                      |  |  grep_search         |
  |  list_adrs           |  |  EXECUTION /         |  |  semantic_search     |
  |  query_docs          |  |  REDUCTION /         |  |  edit / write        |
  |  read_docs           |  |  EXTERNAL FETCH      |  |                      |
  |  get_history         |  |                      |  |                      |
  |                      |  |  ctx_stats           |  |                      |
  |  Backed by:          |  |  ctx_execute         |  |                      |
  |   * Qdrant index     |  |  ctx_execute_file    |  |                      |
  |   * docs/ +          |  |  ctx_fetch_and_index |  |                      |
  |     .github/context/ |  |  ctx_insight         |  |                      |
  |                      |  |                      |  |                      |
  |  Source of truth     |  |  Backed by:          |  |                      |
  |  for ANYTHING        |  |   * sandboxed Node   |  |                      |
  |  written down        |  |   * AdGuard DNS      |  |                      |
  |  in this repo        |  |     allowlist        |  |                      |
  |                      |  |   * SQLite session   |  |                      |
  |                      |  |     store            |  |                      |
  +-----------+----------+  +-----------+----------+  +-----------+----------+
              |                         |                         |
              |  empty / low score?     |  hooks fired:           |
              |  -> tell user index     |   PreToolUse rewrites   |
              |     looks stale         |   PostToolUse compresses|
              |  -> fall back to        |   summary saved by      |
              |     direct read_file    |   PreCompact            |
              |  -> NEVER guess         |                         |
              |     from training data  |                         |
              v                         v                         v
  +-----------------------------------------------------------------------------+
  |     ANSWER  +  precise file links (path#Lstart-Lend)  +  citations          |
  +-----------------------------------------------------------------------------+

           ============== HARD PRECEDENCE RULES ==============
           1. Knowledge intent   ->  RAG first.   Never substitute ctx_execute.
           2. Run / summarise    ->  context-mode first.  Never substitute
                                     read_file + manual summary.
           3. External URL       ->  ctx_fetch_and_index ONLY.
                                     Never raw fetch_webpage (bypasses AdGuard).
           4. Both MCPs empty    ->  direct read_file / grep_search,
                                     name the failing MCP to the user.
           5. NEVER call both    MCPs for the same atomic intent.
```

**Reading rule:** anything written down in the repo → MCP #1. Anything
that needs to *run*, *summarise*, or *reach the outside world* →
MCP #2. Direct file tools are the fallback, never the first stop for
indexed knowledge.

The subsections below break each box into the concrete artefacts you
have to ship: tool tables (§13.2), the precedence rule itself (§13.3),
the hook file that makes the flow visible (§13.4), the combined runtime
view (§13.5), the three failure modes (§13.6), verification
prompts (§13.7), and rollback (§13.8).

### 13.1 Two MCPs serve two different jobs

| MCP | Job | Tools it owns |
|---|---|---|
| RAG (`ecommerceapp-rag-*`) | **Knowledge retrieval** over indexed docs | `list_adrs`, `query_docs`, `read_docs`, `get_history` |
| context-mode (`ecommerceapp-context-mode`) | **Context reduction / sandboxed execution / external fetch** | `ctx_stats`, `ctx_execute`, `ctx_execute_file`, `ctx_fetch_and_index`, `ctx_insight` |

They never compete for the same intent. The risk is not collision,
it's the agent picking neither because the routing rules don't say
which one owns which intent. That's the wiring gap.

### 13.2 Extend the global instruction with a second tool table

Do **not** create a second instruction file. Append a second table to
the existing global instruction (`rag.instructions.md` or rename to
`mcp.instructions.md` once you have more than one MCP).

```markdown
## context-mode — when to use the ctx_* tools

| Tool                       | When to use                                                            |
| -------------------------- | ---------------------------------------------------------------------- |
| `ctx_stats`                | "how much context have we saved this session?" / sanity check          |
| `ctx_execute(lang, code)`  | One-shot sandboxed execution — math, parsing, regex on a small string  |
| `ctx_execute_file(path)`   | Run analysis over a file and return only the **summary** (not body)    |
| `ctx_fetch_and_index(url)` | Pull an allowlisted external URL through AdGuard and add it to context |
| `ctx_insight()`            | Open the local web UI to inspect what's currently in context           |
```

Keep the RAG table immediately above it. Same applyTo, same file.
One scroll, both surfaces visible.

### 13.3 Add a precedence rule (this is the part that breaks)

Tool tables alone don't decide which MCP to call first when an intent
**could** map to either. Add an explicit precedence block:

```markdown
## MCP precedence

When more than one MCP could plausibly answer a request:

1. **Knowledge questions** (anything that quotes docs, ADRs, rules,
   project state) → RAG first. Never substitute `ctx_execute` for it.
2. **Sandboxed execution** (run a snippet, compute a value, summarise a
   file's structure without loading its content) → context-mode first.
   Never substitute `read_file` + manual summary for it when the body
   is large.
3. **External fetch** (a URL the user pasted) → `ctx_fetch_and_index`
   only. Never raw `fetch_webpage` for project work — it bypasses the
   AdGuard allowlist.
4. **Both MCPs empty / unhealthy** → fall back to direct read_file /
   grep_search and tell the user which MCP failed.

Never call both MCPs for the same atomic intent. If unsure, pick the
one whose table matches the user's verb ("what does ADR-X say" → RAG;
"run this snippet" → context-mode).
```

Three pieces matter: the numbered list, the word **first** under each
bucket, and the closing "never call both for the same atomic intent".
Without the third line the agent will call RAG, then "double-check"
with `ctx_execute`, doubling the cost.

### 13.4 Hooks: where context-mode actually plugs into the flow

context-mode ships five hooks (per
[context-mode-integration.md Phase 3](../roadmap/context-mode-integration.md)):
`PreToolUse`, `PostToolUse`, `UserPromptSubmit`, `PreCompact`,
`SessionStart`. These hooks fire inside the agent's tool-call loop —
they are the actual flow plug-in, not the container.

| Hook            | What it does for the flow                                            |
| --------------- | -------------------------------------------------------------------- |
| `SessionStart`  | Loads the last summarised session from SQLite — **survives compaction** |
| `UserPromptSubmit` | Re-injects the running summary as system context for each turn   |
| `PreToolUse`    | Intercepts a tool call, rewrites the args (e.g. swap raw `fetch_webpage` for `ctx_fetch_and_index`), or denies it per `.claude/settings.json` |
| `PostToolUse`   | Replaces the tool's raw output in the transcript with a short reference; full output stays in context-mode's store |
| `PreCompact`    | Snapshots the current summary to SQLite before VS Code compacts the window |

The hook file (`.github/hooks/context-mode.json`) is what makes the
flow change visible. Without hooks, the container is just an idle
service — the agent still dumps raw output into the window and your
context savings stay at 0. **Wiring the hooks is the migration**, not
adding the compose service.

### 13.5 The combined flow

```text
                 User question / instruction
                            |
                            v
              +---------------------------+
              | SessionStart hook         |  context-mode plug-in
              | (re-inject last summary)  |  (transparent to agent)
              +---------------------------+
                            |
                            v
                 Agent reads global instructions
                 (RAG table + ctx table + precedence)
                            |
                            v
              +---------------------------+
              | Intent classification     |
              | (per §13.3 precedence)    |
              +---------------------------+
                |              |                 |
   knowledge    |  execute /   |  external       |  pure code task
                v  summarise   v  fetch          v
         RAG MCP first   context-mode MCP   ctx_fetch_and_index   direct read_file
         (§4..§9 flow)   (ctx_execute*)    (AdGuard allowlist)    + grep
                |              |                 |                 |
                +------+-------+-----------------+-----------------+
                       |
                       v
              +---------------------------+
              | PostToolUse hook          |
              | (raw output --> reference)|
              +---------------------------+
                       |
                       v
                  Answer + citations
                       |
                       v
              +---------------------------+
              | PreCompact (snapshot DB)  |  if window fills
              +---------------------------+
```

Note: the hooks wrap **every** tool call, including RAG tools.
context-mode's `PostToolUse` will also compress `read_docs` output to
a reference. That's the win — RAG benefits from the same context
reduction as everything else, transparently.

### 13.6 Avoid the three common wiring failures

1. **Only the container is wired, hooks are not.** The agent uses
   neither MCP correctly. Symptom: `ctx_stats` shows 0% reduction
   after a long session. Fix: complete Phase 3 of the context-mode
   roadmap (`.github/hooks/context-mode.json` + restart VS Code).
2. **No precedence rule.** The agent calls both MCPs "to be safe".
   Symptom: every ADR question shows up in `ctx stats` as wasted
   tokens. Fix: add §13.3 to the global instruction; re-run §9
   verification with prompts that span both surfaces ("summarise
   ADR-0028 and run a sanity check on its sample code").
3. **`fetch_webpage` left ungoverned.** The agent fetches a URL
   directly, bypassing AdGuard. Symptom: a request appears in the
   AdGuard query log under "DEFAULT" client, not `context-mode`.
   Fix: add a `PreToolUse` hook entry that rewrites `fetch_webpage`
   args into `ctx_fetch_and_index`, OR add a hard rule in the global
   instruction: "NEVER use `fetch_webpage` for project work — use
   `ctx_fetch_and_index`."

### 13.7 Verification (extend §9)

Add these to the §9 prompt set:

| Prompt                                                    | Expected behaviour                              |
|-----------------------------------------------------------|-------------------------------------------------|
| "How much context have we saved this session?"            | `ctx_stats` only                                |
| "Compute the SHA-256 of the string 'hello'."              | `ctx_execute` only — no RAG, no direct read    |
| "Summarise the structure of `Program.cs`."                | `ctx_execute_file` — not `read_file` + manual  |
| "Fetch https://example.com/foo.md and tell me what it says." | `ctx_fetch_and_index` — not raw `fetch_webpage` |
| "Summarise ADR-0028 and verify its sample code parses."   | RAG first (`read_docs`), then `ctx_execute` on the snippet — never both for the same fact |

If the precedence block is correct, no prompt above should trigger
both MCPs.

### 13.8 Rollback for the second MCP

context-mode is `--profile monitoring`-gated in `docker-compose.yaml`,
so stopping it is one command. Removing the hooks file and reverting
the §NN precedence section in the global instruction takes the agent
back to RAG-only routing without touching RAG. The two MCPs are
independent; either can be rolled back without breaking the other.

### 13.9 L1 handoff: caching a RAG result in context-mode for the same session

Even before the hooks ship (§13.4), the two MCPs can be wired into a manual handoff that solves one specific cost problem: **the same RAG question asked 3+ times in one session pays full embedding + reranker cost every time**. The fix is to write the first RAG result into context-mode's FTS5 store and recall it with sub-second `ctx_search` afterwards. We call this **L1** — read-side caching only, no hooks.

The skill is [.github/skills/rag-with-memory/SKILL.md](../../.github/skills/rag-with-memory/SKILL.md). It documents the 3-call shape:

```
query_docs(...)                   --  full cost, first time
  → format chunks as a single markdown blob with source attributions
  → ctx_index(text=blob, kind="rag-snapshot", title="<query>")
  → ctx_search("<rewording>")     --  sub-second FTS5 recall thereafter
```

Three pieces that have to land before this works:

1. **Three-surface disambiguation in the global instruction.** `semantic_search` (VS Code embedded), `memory.*` (VS Code `/memories/`), and `ctx_*` (context-mode FTS5) all show up as "memory-like" tools. The skill names which one owns L1 caching (`ctx_*`) and why the other two don't. Without this table, agents will write the blob to `/memories/` instead, which doesn't survive a window restart in the same way.
2. **Parametric workspace mount.** L1 only works if `ctx_index` and `ctx_search` see the same working directory across calls. Hard-coding `/workspace` in compose breaks forks that run the container with a different mount. Use `volumes: .:${CONTEXT_MODE_WORKSPACE:-/workspace}:ro` and let the skill probe the live value with `ctx_execute("python", "import os; print(os.getcwd())")` as Step 0.
3. **Multilingual recall caveat.** context-mode FTS5 uses an English Porter stemmer plus trigram fallback. Polish/German queries against an English blob get zero hits where the same query against RAG (which has a multilingual glossary) returns the top result. Document this in the skill and route mixed-language sessions either through RAG every time or with the keyword translated to English before `ctx_search`. Phase 7.1 of the [context-mode roadmap](../roadmap/context-mode-integration.md) tracks injecting the RAG glossary into the L1 blob as a design input.

#### Built-in subagent surface restriction (LIMIT-1)

Built-in subagents (`Explore`, `runSubagent` invocations of any custom agent) **cannot call MCP tools and cannot run `tool_search`**. The pattern that survives this restriction is **inline-chunks**: the parent agent fetches RAG, formats the chunks as markdown with full source attributions, and passes the blob in the subagent's prompt. The subagent reasons over the blob using only its file/grep tools — no MCP calls — and returns its answer. This is the only viable way to delegate cross-tool work to a subagent today.

Same restriction applies to **named custom agents invoked via `runSubagent`** (we hit this on `@copilot-setup-maintainer` in Session 26: the agent surfaced 4 drift items with proposed edits because it could not execute them itself). The parent agent has to apply the edits.

#### When L1 is worth wiring vs. waiting for hooks (L3)

L1 is worth shipping ahead of hooks when:

- The same 3–5 RAG queries are recurring in long sessions (post-compaction window, multi-hour BC walkthroughs).
- The team accepts a manual `ctx_index` call as part of the routing flow (the skill makes it a 5-line recipe).
- Multilingual queries are an exception, not the norm.

Wait for hooks (L3) when:

- Most queries are one-shot — the cache rarely pays for itself.
- Multilingual sessions are the dominant flow.
- The team can't tolerate any manual ceremony in the routing path.

L1 and L3 are not mutually exclusive — L1 ships the read side now; L3's `PostToolUse` hook will write the same blob automatically once the hook file lands per Phase 3 of the context-mode roadmap.

---

## Cross-references

- Tool surface: [rag-architecture.md §8 MCP tools](rag-architecture.md#8-mcp-tools)
- Global instruction example: [.github/instructions/rag.instructions.md](../../.github/instructions/rag.instructions.md)
- Trigger table example: [.github/instructions/mcp-routing.instructions.md](../../.github/instructions/mcp-routing.instructions.md) (canonical, `applyTo: **`). `copilot-instructions.md §12` is the short per-repo pointer to that file, not the table itself.
- Error envelope contract: [rag-architecture.md §14](rag-architecture.md#14-error-handling-sanitisation-and-middleware)
- Maintenance prompt: [.github/prompts/rag-sync.prompt.md](../../.github/prompts/rag-sync.prompt.md)


---

## 14. Applied: ECommerceApp case study (2026-05-27)

Real-world execution of this playbook on this repository, split into two commits to keep the diff reviewable.

### Phase 0 — Precedence wiring (commit `cef1bca5`)

Single-file pilot: added the 5 HARD precedence rules, ASCII flow diagram, and `ctx_*` carve-outs to `.github/instructions/rag.instructions.md` + `.github/copilot-instructions.md §13.0`. No agent/prompt/skill changes yet. Goal was to prove the wording works before scaling.

### Phases 1-5 — Full rollout (commit `<filled after commit>`)

One sweep across the entire `.github/` workflow surface, ~20 files, zero production code, zero tests touched:

- **Phase 1 — Foundation**: extracted canonical single-source-of-truth into new `.github/instructions/mcp-routing.instructions.md` (applyTo: \`**\`). Status tables for both MCPs, ASCII flow diagram, RAG tool table, context-mode tool table (gated as "dormant" until ADR-0029 hooks land), 5 HARD precedence rules, fallback ladder, server variants. `rag.instructions.md` trimmed to RAG-only ops (~50 lines). `copilot-instructions.md §12` collapsed to 6-bullet summary linking the canonical file. `docs-index.instructions.md` lifted MCP routing to top-of-table.
- **Phase 2 — Agents + pipeline**: per-agent MCP scopes wired into all 8 agents (Planner=RAG read-only; Implementer=RAG + `ctx_execute/_file/_fetch`; **Verifier=NONE** as explicit hard rule; Code-reviewer=RAG read-only; PR-commit=`get_history` to verify ADR refs; BC-switch Step 0 RAG lookup; ADR-generator prefers `list_adrs`/`query_docs`/`get_history` over folder listing). `AGENT-PIPELINE.md` max-iter table gained an "MCP tools allowed" column.
- **Phase 3 — Prompts**: Step 0 MCP lookup added to `bc-analysis`, `flow-analysis`, `pr-review`, `refactor` (inside Pre-edit gate), and `bc-implementation` (Step 0a). **Skipped**: 16 skills bulk pass — 5 are already RAG-native, 11 are creator-scaffolds that don't read project knowledge at runtime (Planner/Implementer enforce routing before invoking them). Recorded as a known follow-up if the user wants exhaustive coverage.
- **Phase 4 — Pre-edit + safety + memory + anti-patterns**: pre-edit checklist became MCP-first (prefer `query_docs`/`get_history` over raw file reads); explicit URL handling rule (`ctx_fetch_and_index` only for project URLs); architecture-suggestion guard (`query_docs` for governing ADR first); `safety.instructions.md` adds external-HTTP rule + "verifier MUST NOT use MCPs" rule; `agent-memory.instructions.md` adds pre-write `query_docs` dedupe check; `anti-patterns-critical.context.md` gains 4 BLOCKS-MERGE rules (double-MCP, raw `fetch_webpage` for project URLs, quoting ADRs from training data, MCP calls inside verifier).
- **Phase 5 — Changelog + retrospective**: `COPILOT-SETUP-CHANGELOG.md` Session 25 entry + this §14.

### Phase 6 — L1 RAG↔context-mode handoff + char-budget right-sizing (Session 26, commits `30986fe`, `d1acd821`, `e96ee55e`)

Three deliverables that landed together once the L1 read-side handoff was validated end-to-end:

- **New skill** `rag-with-memory` documenting the 3-call shape (`query_docs` → format → `ctx_index` → `ctx_search`), Step 0 mount probe, three-surface disambiguation table (`semantic_search` vs `memory.*` vs `ctx_*`), inline-chunks subagent pattern, and the multilingual recall caveat. Validated by 4 tests: 1 primary-agent POC + 2 subagent diagnostics + 1 fresh-window user-driven run (3/3 successful recalls at 60.8% latency improvement).
- **Parametric workspace mount** `${CONTEXT_MODE_WORKSPACE:-/workspace}` in `docker-compose.yaml` plus a Step 0 probe in the skill — forks that mount the container under a different path no longer break L1.
- **Right-sized `copilot-instructions.md`** from 11,975 → 7,409 chars (-38%) by collapsing §12 (MCP routing — already auto-loaded via `mcp-routing.instructions.md` with `applyTo: **`) and extracting §14 (batched-tasks detection) to a dedicated `batched-tasks.instructions.md` with `applyTo: **`. The maintainer ownership table's char budget was raised from a 4K hard limit to an 8K soft budget with rationale (the 4K target was set in Session 17 when the file held ~3K of content; current 14 sections + 4 domain constants + cross-link pointers will not fit).

Three follow-ups documented but not implemented:

- **LIMIT-1** (built-in subagent surface restriction): CONFIRMED via 3 explicit `tool_search` probes in a clean Session 26 subagent. No workaround besides the inline-chunks pattern (skill §"Delegating to a subagent"). Also confirmed for `runSubagent` invocations of named custom agents (`@copilot-setup-maintainer`).
- **LIMIT-2** (probe enforcement deferred): the workspace probe is a skill recommendation, not an enforced precondition. A future hook could fail-fast if `ctx_search` returns zero results because the mount differs from the previous session.
- **LIMIT-3** (multilingual FTS5 gap): documented in the skill and queued as a design input for Phase 7.1 of the context-mode roadmap. Out of scope for L1.

### What the case study confirms

- The **single-source-of-truth pattern** (Phase 1) is the most important hour of the rollout. Every later phase becomes a one-line pointer (`[mcp-routing.instructions.md](mcp-routing.instructions.md)`), so future rule changes happen in **one** place rather than fanning out across 20+ files.
- "**NEVER call both MCPs for the same atomic intent**" is the rule that pays for itself fastest — without it, agents will happily call `query_docs` AND `ctx_execute_file` for the same ADR "just to be thorough" and double the context cost.
- A **dormant** MCP can be wired with full precedence rules ahead of activation, as long as every reference carries an "applies once <hooks land>" gating clause. This decouples docs work from infra work.
- **Verifier-must-not-use-MCPs** is the exception that proves the rule. Mark it explicitly in the agent file _and_ in the pipeline orchestrator (`AGENT-PIPELINE.md` MCP column) — agents will ignore one or the other if the rule lives in only one place.
- Phase 0 + Phases 1-5 as **two separate commits** is the right granularity: pilot is small enough to read in one screen, full rollout big enough that splitting it further would lose the "one cohesive change" narrative.
- **L1 (manual read-side handoff) can ship ahead of L3 (hooks)** as a `_get_` + `_put_` + `_get_` pattern documented in a skill. The runtime savings are smaller than the hook-based flow, but the pattern itself is what unblocks recurring queries in long sessions — and the same `ctx_index`/`ctx_search` plumbing will be reused once `PostToolUse` writes the blob automatically.
- **Built-in subagents have a hard MCP surface restriction.** Neither `Explore` nor `runSubagent` invocations of named custom agents can call MCP tools or run `tool_search`. The inline-chunks pattern (parent fetches RAG, formats chunks as markdown, passes the blob in the subagent's prompt) is the only viable workaround. Record this in the skill that defines the subagent flow — not in the agent file alone — so future maintainers see it at the point of use.
- **Char budget for `copilot-instructions.md` is around 8K once cross-link pointers and domain constants are realistic**, not the 4K target inherited from when the file was a stub. Refactor toward `applyTo: **` instruction files when the root policy file approaches the budget — never delete unique policy to fit a number. Trim recipe: identify duplicates of canonical `applyTo: **` files, collapse to pointer; extract distinct topics to their own `*.instructions.md` with `applyTo: **`; keep domain constants (§8–§10) and unique policy in the root file.
- **Multilingual recall depends on which surface holds the indexed text.** RAG has a multilingual glossary; context-mode FTS5 has English-only stemming. Mixed-language sessions need either a glossary injection into the L1 blob (Phase 7.1 design input) or an English keyword in `ctx_search`. Document this caveat in the skill that ships the cache, not as a separate note that gets lost.
