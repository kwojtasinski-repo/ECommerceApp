# Repository Index

> **Machine-readable, human-friendly map of the entire codebase.**
> Use this file to quickly locate code areas. Do NOT load everything at once —
> read only the sections relevant to your current task (lazy loading).
>
> For project status and blockers → see [`project-state.md`](./project-state.md)
> For confirmed bugs → see [`known-issues.md`](./known-issues.md)
> For documentation lookup → see [`.github/instructions/docs-index.instructions.md`](../instructions/docs-index.instructions.md)

_Last updated: 2026-06-05_

---

## At a Glance

| Metric                    | Value                          |
| ------------------------- | ------------------------------ |
| **Total C# source files** | ~1,143 (excl. bin/obj)         |
| **Total lines of C#**     | ~44,500 (excl. migrations)     |
| **Razor views (.cshtml)** | 176                            |
| **JavaScript modules**    | 11                             |
| **Identity Razor Pages**  | 14                             |
| **ADRs**                  | 25                             |
| **DbContexts**            | 12 (1 legacy + 11 per-BC)      |
| **DB migration folders**  | 12                             |
| **HTTP scenario files**   | 9                              |
| **Test files**            | 149 (97 unit + 52 integration) |

---

## Solution Projects

| Project                         | Files                | Lines  | Role                                                 |
| ------------------------------- | -------------------- | ------ | ---------------------------------------------------- |
| `ECommerceApp.Domain`           | 191                  | 3,568  | Domain models, aggregates, interfaces, value objects |
| `ECommerceApp.Application`      | 376                  | 12,721 | Services, DTOs, VMs, handlers, messaging, middleware |
| `ECommerceApp.Infrastructure`   | 245                  | 6,857  | EF Core, repositories, DbContexts, external clients  |
| `ECommerceApp.Web`              | 44 .cs + 130 .cshtml | 3,830  | MVC controllers, views, Identity pages, frontend     |
| `ECommerceApp.API`              | 29                   | 1,889  | REST controllers, JWT auth                           |
| `ECommerceApp.UnitTests`        | 96                   | 10,844 | xUnit unit tests                                     |
| `ECommerceApp.IntegrationTests` | 65                   | 4,837  | xUnit integration tests                              |

---

## Bounded Context Map — Code Locations

Each BC has code in up to 4 layers. "Legacy" means old flat code that coexists and will be removed during atomic switch.

### Core / Catalog

| Layer           | Path                                                                                                                                 | Key files                                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ |
| Domain          | `Domain/Catalog/Products/`                                                                                                           | `Product.cs`, `Category.cs`, `Tag.cs`, `Image.cs`, `ProductTag.cs` + IDs, VOs, Events (27 files) |
| Application     | `Application/Catalog/Products/`                                                                                                      | DTOs, Messages, Services, ViewModels                                                             |
| Infrastructure  | `Infrastructure/Catalog/Products/`                                                                                                   | `CatalogDbContext.cs`, Configurations, Repositories, Migrations                                  |
| Legacy domain   | `Domain/Model/Item.cs`, `Tag.cs`, `Type.cs`, `Brand.cs`, `Image.cs`, `ItemTag.cs`                                                    | ← do not extend                                                                                  |
| Legacy services | `Application/Services/Items/` (10 files)                                                                                             | `ItemService`, `ImageService`, `ItemHandler`, etc.                                               |
| Legacy repos    | `Infrastructure/Repositories/ItemRepository.cs`, `ImageRepository.cs`, `TagRepository.cs`, `TypeRepository.cs`, `BrandRepository.cs` |                                                                                                  |
| Web (V2)        | `Web/Controllers/V2ProductController.cs`, `V2CategoryController.cs`, `V2TagController.cs`                                            |                                                                                                  |
| Web (legacy)    | `Web/Controllers/ItemController.cs`, `ImageController.cs`, `TagController.cs`, `TypeController.cs`, `BrandController.cs`             |                                                                                                  |
| API (V2)        | `API/Controllers/V2/CatalogController.cs`                                                                                            |                                                                                                  |
| API (legacy)    | `API/Controllers/ItemController.cs`, `ImageController.cs`, `TagController.cs`, `TypeController.cs`, `BrandController.cs`             |                                                                                                  |
| Views (V2)      | `Web/Views/V2Product/` (4), `V2Category/` (3), `V2Tag/` (3)                                                                          |                                                                                                  |
| Views (legacy)  | `Web/Views/Item/` (6), `Brand/` (4), `Tag/` (4), `Type/` (4)                                                                         |                                                                                                  |
| ADR             | `docs/adr/0007`                                                                                                                      |                                                                                                  |

