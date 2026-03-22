# ADR-0025: API Tiered Access Model — Trusted Purchase Policy, Quantity Limits, and Payment URL

## Status
Accepted

## Date
2026-03-22

## Context

The V2 API (`api/v2/`) currently exposes the full purchase flow — cart write, checkout initiation,
checkout confirmation, and order placement — to **any authenticated user** with no abuse protection
and no differentiation between trusted integrators and ordinary account holders.

Three concrete problems identified:

**Problem 1 — No tiered access.**
Any user who can authenticate via JWT can place unlimited orders through the API. This is
inappropriate for a storefront that expects purchase-flow API consumers to be vetted integrators
or service accounts, not arbitrary end users.

**Problem 2 — No quantity cap.**
`Shared.Quantity` only validates `value > 0`. There is no upper bound on the number of units
of a single product a caller can add to the cart or include in an order via the API. This
enables bulk-purchase abuse (automated ordering bots, inventory exhaustion attacks).

The same gap exists on the Web project — `AddToCartDto` has no validator. Both must be addressed,
with separate limits for each surface (Web = storefront, API = integration channel).

**Problem 3 — Checkout confirm returns no payment URL.**
`POST /api/v2/checkout/confirm` currently returns `{ orderId }` only. API consumers need a
redirect URL to send the user to the Web payment page. Without this, the integrator must
hard-code or guess the payment route — a coupling risk across deployments and environments.

## Decision

### § 1 Authorization policy — `TrustedApiUser`

We will introduce a named ASP.NET Core authorization policy **`TrustedApiUser`** with the
following requirement:

> Caller must be authenticated AND (has claim `api:purchase = true`) OR (has role `Service`, `Manager`, or `Administrator`).

**Claim `api:purchase`**: A custom JWT claim emitted by `LoginController` when the user account
has been explicitly granted purchase API access by an administrator. This is granted via the
backoffice user-management UI (future Backoffice BC — ADR-0020). The claim value is `"true"`.

**Management roles**: The existing `Service`, `Manager`, and `Administrator` roles automatically
qualify as trusted purchasers without the claim. These roles represent internal operators and
service accounts who are inherently trusted to execute the purchase flow. This matches the
existing `MaintenanceRole` constant pattern in `BaseController`.

No new role is introduced. This avoids polluting the role taxonomy with an API-specific concept.
Role assignment is coarse-grained; the claim is the fine-grained, revocable grant for external
integrators who hold the standard `User` role.

**Policy registration (API `Program.cs` / `Startup.cs`):**

```csharp
options.AddPolicy("TrustedApiUser", policy =>
    policy.RequireAuthenticatedUser()
          .RequireAssertion(ctx =>
              ctx.User.HasClaim("api:purchase", "true") ||
              ctx.User.IsInRole(UserPermissions.Roles.Service) ||
              ctx.User.IsInRole(UserPermissions.Roles.Manager) ||
              ctx.User.IsInRole(UserPermissions.Roles.Administrator)));
```

**Endpoints protected by `TrustedApiUser` policy:**

| Controller | Action | Protection |
|---|---|---|
| `CartController` | `POST /items` | `[Authorize(Policy = "TrustedApiUser")]` |
| `CartController` | `DELETE /items/{id}` | `[Authorize(Policy = "TrustedApiUser")]` |
| `CartController` | `DELETE /` | `[Authorize(Policy = "TrustedApiUser")]` |
| `CheckoutController` | `POST /initiate` | `[Authorize(Policy = "TrustedApiUser")]` |
| `CheckoutController` | `POST /confirm` | `[Authorize(Policy = "TrustedApiUser")]` |
| `OrdersController` | `POST /` | `[Authorize(Policy = "TrustedApiUser")]` |
| `CartController` | `GET /` | `[Authorize]` (read = any auth user) |
| `CheckoutController` | `GET /price-changes` | `[Authorize]` (read = any auth user) |
| `OrdersController` | `GET /my` | `[Authorize]` (read = any auth user) |

**Ownership enforcement on GET-by-id:**
`GET /api/v2/orders/{id}` and `GET /api/v2/payments/{id}` currently perform no ownership check —
any authenticated user can read any record by ID. These must validate that the returned record
belongs to the caller before responding.

### § 2 Quantity limit — max 5 units of one product per API order line

**Scope**: Maximum **5 units of a single product** in any one cart line added via the API.
This is a per-line limit, not a total-order limit. A caller may add multiple distinct products
but may not add more than 5 units of any single product.

**Limit value**: **5** (hardcoded constant for now — see Future work below).

**Named constant:**

```csharp
// ECommerceApp.API/Options/ApiPurchaseOptions.cs
public sealed class ApiPurchaseOptions
{
    public const int MaxQuantityPerOrderLine = 5;
}
```

**Enforcement point**: An `ActionFilter` (`MaxApiQuantityFilter`) applied to
`CartController.AddOrUpdate`. This is an API-layer concern — it does not belong in the domain
(`Shared.Quantity` stays pure: `value > 0` only) and it does not belong in the application
layer (Application layer is shared with the Web project which has a different limit).

**Web project**: A separate `AddToCartDtoValidator` (FluentValidation) will be created in the
Application layer with `RuleFor(x => x.Quantity).LessThanOrEqualTo(99)`. The Web and API limits
are independent and intentionally different.

**Future work (tracked):**
Both the API and Web quantity limits will become **backoffice-configurable settings**.
The Backoffice BC (ADR-0020) will own two independent settings:

