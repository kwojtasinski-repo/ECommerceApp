---
name: setup-auto-cache-hook
description: >
  Install and wire up the L3 RAG-to-context-mode auto-cache hook for a NEW project.
  Every RAG tool response is auto-indexed into context-mode's FTS5 store, so subsequent
  recalls cost zero RAG re-bill. Covers hook drop-in, source-label convention,
  AUTO_CACHE_DEBUG, and the "context-mode not running" silent-no-op trap.
argument-hint: "<project-name> [--debug]"
---

# setup-auto-cache-hook — wire up the L3 RAG auto-cache

The L3 hook is a host-side PostToolUse fan-out: after every RAG tool call
(`query_docs`, `read_docs`, `get_history`, `query_docs_cached`, `list_adrs`), it
auto-indexes the response into context-mode's FTS5 store under a deterministic
`rag-auto-*` source label.

Net effect: the agent calls RAG ONCE per topic; future recalls within the session use
`ctx_search(source="rag-auto-")` for zero-token-cost lookup.

See [ADR-0029 Amendment 1](../../../docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md)
and [docs/rag/auto-cache-hook.md](../../../docs/rag/auto-cache-hook.md).

---

## When to use

- Project has BOTH RAG and context-mode running and the agent regularly re-reads the
  same docs.
- Migrating from L2 (`query_docs_cached`) to L3 — the hook makes the explicit cache
  call unnecessary.
- Recovering after a hook deletion / accidental revert.

## When NOT to use

- Project has only RAG, no context-mode → there's nothing to cache into.
- Project has only context-mode, no RAG → there's nothing to fire the hook from.
- Single-shot use-case (CI script that runs one query and exits) — the hook adds
  startup overhead with no recall benefit.

---

## Steps

### 1. Prerequisites

- E1 (RAG) up and reachable: `query_docs("smoke")` returns at least one chunk.
- E2 (context-mode) up and reachable: `ctx_index("x", "smoke")` then
  `ctx_search(["x"], "smoke")` returns 1 result.
- A VS Code workspace OR a host that supports PostToolUse hooks (Claude Desktop,
  Cursor, etc.). Plain Copilot Web does NOT support host-side hooks.

### 2. Copy the hook files

From the ECommerceApp checkout:

```sh
mkdir -p .github/hooks
cp <ecommerceapp-checkout>/.github/hooks/auto-cache.mjs           .github/hooks/
cp <ecommerceapp-checkout>/.github/hooks/auto-cache.probes.mjs    .github/hooks/
cp <ecommerceapp-checkout>/.github/hooks/posttooluse-chain.mjs    .github/hooks/
cp <ecommerceapp-checkout>/.github/hooks/posttooluse-chain.sh     .github/hooks/   # bash fallback
cp <ecommerceapp-checkout>/.github/hooks/context-mode.json        .github/hooks/
chmod +x .github/hooks/posttooluse-chain.sh
```

Hook chain layout:

| File | Role |
|---|---|
| `context-mode.json` | Declares the PostToolUse / PreToolUse / UserPromptSubmit / PreCompact / SessionStart commands |
| `posttooluse-chain.mjs` | Node ESM fan-out wrapper invoked by `context-mode.json` (cross-platform) |
| `posttooluse-chain.sh` | Bash fallback for headless Linux/macOS (SSH/CI) |
| `auto-cache.mjs` | The actual cache logic: detect RAG response, derive source label, call `ctx_index` |
| `auto-cache.probes.mjs` | Self-test probes — verify hook is wired correctly |

### 3. Register the hook with VS Code

