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
