# ADR-0033: GO-Gate Hooks — No File Edits Until the Human Writes IMPLEMENT

**Status**: Accepted — guardrail for interactive sessions, known gaps documented
**Scope**: Agent tooling — `tools/hooks/`, `.claude/hooks/`, `.github/hooks/`, `.claude/settings.json`

## What this decision covers
Why edit tools are blocked for AI agents until the human writes `IMPLEMENT`, why the design is one
tool-agnostic core plus thin Claude/Copilot adapters, and why it is fail-open and edit-tools-only.

## Files in this folder

| File | Purpose | When to read |
|------|---------|--------------|
| 0033-go-gate-hooks.md | Decision table, verified findings, alternatives, known gaps, test command | Changing the gate, adding an adapter, or debugging why an agent edit was denied |

## Key rules
- Only an uppercase `IMPLEMENT` at the start of the prompt (optionally after `APPROVE`) unlocks edits; `HOLD`/`STOP` re-lock.
- The core never names a tool; adapters never hold decision logic.
- Every failure path allows (fail-open); a deny is JSON on stdout with exit 0.
- Do not "improve" a verified workaround without re-probing the live surface.
- Disable locally with `ECOMMERCEAPP_GATE=off`; CI (`CI`, `GITHUB_ACTIONS`) bypasses automatically.

## Related ADRs
- None; companion docs: `.github/AGENT-PIPELINE.md` (GO gate section)