### Sales / Orders

| Layer           | Path                                                                       | Key files                                                                                     |
| --------------- | -------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Domain          | `Domain/Sales/Orders/`                                                     | `Order.cs`, `OrderItem.cs`, `OrderCustomer.cs`, `OrderEvent.cs` + IDs, VOs, Events (21 files) |
| Application     | `Application/Sales/Orders/`                                                | Contracts, DTOs, Handlers, Messages, Results, Services, ViewModels                            |
| Infrastructure  | `Infrastructure/Sales/Orders/`                                             | `OrdersDbContext.cs`, Adapters, Configurations, Repositories, Migrations                      |
| Legacy domain   | `Domain/Model/Order.cs`, `OrderItem.cs`                                    |                                                                                               |
| Legacy services | `Application/Services/Orders/` (4 files)                                   | `OrderService`, `OrderItemService`                                                            |
| Legacy repos    | `Infrastructure/Repositories/OrderRepository.cs`, `OrderItemRepository.cs` |                                                                                               |
| Web (legacy)    | `Web/Controllers/OrderController.cs`, `OrderItemController.cs`             |                                                                                               |
| API (V2)        | `API/Controllers/V2/OrdersController.cs`                                   |                                                                                               |
| API (legacy)    | `API/Controllers/OrderController.cs`, `OrderItemController.cs`             |                                                                                               |
| Views (legacy)  | `Web/Views/Order/` (15), `OrderItem/` (3)                                  |                                                                                               |
| ADR             | `docs/adr/0014`                                                            |                                                                                               |

### Sales / Payments

| Layer           | Path                                               | Key files                                                        |
| --------------- | -------------------------------------------------- | ---------------------------------------------------------------- |
| Domain          | `Domain/Sales/Payments/`                           | `Payment.cs`, `PaymentStatus.cs`, Events (9 files)               |
| Application     | `Application/Sales/Payments/`                      | DTOs, Handlers, Messages, Results, Services, ViewModels          |
| Infrastructure  | `Infrastructure/Sales/Payments/`                   | `PaymentsDbContext.cs`, Configurations, Repositories, Migrations |
| Legacy domain   | `Domain/Model/Payment.cs`, `PaymentState.cs`       |                                                                  |
| Legacy services | `Application/Services/Payments/` (4 files)         | `PaymentService`, `PaymentHandler`                               |
| Legacy repos    | `Infrastructure/Repositories/PaymentRepository.cs` |                                                                  |
| Web (legacy)    | `Web/Controllers/PaymentController.cs`             |                                                                  |
| API (V2)        | `API/Controllers/V2/PaymentsController.cs`         |                                                                  |
| API (legacy)    | `API/Controllers/PaymentController.cs`             |                                                                  |
| Views (legacy)  | `Web/Views/Payment/` (5)                           |                                                                  |
| ADR             | `docs/adr/0015`                                    |                                                                  |

### Sales / Coupons

