# Research plan — hooks usage

## Phases and gates

| Phase | Output | Gate |
|---|---|---|
| 1. Plan (this file) | `planning/*.md` | **Checkpoint 1** — scope approval |
| 2. Gather internal | `analysis/findings/codebase-hooks.md` | none (already within approved scope) |
| 3. Gather external — official docs | `analysis/findings/external-official-docs.md` | — |
| 4. Gather external — GitHub repos | `analysis/findings/external-repos.md` | **Checkpoint 2** if a new source family appears (e.g. Copilot cloud agent docs, pre-commit framework) |
| 5. Gather external — blogs + issues | `analysis/findings/external-blogs.md`, `external-issues.md` | — |
| 6. Verify | `analysis/findings/99-verification.md` | **Checkpoint 3** if contradictions / weak evidence |
| 7. Synthesize | `findings.md`, `decision-matrix.md`, `recommendations.md`, `analysis/findings/00-summary.md`, `98-rejected.md` | — |

Gathering files contain raw evidence only (quote / link / date / rating). No synthesis until phase 7.

## Gathering strategy

### Internal (read-only, repo paths + line numbers)
- `.claude/settings.json`, `.claude/hooks/go-gate-adapter.mjs` (+ test)
- `.github/hooks/go-gate.json`, `.github/hooks/go-gate-adapter.mjs` (+ test)
- `tools/hooks/gate-core.mjs`, `gate-cli.mjs`, `gate.config.json`, `README.md`, `gate-core.test.mjs`
- `docs/adr/0033/0033-go-gate-hooks.md`, `docs/adr/0033/README.md`
- `.github/context/agent-decisions.md` entries on hooks (auto-cache hook, context-mode Phase 3 hooks)
- `docs/rag/auto-cache-hook.md` (existing Copilot PostToolUse hook in this repo)
- `.claude/skills/linq-complexity-discovery/SKILL.md` + `.github/instructions/efcore.instructions.md` (example of a heuristic rule enforced by skill, not hook)

### External
- **(a) Official docs** — fetch and quote directly:
  - Anthropic Claude Code: hooks reference, hooks guide, settings / permissions, security considerations.
  - VS Code: Copilot agent hooks page; custom instructions page.
  - GitHub Docs: Copilot CLI hooks / config; Copilot coding agent (cloud) hooks / environment; repository custom instructions.
  - pre-commit framework docs only if Checkpoint 2 approves.
- **(b) GitHub repositories** — search for real `.claude/settings.json` with `hooks`; target ≥ 5 independent
  repos/authors. For each: link, which events, what the hook does, fail mode, tests/docs present.
- **(c) Engineering posts** — Anthropic engineering blog, posts with concrete configs. Prefer ≥ 2 independent authors per claim.
- **(d) Issue trackers** — `anthropics/claude-code` issues on hooks (Windows, latency, exit codes, loops,
  permissions interaction); `microsoft/vscode` / `github/copilot-cli` if reachable.

### Tooling
- Repo docs/ADRs: RAG `query_docs` / `get_history` first, direct read as fallback (stated in findings).
- External: `fetch_webpage` on specific URLs found via official doc navigation and GitHub search
  (`github_text_search` / `github_repo` if available). No URL is cited unless it was opened in this run.

## Verification approach

- Code vs ADR-0033 (does implemented fail mode / exit code / matcher behaviour match the ADR?).
- Official docs vs repo practice (do repos use exit 2 vs JSON deny as docs describe?).
- Claude vs Copilot hook semantics (event names, payload, blocking semantics, Windows/PowerShell behaviour).
- Each H1–H8 checked against ≥ 1 doc fact and ≥ 1 independent practice source where possible.
- Date check: hook APIs change quickly; record doc access date and flag claims older than ~6 months.

## Reliability scale

| Rating | Meaning |
|---|---|
| High | Official vendor doc opened this run, or code opened and line cited |
| Medium | Real repo config opened (single repo), or engineering post with concrete config |
| Low | Opinion / blog without config, single author, search snippet only |
| Single-author | Added as a flag when all evidence for a claim comes from one author |

## Risks

- Official docs may have moved; record redirects. If a page cannot be fetched → "not verified", never paraphrased from memory.
- GitHub search tooling may be rate-limited or unavailable → fewer repos; reported as a gap.
- Copilot hooks are Preview → semantics may differ from ADR-0033's 2026-10-13 findings; report deltas.
