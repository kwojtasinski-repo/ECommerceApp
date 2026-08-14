## Migration plan

> Revised 2026-08-14. The step-by-step detail now lives in `.github/plans/01`–`08-phase-*`
> (implementation + validation pairs); this file is the short index + prerequisite, not the source
> of truth for exact file/method shapes — read the phase files for that.

**Prerequisite (verify before starting Phase 1):** confirm what rate-limiting infrastructure
already exists for public Web endpoints. Guest checkout removes the "must be authenticated" gate
that today incidentally throttles abuse of `CheckoutController`'s actions — this must be replaced
before `[AllowAnonymous]` ships, not after.

| Step | Phase file | Summary |
|---|---|---|
| 1 | `01-phase-guest-shopper-identity-*` | `IShopperIdentityResolver` (dedicated service, not `BaseController`), guest cookie, `[AllowAnonymous]` on `ECommerceApp.Web`'s `CheckoutController` |
| 2 | `02-phase-guest-customer-provisioning-*` | `GetOrCreateForGuestAsync`/`EnsureGuestCustomerAsync`, wired into `PlaceOrder` POST |
| 3 | `03-phase-guest-account-promotion-*` | `UserProfile.ReassignOwner`, `IGuestPromotionService`, `CreateAccount` Web action |
| 4 | `04-phase-guest-profile-cleanup-*` | Scheduled cleanup of unclaimed `UserProfile` rows with no `Order` |
| 5 | `05-phase-verification-code-primitive-*` | Generic `VerificationCode` (Supporting BC), consumed via ACL — the shared building block for Steps 6 and 7 |
| 6 | `06-phase-guest-account-linking-*` | Registration-success handler, redemption reassigns **all** matching profiles, admin-only Backoffice interim view |
| 7 | `07-phase-guest-order-access-recovery-*` | Order-access token (minted at `PlaceOrder` POST), login-page recovery, redemption unlocks **exactly one** order |
| 8 | `08-phase-guest-checkout-regression-*` | Closed anonymous-endpoint allowlist test, session-isolation test (concurrent decoy sessions), full unit + `Web.IntegrationTests` + `Web.E2E` regression |

Ordering: 1 → 2 → 3, with 4 startable any time after 2. 5 has no dependency on 1–4 and can be built
in parallel. 6 depends on 5 and 2. 7 depends on 5 and 2/3. 8 depends on all of the above being
independently PASS.

**Tests, by category** (detail in each phase file):
- Unit: `ECommerceApp.UnitTests` — identity resolution, guest provisioning idempotency,
  `ReassignOwner` validation, `VerificationCode` lifecycle, ownership/ scoping checks
- Integration: `ECommerceApp.Web.IntegrationTests` — full guest checkout with no login header,
  registration-response-identical regression (Problem 4), scoping-asymmetry regression (§6 vs §11),
  anonymous-endpoint-allowlist regression
- Browser E2E: `ECommerceApp.Web.E2E` — full guest lifecycle through payment/promotion, and the
  order-access recovery flow through the admin Backoffice view, per Phase 8

**Not in this migration**: anything under `ECommerceApp.API` (explicit non-goal, ADR §2); real
outbound email delivery (§10's admin view is the interim substitute); passwordless login for real
accounts using `VerificationCode` as a third consumer (considered, explicitly deferred to its own
future ADR — see the main ADR's Alternatives section).
