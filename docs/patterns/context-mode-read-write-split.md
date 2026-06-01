# Context-mode read/write split — token economy & flow

> **Status:** Pattern documented after empirical validation (mixed-workload tests).
> **Companion ADR:** [ADR-0029](../adr/0029/0029-context-mode-mcp-sandbox.md).
> **When to read:** any time you ask "should I use context-mode for X?" — this is the rule.

## TL;DR

Describe the **job**, not the tool. The agent should infer the path automatically.

| Operation type | Tool path | Why |
|---|---|---|
| **READ / analyze / derive** | `ctx_execute_file` / `ctx_execute` (sandbox) | Large file content stays out of the conversation. Returns only the *result*. |
| **WRITE / edit / refactor** | `replace_string_in_file` / `create_file` / `multi_replace_string_in_file` (native VS Code) | Diffs are tiny. Native tools have proper undo, git tracking, permissions. |
| **EXECUTE on host (build, test, git)** | `run_in_terminal` (native VS Code) | Stateful, dev-environment-bound; not a sandbox concern. |

**These three paths NEVER overlap.** That non-overlap is the entire optimization.

Examples:

- `Przeanalizuj ten log i powiedz, co najczęściej się psuje` → sandbox read/derive path
- `Policz wszystkie użycia symbolu w repo` → sandbox read/derive path
- `Podsumuj ten duży plik przed refaktorem` → sandbox read/derive path
- `Zmień implementację w tym pliku` → native edit path
- `Uruchom build i testy` → host execution path

## Path normalization for ctx_execute_file

When using `ctx_execute_file`, the path is resolved inside the container, not on the host OS.

| Input path style | Container path (correct) |
|---|---|
| `docs/adr/0028/amendments/0028-004-per-collection-config-gap.md` | `/workspace/docs/adr/0028/amendments/0028-004-per-collection-config-gap.md` |
| `tools/rag/queries.yaml` | `/workspace/tools/rag/queries.yaml` |
| Host absolute path (`C:\...`, `/Users/...`, `/home/...`) | ❌ Do not pass host absolute paths to `ctx_execute_file` |

Rule: convert repo-relative `<relative>` to `<CONTEXT_MODE_WORKSPACE>/<relative>`.
Default mount is `/workspace`.

If unsure, probe once per session:

```shell
echo $CONTEXT_MODE_WORKSPACE
```

---

## The flow

