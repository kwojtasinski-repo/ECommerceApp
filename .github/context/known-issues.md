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

### [KI-008] FluentAssertions → AwesomeAssertions migration required for .NET 8+
- **Severity**: 🔵 Low (deferred — triggered only on .NET 8+ upgrade)
- **Location**: All test projects (`ECommerceApp.Tests`, `ECommerceApp.IntegrationTests`)
- **Symptom**: FluentAssertions v6.x uses Apache 2.0 license but v7+ switched to a commercial license. v6 targets .NET 6/7 and will not receive updates for .NET 9+.
- **Root cause**: License change in FluentAssertions v7. Staying on v6 means no .NET 9+ support; upgrading to v7 requires a commercial license.
- **Fix**: Replace NuGet package `FluentAssertions` → `AwesomeAssertions` (community fork, Apache 2.0, API-compatible with v6). Replace `using FluentAssertions;` → `using AwesomeAssertions;` across all test files. No assertion syntax changes needed.
- **Condition**: Do NOT execute this migration until the project targets .NET 8 or later. On .NET 7 the current FluentAssertions v6 works fine.
- **Fix tracked in**: .NET 8+ upgrade roadmap (when created).

---

## Deferred Design Decisions

---

## Resolved

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

---

*Last reviewed: 2026-04-09*
