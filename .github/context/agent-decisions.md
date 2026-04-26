# Agent Decisions Log

> **Append-only log of in-session corrections to AI agent behavior.**
> Operational, not architectural. For _why the system is built that way_ → ADRs in `docs/adr/`.
> For _how the agent should behave / what it missed / what to never repeat_ → here.
>
> **Read before non-trivial agent work** to avoid repeating past mistakes.
> **Append after any meaningful correction** that an agent received during a session.

---

## When to write here vs. an ADR

| Situation                                                                   | Goes to              |
| --------------------------------------------------------------------------- | -------------------- |
| Architectural decision (BC, pattern, technology, cross-cutting)             | ADR                  |
| "Will I want to explain this to a new dev in a year?"                       | ADR                  |
| Agent missed a guard, forgot to read a context file, ignored an instruction | `agent-decisions.md` |
| Naming/format nit that the agent kept getting wrong                         | `agent-decisions.md` |
| Tool selection mistake (used wrong skill, wrong scope)                      | `agent-decisions.md` |
| Recurring drift you keep correcting in chat                                 | `agent-decisions.md` |

**Promotion rule**: if the same correction appears **2+ times** → promote to a permanent rule:

- Architectural rule → `anti-patterns-critical.context.md` or relevant `*.instructions.md`
- Workflow rule → relevant agent file (`bc-switch.md`, `code-reviewer.md`, etc.)
- Decision-level rule → new ADR via `@adr-generator`

When promoted, mark the entry **Status: Promoted → ADR-NNNN** (or file ref) and keep it for history.

---

## Entry format (Variant A — required)

```markdown
## YYYY-MM-DD — <agent-name> / <area>

- **Context**: What the agent tried to do.
- **Decision**: What the human decided instead (NO / YES / different approach).
- **Rationale**: Why — link to project-state line, ADR, instruction file, or commit.
- **Action**: What changes to instructions/agents/skills should follow (one concrete action).
- **Promote?**: When does this graduate to a permanent rule (e.g. "after 2nd occurrence → anti-patterns-critical").
- **Status**: Open | Resolved | Promoted → <ref>
```

Rules:

- One H2 per entry. **Append, do not edit history**.
- Date in `YYYY-MM-DD` format (today's real date).
- Keep entries scannable — 5–10 lines each. Link, don't quote.

---

## 2026-04-27 — Copilot / RAG MCP server config location

- **Context**: Agent created `.github/copilot/mcp.json` to register the RAG MCP server, then told the user the server was registered. VS Code's MCP browser showed no servers.
- **Decision**: The correct location is `.vscode/mcp.json`. `.github/copilot/mcp.json` is not read by VS Code's MCP server browser — it is only relevant for GitHub Codespaces / future GitHub Copilot tooling.
- **Rationale**: VS Code reads workspace MCP config from `.vscode/mcp.json`. The `.github/copilot/` path has no VS Code runtime effect.
- **Action**: Always create `.vscode/mcp.json` for VS Code MCP registration. Keep `.github/copilot/mcp.json` as a secondary copy for Codespaces compatibility only.
- **Promote?**: After 2nd occurrence → add to `docs-index.instructions.md` or a tooling note.
- **Status**: Resolved
- All entries in **English** for AI parsability.

---

## Example entry (template — replace with real ones)

## 2026-04-21 — bc-switch / Sales/Payments

- **Context**: Agent attempted to delete `Application/Services/Payments/PaymentHandler.cs` as part of the atomic switch.
- **Decision**: Do NOT delete during the switch.
- **Rationale**: `project-state.md` notes "Legacy `PaymentHandler` retained for Step 5 cleanup". The agent skipped reading project-state and assumed atomic switch implies delete-all-legacy.
- **Action**: Strengthen `bc-switch.md` Step 1 to require quoting the project-state line for the BC before any delete operation.
- **Promote?**: After 2nd occurrence → add explicit anti-pattern to `anti-patterns-critical.context.md` ("No legacy delete without project-state quote").
- **Status**: Open

---

## Entries

<!-- Append new entries below this line, newest at the bottom. -->
