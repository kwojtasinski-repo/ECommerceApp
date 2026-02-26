# Bounded Context Map — ECommerceApp

> **Living document.** Update this map when BC boundaries change or new ADRs are accepted.
> Strategic direction: [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
> Module taxonomy: [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../adr/0004-module-taxonomy-and-bounded-context-grouping.md)
> Folder organization: [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../adr/0003-feature-folder-organization-for-new-bounded-context-code.md)
> Catalog BC design: [ADR-0007 — Catalog BC — Product, Category and Tag Aggregate Design](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
> Currencies BC design: [ADR-0008 — Supporting/Currencies BC — Currency and CurrencyRate Aggregate Design](../adr/0008-supporting-currencies-bc-design.md)
> TimeManagement BC design: [ADR-0009 — Supporting/TimeManagement BC — Scheduled and Deferred Job Design](../adr/0009-supporting-timemanagement-bc-design.md)

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
| **Orders** | Behavioral aggregate | Rich domain model → target | 🔴 Currently anemic |
| **Payments** | Behavioral aggregate | Rich domain model + state machine → target | 🔴 Currently anemic |
| **Refunds** | Behavioral aggregate | Rich domain model → target | 🔴 Currently anemic |
| **Catalog** (`Product`) | Mixed | Rich domain model, `ProductStatus` state machine, owned `Image`, `ProductDbContext`, feature-folder — see ADR-0007 | ✅ New implementation ready (parallel) |
| **Coupons** | Reference + behavior | `AbstractService` + `CouponHandler` | 🟡 Acceptable for now |
| **AccountProfile** (`UserProfile`) | Behavioral aggregate | Rich domain model, owned `Address`, own `UserProfileDbContext` — see ADR-0005 | ✅ New implementation ready (parallel) |
| **Customers** (legacy) | Reference | `AbstractService` | ⚠️ To be replaced by AccountProfile BC |
| **Currencies** | Reference + external | Rich domain model, `CurrencyCode`/`CurrencyDescription` VOs, own `CurrencyDbContext`, fully async NBP — see ADR-0008 | ✅ New implementation ready (parallel) |
| **TimeManagement** | Supporting infrastructure | `Channel<T>` async dispatch, `BackgroundService` scheduler + poller + dispatcher, `IScheduledTask` plugin contract, `IDeferredJobScheduler`, `TimeManagementDbContext`, lazy DB init — see ADR-0009 | 🔵 ADR accepted, implementation not started |
| **Identity / IAM** | Infrastructure | ASP.NET Core Identity | ✅ Keep isolated |

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

> 🔵 Deferred: IAM refresh token — separate ADR required.
> 🔵 Deferred: `Category.ParentId`/`IsVisible`, `Tag.Color`/`IsVisible` — tracked in [ADR-0007 §8/§9 Implementation Status](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md).

---

### Next BCs to implement

| BC | ADR | Status | Notes |
|---|---|---|---|
| **Supporting/TimeManagement** | [ADR-0009](../adr/0009-supporting-timemanagement-bc-design.md) | 🔵 In progress | Domain + Infrastructure + Application + `CurrencyRateSyncTask` + unit tests |
| **Sales/Orders** | — | ⬜ Not started | `Order.MarkAsPaid()`, factory, private setters |
| **Sales/Payments** | — | ⬜ Not started | After Orders — `Payment` factory, state machine |
| **Sales/Coupons** | — | ⬜ Not started | After Orders + Payments — resolve `CouponHandler` direct `Order.Cost` write |
| **Presale/Checkout** | — | ⬜ Not started | Greenfield — after all Sales BCs stable |

---

### Technical debt (cross-cutting)

| Task | ADR | Status |
|---|---|---|
| Per-BC `DbContext` interfaces | Planned ADR-0010 | ⬜ Not started |
| `PaymentHandler` → event-based coordination | Planned ADR (Saga) | ⬜ Not started |
| `CouponHandler` — remove direct `Order.Cost` write | ADR-0002 §9 | ⬜ Not started |
| Remove `ApplicationUser` nav from `Order` | ADR-0002 §8 | ⬜ Part of Sales/Orders migration |

---

## References

- [ADR-0001 — Project Overview and Technology Stack](../adr/0001-project-overview-and-technology-stack.md)
- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
- [ADR-0005 — AccountProfile BC: UserProfile Aggregate Design](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [ADR-0007 — Catalog BC: Product, Category and Tag Aggregate Design](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)
- [ADR-0008 — Supporting/Currencies BC: Currency and CurrencyRate Aggregate Design](../adr/0008-supporting-currencies-bc-design.md)
- [ADR-0009 — Supporting/TimeManagement BC: Scheduled and Deferred Job Design](../adr/0009-supporting-timemanagement-bc-design.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 16
