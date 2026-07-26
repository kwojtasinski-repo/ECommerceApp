# Phase 1 validation — cart restore on `OrderPlacementFailed`

> For Claude, in a later session, after GitHub Copilot (`@implementer`, GPT-5.4-mini) has executed
> [`01-phase-cart-restore-implementation.md`](./01-phase-cart-restore-implementation.md).
> Independent cross-model check — do not trust the implementer's own embedded verification claims
> without re-running them yourself. Ground truth for intent: that plan file, and
> [`docs/roadmap/order-placement-compensation-followup.md`](../../docs/roadmap/order-placement-compensation-followup.md).

## Before you start

1. Read `01-phase-cart-restore-implementation.md` in full (not just the summary).
2. Read `docs/roadmap/order-placement-compensation-followup.md` § "Workstream 1 spec" for the rationale
   behind each decision — you're checking the implementation matches *intent*, not just "does it compile".
3. Run `git log --oneline -5` and `git diff` (or inspect the relevant commit) to see exactly what changed.
4. Confirm the changed files match the plan's "Files to add" / "Files to modify" list exactly. Anything
   extra is scope creep — flag it, don't silently accept it.

## Step 1 — Deterministic verification (run yourself, don't trust claims)

```powershell
dotnet build ECommerceApp.sln --configuration Release --nologo
dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo
dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo
```

All three must exit 0. If any fails, this is an automatic **FAIL** — quote the verbatim error, do not
paraphrase, and stop here (don't proceed to semantic review of a build that doesn't compile).

## Step 2 — Test coverage check (all five must exist and be meaningfully written, not stubs)

- [ ] `CartServiceTests.RestoreAsync_ShouldUpsertEachLineAndRefreshCache`
- [ ] `CartServiceTests.RestoreAsync_ProductNotInCatalog_ShouldStillRestoreLine`
- [ ] `OrderPlacementFailedHandlerTests` (Presale) — `RestoreAsync` called with message items
- [ ] `OrderPlacementFailedHandlerTests` (Presale) — throwing `RestoreAsync` is caught and logged, not propagated
- [ ] `OrderPlacementFailedFanOutTests` — new integration test proving cart lines exist again after the
      full `OrderPlaced` → `OrderPlacementFailed` fan-out, plus the class docstring was updated

## Step 3 — Spec-conformance check (the decisions the implementer might get wrong or "improve")

Check each of these explicitly — these are documented decisions, not oversights, and a well-meaning
model may "fix" them into something that looks more correct but contradicts the plan:

- [ ] `CartLine.Create(userId.Value, ...)` — the `.Value` is present. (`PresaleUserId` → `string` has no
      implicit conversion; if this compiles without `.Value`, something else changed — investigate.)
- [ ] `RestoreAsync` overwrites cart lines (calls the same upsert primitive as `SetCartItemAsync`) — it
      must **not** route through `AddToCartAsync` or otherwise apply `MaxQuantityPerOrderLine`.
- [ ] No catalog-existence validation was added before restoring a line.
- [ ] Cache is refreshed once after the loop, not once per item.
- [ ] `ISoftReservationService` is **not** touched anywhere in this change. If it was, that's scope
      creep beyond the plan — flag it, don't approve silently even if it "looks like an improvement".
- [ ] `OrderPlacementFailedHandler`'s new code path catches exceptions from `RestoreAsync` and logs —
      does not rethrow (the handler must stay best-effort/no-op-safe like its Payments/Inventory siblings).
- [ ] No changes to `Infrastructure/Migrations/` (there should be none needed — flag immediately if present).
- [ ] No changes outside the Presale BC (Payments/Inventory/Orders files untouched).

## Step 4 — Standard code-review checklist (abbreviated `code-reviewer.md` pass, scoped to changed files)

- [ ] No hardcoded secrets, no `[AllowAnonymous]` changes, no raw SQL.
- [ ] Braces on all control flow; no file-scoped namespaces (project convention); no `.Result`/`.Wait()`.
- [ ] No direct cross-BC service calls introduced (this change should be entirely internal to Presale
      plus the existing `IMessageHandler<OrderPlacementFailed>` subscription — no new `IMessageBroker`
      publishes were part of the plan; flag if one was added).
- [ ] Test naming follows `Method_Conditions_ExpectedResult`.

## Verdict

Report in this shape:

```
═══════════ PHASE 1 VALIDATION: PASS | FAIL ═══════════
Build:                PASS | FAIL
Unit tests:            PASS (<n> passed) | FAIL
Integration tests:     PASS (<n> passed) | FAIL
Test coverage (5/5):   <checklist result>
Spec conformance:      <checklist result — list any deviation>
Code review:           CLEAN | <findings>
═══════════════════════════════════════════════════════
```

## On PASS — cleanup (do exactly this, nothing more)

1. Delete **only** this phase's two files:
   - `.github/plans/01-phase-cart-restore-implementation.md`
   - `.github/plans/01-phase-cart-restore-validation.md`
2. Check `.github/plans/` for any other files (later phases, e.g. `02-phase-*`, `03-phase-*`).
   - If other phase files exist → **stop, leave the folder in place.** Never delete another phase's
     files, and never delete the folder while anything else lives in it.
   - If nothing else is left in the folder → delete the empty `.github/plans/` folder too.
3. Do **not** delete or edit `docs/roadmap/order-placement-compensation-followup.md` — that's the
   permanent tracking doc, not a pipeline artifact. Update its workstream 1 status to done instead.

## On FAIL

Report the failures per the shape above. Do not attempt to fix silently — this mirrors the pipeline's
HITL discipline (`.github/agents/verifier.md`, `.github/agents/code-reviewer.md`): surface findings,
let the human decide whether to send it back to Copilot with feedback or fix directly. Do not delete
either file on a FAIL verdict.
