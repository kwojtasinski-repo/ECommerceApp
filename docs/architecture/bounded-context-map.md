﻿﻿# Bounded Context Map — ECommerceApp

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
│         │ impl ✅   │ Fulfillment  │  │        Coupons           │ │
│  ┌──────▼──────┐    │ (Refund —    │  │                          │ │
│  │   Catalog   │    │  ADR-0017)   │  │ Coupon                   │ │
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

| Coupling                                    | Location                                          | Problem                                                  |
| ------------------------------------------- | ------------------------------------------------- | -------------------------------------------------------- |
| `PaymentHandler` calls `OrderService`       | `Application/Services/Payments/PaymentHandler.cs` | Cross-BC synchronous call — Payment controls Order state |
| `Order.User` navigation → `ApplicationUser` | `Domain/Model/Order.cs`                           | IAM concept leaks into Orders BC                         |
| `Order.IsPaid = true` set externally        | `Application/Services/Payments/PaymentHandler.cs` | Order state mutated from outside — no encapsulation      |
| All aggregates in one `Context`             | `Infrastructure/Database/Context.cs`              | No persistence-level BC boundary                         |
| `CouponHandler` reaches into `Order.Cost`   | `Application/Services/Coupons/CouponHandler.cs`   | Coupons BC writes directly to Orders aggregate           |

---

## Target state (to-be)

BC boundaries enforced via **per-BC DbContext interfaces** (logical separation, single physical DB).
Aggregates own their state transitions. Cross-BC communication via domain events.

