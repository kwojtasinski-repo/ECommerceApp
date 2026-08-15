## Plan: Scheduled cleanup of unclaimed guest `UserProfile` rows

> Reviewed 2026-08-14 against ADR-0030's revision (Web-only scope, `VerificationCode`, order-access
> tokens, admin interim view): **no changes needed**. This phase is pure backend (repository query +
> scheduled task, `Supporting/TimeManagement`), touches no controller and no HTTP surface, so it is
> unaffected by the API-vs-Web scope decision. Its precondition on Phase 2 (unclaimed profiles must
> exist) still holds unchanged. One addition worth noting for implementation time: Phase 5 also adds
> a `VerificationCode` table with its own `ExpiresAt`/`ConsumedAt` lifecycle — that table needs its
> own, separate expired-row cleanup (see Phase 5's Risks section); this phase's job purges
> `UserProfile` rows only and should not be widened to cover `VerificationCode` as well.

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

### Files to modify (confirmed 2026-08-14 against real code)
- `ECommerceApp.Domain/AccountProfile/UserProfile.cs` — **confirmed: no `CreatedAt` today** (props are
  `Id, UserId, FirstName, LastName, IsCompany, NIP, CompanyName, Email, PhoneNumber, Addresses`). Add
  `CreatedAt` (private set, stamped at construction). **Migration approved by human (2026-08-14)**, additive
  only.
- `ECommerceApp.Domain/AccountProfile/IUserProfileRepository.cs` — add `Task<List<UserProfile>> GetOlderThanAsync(DateTime cutoff, CancellationToken ct = default)`. Note (confirmed): "unclaimed" is **not** a
  stored flag anywhere (ADR-0030 §4 — guest-ness is a derived negative lookup against `AspNetUsers`, per-BC
  `DbContext`s mean no cross-schema SQL join is possible, ADR-0013). So this repository method returns
  *all* profiles older than cutoff regardless of claim status; the claim-status filter happens in the task
  itself via a **batched** call into `IGuestAccountProvisioner` (Application-layer ACL, already used by
  `GuestPromotionService`) — do not attempt the unclaimed filter inside the repository/SQL layer.
- `ECommerceApp.Infrastructure/AccountProfile/Repositories/UserProfileRepository.cs` — implement `GetOlderThanAsync`, following existing `AsNoTracking()`/`IgnoreAutoIncludes()` list-query style (see e.g. lines 71-73).
- `ECommerceApp.Application/AccountProfile/Services/IGuestAccountProvisioner.cs` + its implementation
  (`ECommerceApp.Infrastructure/Identity/IAM/Adapters/GuestAccountProvisioner.cs`) — add a **batched**
  registration check (e.g. `Task<HashSet<string>> GetRegisteredUserIdsAsync(IEnumerable<string> userIds)`)
  backed by `_userManager.Users.Where(u => ids.Contains(u.Id))`, so the cleanup task doesn't do N+1
  `FindByIdAsync` calls per candidate. The existing single-id `IsRegisteredAsync` stays as-is for its
  current caller (`GuestPromotionService`).
- `ECommerceApp.Application/Sales/Orders/Queries/` — add `CustomersWithOrdersQuery(IReadOnlyCollection<int> CustomerIds): IQuery<IReadOnlySet<int>>` + handler, mirroring `OrderExistsQuery`/`OrderExistsQueryHandler`
  exactly (`ECommerceApp.Application/Sales/Orders/Queries/OrderExistsQuery.cs`,
  `ECommerceApp.Infrastructure/Sales/Orders/Handlers/OrderExistsQueryHandler.cs`). Add a batched
  `IOrderRepository`/`IOrderService` method backing it (`Order.CustomerId` maps to `UserProfile.Id`, confirmed via `IOrderRepository.GetByCustomerIdAsync`). Call it from the task via `IModuleClient.SendAsync`,
  same pattern `CouponService.cs:51-56` already uses.
- `ECommerceApp.Application/AccountProfile/Services/Extensions.cs` — add
  `services.AddScoped<IScheduledTask, UnclaimedGuestProfileCleanupTask>();` to `AddUserProfileServices`,
  matching `Identity/IAM/Services/Extensions.cs`'s `AddIamServices` pattern (needs
  `using ECommerceApp.Application.Supporting.TimeManagement;`).
- `GuestProfileCleanupOptions` (new, `ECommerceApp.Application/AccountProfile/Options/` or similar) —
  POCO with `SectionName` const, `bool Enabled`, `int RetentionDays`, following
  `ECommerceApp.Web/Areas/Catalog/Options/CatalogOptions.cs`'s pattern (not the hardcoded-`const` style of
  `CheckoutOptions`). Bind via `services.Configure<GuestProfileCleanupOptions>(...)` in `Startup.cs`,
  matching `CatalogOptions`'s registration site. Add to `ECommerceApp.Web/appsettings.json`:
  `"GuestProfileCleanup": { "Enabled": true, "RetentionDays": 90 }`.