| Layer           | Path                                                                                                    | Key files                                                                  |
| --------------- | ------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Domain          | `Domain/Sales/Coupons/`                                                                                 | `Coupon.cs`, `CouponUsed.cs`, `CouponStatus.cs` (7 files)                  |
| Application     | `Application/Sales/Coupons/`                                                                            | Contracts, Handlers, Messages, Results, Services                           |
| Infrastructure  | `Infrastructure/Sales/Coupons/`                                                                         | `CouponsDbContext.cs`, Adapters, Configurations, Repositories, Migrations  |
| Legacy domain   | `Domain/Model/Coupon.cs`, `CouponType.cs`, `CouponUsed.cs`                                              |                                                                            |
| Legacy services | `Application/Services/Coupons/` (9 files)                                                               | `CouponService`, `CouponTypeService`, `CouponUsedService`, `CouponHandler` |
| Legacy repos    | `Infrastructure/Repositories/CouponRepository.cs`, `CouponTypeRepository.cs`, `CouponUsedRepository.cs` |                                                                            |
| Web (legacy)    | `Web/Controllers/CouponController.cs`, `CouponTypeController.cs`, `CouponUsedController.cs`             |                                                                            |
| API (legacy)    | `API/Controllers/CouponController.cs`                                                                   |                                                                            |
| Views (legacy)  | `Web/Views/Coupon/` (4), `CouponType/` (4), `CouponUsed/` (4)                                           |                                                                            |
| ADR             | `docs/adr/0016`                                                                                         |                                                                            |

### Sales / Fulfillment

| Layer           | Path                                              | Key files                                                 |
| --------------- | ------------------------------------------------- | --------------------------------------------------------- |
| Domain          | `Domain/Sales/Fulfillment/`                       | `Refund.cs`, `RefundItem.cs`, `RefundStatus.cs` (5 files) |
| Application     | `Application/Sales/Fulfillment/`                  | Contracts, DTOs, Messages, Results, Services, ViewModels  |
| Infrastructure  | `Infrastructure/Sales/Fulfillment/`               | `FulfillmentDbContext.cs` + standard BC structure         |
| Legacy domain   | `Domain/Model/Refund.cs`                          |                                                           |
| Legacy services | `Application/Services/Refunds/` (2 files)         | `RefundService`                                           |
| Legacy repos    | `Infrastructure/Repositories/RefundRepository.cs` |                                                           |
| Web (legacy)    | `Web/Controllers/RefundController.cs`             |                                                           |
| API (V2)        | `API/Controllers/V2/RefundsController.cs`         |                                                           |
| API (legacy)    | `API/Controllers/RefundController.cs`             |                                                           |
| Views (legacy)  | `Web/Views/Refund/` (3)                           |                                                           |
| ADR             | `docs/adr/0017`                                   |                                                           |

### Inventory / Availability

| Layer          | Path                                        | Key files                                                                                              |
| -------------- | ------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Domain         | `Domain/Inventory/Availability/`            | `StockItem.cs`, `Reservation.cs`, `ProductSnapshot.cs`, `PendingStockAdjustment.cs`, Events (21 files) |
| Application    | `Application/Inventory/Availability/`       | DTOs, Handlers, `InventoryOptions.cs`, Messages, Services, ViewModels                                  |
| Infrastructure | `Infrastructure/Inventory/Availability/`    | `AvailabilityDbContext.cs`, Configurations, Repositories, Migrations                                   |
| Web (none yet) | —                                           |                                                                                                        |
| API (V2)       | `API/Controllers/V2/InventoryController.cs` |                                                                                                        |
| ADR            | `docs/adr/0011`                             |                                                                                                        |

### Presale / Checkout

| Layer          | Path                                                            | Key files                                                                     |
| -------------- | --------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| Domain         | `Domain/Presale/Checkout/`                                      | `CartLine.cs`, `SoftReservation.cs`, `StockSnapshot.cs` (10 files)            |
| Application    | `Application/Presale/Checkout/`                                 | Contracts, DTOs, Handlers, `PresaleOptions.cs`, Results, Services, ViewModels |
| Application VM | `Application/Presale/Checkout/ViewModels/StorefrontProductVm.cs` | `StorefrontProductVm` — includes `MainImageUrl?` (main image URL, nullable)  |
| Infrastructure | `Infrastructure/Presale/Checkout/`                              | `PresaleDbContext.cs`, Adapters, Configurations, Repositories, Migrations     |
| API (V2)       | `API/Controllers/V2/CartController.cs`, `CheckoutController.cs` |                                                                               |
| API (legacy)   | `API/Controllers/Presale/StorefrontController.cs`               |                                                                               |
| ADR            | `docs/adr/0012`                                                 |                                                                               |

