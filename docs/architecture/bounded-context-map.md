# Bounded Context Map — ECommerceApp

> **Living document.** Update this map when BC boundaries change or new ADRs are accepted.
> Strategic direction: [ADR-0002 — Post-Event-Storming Architectural Evolution Strategy](../adr/0002-post-event-storming-architectural-evolution-strategy.md)
> Module taxonomy: [ADR-0004 — Module Taxonomy and Bounded Context Grouping](../adr/0004-module-taxonomy-and-bounded-context-grouping.md)
> Folder organization: [ADR-0003 — Feature-Folder Organization for New Bounded Context Code](../adr/0003-feature-folder-organization-for-new-bounded-context-code.md)
> Catalog BC design: [ADR-0007 — Catalog BC — Product, Category and Tag Aggregate Design](../adr/0007-catalog-bc-product-category-tag-aggregate-design.md)

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
│  │ Customer ⚠️  │  └──────────────┘  └──────────────────────────┘   │
│  │ Address     │                                                     │
│  └──────┬──────┘                                                     │
│         │ parallel                                                   │
│         │ impl ✅                                                     │
│  ┌──────▼──────────────────┐                                        │
│  │  AccountProfile (new,   │                                        │
│  │  own UserProfileDbCtx)  │                                        │
│  │  UserProfile aggregate  │                                        │
│  └─────────────────────────┘                                        │
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
| **AccountProfile BC — Domain layer** (`UserProfile` aggregate, owned `Address`, `UserProfileCreated` event, `IUserProfileRepository`) | ADR-0005 | ✅ Done |
| **AccountProfile BC — Infrastructure layer** (`UserProfileDbContext`, `profile.*` schema, `OwnsMany` Address config, `UserProfileRepository`, DI) | ADR-0005 | ✅ Done |
| **AccountProfile BC — Application layer** (DTOs, ViewModels, `IUserProfileService` incl. address ops, validators, DI) | ADR-0005 | ✅ Done |
| **AccountProfile BC — Unit tests** (`UserProfileAggregateTests`, `UserProfileServiceTests`) | ADR-0005 | ✅ Done |
| **AccountProfile BC — DB migration** (`profile` schema, `UserProfiles` + `Addresses` tables) | ADR-0005 — requires migration approval | ⬜ Pending approval |
| AccountProfile BC — Integration tests | ADR-0005 | ⬜ Not started |
| Migrate `CustomerController` / `AddressController` / `ContactDetailController` (Web + API) → `IUserProfileService` | ADR-0005 | ⬜ Not started |
| Atomic switch — remove old Customer/Address/ContactDetail registrations | ADR-0005 | ⬜ After integration tests pass |
| **Shared `DomainException`** in `Domain.Shared` | ADR-0006 § Migration plan | ✅ Done |
| **Shared `Price` VO** in `Domain.Shared` (PLN-only, Catalog + Orders) | ADR-0006 § Migration plan | ✅ Done |
| **Shared `Money` VO** in `Domain.Shared` (transactional amount with rate) | ADR-0006 § Migration plan | ✅ Done |
| **Catalog BC — Domain layer** (`Product` aggregate, `Category`, `Tag`, `Image` owned entity, `ProductStatus` state machine, domain events `ProductCreated`/`ProductPublished`/`ProductUnpublished`, typed IDs `ProductId`/`CategoryId`/`TagId`/`ImageId`, `ProductTag` join entity) | ADR-0007 | ✅ Done |
| **Catalog BC — Value Objects** (`ProductName`, `ProductDescription`, `ProductQuantity`, `CategoryName`, `CategorySlug` max 100, `TagName`, `TagSlug` max 30, `ImageFileName`) | ADR-0007 | ✅ Done |
| **Catalog BC — Image invariant** (`SetAsMain`/`ClearMain` internal, `Product.SetMainImage`, `Product.ReorderImages`, auto sort-order on add) | ADR-0007 | ✅ Done |
| **Catalog BC — Infrastructure layer** (`ProductDbContext`, `catalog.*` schema, EF configs with explicit `HasMaxLength` on all string columns, `ProductRepository`, `CategoryRepository`, `ProductTagRepository`, DI) | ADR-0007 | ✅ Done |
| **Catalog BC — Application layer** (DTOs with FluentValidation, ViewModels with AutoMapper, `IProductService`/`ProductService`, `ICategoryService`/`CategoryService`, `IProductTagService`/`ProductTagService`, `CategoryName` in `ProductDetailsVm`, global VO converters in `MappingProfile`) | ADR-0007 | ✅ Done |
| **Catalog BC — Unit tests** (`ProductAggregateTests`, `ValueObjectTests`) | ADR-0007 | ✅ Done |
| **Catalog BC — DB migration** (`InitCatalogSchema`, `catalog.*` tables) | ADR-0007 — requires migration approval | ⬜ Migration generated, pending apply |
| Catalog BC — Regenerate migration after category hierarchy / tag color features | ADR-0007 | ⬜ Deferred |
| **Catalog BC: Category** — add `ParentId` + `IsVisible` (hierarchy / filtering) | ADR-0007 § 8 | ⬜ Separate ADR required |
| **Catalog BC: Tag** — add `Color` + `IsVisible` | ADR-0007 § 9 | ⬜ Deferred |
| Migrate `ItemController` / `ImageController` / `TagController` (Web + API) → new `IProductService` / `ICategoryService` / `IProductTagService` | ADR-0007 | ⬜ Not started |
| Atomic switch — flip Catalog to new BC, remove legacy `Domain.Model.Item`, `Image`, `Tag`, `Brand`, `Type` | ADR-0007 | ⬜ After integration tests pass |
| **Shared `Money` VO** in `Domain.Shared` (Amount + CurrencyCode + Rate, Payments) | ADR-0006 § Migration plan | ✅ Done |
| **Catalog/Products BC — Domain layer** (`Item` aggregate, `Category`, `Tag`, `Image`, `ItemTag`, typed IDs, VOs, domain events, repository interfaces) | ADR-0003/0004 | ✅ Done |
| **Catalog/Products BC — Infrastructure layer** (`ProductDbContext`, `catalog.*` schema, EF configurations, repositories, DI) | ADR-0003/0004 | ✅ Done |
| **Catalog/Products BC — Application layer** (DTOs, ViewModels, `IProductService`, `ICategoryService`, `IProductTagService`, `IImageUrlBuilder`, validators, DI) | ADR-0003/0004 | ✅ Done |
| **Catalog/Products BC — Unit tests** (`ItemAggregateTests`, `ValueObjectTests`) | ADR-0003/0004 | ✅ Done |
| **Catalog/Products BC — DB migration** (`catalog` schema, `Items` + `Categories` + `Tags` + `Images` + `ItemTags` tables) | Requires migration approval | ⬜ Pending approval |
| Catalog/Products BC — Integration tests | ADR-0003/0004 | ⬜ Not started |
| Migrate `ItemController` / `BrandController` / `TypeController` / `TagController` (Web + API) → new Catalog services | ADR-0003/0004 | ⬜ Not started |
| Atomic switch — remove old Item/Brand/Type/Tag registrations | ADR-0003/0004 | ⬜ After integration tests pass |
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
- [ADR-0005 — AccountProfile BC: UserProfile Aggregate Design](../adr/0005-accountprofile-bc-userprofile-aggregate-design.md)
- [ADR-0006 — Strongly-Typed IDs and Self-Validating Value Objects as Shared Domain Primitives](../adr/0006-typedid-and-value-objects-as-shared-domain-primitives.md)
- [`.github/instructions/dotnet-instructions.md`](../../.github/instructions/dotnet-instructions.md) § 16
