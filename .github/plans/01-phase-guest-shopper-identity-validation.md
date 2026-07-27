## Validation: Phase 1 — Guest shopper identity

> Independent check — run in a fresh session/context from the one that implemented this phase.
> Do not trust the implementing session's own claims; re-derive every PASS/FAIL here.

### 1. Deterministic verification (build + tests — do not trust prior claims)
- [ ] `dotnet build ECommerceApp.sln --configuration Release --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo` → exit 0, includes ArchUnitNET
- [ ] `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] Re-run the full existing test suite (not just new tests) — confirm zero regressions for the authenticated flow

### 2. Test-coverage checklist (does the test suite actually cover the plan's claims?)
- [ ] A test exists asserting `GetOrCreateShopperId()` returns the JWT claim value when authenticated (no behavior change for logged-in users)
- [ ] A test exists asserting a fresh anonymous request receives a `Set-Cookie` response header with the guest token
- [ ] A test exists asserting a second anonymous request presenting that cookie resolves to the **same** `PresaleUserId` (not a new one each time)
- [ ] A test exists asserting anonymous `POST /api/cart` succeeds (no 401)
- [ ] A test exists asserting anonymous `POST /api/checkout/initiate` succeeds and creates `SoftReservation`s scoped to the guest token
- [ ] A test exists asserting anonymous `POST /api/checkout/confirm` **still returns 401** (Phase 2 boundary — this phase must not accidentally unlock Confirm)

### 3. Spec-conformance checklist (ADR-0030 §1–2 — catch "improvements" that drift from intent)
- [ ] No changes were made to `PresaleUserId`, `CartLine`, `SoftReservation`, `CartService`, `SoftReservationService`, or any other Domain/Application Presale type — this phase is API-layer only
- [ ] Guest cookie token is cryptographically random and prefixed (e.g. `gst_`) — not a sequential ID, not derived from request metadata
- [ ] Guest cookie is `HttpOnly`, `Secure`, `SameSite=Lax`
- [ ] Guest cookie expiry is bounded (checkout-window scale) — not a long-lived persistent cookie
- [ ] `GetOrCreateShopperId()` is the **only** place resolving guest-vs-authenticated identity — no duplicated logic inline in `CartController`/`CheckoutController`
- [ ] `ApiPolicies.TrustedApiUser` still exists and is unchanged in definition — only its application to Cart/Initiate actions was removed, not the policy itself
- [ ] `CheckoutController.Confirm` was **not** touched in this phase (still `[Authorize(Policy = ApiPolicies.TrustedApiUser)]`)
- [ ] No `IsGuest`-style flag was introduced anywhere as a shortcut

### 4. Code review pass (standard)
- [ ] No hardcoded secrets
- [ ] No raw SQL introduced
- [ ] `[AllowAnonymous]` usage is justified by a comment or is self-evident from ADR-0030 reference
- [ ] File-scoped namespaces / braces / style consistent with surrounding `ECommerceApp.API` code
- [ ] No `.Result`/`.Wait()` blocking-async introduced

### 5. Manual/exploratory check (if environment available)
- [ ] Start the API, issue an anonymous `POST /api/cart` via curl/Postman, confirm `Set-Cookie` header present and cart persists across two calls reusing the cookie
- [ ] Confirm an authenticated request (real JWT) is unaffected — same response shape as before this phase

---

### Outcome

- [ ] **PASS** — proceed to cleanup (below), then unblock Phase 2
- [ ] **FAIL** — do not clean up. Report findings below, route to human: fix directly / send back for fixes / abort.

**Findings (if FAIL):**
<!-- one entry per failed check above, with file:line and concrete reproduction -->

---

### Cleanup (only after PASS)
1. Delete this file and `01-phase-guest-shopper-identity-implementation.md` — no other phase's files.
2. Do **not** delete `.github/plans/` yet — Phases 2–4 files still live there.
3. Update `docs/roadmap/guest-checkout.md` Step 1/2 rows and the flow diagram's "guest browses" section to reflect completion; update `docs/adr/0030/checklist.md` — check off the rows this phase satisfies (`GetOrCreateShopperId`, cookie attributes, `[AllowAnonymous]` on Cart/Initiate, `TrustedApiUser` unchanged elsewhere).
