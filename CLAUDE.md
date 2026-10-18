# CLAUDE.md

Project docs live in [`README.md`](README.md) and [`docs/README.md`](docs/README.md) — read those for
architecture and setup. This file only covers what's specific to working here as Claude Code.

## This repo has an extensive Copilot instruction set — use it too

`.github/instructions/*.instructions.md` files carry per-area rules (frontend, EF Core, testing,
migrations, etc.), each scoped with `applyTo:` globs. They're written for GitHub Copilot but the rules
themselves are tool-agnostic — read the ones matching the files you're about to touch before editing.
Start at [`.github/instructions/agent-workflow.instructions.md`](.github/instructions/agent-workflow.instructions.md)
(front door for task routing) or [`.github/instructions/docs-index.instructions.md`](.github/instructions/docs-index.instructions.md)
(full routing table).

## Delegated work must show progress

Every subagent or long task gets a numbered list of small steps, shown to the user up front, and writes a
one-line progress file after each step (e.g. `progress.txt` in the scratchpad: `3/9 done: per-class runs`).
When asked for status, answer "step N of M done, current X, remaining Y". Split anything expected to run 30+
minutes into shorter agents that each report back.

## LINQ over EF Core: show the SQL when it is not obvious

When you write or change LINQ over a `DbSet` (mixed `&&`/`||`, nullable columns, `TypedId`/converters,
`Any`/`All`/`Contains`, `ExecuteUpdate`/`ExecuteDelete`, `Join`/`GroupBy`, predicates duplicated across methods),
use the `linq-complexity-discovery` skill. It generates the SQL deliberately (captured from the real repository method
with command interceptors on a SQL Server context that never connects, also for `ExecuteUpdate`/`ExecuteDelete`;
InMemory cannot do it) and keeps it in a `// SQL (EF Core, SQL Server):` comment
above the query. Never write that SQL from memory, and when you change such a query regenerate or delete the comment.
Canonical rules: [`.github/skills/linq-complexity-discovery/SKILL.md`](.github/skills/linq-complexity-discovery/SKILL.md),
short form in [`.github/instructions/efcore.instructions.md`](.github/instructions/efcore.instructions.md).

## GO gate (edits locked until you write `IMPLEMENT`)

Hooks in `.claude/settings.json` deny `Edit`/`Write`/`NotebookEdit` until the human's prompt starts with
`IMPLEMENT` (optionally `APPROVE IMPLEMENT`); `HOLD`/`STOP` re-lock. If an edit is denied, ask the user and
do not retry; only after their explicit approval run the `node tools/hooks/gate-cli.mjs grant <sessionId>`
command printed in the deny message. Writing to `.github/plans/**`, `docs/research/**`, `docs/reports/**` is
always allowed. A tool's own permission refusal is a separate layer from the gate — see
[`tools/hooks/README.md`](tools/hooks/README.md). Design and gaps: [ADR-0033](docs/adr/0033/README.md).

## Frontend / UI work (`ECommerceApp.Web`)

Before adding or changing any button, color, or style in a `.cshtml` view or `wwwroot/css`:

- Read [`docs/style-guides/colors.md`](docs/style-guides/colors.md) — palette rationale, full token
  table, page canvas, navbar/typography sizing, vendor-theming discipline.
- Read [`docs/style-guides/buttons.md`](docs/style-guides/buttons.md) — which Bootstrap class and visual
  weight (solid/outline/link) for which action. Don't invent a new `.btn-*` class or hardcode a new hex
  value; extend the relevant style guide's decision table first if nothing fits.
- Color tokens (`--brand`, `--accent`, `--caution`, ...) are defined once in
  [`ECommerceApp.Web/wwwroot/css/site.css`](ECommerceApp.Web/wwwroot/css/site.css) `:root`. Every override
  elsewhere references those tokens by name — never a fresh literal color.
- General Bootstrap 5 / JS-module conventions:
  [`.github/instructions/frontend.instructions.md`](.github/instructions/frontend.instructions.md).

More style guides (forms, tables, etc.) will land under `docs/style-guides/` as they're written — check
[`docs/style-guides/README.md`](docs/style-guides/README.md) for the current list.