### Supporting / Currencies

| Layer           | Path                                                                             | Key files                                                        |
| --------------- | -------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Domain          | `Domain/Supporting/Currencies/`                                                  | `Currency.cs`, `CurrencyRate.cs`, ValueObjects (8 files)         |
| Application     | `Application/Supporting/Currencies/`                                             | DTOs, Services, ViewModels                                       |
| Infrastructure  | `Infrastructure/Supporting/Currencies/`                                          | `CurrencyDbContext.cs`, Configurations, Repositories, Migrations |
| Legacy domain   | `Domain/Model/Currency.cs`, `CurrencyRate.cs`                                    |                                                                  |
| Legacy services | `Application/Services/Currencies/` (4 files)                                     | `CurrencyService`, `CurrencyRateService`                         |
| Legacy repos    | `Infrastructure/Repositories/CurrencyRepository.cs`, `CurrencyRateRepository.cs` |                                                                  |
| Web (V2)        | `Web/Controllers/V2CurrencyController.cs`                                        |                                                                  |
| Web (legacy)    | `Web/Controllers/CurrencyController.cs`                                          |                                                                  |
| API (V2)        | `API/Controllers/V2/CurrenciesController.cs`                                     |                                                                  |
| Views (V2)      | `Web/Views/V2Currency/` (5)                                                      |                                                                  |
| Views (legacy)  | `Web/Views/Currency/` (4)                                                        |                                                                  |
| ADR             | `docs/adr/0008`                                                                  |                                                                  |

### Supporting / TimeManagement

| Layer          | Path                                                               | Key files                                                                                                                                                               |
| -------------- | ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Domain         | `Domain/Supporting/TimeManagement/`                                | `ScheduledJob.cs`, `JobExecution.cs`, `DeferredJobInstance.cs`, Events (17 files)                                                                                       |
| Application    | `Application/Supporting/TimeManagement/`                           | Handlers, Models, Services, scheduler interfaces                                                                                                                        |
| Infrastructure | `Infrastructure/Supporting/TimeManagement/`                        | `TimeManagementDbContext.cs`, `CronSchedulerService.cs`, `DeferredJobPollerService.cs`, `DeferredJobScheduler.cs`, poller/dispatcher services, Repositories, Migrations |
| Web (Area) ✅  | `Web/Areas/Jobs/Controllers/JobManagementController.cs`            | 2 views: Index, History                                                                                                                                                 |
| Web (V2)       | `Web/Controllers/V2JobController.cs`                               |                                                                                                                                                                         |
| Views (V2)     | `Web/Views/V2Job/` (5)                                             |                                                                                                                                                                         |
| ADR            | `docs/adr/0009`                                                    |                                                                                                                                                                         |

### AccountProfile