```
┌──────────────────────── READ path (analysis) ──────────────────────┐
│                                                                     │
│   You ask Copilot Chat: "find every X in big-file.cs"               │
│                          │                                          │
│                          ▼                                          │
│                  ctx_execute_file (MCP)                             │
│                          │                                          │
│                          ▼                                          │
│           ┌───────────────────────────────────┐                    │
│           │  context-mode container            │                    │
│           │   $CONTEXT_MODE_WORKSPACE :ro     │ ← your source     │
│           │   (default /workspace, parametric) │   READ-ONLY       │
│           │   node sandbox (caps dropped)      │   READ-ONLY       │
│           │   DNS → AdGuard (allowlist)        │                    │
│           │   read_only rootfs, tmpfs /tmp     │                    │
│           └───────────────────────────────────┘                    │
│                          │                                          │
│                          ▼                                          │
│              Returns ONLY the derived answer                        │
│              (e.g. 3 numbers + 19 names ≈ 500 tok)                  │
│              Raw 8500-tok file content NEVER enters the chat        │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌──────────────────────── WRITE path (edits) ────────────────────────┐
│                                                                     │
│   Copilot decides: "I'll fix method Foo in big-file.cs"             │
│                          │                                          │
│                          ▼                                          │
│         replace_string_in_file  (NATIVE VS Code tool)               │
│         create_file             (NOT MCP, NOT context-mode)         │
│         multi_replace_string_in_file                                │
│                          │                                          │
│                          ▼                                          │
│           ┌───────────────────────────────────┐                    │
│           │  VS Code extension host process    │                    │
│           │   Normal filesystem permissions    │ ← YOUR perms      │
│           │   Diff visible in chat timeline    │                    │
│           │   Source Control + Undo as safety  │                    │
│           └───────────────────────────────────┘                    │
│                          │                                          │
│                          ▼                                          │
│                 File on disk modified                               │
│                 (cost ≈ diff size, not file size)                   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────── EXECUTE path (build / test / git) ──────────────┐
│                                                                     │
│   Copilot runs: dotnet build / dotnet test / git status / etc.      │
│                          │                                          │
│                          ▼                                          │
│              run_in_terminal  (NATIVE VS Code tool)                 │
│                          │                                          │
│                          ▼                                          │
│              Host shell, your dev environment                       │
│              Stateful (your SDK, your secrets, your CWD)            │
│              NOT a sandbox concern                                  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Why three paths and not one

- **Sandbox is for the most expensive thing (reading content).** A 1000-line file is ~12K tokens raw. Pulling it into the conversation just to count something is a tax.
- **Native edit tools are for the cheapest thing (write diffs).** A 5-line method swap is ~50 tokens emitted. Sandboxing it adds latency without saving tokens — and breaks undo/git/permissions integration.
- **Terminal is for stateful operations.** Builds, tests, git, package installs depend on your dev environment. They can't run in an ephemeral sandbox with `:ro` repo.

The split is not a tradeoff — it's three different cost optimizations stacked.

---

## Empirical token savings

Measured on this repository, mixed workloads (Copilot Chat, Claude Sonnet 4 / GPT-5-mini, real prompts).

### Per-operation savings (READ path)

| Workload | Without ctx | With ctx | Saving |
|---|---|---|---|
| Hash of 1 file | 3–5K tok (read) + wrong answer (hallucinated hex) | 200 tok | **~95%** (and correctness) |
| Count regex matches in 700-line file | 8.5K tok | 300 tok | **~96%** |
| List public methods in 1.5K-line service | 18K tok | 600 tok | **~97%** |
| Find files > 300 LoC across folder | 30–80K tok | 500 tok | **~98%** |
| Summarize 700-line log → 5 error lines | 8K tok | 300 tok | **~96%** |
| Extract section from indexed external doc | 4K tok | 400 tok | **~90%** |

### Per-operation savings (WRITE path)

| Workload | Native edit cost | Why already optimal |
|---|---|---|
| Replace 1 method body | ~80 tok (oldString + newString) | Model already knew the diff from prior read; emits only the change |
| Add a using directive | ~30 tok | Trivial diff |
| Add 1 new file | ~250 tok | One create_file call with content |
| Multi-file rename via multi_replace | ~50 tok per file × N | Linear in number of files, not file size |

WRITE path is already near-optimal without context-mode — that's why the read/write split works.

### Workload-level savings (realistic agent session)

A typical 10-step Copilot agent session that touches your repo:

| Step | Operation | Without ctx | With split | Saving |
|---|---|---|---|---|
| 1 | Read 3 large files to understand context | 30K tok | 1.5K tok (3 × ctx_execute_file derivation) | 95% |
| 2 | Search for usages of a symbol across BC | 15K tok (grep + multiple reads) | 800 tok (ctx_execute returns just hits) | 95% |
| 3 | Verify ADR rule for the change | 2K tok (RAG query — unchanged) | 2K tok (RAG, same) | 0% |
| 4 | Propose edit + apply | 200 tok (diff) | 200 tok (diff) | 0% |
| 5 | Apply 2 more edits | 400 tok | 400 tok | 0% |
| 6 | Run build, capture errors | 8K tok (full output) | 600 tok (ctx_execute filters to FAIL lines) | 93% |
| 7 | Fix one error | 300 tok | 300 tok | 0% |
| 8 | Re-run build | 4K tok | 400 tok | 90% |
| 9 | Run unit tests for the BC | 10K tok | 800 tok | 92% |
| 10 | Commit message draft | 200 tok | 200 tok | 0% |
| **Total** | | **70.1K tok** | **7.2K tok** | **~90%** |

**Realistic mixed-workload saving: 70–90% on conversation token cost.**

> Note: total Context Window saving will be smaller (~30–50%) because *System Instructions* + *Tool Definitions* are fixed overhead (~40K tok of the 192K budget). Read economy is dominated by Tool Results, where ctx-mode wins big.

### When the split saves nothing (be honest)

| Workload | Saving | Why |
|---|---|---|
| Pure RAG knowledge batch (10 ADR questions) | **0%** | Routes entirely to RAG MCP; context-mode is bystander |
| Single small-file edit (< 100 lines) | **~5%** | Read overhead is tiny; not worth the tool switch |
| Brainstorming / spec writing | **0%** | No file ops at all |
| Translation / wording suggestions | **0%** | Pure LLM work |

This is **by design** — the split optimizes the workloads that dominate agent token cost (multi-file reads + builds + test runs), not the ones that are already cheap.

---

## Security model (why the split is also safe)

```
                ┌─────────────────────────┐
                │   Threat surface         │
                └────────────┬────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        ▼                    ▼                    ▼
  Hallucinated         Prompt injection    MCP server bug
  destructive          via fetched URL     (path traversal)
  code in ctx_execute  → "rm -rf repo"
        │                    │                    │
        ▼                    ▼                    ▼
  $WORKSPACE :ro       AdGuard allowlist   read_only rootfs
  EROFS on write       blocks egress to    + cap_drop ALL +
  → no damage          attacker domain     no-new-privileges +
                                            non-root user +
                                            tmpfs /tmp 64m
        │                    │                    │
        └────────────────────┴────────────────────┘
                             │
                             ▼
                  ┌─────────────────────────┐
                  │ Net residual risk:       │
                  │ READ of secrets in repo  │
                  │ (mitigated by NOT        │
                  │  committing secrets)     │
                  └─────────────────────────┘