| Setting | Default | Enforced by |
|---|---|---|
| `ApiMaxQuantityPerOrderLine` | 5 | `MaxApiQuantityFilter` (API layer) |
| `WebMaxQuantityPerOrderLine` | 99 | `AddToCartDtoValidator` (Application layer) |

- Both values stored in `backoffice.PurchaseLimitSettings` DB table.
- Loaded into `IMemoryCache` on application startup and refreshed periodically
  (same pattern as the Currencies BC NBP rate cache).
- Surfaced via `IApiPurchaseLimitsService` with two methods:
  `GetApiMaxQuantityPerOrderLineAsync()` and `GetWebMaxQuantityPerOrderLineAsync()`.
- The `ActionFilter` and the `AddToCartDtoValidator` each resolve their own limit
  from `IApiPurchaseLimitsService` instead of the hardcoded constant.
- Until the Backoffice BC is live, the constants in `ApiPurchaseOptions` are authoritative:
  `MaxQuantityPerOrderLine = 5` (API) and `MaxWebQuantityPerOrderLine = 99` (Web).

### § 3 Payment URL in checkout confirm response

`POST /api/v2/checkout/confirm` will return:

```json
{
  "orderId": 42,
  "paymentUrl": "https://app.example.com/Sales/Payments/Create?orderId=42"
}
```

**Construction**: The payment URL is assembled from:
- `WebOptions:BaseUrl` — configured in `API/appsettings.json` per environment (e.g.,
  `https://localhost:5001` in development, `https://app.example.com` in production).
- Fixed path: `/Sales/Payments/Create?orderId={orderId}`.

**Options class:**

```csharp
// ECommerceApp.API/Options/WebOptions.cs
public sealed class WebOptions
{
    public string BaseUrl { get; init; } = string.Empty;
}
```

This couples the API deployment config to the Web project's route structure. This is an accepted
trade-off for the current monolith deployment model. When the API and Web are decoupled to
separate deployments, `WebOptions:BaseUrl` is the only file that changes.

## Consequences

### Positive
- Purchase-flow API access is explicitly granted (claim) or role-scoped (Service) — no
  accidental open access.
- The `Service` role continues to work without change — existing automation is unaffected.
- Quantity abuse is blocked at the API edge — domain stays pure.
- Web and API quantity limits are independently tunable.
- API consumers receive an actionable payment URL — no route guessing.
- Quantity limit is hardcoded now but the architecture for future configurability is pre-decided.

### Negative
- Claim `api:purchase` must be emitted by `LoginController` — requires a check against the
  user's account flags at JWT issuance time. This touches `LoginController` and the JWT claim
  assembly pipeline.
- `WebOptions:BaseUrl` must be kept in sync with the Web project's actual deployment URL
  across environments. A misconfiguration produces a broken payment link silently.

### Risks & mitigations
- **Stale payment URL config**: Mitigated by making `WebOptions:BaseUrl` a required config
  value — startup validation fails fast if the value is empty.
- **Claim not revoked in existing tokens**: JWT tokens are short-lived (15 min default). Revoking
  the `api:purchase` claim takes effect on the next token refresh. For immediate revocation,
  the existing token blocklist mechanism (if any) must be used.

## Alternatives considered

- **New `TrustedApiUser` role** — rejected. Adding a role for a single API concern pollutes the
  role taxonomy. Claims are the right mechanism for fine-grained, revocable per-user grants. The
  existing management roles (Service, Manager, Administrator) already cover the service-account
  and operator use cases.
- **Domain-level quantity cap in `Shared.Quantity`** — rejected. The domain value object should
  not encode an API policy. The maximum is a business/channel rule, not a fundamental invariant
  of "what a quantity is". Different channels (Web, API, backoffice) may have different maxima.
- **Total-order quantity cap** — rejected in favour of per-line cap. A per-line cap is simpler
  to enforce at the `AddOrUpdate` action, does not require reading the full cart state, and
  prevents the most common abuse pattern (bulk-adding one product).
- **Return only `orderId` from confirm** — rejected. API consumer must not hard-code Web routes.
  A configured base URL is the correct coupling point.
- **Embed payment URL in the Payment BC** — not applicable. The payment page lives in the Web
  project. The Payments BC does not own UI routes.

## Migration plan

Implementation is split into three phases tracked in `orders-atomic-switch.md` Step 4:

**Phase 4a — Authorization policy**
1. Add `TrustedApiUser` policy to `API/Startup.cs`.
2. Add `[Authorize(Policy = "TrustedApiUser")]` to write endpoints in `CartController`,
   `CheckoutController`, `OrdersController`.
3. Add claim `api:purchase` emission to `LoginController` JWT assembly.
4. Add ownership check to `GET /api/v2/orders/{id}` and `GET /api/v2/payments/{id}`.

**Phase 4b — Quantity limit**
1. Create `ApiPurchaseOptions` with `MaxQuantityPerOrderLine = 5`.
2. Create `MaxApiQuantityFilter` action filter.
3. Apply filter to `CartController.AddOrUpdate`.
4. Create `AddToCartDtoValidator` in Application layer for the Web 99-per-line limit.

**Phase 4c — Payment URL**
1. Create `WebOptions` class and register in `API/Startup.cs`.
2. Inject `IOptions<WebOptions>` into `API/Controllers/V2/CheckoutController`.
3. Update `Confirm` action to return `{ orderId, paymentUrl }`.
4. Add `WebOptions:BaseUrl` to `API/appsettings.json` and `API/appsettings.Development.json`.
