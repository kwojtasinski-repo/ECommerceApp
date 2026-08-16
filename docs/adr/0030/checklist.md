## Conformance checklist

> Revised 2026-08-14 to match ADR-0030's revision (Web-only scope, dedicated
> `IShopperIdentityResolver`, `VerificationCode`, order-access recovery, session isolation). See
> the ADR's Revision note for what changed and why.

### Identity & authorization (§1, §1a, §2) — Phase 1, verified PASS (independent re-validation, 2026-08-14 round 2)
- [x] `IShopperIdentityResolver` is the only place that decides authenticated-vs-guest identity for
      `CheckoutController` (`ECommerceApp.Web`) — no duplicate logic inline in the controller
- [x] `IShopperIdentityResolver` is injected **only** into `CheckoutController` — it is not a
      `BaseController` method, and no other Web controller depends on it
- [x] Guest session cookie value is cryptographically random (≥128-bit) and prefixed (e.g. `gst_`)
      so it can never collide with an `AspNetUsers.Id`
- [x] Guest session cookie is `HttpOnly`, `Secure`, `SameSite=Lax`, with an expiry bounded to the
      checkout window (same order of magnitude as `PresaleOptions.SoftReservationTtl`)
- [x] The guest session cookie is never read by `[Authorize]`/cookie-auth — consumed exclusively by
      `IShopperIdentityResolver`
- [x] `CheckoutController`'s guest-eligible actions use `[AllowAnonymous]`; the `AddToCart` login
      redirect is removed, not merely bypassed for some inputs
- [x] `ECommerceApp.API/Controllers/Presale/{Cart,Checkout}Controller.cs` are byte-for-byte
      unchanged — this feature does not touch the API surface at all
- [x] `PresaleUserId`, `CartLine`, `SoftReservation`, `CartService`, `SoftReservationService`
      require **zero** code changes — guest support is entirely upstream of these types
- [x] `Order.Create` and `Order.CustomerId`'s `> 0` invariant are **not** modified — no nullable
      `CustomerId` on `Order` itself (`PlaceOrderVm.CustomerId` becomes `int?`, which is a
      different layer)

### Guest customer provisioning (§3) — Phase 2, verified PASS (independent re-validation, 2026-08-14 round 2)
- [x] `IAccountProfileClient.EnsureGuestCustomerAsync` is idempotent per `PresaleUserId` — calling
      it twice for the same guest session returns the same `UserProfileId`
- [x] `IUserProfileService.GetOrCreateForGuestAsync` calls `IUserProfileRepository.GetByUserIdAsync`
      before creating a new `UserProfile` — never creates a duplicate for an existing guest session
- [x] Guest `UserProfile.UserId` is populated via the same `UserProfile.Create` factory used for
      registered profiles — no parallel guest-only entity or table
- [x] `UserProfile.Create` does **not** publish `UserProfileCreated` — that event has no existing
      publisher anywhere in this codebase; this feature does not start publishing it either
- [x] No `IsGuest` (or similarly named) boolean property is added to `UserProfile`, `Order`, or any
      shared entity

### Promotion (§5) — Phase 3, verified PASS (independent re-validation, 2026-08-14 round 2)
- [x] `UserProfile.ReassignOwner(string newUserId)` validates non-empty `newUserId` and has no
      other side effects — it does not touch `Order.CustomerId` or any other aggregate
- [x] Promotion (`IGuestPromotionService.PromoteAsync`) never creates a second `UserProfile` row —
      updates the existing guest row in place
- [x] The ownership check (`requestingUserId == UserProfile.UserId`) runs **before** any other work
      in `PromoteAsync` (immediately after loading the profile, before the `AlreadyRegistered` check
      or any mutation). Mismatch maps to `Forbid()`, distinct from `ProfileNotFound`'s `NotFound()`
      — note: under this app's ASP.NET Core Identity Cookie auth scheme (no
      `ConfigureApplicationCookie` customization anywhere in the repo, confirmed by repo-wide grep),
      `Forbid()` is observed on the wire as a `302` redirect to `/Identity/Account/AccessDenied`,
      never a literal `403` — this is standard framework behavior, verified independently by an
      HTTP-level integration test, and satisfies the spec's actual intent (non-enumerable,
      distinguishable-from-404 signal), not a defect.
- [x] The "guest-ness" check (`IsUnclaimed`) is computed via `UserManager.FindByIdAsync(profile.UserId)`
      at read time — never persisted as a column (confirmed: `GuestAccountProvisioner.IsRegisteredAsync`,
      `ECommerceApp.Infrastructure/Identity/IAM/Adapters/GuestAccountProvisioner.cs:21-22`)

### Unclaimed profile cleanup job (§8 Risks) — Phase 4, verified PASS (independent re-validation, 2026-08-15)
- [x] Depends on Phase 2 (unclaimed guest profiles must exist) — verified PASS above
- [x] The "has an order" check is a real cross-BC query
      (`CustomersWithOrdersQuery`/`CustomersWithOrdersQueryHandler` via `IModuleClient.SendAsync`,
      `ECommerceApp.Application/Sales/Orders/Queries/CustomersWithOrdersQuery.cs`,
      `ECommerceApp.Infrastructure/Sales/Orders/Handlers/CustomersWithOrdersQueryHandler.cs`) —
      batched (one query for N candidates), not N+1
