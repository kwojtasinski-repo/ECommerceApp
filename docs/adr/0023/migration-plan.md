## Migration plan

All steps are part of a single PR unless noted. Phases are ordered by dependency.

**Phase 1 — Library swap, `bootstrap-select` removal, and Tom Select introduction:**
1. Update `libman.json`: remove `bootstrap-select`; add `bootstrap@5.3.x`; add `tom-select@2.4.1`.
2. Update `_Layout.cshtml` script/style references to BS5 paths.
3. Remove `bootstrap-select` CSS and JS `<link>`/`<script>` tags from `_Layout.cshtml`.
4. Add Tom Select CSS `<link>` to `<head>` and Tom Select JS `<script>` after `bootstrap.bundle.min.js` in `_Layout.cshtml`.
5. Remove `data-search` class from all `<select>` elements in `Item/AddItem.cshtml` and
   `Item/EditItem.cshtml`.
6. Remove the `.data-search` / `.selectpicker()` initialisation block from `site.js`.
7. Add `id="ItemTagsSelect"` and `class="form-select"` to the tags `<select>` in both views.
8. In each view's `@section Scripts` `DOMInitialized` handler, add:
   `new TomSelect('#ItemTagsSelect', { plugins: ['remove_button'], placeholder: 'Wybierz tagi...' });`
9. Run `libman restore`. Verify no 404s in browser Network tab.

**Phase 2 — `_Layout.cshtml` data attribute and utility class sweep:**
1. Replace all `data-toggle` / `data-target` with `data-bs-toggle` / `data-bs-target`.
2. Replace `dropdown-menu-right` → `dropdown-menu-end`, `ml-auto` → `ms-auto`, `mr-2` → `me-2`.
3. Remove `input-group-append` wrapper `<div>`; move its children to direct `input-group` siblings.
4. Replace `form-inline` with flex utilities.
5. Manual smoke-test: navbar collapse, dropdowns, search bar.

**Phase 3 — V2 views and `Inventory/Index.cshtml` sweep:**
1. For each of the 12 files (11 V2 views + `Inventory/Index.cshtml`):
   - Replace `data-toggle` / `data-target` / `data-dismiss` with BS5 equivalents.
   - Replace any BS4 utility classes with BS5 equivalents.
2. Manual smoke-test: accordion/collapse behaviour, inline modal trigger on `Inventory/Index`.

**Phase 4 — Identity area sweep:**
1. Grep `Areas/Identity/Pages/**/*.cshtml` for `data-toggle`, `data-target`, and BS4 utility classes.
2. Apply the same renaming as Phase 2–3.

**Phase 5 — `modalService.js` rewrite:**
1. Replace module internals per §3:
   - `_modalInstance` management.
   - `showModal` / `closeModal` using `bootstrap.Modal` class.
   - `createModalTemplate` — replace `data-backdrop`/`data-keyboard` with `data-bs-*` equivalents,
     remove them from the element (options passed to `new bootstrap.Modal(el, options)` instead).
   - `createModalHeader` — remove `data-dismiss`; × button uses `closeOnlyHandler` or
     `closeButtonHandler` depending on modal type (passed as parameter or set per public method).
   - `createModalFooter` — remove `data-dismiss`; Close button uses `closeOnlyHandler`.
2. Update `showInformationModal` to use `closeOnlyHandler` for header and footer.
3. Verify `showConfirmationModal` still resolves the Promise on both confirm and deny paths.
4. Manual smoke-test: open info modal and close via ×; open info modal and close via footer;
   open confirm modal and confirm; open confirm modal and deny; verify no double-remove.

**Phase 6 — Post-merge:**
1. Update `frontend.instructions.md` to state Bootstrap 5 target and `bootstrap.Modal` API rule.
2. Update ADR-0021 `modalService.js` deferred item to mark it resolved by this ADR.
3. Update ADR-0022 status from Proposed → Accepted if not already done.
4. Close open item #1 (info modal close bug) in the frontend status report.