| Layer           | Path                                                                                                                                        | Key files                                                                        |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Domain          | `Domain/AccountProfile/`                                                                                                                    | `UserProfile.cs`, `Address.cs`, `UserProfileCreated.cs`, ValueObjects (18 files) |
| Application     | `Application/AccountProfile/`                                                                                                               | DTOs, Services, ViewModels                                                       |
| Infrastructure  | `Infrastructure/AccountProfile/`                                                                                                            | `UserProfileDbContext.cs`, Configurations, Repositories, Migrations              |
| Legacy domain   | `Domain/Model/Customer.cs`, `Address.cs`, `ContactDetail.cs`, `ContactDetailType.cs`                                                        |                                                                                  |
| Legacy services | `Application/Services/Customers/` (2), `Addresses/` (2), `ContactDetails/` (4)                                                              |                                                                                  |
| Legacy repos    | `Infrastructure/Repositories/CustomerRepository.cs`, `AddressRepository.cs`, `ContactDetailRepository.cs`, `ContactDetailTypeRepository.cs` |                                                                                  |
| Web (V2)        | `Web/Controllers/V2ProfileController.cs`                                                                                                    |                                                                                  |
| Web (legacy)    | `Web/Controllers/CustomerController.cs`, `AddressController.cs`, `ContactDetailController.cs`                                               |                                                                                  |
| API (V2)        | `API/Controllers/V2/AccountProfileController.cs`                                                                                            |                                                                                  |
| API (legacy)    | `API/Controllers/CustomerController.cs`, `AddressController.cs`, `ContactDetailController.cs`, `ContactDetailTypeController.cs`             |                                                                                  |
| Views (V2)      | `Web/Views/V2Profile/` (4)                                                                                                                  |                                                                                  |
| Views (legacy)  | `Web/Views/Customer/` (6), `Address/` (3), `ContactDetail/` (3)                                                                             |                                                                                  |
| ADR             | `docs/adr/0005`                                                                                                                             |                                                                                  |

### Identity / IAM

| Layer          | Path                                          | Key files                                     |
| -------------- | --------------------------------------------- | --------------------------------------------- |
| Domain         | `Domain/Identity/IAM/`                        | `ApplicationUser.cs`, `RefreshToken.cs`, `IRefreshTokenRepository.cs` |
| Application    | `Application/Identity/IAM/`                   | DTOs, Services, ViewModels                    |
| Infrastructure | `Infrastructure/Identity/IAM/`                | `IamDbContext.cs`, Auth logic, `RefreshTokenRepository.cs`, Migrations (2) |
| Web (Area) ✅  | `Web/Areas/IAM/Controllers/UserManagementController.cs` | 5 views: Index, AddUser, EditUser, ChangeUserPassword, AddRolesToUser |
| Web (V2)       | `Web/Controllers/V2UserController.cs`         | 4 views: `Web/Views/V2User/` (Index, Add, Edit, Details) |
| Web (legacy)   | `Web/Controllers/UserManagementController.cs` | — legacy, do not extend |
| Web Areas      | `Web/Areas/Identity/Pages/Account/`           | Login, Register, ForgotPassword, Manage pages |
| API (legacy)   | `API/Controllers/LoginController.cs`          | JWT sign-in — pending swap to new `IAuthenticationService` |
| ADR            | `docs/adr/0019`                               |                                               |

---

## Cross-Cutting Concerns

### Shared Domain Primitives

**Path**: `Domain/Shared/`  
**Files**: `TypedId.cs`, `Money.cs`, `Price.cs`, `Quantity.cs`, `UnitCost.cs`, `DomainException.cs`  
**ADR**: `docs/adr/0006`  
**Instructions**: `.github/instructions/shared-primitives.instructions.md`

### Messaging (In-Memory Broker)

**Path**: `Application/Messaging/` + `Infrastructure/Messaging/`  
**Key files**: `IMessageBroker.cs`, `IModuleClient.cs`, `InMemoryMessageBroker.cs`, `ModuleClient.cs`, `AsyncMessageDispatcher.cs`  
**ADR**: `docs/adr/0010`

### Exception Pipeline

**Path**: `Application/Exceptions/` + `Application/Middlewares/`  
**Key files**: `BusinessException.cs`, `ErrorMapToResponse.cs`, `ExceptionMiddleware.cs`  
**Known bug**: KI-001 (error codes silently discarded)

### File Management

**Path**: `Application/FileManager/`  
**Key files**: `FileStore.cs`, `DirectoryWrapper.cs`, `FileWrapper.cs`, `RelativeImageUrlBuilder.cs`

### AutoMapper Profiles

**Path**: `Application/Mapping/`  
**Key files**: `MappingProfile.cs`, `IMapFrom.cs`

