# Known Issues

> Confirmed bugs found during code review. Not yet fixed.
> Update this file when a fix is merged (move to "Resolved" or delete the entry).
> Each entry links to the ADR or roadmap step that tracks the fix.

---

## Critical

---

## High

---

## Medium

---

## Low

---

## Deferred Design Decisions

### [DD-003] Centralized Outbox schema forces cross-schema shared-connection transactions

- **Decision**: `MessagingDbContext` owns one centralized `Outbox`/`Inbox` table pair in its own
  schema, shared by every BC. Any BC write that must commit atomically with its outbox row
  (transactional outbox pattern) therefore has to bridge two different `DbContext` instances —
  done today via `CrossContextTransactionScope` sharing one physical ADO.NET connection/transaction,
  with the transaction passed explicitly as `IOutboxTransaction` down the call stack.
- **Why deferred, not fixed**: the architecturally clean fix — each BC owning its own local outbox
  table, written in the same ordinary same-schema transaction as its business data — removes the
  cross-schema sharing requirement entirely and is also the only shape that survives a real service
  extraction unchanged. It is a materially larger change (dispatcher polling N tables instead of
  one, per-BC migrations, per-schema EF configuration) that is not justified while every BC still
  lives in one physical database.
- **Revisit when**: extracting any BC into a separately deployable service becomes concrete,
  scheduled work — migrate that BC's outbox writes to a local table **before** starting the
  extraction. See `docs/adr/0032/0032-cross-schema-outbox-transaction-sharing.md` for full
  context, rejected alternatives (ambient scoped-DI transaction accessor, `TransactionScope`/MSDTC),
  and the exit trigger.

---

## Resolved

### [KI-014] Nested-transaction crash: BC service opens its own transaction when caller already has one open ✅

- **Root cause**: `StockService.ReserveAsync`/`ReleaseAsync`/`FulfillAsync` each unconditionally
  called `_unitOfWork.BeginTransactionAsync()` internally. `RefundApprovedHandler` already fixed
  this for `ReturnAsync`, but `OrderPlacedHandler` (→ `ReserveAsync`), `OrderShippedHandler`
  (→ `FulfillAsync`), and `ShipmentStockOutcomeHandlerBase` (→ `FulfillAsync`/`ReleaseAsync`, used
  by `ShipmentDeliveredHandler`/`ShipmentFailedHandler`/`ShipmentPartiallyDeliveredHandler`) all
  open their own transaction first and then call into a `StockService` method that tries to open a
  **second** transaction on the same `AvailabilityDbContext` — EF Core throws
  `InvalidOperationException: "The connection is already in a transaction..."` on the second
  `BeginTransactionAsync`. Not caught by existing unit tests because they mock `IStockService`/
  `IInventoryUnitOfWork`; only a real relational provider enforces transaction semantics.
- **Fix**: Added transaction-accepting overloads — `ReserveAsync(dto, transaction, ct)`,
  `ReleaseAsync(orderId, productId, quantity, transaction, ct)`,
  `FulfillAsync(orderId, productId, quantity, transaction, ct)` — mirroring the existing
  `ReturnAsync(productId, quantity, transaction, ct)` pattern (public transaction-owning overload +
  public transaction-accepting overload + private `PersistXxxAsync` helper shared by both). Updated
  `OrderPlacedHandler`, `OrderShippedHandler`, and `ShipmentStockOutcomeHandlerBase` to pass their
  already-open transaction through instead of letting the service open a second one.
- **Files changed**: `IStockService.cs`, `StockService.cs`, `OrderPlacedHandler.cs`,
  `OrderShippedHandler.cs`, `ShipmentStockOutcomeHandlerBase.cs`, plus matching mock setups/verifies
  in `OrderPlacedHandlerTests.cs`, `OrderShippedHandlerTests.cs`,
  `ShipmentDeliveredHandlerTests.cs`, `ShipmentFailedHandlerTests.cs`,
  `ShipmentPartiallyDeliveredHandlerTests.cs`.
- **Related**: `docs/adr/0032/0032-cross-schema-outbox-transaction-sharing.md` documents the
  separate (deferred, not a bug) decision behind *why* `IOutboxTransaction` is passed as an
  explicit parameter at all — see `[DD-003]` above.
