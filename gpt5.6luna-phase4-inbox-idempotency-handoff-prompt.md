# TASK: Phase 4 — Inbox / consumer-side idempotency (full phase, from Step 2 onward)

## Context — read the plan first, it is the authoritative spec

The governing plan is `.github/plans/04-phase-inbox-idempotency-implementation.md` — **read it in full
before writing anything.** Its companion validation checklist is
`.github/plans/04-phase-inbox-idempotency-validation.md`. Both are gitignored by default in this repo
(working scratch, not permanent docs) but were force-added for this phase specifically because it's
paused/resumed across sessions — do not "clean that up," it's intentional.

The plan file already contains, fully worked out — **do not redo or second-guess these, they are
settled facts, not proposals**:
- **Step 1 (the audit)**: all 40 registered consumer handlers across the 20 Phase-3 message types,
  classified naturally-idempotent vs needs-dedup, with per-handler reasoning tied to the actual
  domain/aggregate code. Result: 19 need dedup, 21 don't. This was independently spot-checked already.
- **Step 2 (the message-id design decision)**: **option (a), per-handler dedup**, decided using the
  audit data (dedup-needing and dedup-safe handlers are mixed for most multi-handler message types, so
  dispatcher-level dedup would incorrectly gate safe handlers too). The plan spells out the exact
  mechanism: a new additive `IIdAwareMessageHandler<TMessage>` interface, an optional
  `outboxMessageId` parameter on `IModuleClient.PublishAsync`, the exact `ModuleClient`/`OutboxDispatcher`
  changes, and why the 21 naturally-idempotent handlers are untouched. Read that section
  (`### Step 1 — the audit`, the "Decision recorded 2026-08-02" subsection) closely — it references the
  real current code (`IModuleClient.cs`, `IMessageHandler.cs`, `ModuleClient.cs`, `OutboxDispatcher.cs`)
  by name; verify those files still look the way the plan describes before proceeding, and stop/report
  if they've drifted.

**Your job starts at Step 3** of the plan's "Steps (atomic, ordered)" list: build the new infra, wire it
into the 19 dedup-needing handlers, write tests, validate.

## Working norms (non-negotiable, regardless of how the rest of this prompt reads)

- **Run everything yourself, unattended, all the way to a real `dotnet build`/`dotnet test` pass with
  pasted output.** Do not stop mid-task to ask permission to run a build or test command. Only stop if
  something in the plan turns out to be factually stale against the real current code, or you hit a
  genuine design ambiguity the plan doesn't resolve — in that case, stop and report the specific
  discrepancy rather than guessing past it.
- **Git discipline**: stage only the files you actually touched, with explicit paths
  (`git add <exact-path> <exact-path> ...`). **Never** run `git add -A` or `git add .` — this repo root
  has unrelated scratch content (other projects, prompt files, demo folders) that must never be swept
  into a commit. Run `git status --porcelain` before staging and sanity-check the list.
- Commit only once the full validation section below is genuinely green — don't commit partial/broken
  work "to checkpoint progress." If you need to stop partway through, leave the working tree as-is and
  report exactly what's done vs. pending; do not commit broken intermediate state.
- If you get stuck on any one handler, don't skip it silently — report which one and why, so it can be
  triaged rather than silently left unaudited-in-practice.

## Suggested execution order (matches the plan's own "Steps" list; feel free to batch handlers by
bounded context as you go, but do the infra first — everything else depends on it)

1. **Infra first**: `ProcessedMessage` (Domain), `IProcessedMessageGuard`/`ProcessedMessageGuard`
   (Application/Infrastructure), EF configuration + migration for `messaging.Inbox`, the new
   `IIdAwareMessageHandler<TMessage>` interface, and the `IModuleClient`/`ModuleClient`/`OutboxDispatcher`
   changes from the Step 2 decision. Mirror the shape of the existing `IOutboxWriter`/`OutboxWriter` +
   `OutboxMessage` + `MessagingDbContext` pattern (same `messaging` schema, same
   `CrossContextTransactionScope` technique) — it's the closest existing analog in this codebase, read it
   before designing the Inbox side from scratch.
