# Bounded Context Map — ECommerceApp

> **Living document.** Update this map when BC boundaries change or new ADRs are accepted.
> Strategic direction: [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
> Module taxonomy: [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../adr/0004-module-taxonomy-and-bounded-context-grouping.md)
> Folder organization: [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../adr/0003-feature-folder-organization-for-new-bounded-context-code.md)
> Catalog BC design: [ADR-0007 — Catalog BC — Product, Category and Tag Aggregate Design](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
> Currencies BC design: [ADR-0008 — Supporting/Currencies BC — Currency and CurrencyRate Aggregate Design](../adr/0008-supporting-currencies-bc-design.md)
> TimeManagement BC design: [ADR-0009 — Supporting/TimeManagement BC — Scheduled and Deferred Job Design](../adr/0009-supporting-timemanagement-bc-design.md)
> Inventory BC design: [ADR-0011 — Inventory/Availability BC Design](../adr/0011-inventory-availability-bc-design.md)
> Presale/Checkout BC design: [ADR-0012 — Presale/Checkout BC Design](../adr/0012-presale-checkout-bc-design.md)
> Payments BC design: [ADR-0015 — Sales/Payments BC Design](../adr/0015-sales-payments-bc-design.md)
> Per-BC DbContext interfaces: [ADR-0013 — Per-BC DbContext Interfaces](../adr/0013-per-bc-dbcontext-interfaces.md)
> Sales/Orders BC design: [ADR-0014 — Sales/Orders BC — Order and OrderItem Aggregate Design](../adr/0014-sales-orders-bc-design.md)
> Coupons BC design: [ADR-0016 — Sales/Coupons BC Design](../adr/0016-sales-coupons-bc-design.md)
> Fulfillment BC design: [ADR-0017 — Sales/Fulfillment BC Design](../adr/0017-sales-fulfillment-bc-design.md)
> Communication BC design: [ADR-0018 — Supporting/Communication BC — Customer Notification Design](../adr/0018-supporting-communication-bc-design.md)
> Identity/IAM BC design: [ADR-0019 — Identity/IAM BC — Standalone Design and Atomic Switch Plan](../adr/0019-identity-iam-bc-design.md)
> Backoffice BC design: [ADR-0020 — Backoffice BC — Admin Aggregation and Reporting Design](../adr/0020-backoffice-bc-design.md)

---

## Module taxonomy (target folder hierarchy)

```
CORE BUSINESS
├── Sales
│   ├── Orders          → Domain.Sales.Orders
│   ├── Payments        → Domain.Sales.Payments
│   ├── Coupons         → Domain.Sales.Coupons
│   └── Fulfillment     → Domain.Sales.Fulfillment     [greenfield]
│
├── Inventory
│   └── Availability    → Domain.Inventory.Availability [greenfield]
│
├── Presale
│   └── Checkout        → Domain.Presale.Checkout       [greenfield]
│
├── Catalog
│   └── Products        → Domain.Catalog.Products
│
├── AccountProfile → Domain.AccountProfile (UserProfile aggregate)
│
├── Identity
│   └── IAM             → Domain.Identity.IAM
│
├── Supporting
│   ├── Currencies      → Domain.Supporting.Currencies
│   ├── TimeManagement  → Domain.Supporting.TimeManagement [greenfield]
│   └── Communication   → Domain.Supporting.Communication  [greenfield]
│
└── Backoffice          → Application.Backoffice (no domain model)
```

---

## Current state (as-is)

All bounded contexts share a **single EF Core `Context`** and a **single MSSQL database**.
BC boundaries are logical only — not enforced at the infrastructure level.

```
┌─────────────────────────────────────────────────────────────────────┐
│                     Single shared Context                           │
│                                                                     │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │
│  │   Catalog   │  │    Orders    │  │        Payments          │   │
│  │  (legacy)   │  │              │  │                          │   │
│  │ Item ⚠️      │  │ Order        │  │ Payment                  │   │
│  │ Image       │  │ OrderItem    │  │ PaymentState (enum)       │   │
│  │ Brand       │  │              │  │                          │   │
│  │ Tag         │  └──────┬───────┘  └──────────┬───────────────┘   │
│  │ Type        │         │ direct svc call      │ direct svc call   │
│  └──────┬──────┘         ▼                      ▼                   │
│         │ parallel  ┌──────────────┐  ┌──────────────────────────┐ │
│         │ impl ✅   │   Refunds    │  │        Coupons           │ │
│  ┌──────▼──────┐    │              │  │                          │ │
│  │   Catalog   │    │ Refund       │  │ Coupon                   │ │
│  │ (new, own   │    │              │  │ CouponType               │ │
│  │ DbContext)  │    └──────────────┘  │ CouponUsed               │ │
│  │ Product     │                      └──────────────────────────┘ │
│  │ Category    │                                                    │
│  │ Tag / Image │  ┌──────────────┐  ┌──────────────────────────┐   │
│  └─────────────┘  │  Currencies  │  │   Identity / IAM         │   │
│                   │              │  │                          │   │
│  ┌─────────────┐  │ Currency     │  │ ApplicationUser ⚠️        │   │
│  │  Customers  │  │ CurrencyRate │  │ (leaks into Order.User)  │   │
│  │  (legacy)   │  │ (NBP API)   │  │                          │   │
│  │ Customer ⚠️  │  └──────┬───────┘  └──────────────────────────┘   │
│  │ Address     │         │ parallel                                 │
│  └──────┬──────┘         │ impl ✅                                   │
│         │ parallel  ┌────▼─────────────────┐                        │
│         │ impl ✅   │  Currencies (new,    │                        │
│  ┌──────▼──────────────────┐  own CurrencyDbCtx) │                  │
│  │  AccountProfile (new,   │  Currency (rich)     │                  │
│  │  own UserProfileDbCtx)  │  CurrencyRate        │                  │
│  │  UserProfile aggregate  │  (async NBP API)     │                  │
│  └─────────────────────────┘  └────────────────────┘                │
└─────────────────────────────────────────────────────────────────────┘
```

### Current coupling hotspots

| Coupling | Location | Problem |
|---|---|---|
| `PaymentHandler` calls `OrderService` | `Application/Services/Payments/PaymentHandler.cs` | Cross-BC synchronous call — Payment controls Order state |
| `Order.User` navigation → `ApplicationUser` | `Domain/Model/Order.cs` | IAM concept leaks into Orders BC |
| `Order.IsPaid = true` set externally | `Application/Services/Payments/PaymentHandler.cs` | Order state mutated from outside — no encapsulation |
| All aggregates in one `Context` | `Infrastructure/Database/Context.cs` | No persistence-level BC boundary |
| `CouponHandler` reaches into `Order.Cost` | `Application/Services/Coupons/CouponHandler.cs` | Coupons BC writes directly to Orders aggregate |

---

## Target state (to-be)

BC boundaries enforced via **per-BC DbContext interfaces** (logical separation, single physical DB).
Aggregates own their state transitions. Cross-BC communication via domain events.

```
┌───────────────┐     events      ┌───────────────┐     events     ┌──────────────┐
│    Catalog    │ ─────────────► │     Orders    │ ────────────► │   Payments   │
│               │                 │               │                │              │
│ IItemDbContext│                 │ IOrderDbCtx   │                │ IPaymentDbCtx│
│               │                 │               │                │              │
│ Item (rich)   │                 │ Order (rich)  │                │ Payment      │
│ Brand/Tag/Type│                 │ OrderItem     │                │ (state mach.)│
└───────────────┘                 └───────┬───────┘                └──────┬───────┘
                                          │ events                         │ events
                                          ▼                                ▼
                                  ┌───────────────┐                ┌──────────────┐
                                  │    Refunds    │                │   Coupons    │
                                  │               │                │              │
                                  │ IRefundDbCtx  │                │ ICouponDbCtx │
                                  │ Refund        │                │ Coupon       │
                                  └───────────────┘                └──────────────┘

┌───────────────────────┐  ┌──────────────────┐  ┌──────────────────────────────────────┐
│   AccountProfile      │  │   Currencies     │  │           Identity / IAM             │
│                       │  │                  │  │                                      │
│ IUserProfileDbContext │  │ ICurrencyDbCtx   │  │ No navigation props in domain models │
│ UserProfile (rich)    │  │ Currency         │  │ Only string UserId references        │
│ Address (owned VO)    │  │ CurrencyRate     │  │                                      │
└───────────────────────┘  └──────────────────┘  └──────────────────────────────────────┘
```

### Target rules (per ADR-0002 § 8)

- Each BC owns its `DbContext` interface — no cross-BC `DbSet` access
- Cross-BC integration only via domain events or well-defined API contracts
- `ApplicationUser` never appears as a navigation property in any domain model
- `Payment` never controls `Order` lifecycle — events only
- `Availability` / inventory is an explicit domain participant, never a side effect

---

## BC classification

| Bounded Context | Type | Pattern | Status |
|---|---|---|---|
| **Orders** | Behavioral aggregate | Rich domain model → target | 🟡 New implementation in progress (parallel) — Domain, Application, Infrastructure, Unit tests done; DB migration pending; atomic switch pending |
| **Payments** | Behavioral aggregate | Rich domain model + state machine → target | 🔴 Currently anemic |
| **Refunds** | Behavioral aggregate | Rich domain model → target | 🔴 Currently anemic |
| **Catalog** (`Product`) | Mixed | Rich domain model, `ProductStatus` state machine, owned `Image`, `ProductDbContext`, feature-folder — see ADR-0007 | ✅ New implementation ready (parallel) |
| **Coupons** | Reference + behavior | `AbstractService` + `CouponHandler` | 📋 ADR proposed — [ADR-0016](../adr/0016-sales-coupons-bc-design.md) Slice 1: one-time coupon on order; Slice 2 deferred |
| **AccountProfile** (`UserProfile`) | Behavioral aggregate | Rich domain model, owned `Address`, own `UserProfileDbContext` — see ADR-0005 | ✅ New implementation ready (parallel) |
| **Customers** (legacy) | Reference | `AbstractService` | ⚠️ To be replaced by AccountProfile BC |
| **Currencies** | Reference + external | Rich domain model, `CurrencyCode`/`CurrencyDescription` VOs, own `CurrencyDbContext`, fully async NBP — see ADR-0008 | ✅ New implementation ready (parallel) |
| **TimeManagement** | Supporting infrastructure | `Channel<T>` async dispatch, `BackgroundService` scheduler + poller + dispatcher, `IScheduledTask` plugin contract, `IDeferredJobScheduler`, `TimeManagementDbContext`, lazy DB init — see ADR-0009 | ✅ New implementation ready (greenfield) |
| **Inventory/Availability** | Behavioral aggregate | Counter aggregate (`StockItem`), `Reservation`, `ProductSnapshot` ACL, `AvailabilityDbContext`, `RowVersion` optimistic locking, deferred `StockAdjustmentJob` with command coalescing, `PaymentWindowTimeoutJob`, publishes `AvailabilityChanged` outbound — see ADR-0011. Note: `SoftReservation` moved to Presale/Checkout (ADR-0012). | ✅ New implementation ready (parallel) |
| **Messaging** | Shared infrastructure | `IMessageBroker`, `IMessageHandler<T>`, `BackgroundMessageDispatcher`, retry + dead-letter — see ADR-0010 | ✅ Ready — `StockAvailabilityChanged` active; Presale/Checkout Slice 1 subscribed |
| **Presale/Checkout** | Behavioral (Slice 1) | `CartLine` write-through cache, `SoftReservation` (DB + cache, captures `UnitPrice` at checkout initiation), `StockSnapshot` event-driven read model, `SoftReservationExpiredJob`, ACL interfaces (`ICatalogClient`, `IStockClient`), BFF `StorefrontController` in API — see ADR-0012. Slice 2 (cart-to-order) blocked by Sales/Orders. | ✅ Slice 1 implemented (parallel) |
| **Payments** | Behavioral aggregate | `Payment` state machine (Pending → Confirmed / Expired / Refunded), `PaymentWindowExpiredJob` (Payments BC timer), `OrderPlacedHandler` initializes payment, publishes `PaymentExpired` → chain to `OrderCancelled` — see ADR-0015 | 📋 ADR proposed |
| **Identity / IAM** | Infrastructure | ASP.NET Core Identity | ✅ Keep isolated |
| **Backoffice** | Application-only | Admin aggregations, reporting — no domain model | ⚠️ No ADR — scope and boundaries undefined |

---

## Refactoring progress tracker

> **Strategy: Parallel Change** — new BC built alongside old, existing behavior never broken.
> The code and ADRs are the authoritative record of what was built. This tracker answers **"what's next?"**.

### Completed BCs (core implementation done — switch pending)

| BC | ADRs | Pending to switch |
|---|---|---|
| **Identity / IAM** | [ADR-0002 §8](../adr/0002-post-event-storming-architectural-evolution-strategy.md) | Migrate `LoginController` + `UserManagementController` → flip `UseIamStore: true` → remove old `IUserService` / `AuthenticationService` / `Domain/Model/ApplicationUser.cs` → integration tests |
| **AccountProfile** | [ADR-0005](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md), [ADR-0006](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md) | DB migration approval → integration tests → migrate `CustomerController` / `AddressController` / `ContactDetailController` → atomic switch |
| **Catalog** | [ADR-0007](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md) | DB migration approval → integration tests → migrate `ItemController` / `ImageController` / `TagController` → atomic switch |
| **Currencies** | [ADR-0008](../adr/0008-supporting-currencies-bc-design.md), [ADR-0006](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md) | DB migration approval → integration tests → migrate `CurrencyController` (→ async) → coordinate with Catalog switch (`ItemService` dep) → atomic switch |
| **Shared domain primitives** (`TypedId<T>`, `Price`, `Money`, `DomainException`) | [ADR-0006](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md) | — complete |
| **Supporting/TimeManagement** | [ADR-0009](../adr/0009-supporting-timemanagement-bc-design.md) | DB migration approval (two coordinated migrations) → integration tests → `CurrencyRateSyncTask` atomic switch |
| **Inventory/Availability** | [ADR-0011](../adr/0011-inventory-availability-bc-design.md) | DB migration approval (`InitInventorySchema`) → data migration (`Items.Quantity` → `inventory.StockItems`) → integration tests → replace `ItemHandler` calls with `IMessageBroker.PublishAsync(new OrderPlaced(...))` → atomic switch. `AvailabilityChanged` publishing ✅. Soft-hold artifacts removed ✅. |
| **Presale/Checkout — Slice 1** | [ADR-0012](../adr/0012-presale-checkout-bc-design.md) | DB migration approval (`InitPresaleSchema`) → integration tests → Slice 2 blocked by Sales/Orders |
| **Messaging infrastructure** | [ADR-0010](../adr/0010-in-memory-message-broker-for-cross-bc-communication.md) | Integration tests → activate for additional BCs (Orders, Payments) |

> 🔵 Deferred: IAM refresh token — separate ADR required.
> 🔵 Deferred: `Category.ParentId`/`IsVisible`, `Tag.Color`/`IsVisible` — tracked in [ADR-0007 §8/§9 Implementation Status](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md).

---

### Next BCs to implement

| # | BC | ADR | Status | Blocked by |
|---|---|---|---|---|
| 1 | **Sales/Orders** | [ADR-0014](../adr/0014-sales-orders-bc-design.md) | 🟡 In progress — Domain ✅, Application ✅, Infrastructure ✅, Unit tests ✅ — DB migration pending approval; integration tests + atomic switch pending | — (legacy migration; Checkout Slice 2 + Payments depend on it) |
| 2 | **Presale/Checkout — Slice 2** (cart + checkout write flow) | [ADR-0012](../adr/0012-presale-checkout-bc-design.md) §11–14 (formal amendment — not a separate ADR) | ⬜ Not started | Orders (#1) |
| 3 | **Sales/Payments** | [ADR-0015](../adr/0015-sales-payments-bc-design.md) | 🟡 In progress — Domain ✅, Application ✅, Infrastructure ✅, Unit tests ✅ — DB migrations pending approval; integration tests + atomic switch pending | Orders (#1); fixes `PaymentHandler → OrderService` sync call |
| 4 | **Sales/Coupons** | [ADR-0016](../adr/0016-sales-coupons-bc-design.md) | ⬜ Not started — Slice 1: one-time coupon on order; Slice 2 (CouponType, expiry, per-item) deferred | Orders (#1) + Payments (#3); fixes `CouponHandler → Order.Cost` |
| 5 | **Sales/Fulfillment** | [ADR-0017](../adr/0017-sales-fulfillment-bc-design.md) | 📋 ADR proposed — Slice 1: Refund aggregate (`RefundApproved` redesign, Payments extension); Slice 2: Shipment deferred | Orders (#1) + Payments (#3); fixes `RefundService → OrderService` sync call + `RefundApproved` wrong-BC ownership |

---

### Technical debt (cross-cutting)

| Task | ADR | Status |
|---|---|---|
| Per-BC `DbContext` interfaces | [ADR-0013](../adr/0013-per-bc-dbcontext-interfaces.md) | ⬜ Not started — gate: 80–100% BC implementations complete |
| `PaymentHandler` → event-based coordination | [ADR-0015](../adr/0015-sales-payments-bc-design.md) | 🟡 In progress — handlers + job + aggregate implemented ✅; atomic switch (remove legacy `PaymentHandler`) pending integration tests |
| `CouponHandler` — remove direct `Order.Cost` write | [ADR-0016](../adr/0016-sales-coupons-bc-design.md) | ⬜ Not started — atomic switch step 10 |
| `RefundService` → event-based coordination; `RefundApproved` moved to Fulfillment BC; `Payment.IssueRefund` per-item design replaced | [ADR-0017](../adr/0017-sales-fulfillment-bc-design.md) | ⬜ Not started — atomic switch step 11 |
| Remove `ApplicationUser` nav from `Order` | [ADR-0019](../adr/0019-identity-iam-bc-design.md) §5 step 6 | ⬜ Part of Sales/Orders + IAM atomic switch — coordinate per ADR-0019 §5 |
| Frontend error pipeline — `BusinessException._codes` silently discarded; `errors.js:showErrorFromResponse` hides all MVC errors; JS client drift (`ajaxRequest.js` vs `fetch`) | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) | ⬜ Phase 1 (error pipeline) + Phase 2 (bug fixes) not started |

---

### BC ADR coverage gaps

| BC | Current ADR coverage | Gap |
|---|---|---|
| **Supporting/Communication** | [ADR-0018](../adr/0018-supporting-communication-bc-design.md) ✅ | Covered — lean ADR proposed. Blocked by Fulfillment Slice 1 + Coupons Slice 1. |
| **Identity / IAM** | [ADR-0019](../adr/0019-identity-iam-bc-design.md) ✅ | Covered — standalone ADR proposed; atomic switch steps defined |
| **Backoffice** | [ADR-0020](../adr/0020-backoffice-bc-design.md) ✅ | Covered — application-only BC; no domain model. Scope and query assembly pattern defined. |
| **Currencies** | [ADR-0008](../adr/0008-supporting-currencies-bc-design.md) ✅ | Covered — new implementation ready (parallel) |

---

## References

- [ADR-0001 — Project Overview and Technology Stack](../adr/0001-project-overview-and-technology-stack.md)
- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0005 — AccountProfile BC: UserProfile Aggregate Design](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0007 — Catalog BC: Product, Category and Tag Aggregate Design](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0008 — Supporting/Currencies BC: Currency and CurrencyRate Aggregate Design](../adr/0008-supporting-currencies-bc-design.md)
- [ADR-0009 — Supporting/TimeManagement BC: Scheduled and Deferred Job Design](../adr/0009-supporting-timemanagement-bc-design.md)
- [ADR-0013 — Per-BC DbContext Interfaces](../adr/0013-per-bc-dbcontext-interfaces.md)
- [ADR-0014 — Sales/Orders BC: Order and OrderItem Aggregate Design](../adr/0014-sales-orders-bc-design.md)
- [ADR-0015 — Sales/Payments BC Design](../adr/0015-sales-payments-bc-design.md)
- [ADR-0016 — Sales/Coupons BC Design](../adr/0016-sales-coupons-bc-design.md)
- [ADR-0017 — Sales/Fulfillment BC Design](../adr/0017-sales-fulfillment-bc-design.md)
- [ADR-0018 — Supporting/Communication BC: Customer Notification Design](../adr/0018-supporting-communication-bc-design.md)
- [ADR-0019 — Identity/IAM BC: Standalone Design and Atomic Switch Plan](../adr/0019-identity-iam-bc-design.md)
- [ADR-0020 — Backoffice BC: Admin Aggregation and Reporting Design](../adr/0020-backoffice-bc-design.md)
- [ADR-0021 — Frontend Error Pipeline and JS Migration Strategy](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 16