### External Integrations

**Path**: `Application/External/`  
**Purpose**: NBP (Polish National Bank) currency API client

### Permissions

**Path**: `Application/Permissions/UserPermissions.cs`

---

## Legacy Infrastructure (shared DbContext)

The original monolithic DbContext and its configuration files. All new BCs have their own per-BC DbContext.

| Component           | Path                                                                |
| ------------------- | ------------------------------------------------------------------- |
| Legacy `Context.cs` | `Infrastructure/Database/Context.cs`                                |
| EF Configurations   | `Infrastructure/Database/Configurations/` (19 entity configs)       |
| Seed data           | `Infrastructure/Database/SeedData/`                                 |
| DB initializer      | `Infrastructure/Database/DbInitializer.cs`, `DatabaseInitalizer.cs` |
| Migrator            | `Infrastructure/Database/DbContextMigrator.cs`                      |
| Legacy migrations   | `Infrastructure/Migrations/` (25 migration files)                   |
| Legacy repositories | `Infrastructure/Repositories/` (20 repository files)                |

---

## Per-BC DbContexts (new architecture)

| BC             | DbContext                 | Schema constants          | Migration folder                                           |
| -------------- | ------------------------- | ------------------------- | ---------------------------------------------------------- |
| AccountProfile | `UserProfileDbContext`    | `UserProfileConstants`    | `Infrastructure/AccountProfile/Migrations/` (7)            |
| Catalog        | `CatalogDbContext`        | `CatalogConstants`        | `Infrastructure/Catalog/Products/Migrations/` (9)          |
| Identity/IAM   | `IamDbContext`            | —                         | `Infrastructure/Identity/IAM/Migrations/` (3)              |
| Inventory      | `AvailabilityDbContext`   | `AvailabilityConstants`   | `Infrastructure/Inventory/Availability/Migrations/` (3)    |
| Presale        | `PresaleDbContext`        | `PresaleConstants`        | `Infrastructure/Presale/Checkout/Migrations/` (5)          |
| Coupons        | `CouponsDbContext`        | `CouponsConstants`        | `Infrastructure/Sales/Coupons/Migrations/` (3)             |
| Fulfillment    | `FulfillmentDbContext`    | —                         | `Infrastructure/Sales/Fulfillment/Migrations/` (3)         |
| Orders         | `OrdersDbContext`         | `OrdersConstants`         | `Infrastructure/Sales/Orders/Migrations/` (3)              |
| Payments       | `PaymentsDbContext`       | `PaymentsConstants`       | `Infrastructure/Sales/Payments/Migrations/` (3)            |
| Currencies     | `CurrencyDbContext`       | `CurrencyConstants`       | `Infrastructure/Supporting/Currencies/Migrations/` (7)     |
| TimeManagement | `TimeManagementDbContext` | `TimeManagementConstants` | `Infrastructure/Supporting/TimeManagement/Migrations/` (3) |

---

## DI Registration Graph

Entry points in `Startup.cs` / `Program.cs`:

```
services.AddApplication()       → Application/DependencyInjection.cs
services.AddInfrastructure(cfg)  → Infrastructure/DependencyInjection.cs
```

### Application DI chain

```
AddApplication()
 ├─ AddAutoMapper()
 ├─ AddFilesStore()            → FileManager
 ├─ AddErrorHandling()         → ExceptionMiddleware + ErrorMapToResponse
 ├─ AddNbpClient()             → External/Client
 ├─ AddServices()              → 21 legacy services (Items, Orders, Payments, Coupons, etc.)
 ├─ AddIamServices()           → Identity/IAM services
 ├─ AddUserProfileServices()   → AccountProfile services
 ├─ AddCatalogServices()       → Catalog/Products services
 ├─ AddCurrencyServices()      → Supporting/Currencies services
 ├─ AddTimeManagementServices()→ Supporting/TimeManagement services
 ├─ AddMessagingServices()     → IMessageBroker, IModuleClient
 ├─ AddAvailabilityServices()  → Inventory/Availability services
 ├─ AddPresaleServices()       → Presale/Checkout services
 ├─ AddOrderServices()         → Sales/Orders services
 ├─ AddPaymentServices()       → Sales/Payments services
 ├─ AddCouponServices()        → Sales/Coupons services
 ├─ AddFulfillmentServices()   → Sales/Fulfillment services
 └─ FluentValidation           → auto-discover validators
```

