# Style Guides

Visual/UI decision rules for `ECommerceApp.Web`, split by concern so each file
stays short enough to read in one pass. These are **decision rules**, not a
color reference — the actual hex values live in one place:
[`ECommerceApp.Web/wwwroot/css/site.css`](../../ECommerceApp.Web/wwwroot/css/site.css)
(`:root` custom properties). Style guides link to those tokens by name; they
never repeat a hex value, so the guide can't drift from the CSS.

## Why this exists

Multiple agents (human and AI, often in concurrent sessions on this repo)
touch `ECommerceApp.Web` views independently. Without a written rule, each
one re-derives "which color/class for this button" from scratch, and the
app slowly grows inconsistent or duplicate one-off colors. These guides are
the single place to check before inventing a new style.

## Files

| File | Covers |
|---|---|
| [`colors.md`](colors.md) | Palette rationale + full token table, page canvas (paper vs. white), navbar/typography sizing, vendor-theming discipline |
| [`buttons.md`](buttons.md) | Which Bootstrap button class + visual weight (solid/outline/link) to use for which action type |

Add new files here as new concerns need their own rules (e.g. `forms.md`,
`tables.md`, `badges.md`) — keep splitting rather than growing one file past
a single read.

## Related

- Color tokens: [`ECommerceApp.Web/wwwroot/css/site.css`](../../ECommerceApp.Web/wwwroot/css/site.css) `:root` block
- Copilot routing entry: [`.github/instructions/docs-index.instructions.md`](../../.github/instructions/docs-index.instructions.md)