`.vscode/mcp.json` does NOT host hooks. They live in `.github/hooks/context-mode.json`
(this repo's convention) and VS Code reads them via the
[Copilot Hooks spec](https://code.visualstudio.com/api/extension-guides/mcp).

Most VS Code MCP integrations auto-discover `.github/hooks/context-mode.json`. If
yours doesn't, add this to your user `settings.json`:

```jsonc
{
  "chat.mcp.hooks.configPath": ".github/hooks/context-mode.json"
}
```

### 4. Smoke-test the hook chain

Run the bundled probe:

```sh
node .github/hooks/auto-cache.probes.mjs
```

Expected: all probes pass with "✅" markers. Failures print the exact line of
`auto-cache.mjs` that misbehaved.

### 5. Verify the source-label convention

The hook derives `source` deterministically from the RAG response. Standard labels:

| Trigger | Resulting `source` |
|---|---|
| `query_docs_cached` (L2 passthrough) | `rag-cache-...` |
| `get_history(id="NNNN")` | `rag-auto-adr<NNNN>` |
| Question mentions ADR id | `rag-auto-adr<NNNN>-<hash8>` |
| `bc=` filter present | `rag-auto-<slug(bc)>-<hash8>` |
| `query_docs` orientation | `rag-auto-orient-<hash8>` |
| Default | `rag-auto-q-<hash8>` |

All `rag-auto-*` labels are recallable with the partial-match `source="rag-auto-"`.

### 6. End-to-end smoke

In an agent chat:

```
1. query_docs("how does the auto-cache hook work")
   → returns RAG chunks
   → hook fires automatically; PostToolUse `ctx_index` runs
2. ctx_search(["auto-cache hook"], "rag-auto-")
   → expected: the same content the RAG call returned, indexed under rag-auto-q-<hash>
```

If step 2 returns empty, the hook didn't fire — see Debug mode.

### 7. Debug mode

Set the env var before launching VS Code (or the agent host):

```sh
export AUTO_CACHE_DEBUG=1   # POSIX
# PowerShell:
$env:AUTO_CACHE_DEBUG = "1"
```

The hook writes a structured log to stderr on every fire — RAG tool name, derived
source label, character count indexed, success/failure of the `ctx_index` call.

Turn off (`AUTO_CACHE_DEBUG=0` or unset) once verified. Debug output is noisy.

### 8. Optional: project-specific source prefix

For projects that want to namespace their cache (e.g. a monorepo with two RAG sources),
edit `auto-cache.mjs` line ~12:

```js
const SOURCE_PREFIX = 'rag-auto-';
// change to e.g. 'acmeapp-rag-' for project-specific namespacing
```

If you do this, update all recall calls accordingly and tell the agent's
operating instructions about the new prefix.

---

## Common mistakes

- **Installing the hook before context-mode is up.** Hook fires, tries `ctx_index`,
  gets connection-refused, swallows the error → looks like the hook doesn't work.
  Always verify `ctx_doctor()` is green BEFORE enabling the hook.
- **Wrong source label prefix.** Edited `SOURCE_PREFIX` in `auto-cache.mjs` to
  `myproject-` but kept agent instructions referring to `rag-auto-`. Agent's
  `ctx_search(source="rag-auto-")` returns empty even though caching is working —
  data is under `myproject-` instead.
- **Leaving `AUTO_CACHE_DEBUG=1` on permanently.** Noisy stderr clutters the agent's
  view of tool errors. Use only when actively debugging.
- **Editing `auto-cache.mjs` without re-running the probe.** Probe catches 80% of
  breakage (regex changes, label format drift, missing `await`). Always
  `node .github/hooks/auto-cache.probes.mjs` after edits.
- **Hooking via `context-mode.json` but the agent host is Copilot Web.** Copilot Web
  doesn't run host-side hooks. The hook never fires. Symptom: looks like nothing is
  cached after dozens of RAG calls. Confirmation: `ctx_search(source="rag-auto-")` is
  always empty.
- **Deleting `posttooluse-chain.sh` because "we don't use bash".** It's the headless
  fallback for SSH remotes and CI. Keep it.

---

## Worked example: AcmeApp installation

1. RAG + context-mode both up (E1, E2 done).
2. `cp <ecommerceapp>/.github/hooks/* .github/hooks/`.
3. `chmod +x .github/hooks/posttooluse-chain.sh`.
4. `node .github/hooks/auto-cache.probes.mjs` → all green.
5. Reload VS Code MCP servers.
6. Smoke: `query_docs("acmeapp architecture")` → RAG returns 3 chunks.
7. `ctx_search(["acmeapp architecture"], "rag-auto-")` → returns the cached markdown
   under source `rag-auto-q-<hash8>`. ✅
8. `AUTO_CACHE_DEBUG=0` (default) — quiet operation from now on.

---

## Related skills / docs

- [.github/skills/setup-rag-new-project/SKILL.md](../setup-rag-new-project/SKILL.md) (E1 — RAG must be up first)
- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) (E2 — context-mode must be up first)
- [.github/skills/rag-with-memory/SKILL.md](../rag-with-memory/SKILL.md) — the manual L1 + L2 caching skill the L3 hook replaces
- [.github/instructions/mcp-routing.instructions.md](../../instructions/mcp-routing.instructions.md) — §"RAG ↔ context-mode handoff" L3 section
- [docs/rag/auto-cache-hook.md](../../../docs/rag/auto-cache-hook.md) — operator guide
- [docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md](../../../docs/adr/0029/amendments/0029-001-host-side-rag-auto-cache.md) — design + rationale
- [.github/hooks/](../../hooks/) — reference hook chain
