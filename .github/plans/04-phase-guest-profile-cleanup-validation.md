## Validation: Phase 4 — Scheduled cleanup of unclaimed guest `UserProfile` rows

> Independent check — run in a fresh session/context from the one that implemented this phase.
> This job deletes data. Treat the "never delete a profile with an order" checks as
> release-blocking — a failure here is a data-loss bug, not a cosmetic issue.

### 1. Deterministic verification (build + tests — do not trust prior claims)
- [ ] `dotnet build ECommerceApp.sln --configuration Release --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo` → exit 0, includes ArchUnitNET
- [ ] `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] Full existing suite re-run — zero regressions
- [ ] If a migration was added for `CreatedAt` (or equivalent): confirm it was reviewed and explicitly approved by a human per `safety.instructions.md` — check for that approval record/comment before treating this as PASS

### 2. Test-coverage checklist
- [ ] `ExecuteAsync_UnclaimedProfileWithAnOrder_DoesNotDeleteIt` exists and passes — this is the single most important test in this phase
- [ ] `ExecuteAsync_ClaimedProfile_DoesNotDeleteIt` exists (a promoted/registered profile is never touched regardless of age)
- [ ] `ExecuteAsync_ProfileNewerThanThreshold_DoesNotDeleteIt` exists
- [ ] `ExecuteAsync_RepositoryThrows_ReportsFailureNotException` exists — confirms the job fails safe (reports, doesn't crash the scheduler) matching `RefreshTokenCleanupTask`'s pattern
- [ ] Integration test seeds a realistic mixed dataset (claimed / unclaimed-with-order / unclaimed-without-order / too-recent) and asserts exactly the expected subset is gone afterward — not just "some rows were deleted"

### 3. Spec-conformance checklist (ADR-0030 Risks section)
- [ ] The "has an order" check is a real query against Sales/Orders data (via the existing cross-BC mechanism, e.g. `OrderExistsQuery`/`IModuleClient`) — not a guess, not skipped, not a TODO left in place
- [ ] The check happens **before** deletion for every candidate row, not sampled/approximated
- [ ] The task follows the exact `IScheduledTask` shape used by `RefreshTokenCleanupTask` (constructor injection, `TaskName`, `ExecuteAsync(JobExecutionContext, CancellationToken)`, try/catch → `ReportSuccess`/`ReportFailure`) — no divergent pattern invented
- [ ] Retention threshold and recurring cadence were **explicitly confirmed by the human**, not left as the implementer's placeholder guess (check the plan's "Risks / open questions" section was actually resolved, not silently defaulted)
- [ ] `UserProfile.ReassignOwner` (Phase 3) is untouched by this phase — cleanup never calls it

### 4. Code review pass (standard)
- [ ] `UnclaimedGuestProfileCleanupTask` is `internal sealed`
- [ ] The cross-BC order-existence check is batched (one query for N candidates), not N+1
- [ ] No hardcoded secrets, no raw SQL bypassing EF Core
- [ ] DI registration matches the existing `AddIamServices`-style pattern (`services.AddScoped<IScheduledTask, ...>()`)
- [ ] If a migration was added: it is additive only (new nullable/backfilled column), no destructive column changes bundled in

### 5. Manual/exploratory check (if environment available, non-production database only)
- [ ] Seed a test/staging database with a mix of profiles as above, trigger the job manually (via `JobManagementController`/Jobs area if that supports manual trigger), inspect the DB before/after to confirm exactly the expected rows were removed
- [ ] Confirm a profile with an order — even a very old, fully unclaimed one — survives the run

---

### Outcome

- [ ] **PASS** — proceed to cleanup below. This is the last phase — see the final step.
- [ ] **FAIL** — do not clean up. Report findings below, route to human: fix directly / send back for fixes / abort. Given this phase deletes data, prefer "fix and re-validate" over "ship with a known gap."

**Findings (if FAIL):**
<!-- one entry per failed check above, with file:line and concrete reproduction -->

---

### Cleanup (only after PASS)
1. Delete this file and `04-phase-guest-profile-cleanup-implementation.md`.
2. Since this is the last of the four phases for ADR-0030's v1 scope, delete the `.github/plans/` folder itself **only if** no other unrelated phase files are present in it (check first — do not assume it's empty).
3. Update `docs/roadmap/guest-checkout.md`: mark all v1 Steps (1–6) and Acceptance Criteria as complete; update the header Status line from "🔶 Proposed — design only, not started" to reflect implementation completion (confirm exact wording convention against another completed roadmap page, e.g. `presale-slice2.md`, before writing it).
4. Update `docs/adr/0030/0030-guest-checkout-anonymous-order-placement.md` Status from `Proposed` to `Accepted` (only after all 4 phases are independently validated as PASS) and add a `## Implementation Status` table per the `bc-implementation.prompt.md` convention, marking each row `✅ Done`.
5. Update `docs/adr/0030/checklist.md` — every row addressed by Phases 1–4 should now be checked `[x]`. The deferred account-linking flow (§6/migration-plan step 9) intentionally remains unimplemented — leave its related checklist rows unchecked with a note that it's tracked separately, not forgotten.
6. Update `docs/roadmap/README.md`'s Active roadmaps row for guest checkout from "🔶 Proposed" to the appropriate completion status.
