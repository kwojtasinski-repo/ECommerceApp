## Validation: Phase 2 — Guest customer provisioning + `Confirm` unlocked

> Independent check — run in a fresh session/context from the one that implemented this phase.
> This phase touches live order placement — hold it to a higher bar than Phase 1.

### 1. Deterministic verification (build + tests — do not trust prior claims)
- [ ] `dotnet build ECommerceApp.sln --configuration Release --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo` → exit 0, includes ArchUnitNET
- [ ] `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] Full existing test suite re-run — zero regressions, especially every existing authenticated-checkout test

### 2. Test-coverage checklist
- [ ] A test exists for `GetOrCreateForGuestAsync` creating a new profile when none exists for the guest token
- [ ] A test exists for `GetOrCreateForGuestAsync` returning the same `UserProfileId` on a second call for the same token (no duplicate)
- [ ] A test exists for `EnsureGuestCustomerAsync` correctly mapping `CheckoutCustomer` fields into the service call
- [ ] An end-to-end integration test exists: anonymous `Cart → Initiate → Confirm` with no `Authorization` header and no `CustomerId` produces a 200 and a real `orderId`
- [ ] That same integration test asserts `Order.CustomerId` resolves to an actual `UserProfile` row (query it back)
- [ ] That same integration test asserts **zero** `ApplicationUser`/`AspNetUsers` rows were created as a side effect
- [ ] A regression test exists confirming the **authenticated** flow's `Confirm` behavior (required `CustomerId`, same error on omission) is byte-for-byte unchanged

### 3. Spec-conformance checklist (ADR-0030 §3 — catch drift)
- [ ] `Order.CustomerId` invariant (`> 0`, `DomainException` below that) is untouched in `Order.cs`
- [ ] No nullable `CustomerId` was introduced on `Order` itself — only `ConfirmCheckoutRequest.CustomerId` (the DTO) became optional
- [ ] Guest path calls `IUserProfileRepository.GetByUserIdAsync` **before** creating a new `UserProfile` — verify this by reading the implementation, not just trusting the test
- [ ] The guest `UserProfile.UserId` value is the guest session token (from `GetOrCreateShopperId()`), not a placeholder/sentinel string
- [ ] `UserProfile.Create(...)` is the same factory used for registered profiles — no parallel guest-only creation path or shadow entity
- [ ] `UserProfileCreated` domain event is still published for guest-created profiles (parity with the manual-creation path) — or, if intentionally suppressed, this is called out explicitly as a deliberate deviation with a reason
- [ ] `ICustomerExistenceChecker`/`IOrderCustomerResolver` are **not** invoked on this path (matches the already-live `PlaceOrderFromPresaleAsync` behavior — confirm no accidental new dependency was wired in)
- [ ] No `IsGuest` (or equivalent) flag was added anywhere as a shortcut instead of the derived-fact approach

### 4. Code review pass (standard)
- [ ] `AccountProfileClientAdapter`/`UserProfileService` changes are `internal sealed` per existing pattern in the file
- [ ] No hardcoded secrets, no raw SQL
- [ ] `[AllowAnonymous]` on `Confirm` has a comment/reference to ADR-0030
- [ ] Error messages for a guest submitting incomplete `CheckoutCustomer` data are clear and distinct from the "not authenticated" case (no misleading 401 for a guest with bad data — should be 400)
- [ ] Style consistent with surrounding code (file-scoped namespaces, braces, no `.Result`/`.Wait()`)

### 5. Manual/exploratory check (if environment available)
- [ ] Perform a full guest checkout via curl/Postman end to end; confirm the created order is visible in whatever admin/order-list view exists, with sane-looking customer data
- [ ] Attempt the same guest flow twice in a row with the same cookie; confirm no duplicate `UserProfile` row is created (check the DB directly, not just the API response)

---

### Outcome

- [ ] **PASS** — proceed to cleanup, then unblock Phase 3
- [ ] **FAIL** — do not clean up. Report findings below, route to human: fix directly / send back for fixes / abort.

**Findings (if FAIL):**
<!-- one entry per failed check above, with file:line and concrete reproduction -->

---

### Cleanup (only after PASS)
1. Delete this file and `02-phase-guest-customer-provisioning-implementation.md` — no other phase's files.
2. Update `docs/roadmap/guest-checkout.md` Step 3/4 rows, the flow diagram's "confirms order" section, and the first two Acceptance Criteria checkboxes; update `docs/adr/0030/checklist.md` rows for `EnsureGuestCustomerAsync` idempotency, `GetOrCreateForGuestAsync`, and the "no `ApplicationUser` created" rule.
