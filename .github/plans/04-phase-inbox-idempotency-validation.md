## Validation: Phase 4 — Inbox / consumer-side idempotency

> Independent check — run in a fresh session/context from the one that implemented this phase.
> A gap here is a silent double-processing bug (double stock decrement, duplicate payment) that only
> surfaces under a real redelivery — treat every unaudited or ambiguously-audited handler as a
> potential production incident, not a style nitpick.

### 1. Deterministic verification (build + tests — do not trust prior claims)
- [ ] `dotnet build ECommerceApp.sln --configuration Release --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo` → exit 0
- [ ] Full existing suite re-run — zero regressions

### 2. Test-coverage checklist
- [ ] The Step-1 audit table exists (in the plan file, or a linked doc) and covers **every** handler
      for **every** message type retrofitted in Phase 3 — not a subset. Cross-check the audit's handler
      list against Phase 3's actual message-type list; any handler missing from the audit is a FAIL.
- [ ] Every handler classified "needs dedup" has a `DuplicateDeliveryTests` class, and the test
      actually delivers the *same* Outbox row twice and asserts the side effect happened **exactly
      once** — not "at least once", not just "no exception on the second call."
- [ ] Every handler classified "naturally idempotent" has a recorded *reason*, not just a checkbox —
      spot-check 3 of these reasons against the actual handler code and confirm the reasoning holds up
      (e.g. "sets an absolute value, doesn't increment" — verify that's literally what the code does)
- [ ] The "how does a handler know its Outbox message id" design question has one recorded, consistent
      answer applied everywhere — not a different ad hoc solution per handler

### 3. Spec-conformance checklist (ADR/roadmap intent)
- [ ] `messaging.Inbox` is a single shared table (not per-BC-schema tables reintroduced despite the
      corrected design decision)
- [ ] Dedup key is `(OutboxMessage.Id, HandlerType)`, unique-constrained — confirm via the migration
- [ ] `ProcessedMessageGuard` distinguishes a genuine unique-constraint conflict (returns `false`) from
      any other `DbUpdateException` (propagates) — test this distinction explicitly, don't assume any
      `DbUpdateException` means "already processed"
- [ ] No handler was given dedup "just in case" without a recorded rationale — audit-driven, not
      blanket-applied

### 4. Code review pass (standard)
- [ ] No hardcoded secrets, no raw SQL
- [ ] Each dedup-wrapped handler commits the guard-insert and its own business-logic write in the same
      `CrossContextTransactionScope` — spot-check at least 2 handlers by reading the code
- [ ] File-scoped namespaces / braces / style consistent with surrounding code
- [ ] No `.Result`/`.Wait()` blocking-async introduced

### 5. Manual/exploratory check (if environment available)
- [ ] For at least one dedup-wrapped handler, manually redeliver the same Outbox row twice (e.g. via a
      test harness or by resetting its status back to `Pending` after a successful dispatch) and
      confirm the real-world side effect (stock count, payment count) only changed once

---

### Outcome

- [ ] **PASS** — proceed to cleanup below. This is the last phase before cleanup jobs — see Phase 5.
- [ ] **FAIL** — do not clean up. Report findings below, route to human: fix directly / send back for fixes / abort. Given the silent-failure-mode nature of a missed audit entry, prefer "fix and re-validate" over "ship with a known gap."

**Findings (if FAIL):**
<!-- one entry per failed check above, with file:line and concrete reproduction -->

---

### Cleanup (only after PASS)
1. Delete this file and `04-phase-inbox-idempotency-implementation.md`.
2. Do **not** delete `.github/plans/` yet — Phase 5 files still live there.
3. Update `docs/roadmap/generic-saga-orchestration-proposal.md`'s Inbox subsection: record the final
   audit table (or a link to where it lives permanently — not in a since-deleted plan file) and the
   resolved "how does a handler know its message id" decision.