### Infrastructure DI chain

```
AddInfrastructure(cfg)
 ├─ AddDatabase(cfg)              → Legacy Context.cs + DbInitializer
 ├─ AddRepositories()             → 20 legacy repositories
 ├─ AddIamInfrastructure(cfg)     → IamDbContext + auth
 ├─ AddUserProfileInfrastructure()→ UserProfileDbContext + repos
 ├─ AddCatalogInfrastructure()    → CatalogDbContext + repos
 ├─ AddCurrencyInfrastructure()   → CurrencyDbContext + repos
 ├─ AddMessagingInfrastructure()  → InMemoryMessageBroker
 ├─ AddTimeManagementInfrastructure() → TimeManagementDbContext + schedulers
 ├─ AddAvailabilityInfrastructure()   → AvailabilityDbContext + repos
 ├─ AddPresaleInfrastructure()    → PresaleDbContext + repos
 ├─ AddOrdersInfrastructure()     → OrdersDbContext + repos
 ├─ AddPaymentsInfrastructure()   → PaymentsDbContext + repos
 ├─ AddCouponsInfrastructure()    → CouponsDbContext + repos
 └─ AddFulfillmentInfrastructure()→ FulfillmentDbContext + repos
```

---

## Web MVC Layer

### V2 Controllers (new BC code)

`V2CategoryController`, `V2CurrencyController`, `V2JobController`, `V2ProductController`, `V2ProfileController`, `V2TagController`, `V2UserController`

### Legacy Controllers

`AddressController`, `BrandController`, `ContactDetailController`, `CouponController`, `CouponTypeController`, `CouponUsedController`, `CurrencyController`, `CustomerController`, `HomeController`, `ImageController`, `ItemController`, `JobManagementController`, `OrderController`, `OrderItemController`, `PaymentController`, `RefundController`, `TagController`, `TypeController`, `UserManagementController`

### Views (130 total)

| Folder                                                                          | Count | Status               |
| ------------------------------------------------------------------------------- | ----- | -------------------- |
| V2Product, V2Category, V2Tag, V2Currency, V2Job, V2Profile, V2User              | 28    | New BC views         |
| Order (15), Customer (6), Item (6), Payment (5), Shared (5), UserManagement (5) | 42    | Legacy — largest     |
| Remaining legacy (Brand, Tag, Type, Address, Coupon\*, etc.)                    | 55    | Legacy               |
| Home                                                                            | 2     | Shared               |
| Migration, NewBcTest                                                            | 0     | Empty/test scaffolds |

### Identity Area

`Web/Areas/Identity/Pages/Account/` — Login, Register, ForgotPassword, ConfirmEmail, Manage (ChangePassword, Email, Index)

### Frontend JS (`wwwroot/js/`)

10 AMD modules loaded via require.js:
`config.js`, `common.js`, `site.js`, `errors.js`, `forms.js`, `validations.js`, `ajaxRequest.js`, `buttonTemplate.js`, `dialogTemplate.js`, `modalService.js`

**Known bugs**: KI-001 (errors.js), KI-002 (ajaxRequest.js), KI-003 (modalService.js), KI-004 (validations.js), KI-005 (buttonTemplate.js)

### Client libs (`wwwroot/lib/`)

Managed via `libman.json`: bootstrap, bootstrap-select, jQuery, jQuery-validation, fontawesome, require.js, globalize, cldr, he.js

---

## API Layer

### V2 Controllers (new BC code)

