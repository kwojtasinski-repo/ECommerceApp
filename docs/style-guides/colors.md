# Colors & Visual Identity

General design conventions for `ECommerceApp.Web` — palette, page canvas, and
typography/navbar sizing. For button-specific class/weight decisions, see
[`buttons.md`](buttons.md) instead; this file doesn't repeat that table.

## Where this came from

The original template was an untouched ASP.NET MVC starter look (default
`#1b6ec2` blue, base `font-size: 14px`, no brand identity). A short
competitor/trend scan (Allegro, Zalando, 2026 e-commerce UI trends) led to a
deliberate, non-copied identity, built on one rule bigger players share:
**one brand color + one CTA accent, everything else neutral** — not a wall of
saturated colors. Two supporting facts from that scan shaped the specifics:

- 2026 trend is warm neutrals (bone/sand backgrounds, charcoal text) over
  stark white/black, with a warm orange/amber tone as the highest-converting
  CTA color.
- The European Accessibility Act (WCAG 2.1 AA, binding in the EU since June
  2025) requires real contrast and adequately sized click targets — this is
  why menu text/icons were enlarged, not just a taste preference.

## Source of truth

Every hex value below is a CSS custom property defined once in
[`site.css`](../../ECommerceApp.Web/wwwroot/css/site.css) `:root`. Nothing
else in the codebase should hardcode one of these colors as a literal — they
reference the token by name, so the palette can change in one place.

| Token | Value | Role |
|---|---|---|
| `--brand` | `#0F5C56` (deep teal) | Site identity: links, `nav-pills`, dropdown active state, `text-primary`/`bg-primary`, form focus rings |
| `--brand-dark` | `#0B453F` | Navbar brand text, "Szczegóły" row-action buttons, hover state for brand-colored elements |
| `--brand-tint` | `#E4EFEC` | Reserved, not currently used by any rule — light tint variant of `--brand` if a soft-fill surface is ever needed |
| `--accent` | `#E8792C` (warm amber) | The one CTA color — `.btn-primary` only ("Zapisz", "Dodaj do koszyka", navbar brand's accent dot) |
| `--accent-dark` | `#C9631E` | `.btn-primary` hover/active |
| `--caution` | `#92400E` (muted amber-brown) | "Edytuj" row-action buttons — deliberately darker/more muted than `--accent` so edit never reads as "the" CTA |
| `--paper` | `#F7F5F1` (warm off-white) | Page (`body`) background — see "Page canvas" below |

Semantic colors that stayed **Bootstrap defaults on purpose**: `--bs-danger`
(delete/destructive) and `--bs-success` (confirm/approve) — both are already
dark/saturated enough to read clearly, so no custom token was needed. Grey
`btn-secondary`/`btn-outline-secondary` (cancel/back) is also untouched —
low visual weight is the intended look there, not a bug.

## Page canvas: paper vs. white

`body` uses `--paper` (a warm off-white) instead of pure white. Cards,
tables, and form surfaces stay plain white (Bootstrap's default), so they
sit slightly "lifted" off the page instead of blending into a flat white
field. Don't set `--paper` (or any new background color) on a component that
should read as a surface sitting on top of the page — only `body` should
use it.

## Typography & navbar

- Base document `font-size` (14px mobile / 16px desktop, in the "Sticky
  footer styles" block) was **deliberately left alone** — bumping it
  globally risks breaking dense admin tables that were never designed for a
  larger base. Sizing changes are scoped to the navbar specifically:
  - `.navbar-brand`: 1.4rem, bold, with a small `--accent`-colored dot
    (`::before`) as the only graphical mark — there's no logo image.
  - `.navbar .nav-link`: 1.05rem (up from Bootstrap's 1rem default), more
    vertical padding.
  - `.navbar .nav-link i:not(.fa-lg)`: icons bumped to 1.15em. Icons that
    already carry `.fa-lg` (cart, account) are left alone — they're already
    sized up and would double-scale otherwise.
- If a future change wants a larger base font sitewide, audit the admin
  list/table views first — they're the layouts most likely to break.

## Vendor theming discipline

This applies beyond buttons — it's the rule for reskinning *any* third-party
CSS (Bootstrap, Tom Select, or whatever replaces them):

1. Never edit a vendor file under `wwwroot/lib/`. All overrides live in
   `site.css`, loaded after the vendor CSS, so plain cascade order — not
   `!important` — does the work. `!important` is only used where Bootstrap's
   own utility class (`.bg-primary`, `.text-primary`) already forces it.
2. Prefer the vendor's own CSS-variable override hook when it has one (e.g.
   `.dropdown-menu { --bs-dropdown-link-active-bg: var(--brand); }`) over a
   raw property override — it's the sanctioned way of tell the library
   "use this color here" instead of guessing at specificity.
3. When a vendor hardcodes a literal color with no variable hook (Bootstrap's
   `.form-control:focus`, Tom Select's selected-chip background), group all
   such overrides for that vendor together under one clearly labeled comment
   block. If that library is ever replaced, the whole block is deletable in
   one piece — the tokens and the rest of the app stay untouched.
4. Every override, from either case, references a token from the table
   above — never a fresh literal hex, even inside a vendor-specific block.

## More style guides

- [`buttons.md`](buttons.md) — which Bootstrap button class + visual weight
  for which action.

Add new files here (forms, tables, badges, ...) as new concerns come up —
see [`README.md`](README.md).