```
┌───────────────┐     events      ┌───────────────┐     events     ┌──────────────┐
│    Catalog    │ ─────────────► │     Orders    │ ────────────► │   Payments   │
│               │                 │               │                │              │
│ ProductDbCtx  │                 │ IOrderDbCtx   │                │ IPaymentDbCtx│
│               │                 │               │                │              │
│ Product (rich)│                 │ Order (rich)  │                │ Payment      │
│ Category/Tag  │                 │ OrderItem     │                │ (state mach.)│
└───────────────┘                 └───────┬───────┘                └──────┬───────┘
                                          │ events                         │ events
                                          ▼                                ▼
                                  ┌───────────────┐                ┌──────────────┐
                                  │  Fulfillment  │                │   Coupons    │
                                  │               │                │              │
                                  │ IFulfillDbCtx │                │ ICouponDbCtx │
                                  │ Refund/Shipmt │                │ Coupon       │
                                  └───────┬───────┘                └──────┬───────┘
                                          │ events (parallel               │ events
                                          │ fan-out §13)                   │ (name sync §10)
                                          ▼                                │
                                  ┌───────────────┐                        │
                                  │  Inventory    │◄───────────────────────┘
                                  │  Availability │    (Catalog.Messages:
                                  │               │     ProductNameChanged,
                                  │ IAvailDbCtx   │     not direct dep)
                                  └───────────────┘

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

| Bounded Context                     | Type                      | Pattern                                                                                                                                                                                                                                                                                                                                   | Status                                                                                                                                                |
| ----------------------------------- | ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Orders**                          | Behavioral aggregate      | Rich domain model → target                                                                                                                                                                                                                                                                                                                | ✅ **Switch live** — all acceptance criteria met; legacy code cleaned up                                                                              |
| **Fulfillment** (Refund + Shipment) | Behavioral aggregate      | Refund + Shipment aggregates in `Domain/Sales/Fulfillment/` — see ADR-0017                                                                                                                                                                                                                                                                | ✅ **Switch live** — Slice 1 (Refund) + Slice 2 (Shipment) complete; `RefundController` + `ShipmentController` in `Areas/Sales`                       |
| **Catalog** (`Product`)             | Mixed                     | Rich domain model, `ProductStatus` state machine, owned `Image`, `ProductDbContext`, feature-folder — see ADR-0007                                                                                                                                                                                                                        | ✅ **Switch live** — `ProductController`/`CategoryController`/`TagController`/`ImageController` in `Areas/Catalog`; legacy deleted                    |
| **Coupons**                         | Reference + behavior      | Coupon aggregate, rules engine (16 evaluators), `CouponApplicationRecord` audit trail, `CouponsDbContext`, stacking strategy — see ADR-0016                                                                                                                                                                                               | ✅ **Switch live** — Slice 1 + Slice 2 complete; `CouponController` in `Areas/Sales`; legacy deleted                                                  |
| **AccountProfile** (`UserProfile`)  | Behavioral aggregate      | Rich domain model, owned `Address`, own `UserProfileDbContext` — see ADR-0005                                                                                                                                                                                                                                                             | ✅ **Switch live** — `ProfileController` in `Areas/AccountProfile`; legacy `CustomerController`/`AddressController`/`ContactDetailController` deleted |
| **Customers** (legacy)              | Reference                 | `AbstractService` — superseded by AccountProfile BC                                                                                                                                                                                                                                                                                       | ✅ **Replaced** — all controllers deleted; `IUserProfileRepository` used where needed                                                                 |
| **Currencies**                      | Reference + external      | Rich domain model, `CurrencyCode`/`CurrencyDescription` VOs, own `CurrencyDbContext`, fully async NBP — see ADR-0008                                                                                                                                                                                                                      | ✅ **Switch live** — `CurrencyController` in `Areas/Currencies`; legacy controller + views deleted                                                    |
| **TimeManagement**                  | Supporting infrastructure | `Channel<T>` async dispatch, `BackgroundService` scheduler + poller + dispatcher, `IScheduledTask` plugin contract, `IDeferredJobScheduler`, `TimeManagementDbContext`, lazy DB init — see ADR-0009                                                                                                                                       | ✅ **Switch complete** — `JobManagementController` in `Areas/Jobs`; `CurrencyRateSyncTask` uses new BC service                                        |
| **Inventory/Availability**          | Behavioral aggregate      | Counter aggregate (`StockItem`), `Reservation`, `ProductSnapshot` ACL, `AvailabilityDbContext`, `RowVersion` optimistic locking, deferred `StockAdjustmentJob` with command coalescing, `PaymentWindowTimeoutJob`, publishes `AvailabilityChanged` outbound — see ADR-0011. Note: `SoftReservation` moved to Presale/Checkout (ADR-0012). | ✅ **Switch live** — `StockController` in `Areas/Inventory`; legacy controller + views deleted                                                        |
| **Messaging**                       | Shared infrastructure     | `IMessageBroker`, `IMessageHandler<T>`, `BackgroundMessageDispatcher`, retry + dead-letter — see ADR-0010                                                                                                                                                                                                                                 | ✅ **Active** — used by all BCs; Order Placement Saga (Option A) compensation handlers live                                                           |
| **Presale/Checkout**                | Behavioral (Slice 1 + 2)  | `CartLine` write-through cache, `SoftReservation`, `StockSnapshot`, `SoftReservationExpiredJob`, `ICheckoutService`, `IOrderClient` ACL, BFF `StorefrontController`, `GET /price-changes`, `POST /confirm` — see ADR-0012                                                                                                                 | ✅ **Switch live** — Slice 1 + Slice 2 complete; EC-001 decision documented                                                                           |
| **Payments**                        | Behavioral aggregate      | `Payment` state machine, own `IPaymentDbContext`, event-driven coordination — see ADR-0015                                                                                                                                                                                                                                                | ✅ **Switch live** — all acceptance criteria met; `PaymentController` in `Areas/Sales`; legacy `PaymentHandler` deleted                               |
| **Identity / IAM**                  | Infrastructure            | ASP.NET Core Identity                                                                                                                                                                                                                                                                                                                     |
| **Backoffice**                      | Application-only          | Admin aggregations, reporting — no domain model. Pure query-assembly layer: 9 services delegating to per-BC services, 9 web controllers, 21 Razor views. No own DbContext. See ADR-0020.                                                                                                                                                  | ✅ **Switch live** — TIER 1 (9 application services) + TIER 2 (9 controllers, 21 views) + TIER 3 (58 unit tests). ADR-0020 Accepted.                  |

---

## Refactoring progress tracker

> **Strategy: Parallel Change** — new BC built alongside old, existing behavior never broken.
> The code and ADRs are the authoritative record of what was built. This tracker answers **"what's done and what remains?"**.
> _Last updated: 2026-04 — all BCs switched to production._

### All BCs — completed and switched ✅

The parallel-change migration is complete. Every BC has been implemented and switched to production.

| BC                                                                               | ADRs                                                                                                                                                        | State                                                                                                                           |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------- |
| **Identity / IAM**                                                               | [ADR-0019](../adr/0019-identity-iam-bc-design.md)                                                                                                           | ✅ Switch complete — `Context` → `DbContext`, `Domain.Model.ApplicationUser` deleted, legacy controllers/services/repos deleted |
| **AccountProfile**                                                               | [ADR-0005](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md), [ADR-0006](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md) | ✅ Switch live — `ProfileController` in `Areas/AccountProfile`; legacy deleted                                                  |
| **Catalog**                                                                      | [ADR-0007](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)                                                                                 | ✅ Switch live — `ProductController`/`CategoryController`/`TagController`/`ImageController` in `Areas/Catalog`; legacy deleted  |
| **Currencies**                                                                   | [ADR-0008](../adr/0008-supporting-currencies-bc-design.md)                                                                                                  | ✅ Switch live — `CurrencyController` in `Areas/Currencies`; legacy deleted                                                     |
| **Shared domain primitives** (`TypedId<T>`, `Price`, `Money`, `DomainException`) | [ADR-0006](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md)                                                                            | ✅ Complete                                                                                                                     |
| **Supporting/TimeManagement**                                                    | [ADR-0009](../adr/0009-supporting-timemanagement-bc-design.md)                                                                                              | ✅ Switch complete — `JobManagementController` in `Areas/Jobs`; `CurrencyRateSyncTask` uses new BC                              |
| **Inventory/Availability**                                                       | [ADR-0011](../adr/0011-inventory-availability-bc-design.md)                                                                                                 | ✅ Switch live — `StockController` in `Areas/Inventory`; legacy deleted                                                         |
| **Presale/Checkout (Slice 1 + 2)**                                               | [ADR-0012](../adr/0012-presale-checkout-bc-design.md)                                                                                                       | ✅ Switch live — Slice 2 complete; EC-001 decision documented                                                                   |
| **Messaging infrastructure**                                                     | [ADR-0010](../adr/0010-in-memory-message-broker-for-cross-bc-communication.md)                                                                              | ✅ Active — all BCs integrated; Order Placement Saga (Option A) live                                                            |
| **Sales/Orders**                                                                 | [ADR-0014](../adr/0014-sales-orders-bc-design.md)                                                                                                           | ✅ Switch live — `OrderController` in `Areas/Sales`; legacy deleted                                                             |
| **Sales/Payments**                                                               | [ADR-0015](../adr/0015-sales-payments-bc-design.md)                                                                                                         | ✅ Switch live — `PaymentController` in `Areas/Sales`; legacy deleted                                                           |
| **Sales/Coupons (Slice 1 + 2)**                                                  | [ADR-0016](../adr/0016-sales-coupons-bc-design.md)                                                                                                          | ✅ Switch live — rules engine, `CouponApplicationRecord` audit, `CouponController` in `Areas/Sales`; legacy deleted             |
| **Sales/Fulfillment (Slice 1 + 2)**                                              | [ADR-0017](../adr/0017-sales-fulfillment-bc-design.md)                                                                                                      | ✅ Switch live — `RefundController` + `ShipmentController` in `Areas/Sales`; legacy deleted                                     |
| **Supporting/Communication**                                                     | [ADR-0018](../adr/0018-supporting-communication-bc-design.md)                                                                                               | ✅ Complete — 7 handlers, `LoggingNotificationService` stub, `AddCommunicationServices()` DI                                    |
| **Backoffice**                                                                   | [ADR-0020](../adr/0020-backoffice-bc-design.md)                                                                                                             | ✅ Switch live — 9 services, 9 controllers, 21 views in `Areas/Backoffice`; 58 unit tests                                       |
| **Per-BC DbContext interfaces**                                                  | [ADR-0013](../adr/0013-per-bc-dbcontext-interfaces.md)                                                                                                      | ✅ Complete — 10 interfaces, 27 repos updated, DI aliases registered                                                            |

---

### Remaining work

| Task                                                                                                    | ADR                                                                          | Status                                                                                                       |
| ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| **`Context.cs` removal** — empty legacy `DbContext`; blocked by EF migration history                    | —                                                                            | ⬜ Deferred — cannot remove until old EF migrations are consolidated; test infra `BaseTest<T>` depends on it |
| **Storefront §3 — category strip** (`ByCategory` query + view + UI strip)                               | [ADR-0012](../adr/0012-presale-checkout-bc-design.md)                        | ⬜ Not started — `roadmap/storefront-offers.md` §3 optional                                                  |
| **Chunked upload V2 (TUS)** — replace V1 `UploadSessionStore` with `tusdotnet` + `tus-js-client`        | —                                                                            | ⬜ Not started — V1 (`ChunkedUploadService`) is live                                                         |
| **Frontend error pipeline phases**                                                                      | [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) | ✅ Phase 1–4 complete                                                                                        |
| **`IOrderExistenceChecker` contract deduplication** — defined in both Coupons and Fulfillment Contracts | —                                                                            | ⬜ Merge to `Application/Shared/Contracts/`                                                                  |

---

### BC ADR coverage

All BCs have accepted ADRs. No coverage gaps remain.

| BC                           | ADR                                                              |
| ---------------------------- | ---------------------------------------------------------------- |
| **Supporting/Communication** | [ADR-0018](../adr/0018-supporting-communication-bc-design.md) ✅ |
| **Identity / IAM**           | [ADR-0019](../adr/0019-identity-iam-bc-design.md) ✅             |
| **Backoffice**               | [ADR-0020](../adr/0020-backoffice-bc-design.md) ✅               |
| **Currencies**               | [ADR-0008](../adr/0008-supporting-currencies-bc-design.md) ✅    |

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