```

Native edit tools have a different threat surface (handled by VS Code's normal permission model + git history). The two paths' threat models do not compound — each is mitigated independently.

---

## How to discover the workspace mount path

The mount target inside the container is parametric. Default is `/workspace`, but forks/dev overrides can rename it via `CONTEXT_MODE_WORKSPACE` in `.env.context-mode` (see `docker-compose.yaml` — the env var is injected into the container so it's discoverable at runtime).

**Recipe** (run once per session, cache the result):

```
ctx_execute("shell", "echo $CONTEXT_MODE_WORKSPACE")
# → /workspace          (default)
# → /repo               (if a fork overrode it)
```

Use the returned value as the prefix for all absolute paths passed to `ctx_execute_file`, `fs.readFileSync` inside `ctx_execute`, etc. **Never hardcode `/workspace`** in tooling that may run against forked configurations.

---

## Anti-patterns to flag in code review

| Anti-pattern | Why bad | Fix |
|---|---|---|
| Sandbox writes via `fs.writeFileSync('$CONTEXT_MODE_WORKSPACE/...')` | Would bypass git/undo; only saved by `:ro` enforcement | Use native edit tools |
| `read_file` of a > 300-line file just to count something | Wastes ~5–15K tokens | Use `ctx_execute_file` with the filter |
| `run_in_terminal` for hash / regex / sandbox-able derivation | Bypasses sandbox audit trail | Use `ctx_execute` |
| `ctx_execute` for `dotnet build` | Sandbox can't reproduce dev env | Use `run_in_terminal` |
| `ctx_execute` for `git commit` | Sandbox is stateless / detached | Use `run_in_terminal` |
| Using relative paths (`'ECommerceApp.Application'`) inside `ctx_execute` | Sandbox cwd ≠ repo root → silent zero results | Use absolute paths under `$CONTEXT_MODE_WORKSPACE` (default `/workspace`) |

---

## Practical promp guidance

When asking Copilot to do mixed work, label the steps:

```
Step 1 (RAG / knowledge): <question>
Step 2 (context-mode / derivation): <task — give absolute paths under $CONTEXT_MODE_WORKSPACE (default /workspace)>
Step 3 (edit): <change to apply — let Copilot use native tools>
Step 4 (run): <build/test/git — let Copilot use run_in_terminal>
```

Explicit labels prevent weaker models (GPT-5-mini) from collapsing everything onto `run_in_terminal`.

---

## Integration with RAG — knowledge caching across recalls

The read/write/execute split is silent about a fourth path that **does** save tokens at workload level: re-reading the same RAG knowledge multiple times in one session. Each `query_docs` / `read_docs` call re-bills the same tokens. context-mode's FTS5 store can hold the result locally and serve subsequent recalls via BM25 — at the cost of one extra `ctx_index` call up front.

```
                       FIRST CALL                       SUBSEQUENT CALLS
                       ──────────                       ─────────────────

  query_docs("X")  ───────► RAG MCP                     ctx_search(            ───► context-mode
                            (embed + rank + return            queries=[...],         (FTS5 BM25 over
                             N chunks, billed)                source="rag-           local store, free)
                                  │                            cache-X")
                                  ▼                                  │
                            format as markdown                       ▼
                                  │                            ranked sections
                                  ▼                            with preserved
                          ctx_index(content, ───► context-mode  breadcrumbs / code blocks
                            source="rag-          (markdown-aware
                             cache-X")             chunking, FTS5
                                                   index, billed once)
```

| | First lookup | Second lookup | Third lookup | Tenth lookup |
|---|---|---|---|---|
| Without caching (direct RAG every time) | 1× RAG | 2× RAG | 3× RAG | 10× RAG |
| With caching (L1 handoff) | 1× RAG + 1× `ctx_index` | 1× RAG + 1× `ctx_index` + 1× `ctx_search` | (same) + 1× `ctx_search` | (same) + 8× `ctx_search` |

`ctx_search` against a local FTS5 store is effectively free vs. a RAG MCP round trip with embedding + ranking. Break-even is the **second** recall; after that, savings compound linearly.

**When the integration pays off** (re-bill avoided):

- Long debug / multi-step refactor referencing the same ADR or BC docs.
- Planner → implementer → verifier handoff: cache once in planner, all three agents read from cache.
- "We'll be working on X today" sessions.

**When the integration does NOT pay off** (extra `ctx_index` cost wasted):

- One-shot lookup answered once and forgotten.
- Pure code edit with no knowledge re-reads.
- The cache key already exists this session — `ctx_search` it first before re-RAG.

### Surface confusion is the real risk

VS Code exposes three near-identical "memory" surfaces. Without an explicit rule naming all three, weaker models reliably pick the most basic one. Empirical POC with a GPT-5-mini subagent (2026-05-27) used the **wrong** surface in all 3 handoff steps:

| Step | Should call | Subagent called | Why wrong |
|---|---|---|---|
| Knowledge | `query_docs` (RAG MCP) | `semantic_search` (VS Code embedded) | No chunk ranking; grep-style over workspace |
| Cache | `ctx_index` (context-mode FTS5) | `memory.create` (VS Code persistent notes) | Single-doc; no FTS recall |
| Recall | `ctx_search` (BM25 over FTS5) | `memory.view` + manual reading | No ranking; full doc returned |

Routing rules + skill mitigate this. See:

- Canonical rules with the three-surface disambiguation table: [`.github/instructions/mcp-routing.instructions.md` — RAG ↔ context-mode handoff section](../../.github/instructions/mcp-routing.instructions.md)
- Step-by-step skill with naming convention + markdown template + anti-patterns: [`.github/skills/rag-with-memory/SKILL.md`](../../.github/skills/rag-with-memory/SKILL.md)
- L2 shipped 2026-05-27 — single wrapper tool `query_docs_cached` collapses the 3 manual steps into 1 RAG call + 1 pass-through `ctx_index`. Both Python and .NET RAG servers expose it (Phase 7 + 7.3). Open observation on `.NET` `top_k` cap: [`docs/roadmap/context-mode-integration.md` Phase 7.4](../roadmap/context-mode-integration.md). Porting checklist: [`docs/rag/mcp-first-routing-migration-playbook.md` §13.10](../rag/mcp-first-routing-migration-playbook.md).

---

## References

- [ADR-0029 — context-mode MCP sandbox](../adr/0029/0029-context-mode-mcp-sandbox.md)
- [.github/instructions/mcp-routing.instructions.md](../../.github/instructions/mcp-routing.instructions.md) — canonical routing rules
- [docs/getting-started-context-mode.md](../getting-started-context-mode.md) — setup walkthrough
- [.github/context/known-issues.md](../../.github/context/known-issues.md) (KI-014) — first-run AdGuard bootstrap
- [.github/skills/rag-with-memory/SKILL.md](../../.github/skills/rag-with-memory/SKILL.md) — L1 RAG ↔ context-mode handoff
- [docs/roadmap/context-mode-integration.md](../roadmap/context-mode-integration.md) Phase 7 — L2 wrapper tool plan