`AccountProfileController`, `CartController`, `CatalogController`, `CheckoutController`, `CurrenciesController`, `InventoryController`, `JobsController`, `OrdersController`, `PaymentsController`, `RefundsController`

### Legacy Controllers

`AddressController`, `BrandController`, `ContactDetailController`, `ContactDetailTypeController`, `CouponController`, `CustomerController`, `ImageController`, `ItemController`, `LoginController`, `OrderController`, `OrderItemController`, `PaymentController`, `RefundController`, `TagController`, `TypeController`

### HTTP Scenarios (`API/HttpScenarios/`)

`.http` files for manual V2 API testing: `account-profile-v2.http`, `catalog-v2.http`, `currencies-v2.http`, `inventory-v2.http`, `jobs-v2.http`, `payments-v2.http`, `presale-v2.http`, `refunds-v2.http`, `sales-orders-v2.http`

---

## Test Projects

### Unit Tests (`ECommerceApp.UnitTests/` — 96 files, 10,844 lines)

| Folder                       | Coverage area                                                                               |
| ---------------------------- | ------------------------------------------------------------------------------------------- |
| `AccountProfile/`            | UserProfile aggregate, services                                                             |
| `Catalog/Products/`          | Product, Category, Tag aggregates                                                           |
| `Sales/Orders/`              | Order aggregate, services                                                                   |
| `Sales/Payments/`            | Payment aggregate, services                                                                 |
| `Sales/Coupons/`             | Coupon aggregate                                                                            |
| `Sales/Fulfillment/`         | Refund aggregate                                                                            |
| `Inventory/Availability/`    | StockItem, Reservation                                                                      |
| `Presale/Checkout/`          | CartLine, SoftReservation, Checkout                                                         |
| `Supporting/Currencies/`     | Currency, CurrencyRate                                                                      |
| `Supporting/TimeManagement/` | ScheduledJob, JobExecution                                                                  |
| `Identity/`                  | IAM services                                                                                |
| `Customer/`                  | Legacy CustomerProfile                                                                      |
| `Services/`                  | 18 legacy service test files (Address, Brand, Coupon, Currency, Item, Order, Payment, etc.) |
| `Common/`                    | Shared test helpers                                                                         |

### Integration Tests (`ECommerceApp.IntegrationTests/` — 65 files, 4,837 lines)

| Folder          | Coverage area                              |
| --------------- | ------------------------------------------ |
| `API/`          | 15 legacy API controller integration tests |
| `Services/`     | 21 legacy service integration tests        |
| `Sales/Orders/` | New Orders BC integration tests            |
| `Common/`       | Shared test infrastructure                 |
| `TestData/`     | Test data fixtures                         |

---

## Configuration & DevOps

| File                        | Purpose                                  |
| --------------------------- | ---------------------------------------- |
| `docker-compose.yaml`       | Multi-container setup (Web + API + DB)   |
| `Dockerfile-web`            | Web project Docker build                 |
| `Dockerfile-api`            | API project Docker build                 |
| `Web/appsettings.json`      | Web app configuration                    |
| `API/appsettings.json`      | API app configuration                    |
| `*.appsettings.docker.json` | Docker-specific overrides                |
| `Web/libman.json`           | Client-side library manifest             |
| `ECommerceApp.sln`          | Solution with organized solution folders |

---

## Empty / Placeholder Folders

| Path                               | Notes                                                      |
| ---------------------------------- | ---------------------------------------------------------- |
| `Domain/Customer/CustomerProfile/` | Empty — legacy `Customer` model is in `Domain/Model/`      |
| `Domain/Profiles/AccountProfile/`  | Empty — real AccountProfile is in `Domain/AccountProfile/` |
| `.github/workflows/`               | Empty — no CI/CD pipelines yet                             |
| `.github/upgrades/prompts/`        | Empty                                                      |
| `Web/Views/Migration/`             | Empty scaffold                                             |
| `Web/Views/NewBcTest/`             | Empty scaffold                                             |
