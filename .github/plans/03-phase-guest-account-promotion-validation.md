## Validation: Phase 3 — In-place guest → registered account promotion

> Independent check — run in a fresh session/context from the one that implemented this phase.
> This phase creates real Identity accounts and mutates ownership — treat the ownership-check
> tests as release-blocking, not advisory.

### 1. Deterministic verification (build + tests — do not trust prior claims)
- [ ] `dotnet build ECommerceApp.sln --configuration Release --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo` → exit 0, includes ArchUnitNET
- [ ] `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] Full existing suite re-run — zero regressions

### 2. Test-coverage checklist
- [ ] `ReassignOwner` unit tests cover both the empty/whitespace-throws case and the happy path
- [ ] `PromoteAsync_RequestingUserIdDoesNotMatchProfileOwner_ReturnsNotOwner` exists and actually asserts the profile's `UserId` is unchanged afterward (not just the return value)
- [ ] `PromoteAsync_IdentityCreationFails_DoesNotReassignOwner` exists — confirms no partial-state mutation
- [ ] Integration test covers the full happy path: guest checkout → promote → new account can log in
- [ ] Integration test explicitly covers the **cross-session attack scenario**: guest B attempts to promote guest A's `profileId` → 403, guest A's profile unchanged. This test is release-blocking, not optional.
- [ ] A test (or explicit code inspection note) confirms `Order.CustomerId` for any order placed before promotion is identical after promotion — the "no rewriting" guarantee from ADR-0030 §5 is actually verified, not just asserted in prose

### 3. Spec-conformance checklist (ADR-0030 §5)
- [ ] `ReassignOwner` has no side effects beyond setting `UserId` — verify by reading the method, not the tests
- [ ] `GuestPromotionService.PromoteAsync` performs the ownership check **before** calling `UserManager.CreateAsync` (fail fast, don't create an orphaned account for a rejected request)
- [ ] No new `UserProfile` row is created during promotion — same `UserProfileId` before and after
- [ ] `Order.CustomerId` is not read, written, or referenced anywhere in `GuestPromotionService` — promotion genuinely never touches `Order`
- [ ] The "already registered" edge case (profile's `UserId` already resolves to an `ApplicationUser`) is handled explicitly, not silently overwritten
- [ ] No transaction/consistency risk was silently ignored — either a mitigation was implemented (transaction or compensating action) or the open question was explicitly escalated to the human and answered, per the implementation plan's Risks section

### 4. Code review pass (standard)
- [ ] `GuestPromotionService` is `internal sealed` per repo convention
- [ ] `PromotionResult` uses factory methods, no public constructor, matching `CheckoutResult`'s established pattern
- [ ] 403 (not 404) is returned for `NotOwner` — verify this doesn't leak whether a `profileId` exists
- [ ] No hardcoded secrets; password handling goes through `UserManager` only, never logged or persisted outside Identity's own storage
- [ ] Style consistent with surrounding code

### 5. Manual/exploratory check (if environment available)
- [ ] Perform a guest checkout, then call `create-account` with the same session cookie; confirm login works afterward with the new credentials
- [ ] Attempt `create-account` with a *different* browser/session's cookie pointed at the first guest's `profileId` (or a guessed adjacent ID); confirm 403 and no state change

---

### Outcome

- [ ] **PASS** — proceed to cleanup, then unblock Phase 4
- [ ] **FAIL** — do not clean up. Report findings below, route to human: fix directly / send back for fixes / abort.

**Findings (if FAIL):**
<!-- one entry per failed check above, with file:line and concrete reproduction -->

---

### Cleanup (only after PASS)
1. Delete this file and `03-phase-guest-account-promotion-implementation.md` — no other phase's files.
2. Update `docs/roadmap/guest-checkout.md` Step 5 row and the "Optional: guest checks 'create an account'" flow section; update `docs/adr/0030/checklist.md` rows for `ReassignOwner`, promotion scoping, and "no order rewriting."
