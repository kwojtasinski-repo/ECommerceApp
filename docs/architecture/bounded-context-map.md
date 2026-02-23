# Bounded Context Map — ECommerceApp

> **Living document.** Update this map when BC boundaries change or new ADRs are accepted.
> Strategic direction: [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
> Module taxonomy: [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../adr/0004-module-taxonomy-and-bounded-context-grouping.md)
> Folder organization: [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../adr/0003-feature-folder-organization-for-new-bounded-context-code.md)

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
├── Customer
│   └── CustomerProfile → Domain.Customer.CustomerProfile
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
│  │             │  │              │  │                          │   │
│  │ Item        │  │ Order        │  │ Payment                  │   │
│  │ Image       │  │ OrderItem    │  │ PaymentState (enum)       │   │
│  │ Brand       │  │              │  │                          │   │
│  │ Tag         │  └──────┬───────┘  └──────────┬───────────────┘   │
│  │ Type        │         │ direct svc call      │ direct svc call   │
│  └─────────────┘         ▼                      ▼                   │
│                   ┌──────────────┐  ┌──────────────────────────┐   │
│                   │   Refunds    │  │        Coupons           │   │
│                   │              │  │                          │   │
│                   │ Refund       │  │ Coupon                   │   │
│                   │              │  │ CouponType               │   │
│                   └──────────────┘  │ CouponUsed               │   │
│                                     └──────────────────────────┘   │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐   │
│  │  Customers  │  │  Currencies  │  │   Identity / IAM         │   │
│  │             │  │              │  │                          │   │
│  │ Customer    │  │ Currency     │  │ ApplicationUser ⚠️        │   │
│  │ Address     │  │ CurrencyRate │  │ (leaks into Order.User)  │   │
│  │ ContactDetail│  │ (NBP API)   │  │                          │   │
│  └─────────────┘  └──────────────┘  └──────────────────────────┘   │
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

┌───────────────┐  ┌──────────────────┐  ┌──────────────────────────────────────┐
│   Customers   │  │   Currencies     │  │           Identity / IAM             │
│               │  │                  │  │                                      │
│ ICustomerDbCtx│  │ ICurrencyDbCtx   │  │ No navigation props in domain models │
│ Customer      │  │ Currency         │  │ Only string UserId references        │
│ Address       │  │ CurrencyRate     │  │                                      │
└───────────────┘  └──────────────────┘  └──────────────────────────────────────┘
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
| **Catalog** (`Item`) | Mixed | Handler pattern for complex ops | 🟡 Partially rich (`Item` has some methods) |
| **Coupons** | Reference + behavior | `AbstractService` + `CouponHandler` | 🟡 Acceptable for now |
| **Customers** | Reference | `AbstractService` | ✅ Acceptable |
| **Currencies** | Reference + external | `AbstractService` + NBP integration | ✅ Acceptable |
| **Identity / IAM** | Infrastructure | ASP.NET Core Identity | ✅ Keep isolated |

---

## Refactoring progress tracker

> **Strategy: Parallel Change** — new BC built alongside old, existing behavior never broken.
> Switch happens atomically per BC only when new implementation is complete and all tests pass.
> Cross-BC issues (e.g. `ApplicationUser` in `Order.cs`) resolved as part of owning BC's migration — not as standalone fixes.

| Task | Target ADR | Status |
|---|---|---|
| `IamDbContext` + `iam.*` schema | ADR-0002 § 8 | ✅ Done |
| Feature flag `UseIamStore` (parallel change switch) | ADR-0002 § 8 | ✅ Done |
| Application services: `IAuthenticationService`, `IUserManagementService` | ADR-0002 § 8 | ✅ Done |
| Infrastructure: JWT, UserManager, UserContext | ADR-0002 § 8 | ✅ Done |
| Unit tests: `AuthenticationServiceTests`, `UserManagementServiceTests` | ADR-0002 § 8 | ✅ Done |
| New `ApplicationUser` in `Domain/Identity/IAM/` | ADR-0002 § 8 | ✅ Done |
| Migrate `LoginController` (API) → new `IAuthenticationService` | ADR-0002 § 8 | ⬜ Not started |
| Migrate `UserManagementController` (Web) → `IUserManagementService` | ADR-0002 § 8 | ⬜ Not started |
| Flip `UseIamStore: true` — atomic switch | ADR-0002 § 8 | ⬜ Not started |
| Remove old `IUserService` / `UserService` | ADR-0002 § 8 | ⬜ After switch |
| Remove old `IAuthenticationService` / `AuthenticationService` | ADR-0002 § 8 | ⬜ After switch |
| Retire `Domain/Model/ApplicationUser.cs` | ADR-0002 § 8 | ⬜ After switch |
| IAM integration tests | ADR-0002 § 8 | ⬜ Not started |
| Refresh token implementation | 🔵 Deferred — separate ADR | |
| **AccountProfile BC — Domain layer** (`ContactDetailType`, `Address`, `ContactDetail`, `AccountProfile`, `AccountProfileCreated`, repository interfaces) | ADR-0002 § 8 | ✅ Done |
| **AccountProfile BC — Infrastructure layer** (`AccountProfileDbContext`, `profile.*` schema, configs, repositories, DI) | ADR-0002 § 8 | ✅ Done |
| **AccountProfile BC — Application layer** (DTOs, ViewModels, `IAccountProfileService`, `IAccountAddressService`, `IAccountContactDetailService`, `IAccountContactDetailTypeService`, validators, DI) | ADR-0002 § 8 | ✅ Done |
| **AccountProfile BC — Unit tests** (`AccountProfileAggregateTests`, `AccountProfileServiceTests`) | ADR-0002 § 8 | ✅ Done |
| **AccountProfile BC — DB migration** (`profile` schema) | ADR-0002 § 8 — requires migration approval | ⬜ Pending approval |
| AccountProfile BC — Integration tests | ADR-0002 § 8 | ⬜ Not started |
| Migrate `CustomerController` / `AddressController` / `ContactDetailController` (Web + API) → new services | ADR-0002 § 8 | ⬜ Not started |
| Atomic switch — remove old Customer/Address/ContactDetail registrations | ADR-0002 § 8 | ⬜ After integration tests pass |
| Remove `ApplicationUser` nav from `Order` | ADR-0002 § 8 — part of Sales/Orders migration | ⬜ Not started |
| `Order.MarkAsPaid()` — own state transition | ADR-0008 | ⬜ Not started |
| `Payment` factory + private setters | ADR-0008 | ⬜ Not started |
| Per-BC `DbContext` interfaces | ADR-0009 | ⬜ Not started |
| `PaymentHandler` → event-based coordination | Planned ADR-0004 (Saga) | ⬜ Not started |
| `CouponHandler` → no direct `Order.Cost` write | ADR-0002 § 9 | ⬜ Not started |

---

## References

- [ADR-0001 — Project Overview and Technology Stack](../adr/0001-project-overview-and-technology-stack.md)
- [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 16