- [x] The check happens before deletion for every candidate row (`UnclaimedGuestProfileCleanupTask.ExecuteAsync`)
- [x] `UnclaimedGuestProfileCleanupTask` follows the exact `IScheduledTask` shape used by
      `RefreshTokenCleanupTask` (constructor injection, `TaskName`, `ExecuteAsync(JobExecutionContext, CancellationToken)`, try/catch → `ReportSuccess`/`ReportFailure`)
- [x] Retention threshold (90 days, ADR-0030's placeholder) and an `Enabled` toggle are configuration-bound
      (`GuestProfileCleanupOptions`, `GuestProfileCleanup` section in appsettings.json) rather than
      hardcoded — **90 days is the shipped default, not a confirmed business/legal decision**; flagged
      for follow-up discussion (see `.github/plans/04-phase-guest-profile-cleanup-implementation.md`
      Risks section, resolved 2026-08-14)
- [x] `UserProfile.ReassignOwner` (Phase 3) is untouched by this phase — cleanup only ever deletes
- [x] `UnclaimedGuestProfileCleanupTask` is `internal sealed`; the cross-BC check is batched; no raw SQL
- [x] `UserProfile.CreatedAt` migration is additive only (new column with `GETUTCDATE()` default,
      backfills existing rows to migration-apply time rather than treating them as ancient) — human
      approved 2026-08-14 per `safety.instructions.md`
- [x] Recurring cron cadence registered (2026-08-15, human-confirmed cadence: daily at 04:00 UTC) via
      `GuestProfileCleanupScheduledJobReconciler` (`ECommerceApp.Infrastructure/AccountProfile/`), a
      startup `IHostedService` that reconciles the `ScheduledJob` row by name, mirroring
      `MessagingScheduledJobReconciler`'s Outbox/Inbox pattern exactly. Config-driven via
      `GuestProfileCleanupOptions.Schedule` (`GuestProfileCleanup:Schedule` in appsettings.json,
      default `"0 4 * * *"`)

### `VerificationCode` primitive (§9) — Phase 5, verified PASS (independent re-validation, 2026-08-16)
- [x] `VerificationCode`/`VerificationCodeService` contain no branching on `Purpose` and no
      interpretation of `SubjectKey`'s shape — that logic lives only in each consumer's own ACL
- [x] `Code` values are genuinely cryptographically random, not a short numeric OTP, not an
      encoded/reversible id
- [x] No `IAM`/login consumer exists yet — the primitive is generic enough to support one later,
      but none is wired in this feature

### Account linking (§6) — Phase 6, verified PASS (independent re-validation, 2026-08-16)
- [x] Registration (`RegisterModel.OnPostAsync`) returns an identical HTTP response regardless of
      whether a matching unclaimed `UserProfile` is found by email
- [x] Any email-address match against unclaimed `UserProfile`s happens in a background handler,
      never inline in the registration request/response cycle
- [x] Redeeming a `GuestAccountLink` `VerificationCode` reassigns **all** matching unclaimed
      `UserProfile` rows for an email, not just the first (no unique constraint on `Email`)
- [x] No real email is sent — the pending code/link surfaces only in the admin-only Backoffice
      interim view (§10) until real email delivery exists

### Admin interim view (§10) — Phase 6, verified PASS (independent re-validation, 2026-08-16)
- [x] Gated on `[Authorize(Roles = UserPermissions.Roles.Administrator)]` — narrower than the
      `ManagingRole` every other Backoffice controller uses
- [x] Shows pending codes/links for both `GuestAccountLink` and `GuestOrderAccess` purposes in one
      shared view, not two separate controllers (filterable by `Purpose`; only `GuestAccountLink`
      rows exist until Phase 7 adds its own)

### Order access & recovery (§11) — Phase 7, verified PASS (independent re-validation, 2026-08-16)
- [x] The order-access token is minted silently at `PlaceOrder` POST success — no separate guest
      action required
- [x] The token is a genuine random value (≥128-bit), not a hashid/encoded sequential id
- [x] Pay-capability is enforced server-side via `Payment.Status`/`Payment.ExpiresAt` (existing
      3-day window, `PaymentWindowExpiredJob`) — not via the order-access cookie's own lifetime
- [x] The login page's "kontynuuj jako gość" section renders **only** when a valid
      `?guestOrder={token}` is present — never on a bare login-page visit
- [x] No endpoint accepts a bare order number/id and offers to email/generate a code for it — the
      only entry point is the pre-issued token already in a URL
- [x] Redeeming a `GuestOrderAccess` code unlocks **exactly one** order — never all orders for the
      email (this is intentionally the opposite rule from account linking, above — verify both
      directions independently, not just one)

### Session isolation (§12)
- [ ] Every guest-eligible query is filtered by the caller's own resolved identity
      (`PresaleUserId` pre-order, order-access token post-order) — never by a client-supplied id
      alone, never by email as an access grant outside the two proven paths above
- [ ] The set of anonymously-reachable Web endpoints is a closed, explicit list with a regression
      test asserting nothing else gained `[AllowAnonymous]` as a side effect
- [ ] A session-isolation regression test exists exercising concurrent decoy sessions (one guest,
      one authenticated) and confirming the session under test cannot cross into either

### Operational prerequisites
- [ ] `PlaceOrder` POST, guest-cookie issuance, and code-request/redemption endpoints are covered
      by rate limiting before this feature ships (verify existing infrastructure — do not assume
      it exists)