### Files NOT to touch
- `UserProfile.ReassignOwner` (Phase 3) — cleanup only ever deletes, never reassigns
- Any `Order`/`Sales` code — this job must never touch `Order` directly; it only ever decides whether to delete a `UserProfile`, gated on "no associated order"

### Critical rule — never delete a claimed customer's history
Before deleting any `UserProfile` row, the job **must** confirm there is no `Order` referencing it as `CustomerId`. Since there is no FK (ADR-0013, per-BC `DbContext`s), this requires an explicit cross-BC check — reuse whatever existing ACL/query mechanism the Sales/Orders BC already exposes for "does an order exist for this customer" (check `IModuleClient`/`OrderExistsQuery` per `docs/roadmap/README.md` F5 — this query type already exists for a different purpose and may be directly reusable here; confirm at implementation time rather than assuming). A profile with **any** order, regardless of claim status, is never purged by this job.

### Documentation to update (within this phase, not deferred)
- Knowledge graph (`ecommerceapp-kg`) — this phase adds a new scheduled job
  (`UnclaimedGuestProfileCleanupTask`) and a new repository query; the KG explicitly tracks job
  scheduling (per `ecommerceapp-kg-query`'s own description), so regenerate after this phase lands,
  same manual-step caveat as Phases 1–3.
- No `bounded-context-map.md` change expected (no new module, no new cross-BC edge) — confirm
  rather than assume.

### Carried over from Phase 3 (optional, not blocking, unrelated to this phase's own scope)
Phase 3's independent validation (round 2, 2026-08-14) noted a residual gap that was judged
non-blocking and low priority: no test proves a promoted `ApplicationUser` can actually log in
with the password submitted during promotion — only that the row exists
(`GuestPromotionIntegrationTests.cs`/`CheckoutController.CreateAccount`). This is framework
behavior (ASP.NET Identity password hashing/sign-in), not custom logic this feature owns, which is
why it wasn't required for Phase 3 PASS. Parking it here since Phase 4 is the next phase to touch —
pick it up opportunistically if convenient, but do not let it block or widen this phase's own
scope/Outcome. Suggested test: `Promotion_ThenLogin_SucceedsWithSubmittedPassword` in
`ECommerceApp.Web.IntegrationTests/Presale/Checkout/GuestPromotionIntegrationTests.cs` (promote a
guest profile, then POST to the login endpoint with the same credentials, assert an authenticated
session results).

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
- **RESOLVED (2026-08-14, human)**: retention threshold = **90 days**, matching ADR-0030's placeholder. Made
  configurable rather than hardcoded — see `GuestProfileCleanupOptions` below — specifically because the
  human flagged that 90 days is still a placeholder and **needs further business/legal discussion**; a
  config value can be changed without a code deploy once that discussion concludes. Do not treat 90 as
  final-and-settled — it is the shipped default, not a confirmed business decision.
- **RESOLVED (2026-08-14, human)**: ship an `Enabled` feature flag alongside `RetentionDays`, both bound
  from configuration (`GuestProfileCleanup` section, `IOptions<GuestProfileCleanupOptions>`), following the
  existing `CatalogOptions` pattern (`ECommerceApp.Web/Areas/Catalog/Options/CatalogOptions.cs`) rather than
  the hardcoded-`const` anti-pattern seen in `CheckoutOptions`/`ApiPurchaseOptions`. Default `Enabled: true`,
  `RetentionDays: 90` in `appsettings.json`.
- **Open question, needs human input (unchanged)**: recurring cadence for this task (daily? weekly?) —
  confirmed this is **not** a code-time decision: cadence is DB-driven via `ScheduledJob`/`CronSchedule`
  (`ECommerceApp.Infrastructure/Supporting/TimeManagement/CronSchedulerService.cs`), registered at runtime
  (e.g. via the Jobs area UI/`JobManagementController`) and matched to this task by its `TaskName` string.
  This phase only ships the `IScheduledTask` implementation + DI registration; registering an actual cron
  schedule for `TaskName == "UnclaimedGuestProfileCleanup"` is a separate, later operational step.
- **RESOLVED (2026-08-14, human)**: the cross-BC "has this customer got an order" check does not exist yet
  (`OrderExistsQuery` is keyed by `OrderId`, not `CustomerId`) — approved adding a new batched query
  (`CustomersWithOrdersQuery`) rather than reusing `OrderExistsQuery` per-row.
- **Risk**: deleting a `UserProfile` that has non-`Order` references elsewhere not yet accounted for (e.g. saved addresses referenced by a future feature). → Mitigation: this plan only guards against `Order` references per current schema; re-check this list if new BCs gain a dependency on `UserProfile.Id` before this phase ships.

### Rollback plan
- Remove `UnclaimedGuestProfileCleanupTask` and its DI registration; the recurring job simply stops running. If a `CreatedAt` migration was added, rolling back the migration itself follows the standard migration-rollback process (human-approved, per `safety.instructions.md`) — do not attempt to auto-revert a migration.