- **Not touched**: `WithdrawHoldAsync` has no caller that already holds an open transaction (only
  `StockController`, a top-level request) — no transaction-accepting overload added, to avoid
  unrequested API surface. `OrderCancelledHandler`/`OrderPlacementFailedHandler`/
  `PaymentExpiredHandler` call `StockService` methods without opening their own transaction first —
  not affected by this bug class.

### [DD-001] `StockHold` — `Withdrawn` status and non-reversible state transitions ✅

- **Fix**: `Withdrawn = 4` added to `StockHoldStatus`. `StockChangeType.Withdrawn = 6` added for audit entries. `StockHold.Withdraw()` transitions from `Guaranteed`/`Confirmed` → `Withdrawn`; throws `DomainException` from terminal states. Guards added to `Confirm`, `MarkAsReleased`, `MarkAsFulfilled` blocking illegal reverse transitions. `CanWithdraw` flag added to `StockHoldRowVm`; "Wycofaj" button added to `Reservations.cshtml`. `IStockService.WithdrawHoldAsync` + `StockService` implementation releases reserved stock and writes audit entry. `StockController.Withdraw` POST action added.
- **Files changed**: `StockHoldStatus.cs`, `StockChangeType.cs`, `StockHold.cs`, `StockHoldsVm.cs`, `StockQueryService.cs`, `IStockService.cs`, `StockService.cs`, `StockController.cs`, `Reservations.cshtml`, `StockHoldAggregateTests.cs` (17 new tests).
- **Fix**: `ExceptionResponse` now carries `IReadOnlyList<ErrorCodeDto>? Codes`. `ErrorMapToResponse.Map()` maps `BusinessException._codes` → `ErrorCodeDto` list. `errors.js:showErrorFromResponse` handles structured `data.codes` array + flat `data.response` fallback. `_Layout.cshtml:setGlobalError` missing `return` fixed.
- **Files changed**: `ErrorCodeDto.cs` (new), `ExceptionResponse.cs`, `ErrorMapToResponse.cs`, `errors.js`, `_Layout.cshtml`

### [KI-006] `given_valid_order_should_update` — timezone comparison failure ✅

- **Fix**: Changed `Ordered = DateTime.Now` → `DateTime.UtcNow` and `ShouldBeLessThan(DateTime.Now)` → `DateTime.UtcNow`. UTC roundtrips cleanly through the API; ticks match on both sides.
- **Files changed**: `OrderControllerTests.cs`

### [KI-005] `buttonTemplate.js` — invalid HTML button `type` attribute ✅

- **Fix**: Changed `"type"` literal to `"button"` in `modalService.js` confirmation modal button creation.
- **Files changed**: `modalService.js`

### [KI-002] `ajaxRequest.js` image upload silently broken in `EditItem.cshtml` ✅

- **Fix**: `ajaxRequest.js:asyncAjax` now auto-detects `FormData` and sets `processData: false` + `contentType: false`. `EditItem.cshtml:SendAddImageRequest` passes `formData` directly (not `{ formData }`) and handles the jQuery-resolved value correctly — on success: reload; on failure: existing error handlers.
- **Files changed**: `ajaxRequest.js`, `EditItem.cshtml`

### [KI-003] `modalService.js` — `denyAction` fires on info modal close ✅

- **Fix**: Public `close()` now calls `closeModal()` directly instead of `closeButtonHandler()`. Cancel button in confirmation modal changed to call `closeButtonHandler` directly, preserving Promise resolution semantics.
- **Files changed**: `modalService.js`

### [KI-004] `validations.js` email regex — ReDoS risk ✅

- **Fix**: Replaced 4000-char RFC 2822 regex with safe `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`.
- **Files changed**: `validations.js`

### [KI-007] `ModuleClient.PublishAsync` — dispatches to only ONE handler per event type ✅

- **Fix**: Replaced `_serviceProvider.GetService(handlerType)` with `_serviceProvider.GetServices(handlerType)` and a `foreach` loop. All registered `IMessageHandler<T>` implementations now execute. Warning is logged only when no handlers are found.
- **Files changed**: `ModuleClient.cs`