2. **One reference handler**, fully wired + tested end-to-end, before touching the rest — recommend
   `OrderPlacedHandler` in `ECommerceApp.Application/Sales/Payments/Handlers/` (creates a new `Payment`
   row per call, no existing-payment-for-order check — a clean, high-stakes, single-row-create case that
   makes a good template for the others). Get this one fully green (build + its own
   `HandlerNameDuplicateDeliveryTests.cs` per the plan's "Tests required" section) before batching the
   rest.
3. **Remaining 18 handlers**, batched however makes sense to you (by bounded context is a reasonable
   default: Inventory's 4, the other Payments handler, Coupons' audit-trail-duplication pair, Sales/Orders'
   3 no-guard handlers, Communication's 8) — each gets the same dedup-guard wrapping pattern from the
   reference handler, plus its own `HandlerNameDuplicateDeliveryTests.cs`.
   - **Inventory's 4 handlers share a root cause worth noticing, not just patching over**:
     `StockService.FulfillAsync`'s only guard checks the product's *aggregate* `ReservedQuantity` across
     all orders, not the specific hold being fulfilled — under concurrent orders on the same product, a
     redelivery can still pass that guard and double-decrement even *with* Inbox dedup protecting against
     redelivery of the exact same message. Inbox dedup fixes the redelivery case correctly; it does not
     fix that underlying guard weakness for any other trigger path. Flag this explicitly in your final
     report rather than silently treating Inbox dedup as a full fix for those 4 — the plan's Step 2 section
     already calls this out, don't lose it.
4. **`ProcessedMessageGuardTests.cs`** (the guard's own unit tests, not handler-specific) per the plan's
   "Tests required" section.
5. Full validation pass (below), commit.

## Validation — mandatory, run for real and paste actual output

Follow `.github/plans/04-phase-inbox-idempotency-validation.md` in full once code is written. At minimum:

1. `dotnet build ECommerceApp.sln --configuration Release --nologo` → 0 errors.
2. `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
   → paste final summary. Baseline going in: **1056/1056**. Zero regressions; expect this to grow with
   your new `ProcessedMessageGuardTests.cs` and any handler-level unit tests.
3. `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
   → paste final summary. Baseline going in: **220/220**. Expect growth from the per-handler
   `DuplicateDeliveryTests`.
4. Confirm the audit's own coverage rule: **every** handler classified "needs dedup" in the plan's audit
   table has a corresponding `DuplicateDeliveryTests` class that delivers the *same* Outbox row twice and
   asserts the side effect changed exactly once (not "at least once", not just "no exception"). Cross-check
   your test list against all 19 rows in the audit table — a missing one is a gap, not a nitpick.
5. Confirm at least one naturally-idempotent-classified handler's existing tests still pass unmodified
   (proves you didn't accidentally gate a handler that shouldn't be gated).

## Final self-review checklist

- [ ] Confirmed `IModuleClient.cs`/`IMessageHandler.cs`/`ModuleClient.cs`/`OutboxDispatcher.cs` matched
      the plan's description before starting — stated explicitly in your report, not assumed silently.
- [ ] Did not re-decide or deviate from the Step 2 mechanism (per-handler dedup via
      `IIdAwareMessageHandler<T>`) without stopping to report why first.
- [ ] All 21 naturally-idempotent handlers are untouched — no signature changes, no new interface
      implementations, no dedup wrapping added to any of them.
- [ ] All 19 needs-dedup handlers implement `IIdAwareMessageHandler<T>` and have a passing
      `DuplicateDeliveryTests` class.
- [ ] The Inventory `FulfillAsync` aggregate-guard weakness is explicitly flagged in your final report
      as a separate, not-fixed-by-this-phase issue — not silently folded into "dedup handles it."
- [ ] Full build + full unit + full integration suites run for real, output pasted, zero regressions vs.
      the stated baselines.
- [ ] `git status --porcelain` reviewed before staging; only intentionally-touched files staged with
      explicit paths; no broad `git add`.
- [ ] Commit message follows this repo's existing style (see `git log --oneline -10` for examples) and
      does not push.

If anything in the plan file turns out to be stale against the real current code, or you hit a design
question the plan genuinely doesn't answer, stop and report the specific discrepancy rather than guessing
past it. This work will be independently re-verified afterward (fresh context, does not trust this
session's own claims) — optimize for your report being checkable, not just for a green build.
