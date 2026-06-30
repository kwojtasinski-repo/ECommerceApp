---
applyTo: "**"
---

# Pre-Edit Checklist (mandatory before any edit)

Before proposing or committing changes, do these steps:

0. Read `.github/context/agent-decisions.md` for relevant prior corrections.
- MCP-first variant: use `query_docs("<area>")` for targeted chunks.
- For known ADR id, use `get_history(id="NNNN")`.
- Never guess project-specific rules from training data.

1. Read target file(s) and relevant related files.
- For files >500 lines: prefer `ctx_execute_file(path)` for structure first, then `read_file` for exact edit region.

2. Read relevant ADRs.
- Preferred: `get_history(id)`.
- Fallback: direct file read only when RAG is empty.

3. Read applicable stack instructions under `.github/instructions/`.

4. Search impact.
- Check usages/references, migrations, clients, and affected areas.

5. Validate locally.
- Run `dotnet restore`, `dotnet build`, `dotnet test` (or explain why not possible).

6. Include tests for behavioral changes.

7. Include rollback/mitigation for risky changes.

8. Open PR for review unless user explicitly requests otherwise.

Extra routing/safety rules:

- Project-related external URL -> `ctx_fetch_and_index` only (not raw `fetch_webpage`).
- Before proposing architecture changes, run `query_docs("<topic>")` to check existing ADR coverage.
- Never call both RAG and context-mode for one atomic intent.

## Clarification policy (stop and ask when unclear)

Ask before any write/db/git action when:
- BC/ADR/file reference is ambiguous (2+ plausible matches).
- Scope is binary and unspecified.
- Blocker mentioned but resolution path is missing.
- Action is destructive/hard-to-reverse and target is ambiguous.
- Required number/name/path was omitted.

Host-aware asking:
- VS Code: use `vscode_askQuestions` with freeform input (options only when closed set).
- Visual Studio/other: plain chat question with numbered options, then stop.
- Non-interactive host: fail loudly with missing info (do not guess).

## Capability verification rule (external tools)

Before documenting external tool capabilities (runtime/language/CLI/feature): verify empirically.
- context-mode runtimes: `ctx_doctor`.
- sandbox language smoke: small `ctx_execute` call.
- binaries: `docker exec <container> which <cmd>` or `<cmd> --help`.

Schema enums/upstream README/prior memory are not proof of shipped capability.

## Post-edit obligations

1. If corrected on a new recurring mistake, append entry to `.github/context/agent-decisions.md` (Variant A).
2. If correction appears 2nd time, promote to permanent rule (instruction/anti-pattern/ADR).
3. If any `.github/` or `docs/` file changed: invoke `@copilot-setup-maintainer` as last step.
- Minimum always-run: Workflow 11.
- If the task changed Copilot inventory or structure: add Workflow 7 (refresh setup-state).
- If unsure/scope wide: Workflow 6, then refresh setup-state instead of the changelog.
