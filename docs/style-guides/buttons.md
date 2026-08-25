# Buttons

Which Bootstrap button class to put in the view, for every kind of action in
`ECommerceApp.Web`. The HTML always uses stock Bootstrap class names
(`btn-primary`, `btn-outline-warning`, ...) — `site.css` is what re-themes
them. **Never invent a new `.btn-*` class or a fresh hex value.** If nothing
below fits, add a row here first, then use it.

## Source of truth

All colors referenced here are CSS custom properties defined once in
[`site.css`](../../ECommerceApp.Web/wwwroot/css/site.css) `:root`:
`--brand`, `--brand-dark`, `--accent`, `--caution`. Bootstrap's own
`--bs-danger` / `--bs-success` are used as-is for destructive/confirm actions
— they're already dark/saturated enough to not need a custom token.

## Visual weight: solid vs outline vs link

This is a separate axis from color — color says *what kind* of action it is,
weight says *how much attention it should demand*. Three tiers, heaviest
first:

| Tier | Renders as | Use for |
|---|---|---|
| **Solid** | Filled background, white text | Anything that needs to register as a clickable control on its own — a primary CTA, or any repeated table-row action (details/edit/delete/confirm) |
| **Outline** | Border + colored text, no fill | An action that should read as present but deliberately quieter than its neighbors — currently only `btn-outline-secondary` for cancel/back |
| **Link** (`btn-link`) | No border, no fill — looks like plain text | An action that must be a `<button>`/form-submit for technical reasons but should be visually indistinguishable from an ordinary link |

**Naming trap:** `btn-outline-info`, `-warning`, `-danger`, `-success`, and
`-primary` all render **Solid** here despite the class name — only
`btn-outline-secondary` stays genuinely Outline. The HTML keeps Bootstrap's
stock `-outline-` class names (so `asp-` tag helpers and existing markup
don't need touching); `site.css` is what actually decides the weight. Don't
infer rendering from the class name — check this table.

**Why outline defaults to "avoid" here, specifically:** `ECommerceApp.Web`
has no colored/dark surfaces — the page background is `--paper` (near-white)
and cards/tables are plain white. An outline button has no fill to contrast
against that background, so on this app it reads as weak almost everywhere
it's tried (this is what happened on the product list before the row
actions were switched to solid). Outline only earns its keep when the
*point* is to look quieter than a nearby solid button (cancel sitting next
to save) — if there's no such neighbor to be quieter than, use solid.

`btn-link` is not "an even quieter outline" — it's a different job entirely:
making a `<button>` disappear into a list of real `<a>` links. Current uses:
"Wyloguj" inside the account dropdown (`_Layout.cshtml`, sits among `<a
class="dropdown-item">` siblings) and "Send verification email" in
`Identity/Manage/Email.cshtml` (an inline, low-stakes settings action, not a
page CTA). Never use `btn-link` for a table-row action or anything that
needs to be found by scanning — it has no visual footprint at all.

## Decision table

| Action type | Example | Bootstrap class | Weight | Rendering |
|---|---|---|---|---|
| Page's one primary CTA | "Zapisz", "Dodaj do koszyka", "Dodaj kategorię" | `btn-primary` | Solid | `--accent` |
| Identity-colored action outside a table row | "Szczegóły" (product card), "Dodaj adres", "Utwórz konto", "Dodaj korektę" | `btn-outline-primary` | Solid | `--brand` |
| Details / preview (table row) | "Szczegóły", "Podgląd" | `btn-outline-info` + `btn-sm` | Solid | `--brand-dark` |
| Edit (table row) | "Edytuj" | `btn-outline-warning` + `btn-sm` | Solid | `--caution` |
| Delete / destructive (table row) | "Usuń" | `btn-outline-danger` + `btn-sm` | Solid | Bootstrap `--bs-danger` |
| Confirm / approve (table row) | "Zatwierdź" | `btn-outline-success` + `btn-sm` | Solid | Bootstrap `--bs-success` |
| Cancel / back / no-op next to a solid action | "Anuluj", "Wróć" | `btn-secondary` or `btn-outline-secondary` | Outline | Bootstrap default grey — **not** rebranded, stays quiet on purpose |
| `<button>` that must look like a plain link | "Wyloguj" (dropdown), "Send verification email" | `btn-link` | Link | Inherits surrounding text/link color, no fill or border |

Row actions always keep `btn-sm` — small size is part of why solid fill
doesn't overwhelm a table.

**Resolved:** `btn-outline-primary` was flagged as an open item (same
no-fill-to-contrast-against problem as the row actions) and has since been
switched to Solid `--brand` in `site.css`, covering every current use:
product cards (Home, Storefront, ByTag), profile ("Szczegóły", "Dodaj
adres"), checkout ("Wróć do koszyka", "Utwórz konto"), and Stock ("Dodaj
korektę"). Note a couple of those reads more like tier 1 (single-CTA) or
tier "cancel/back" than "identity-colored secondary action" — left as
`btn-outline-primary` for now since the rendering fix covers them either
way; reclassify into a different row here if one of them ever needs a
different *color*, not just weight.

## Rule for a new/unlisted action

1. Is it the single most important action on the page (form submit, add to
   cart)? → `btn-primary`, Solid.
2. Is it a repeated action inside a table row? Pick the closest Bootstrap
   semantic match — `info` for view/preview, `warning` for edit, `danger`
   for delete, `success` for confirm — as `btn-outline-<semantic>` +
   `btn-sm`. `site.css` renders it Solid automatically. Default to Solid for
   anything in this bucket; only drop to Outline if it must look quieter
   than a solid sibling right next to it.
3. Is it a standalone brand-colored action, not in a table row and not the
   page's one primary CTA (e.g. a per-card "Szczegóły")? → `btn-outline-primary`.
   `site.css` renders it Solid `--brand` automatically.
4. Is it a `<button>` that only exists for technical reasons (form submit)
   but should be invisible as a "button" — blending into a list of real
   links or reading as plain inline text? → `btn-link`.
5. None of the above fits? Don't add a new color or a new weight tier. Add a
   row to the table above first (pick a semantic + weight that fits, or flag
   it for a token decision), then use it — so the next agent doesn't have to
   re-derive the same choice.