### [KI-009] No guard against deleting catalog images referenced by order item snapshots ✅

- **Fix**: Soft-delete implemented. `Image.IsDeleted` flag added to domain entity. `ImageService.Delete` marks as deleted (no file removal). `GetAllImages`/`GetProductImages` filter `!IsDeleted`. `GetImageById` returns all rows including soft-deleted so snapshot URL resolution still works. `Image.FileName`/`FileSource`/`Provider` columns split. EF migration `20260408200548_AddImageFileSourceProviderAndSoftDelete` applied.
- **Files changed**: `Image.cs`, `ImageService.cs`, `ImageRepository.cs`, migration.

### [DD-002] No quantity upper limit on cart/order — Web gap ✅

- **Fix**: `AddToCartDtoValidator` created in `Application.Presale.Checkout.Validators`. Web limit: `Quantity.LessThanOrEqualTo(CheckoutOptions.MaxWebQuantityPerOrderLine)`. API limit handled separately via `MaxApiQuantityFilter` (ADR-0025).
- **Files changed**: `AddToCartDtoValidator.cs`

### [KI-010] `/Sales/Payments/Create/{orderId}` returns 404 after PlaceOrder ✅

- **Root cause**: `MessagingOptions.UseBackgroundDispatcher: true` caused the `OrderPlaced` message to be dispatched to a background channel. By the time the user clicked "Zapłać" on the Summary page, `Sales.Payments.OrderPlacedHandler` (which creates the `Payment` row) had not yet run → `GetPendingByOrderIdAsync` returned `null` → 404.
- **Fix**: Set `UseBackgroundDispatcher: false` in `ECommerceApp.Web/appsettings.json`. Handlers now run synchronously within the request scope, so the payment row exists before the redirect to Summary.
- **Files changed**: `appsettings.json` (Web)

### [KI-011] Cart cleared on checkout initiation (before order is placed) ✅

- **Root cause**: `CheckoutService.InitiateAsync` called `_cartService.RemoveRangeAsync` immediately after soft-reserving items (during the `PlaceOrder` GET). If the user navigated away or `PlaceOrderAsync` failed, the cart was permanently empty with no order placed. Cart cleanup was also duplicated — `Presale.Checkout.OrderPlacedHandler` already handles it correctly on `OrderPlaced`.
- **Fix**: Removed the `RemoveRangeAsync` call from `InitiateAsync`. Cart lines are now only cleared by `Presale.Checkout.OrderPlacedHandler` after a successful `OrderPlaced` event.
- **Files changed**: `CheckoutService.cs`

### [KI-012] `GetUsageSummariesAsync` — EF.Property failure on owned `TagName` ✅

- **Fix**: `GetUsageSummariesAsync` was using `EF.Property<string>(t, "Name")` which fails because `Name` is an owned entity (`OwnsOne(TagName)`), not a scalar shadow property. Changed to `OrderBy(t => t.Name.Value)` to match the pattern used by all other queries in the same repository.
- **Files changed**: `ProductTagRepository.cs`

### [KI-013] `/Inventory/Stock` — EF translation failure on `Contains(p.ProductId.Value)` ✅

- **Fix**: `GetByProductIdsAsync` used `productIds.Contains(p.ProductId.Value)` which EF cannot translate because it cannot decompose the value-object accessor inside a `Contains` call. Changed to `productIds.Contains(EF.Property<int>(p, "ProductId"))` to read the mapped column directly.
- **Files changed**: `PendingStockAdjustmentRepository.cs`

### [KI-008] Unit test assertion library migration — FluentAssertions → AwesomeAssertions ✅

- **Fix**: `AwesomeAssertions` v8.3.0 installed in `ECommerceApp.UnitTests.csproj`. All unit test files use `using AwesomeAssertions;` — zero `using FluentAssertions;` remaining. Integration tests correctly use `Shouldly` (by design). Migration executed ahead of .NET 8+ upgrade.
- **Files changed**: `ECommerceApp.UnitTests.csproj`, all `*.cs` test files in `ECommerceApp.UnitTests`.

---

_Last reviewed: 2026-08-26_
