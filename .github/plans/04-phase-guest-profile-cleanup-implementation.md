## Plan: Scheduled cleanup of unclaimed guest `UserProfile` rows

### Scope
- **BC(s)**: AccountProfile (repository query), Supporting/TimeManagement (scheduled task registration)
- **Governing ADR(s)**: ADR-0030 Consequences/Risks (`docs/adr/0030/0030-guest-checkout-anonymous-order-placement.md`)
- **Risk**: low-medium — a bug here deletes data; the "no `Order` reference" guard is the critical invariant
- **Behavioral change**: yes — new recurring background job

### Precondition
- Phase 2 must be **PASS** (unclaimed guest profiles must exist for there to be anything to clean up).
- Phase 3 does not strictly block this phase, but running Phase 3 first reduces the number of profiles this job will ever need to touch (promoted profiles are never unclaimed).

### Files to add
- `ECommerceApp.Application/AccountProfile/Services/UnclaimedGuestProfileCleanupTask.cs` — `internal sealed class ... : IScheduledTask`, following the exact shape of `ECommerceApp.Application/Identity/IAM/Services/RefreshTokenCleanupTask.cs` (periodic sweep, not per-entity `IDeferredJobScheduler.ScheduleAsync` like `SoftReservationExpiredJob` — there is no single expiry instant per profile, this is a recurring sweep)

### Files to modify
- `ECommerceApp.Domain/AccountProfile/IUserProfileRepository.cs` — add a query method, e.g. `Task<List<UserProfile>> GetUnclaimedOlderThanAsync(DateTime cutoff, CancellationToken ct = default)` (naming/signature TBD at implementation time based on whatever "unclaimed" resolution mechanism already exists from Phase 2/3 — likely requires joining against Identity's `AspNetUsers` via `UserManager`, since there is no FK per ADR-0013; confirm exact query shape before writing)
- `ECommerceApp.Infrastructure/AccountProfile/Repositories/UserProfileRepository.cs` — implement the query
- `ECommerceApp.Application/AccountProfile/Services/Extensions.cs` (or wherever `IUserProfileService`/repo DI lives) — register `services.AddScoped<IScheduledTask, UnclaimedGuestProfileCleanupTask>();` matching `Identity/IAM/Services/Extensions.cs`'s `AddIamServices` pattern
- Confirm whether `UserProfile` already exposes a `CreatedAt` timestamp; if not, this is an **additive, non-breaking** schema change (new nullable-then-backfilled column) — call out explicitly as a migration, subject to the repo's `Infrastructure/Migrations/` human-approval gate (`safety.instructions.md`)

### Files NOT to touch
- `UserProfile.ReassignOwner` (Phase 3) — cleanup only ever deletes, never reassigns
- Any `Order`/`Sales` code — this job must never touch `Order` directly; it only ever decides whether to delete a `UserProfile`, gated on "no associated order"

### Critical rule — never delete a claimed customer's history
Before deleting any `UserProfile` row, the job **must** confirm there is no `Order` referencing it as `CustomerId`. Since there is no FK (ADR-0013, per-BC `DbContext`s), this requires an explicit cross-BC check — reuse whatever existing ACL/query mechanism the Sales/Orders BC already exposes for "does an order exist for this customer" (check `IModuleClient`/`OrderExistsQuery` per `docs/roadmap/README.md` F5 — this query type already exists for a different purpose and may be directly reusable here; confirm at implementation time rather than assuming). A profile with **any** order, regardless of claim status, is never purged by this job.

### Tests required (mandatory — behavioral change = yes, and it's destructive)
- Unit: `ECommerceApp.UnitTests/AccountProfile/UnclaimedGuestProfileCleanupTaskTests.cs` — mirror `RefreshTokenCleanupTaskTests.cs` structure:
  - `ExecuteAsync_UnclaimedProfileOlderThanThreshold_DeletesIt`
  - `ExecuteAsync_UnclaimedProfileWithAnOrder_DoesNotDeleteIt` (the critical test)
  - `ExecuteAsync_ClaimedProfile_DoesNotDeleteIt` (i.e. `UserId` resolves to a real `ApplicationUser`)
  - `ExecuteAsync_ProfileNewerThanThreshold_DoesNotDeleteIt`
  - `ExecuteAsync_RepositoryThrows_ReportsFailureNotException` (matches `RefreshTokenCleanupTask`'s try/catch → `ReportFailure` pattern)
- Integration: `ECommerceApp.IntegrationTests/AccountProfile/UnclaimedGuestProfileCleanupIntegrationTests.cs` (new) — seed a mix of claimed/unclaimed/ordered/unordered profiles at varying ages, run the task, assert exactly the expected subset was deleted

### Steps (atomic, ordered)
1. Confirm/add `UserProfile.CreatedAt` (or equivalent) — if a migration is needed, STOP and get explicit human approval per `safety.instructions.md` before generating it.
2. Confirm the cross-BC "has this customer got an order" check to reuse (do not write a new one if `OrderExistsQuery`/`IModuleClient` already covers it).
3. Add the repository query (`GetUnclaimedOlderThanAsync` or equivalent).
4. Add `UnclaimedGuestProfileCleanupTask` implementing `IScheduledTask`.
5. Register in DI.
6. Build.
7. Unit tests — write the "has an order → never delete" test **first**, before the happy-path delete test.
8. Integration tests with a realistic seeded mix.

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET (part of UnitTests)

### Risks / open questions
- **Risk**: cross-BC "has an order" check is expensive if done naively (N+1 query per candidate profile). → Mitigation: batch the check (single query for all candidate `CustomerId`s) rather than per-row.
- **Open question, needs human input**: retention threshold value (ADR-0030 suggested 90 days as a placeholder) — confirm the actual business-acceptable value before shipping; this is a product/legal decision, not a technical one.
- **Open question, needs human input**: recurring cadence for this task (daily? weekly?) — confirm how `IScheduledTask` recurring registration/interval is configured in this codebase (check `Jobs`/`JobManagementController` area and existing `RefreshTokenCleanupTask` registration/trigger config) before assuming a default.
- **Risk**: deleting a `UserProfile` that has non-`Order` references elsewhere not yet accounted for (e.g. saved addresses referenced by a future feature). → Mitigation: this plan only guards against `Order` references per current schema; re-check this list if new BCs gain a dependency on `UserProfile.Id` before this phase ships.

### Rollback plan
- Remove `UnclaimedGuestProfileCleanupTask` and its DI registration; the recurring job simply stops running. If a `CreatedAt` migration was added, rolling back the migration itself follows the standard migration-rollback process (human-approved, per `safety.instructions.md`) — do not attempt to auto-revert a migration.
