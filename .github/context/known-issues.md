# Known Issues

> Confirmed bugs found during code review. Not yet fixed.
> Update this file when a fix is merged (move to "Resolved" or delete the entry).
> Each entry links to the ADR or roadmap step that tracks the fix.

---

## Critical

---

## High

### [KI-009] No guard against deleting catalog images referenced by order item snapshots
- **Severity**: 🔴 High (silent data loss)
- **Location**: `ECommerceApp.Application/Catalog/Images/Services/ImageService.cs` — `Delete(int id)`
- **Symptom**: Deleting a catalog image that is referenced by one or more `OrderItemSnapshot.ImageId` rows causes those snapshots to silently lose their image. The `GET /catalog/images/{id}` endpoint returns `404` for any affected order item detail or order detail view.
- **Root cause**: `ImageService.Delete` has no cross-BC check. `OrderItemSnapshots` (Sales BC) stores `ImageId int?` referencing `Images` (Catalog BC). There is no FK constraint between schemas, and no application-level guard.
- **Impact**: All historical order items whose product image has been deleted will display with no thumbnail. The data loss is silent — no error, no warning.
- **Constraint**: Images that have ever been used in an order item snapshot **must not be deleted**. This is a business invariant: order snapshots must remain self-consistent for audit and customer history purposes.
- **Options for the fix**:
  1. **Soft-delete images** — add `IsDeleted bool` to `Image`; `ImageService.Delete` marks as deleted but keeps the row and file. Display URLs continue to resolve; deleted images are hidden from catalog management UI only.
  2. **Reference-count guard** — before hard-deleting, query `OrderItemSnapshots` for any row with matching `ImageId`. Requires Catalog → Sales cross-BC query (breaks isolation unless done via an Anti-Corruption Layer port).
  3. **Graceful degradation only** — keep hard-delete as-is, but display a placeholder image in views when `Build(id)` returns `404`. Document as expected degradation.
- **Recommended fix**: Option 1 (soft-delete) — keeps BC isolation, preserves file on disk, zero impact on snapshot rendering.
- **Fix tracked in**: To be added to `docs/roadmap/README.md` as catalog image lifecycle work.

---

## Medium

### [KI-007] `ModuleClient.PublishAsync` — dispatches to only ONE handler per event type
- **Severity**: 🟠 Medium
- **Location**: `ECommerceApp.Infrastructure/Messaging/ModuleClient.cs` (line 23)
- **Symptom**: When multiple BCs register `IMessageHandler<T>` for the same event type `T`, only the last-registered handler executes. All other subscribers are silently skipped.
- **Root cause**: `_serviceProvider.GetService(handlerType)` resolves a **single** service. `GetServices()` (plural) is required to resolve all registered handlers. `BackgroundMessageDispatcher` uses `GetServices()` correctly but runs asynchronously via `Channel<T>`, making it unsuitable for synchronous test assertions.
- **Impact**: In production (`UseBackgroundDispatcher = true`), the `BackgroundMessageDispatcher` path is used so this bug is masked. If `UseBackgroundDispatcher` is set to `false`, multi-consumer events (e.g., `OrderPlaced` consumed by Payments + Inventory + Presale) will only reach one handler.
- **Workaround**: Production always uses the background dispatcher. Integration tests use `SynchronousMultiHandlerBroker` which resolves all handlers correctly.
- **Fix tracked in**: [Roadmap F5](../../docs/roadmap/README.md) — ModuleClient evolution.

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

### [KI-006]
- **Severity**: 🔵 Low
- **Location**: `ECommerceApp.IntegrationTests/API/OrderControllerTests.cs`
- **Symptom**: Test fails with `should be 2026-03-15T19:20:03+01:00 but was 2026-03-15T18:20:03Z`. Same moment, different `DateTimeKind` — `DateTime` equality compares ticks directly; local ticks ≠ UTC ticks.
- **Root cause**: `Ordered = DateTime.Now` serializes with `+01:00` offset. The API normalizes to UTC on the roundtrip, returning `Z`. The `.ShouldBe` comparison then fails because the tick values differ.
- **Fix tracked in**: Resolved inline (no ADR needed).

### [KI-005] `buttonTemplate.js` — invalid HTML button `type` attribute
- **Severity**: 🔵 Low
- **Location**: `ECommerceApp.Web/wwwroot/js/buttonTemplate.js`
- **Symptom**: Buttons rendered by `createButton()` have `type="type"` — not a valid HTML button type. Browsers default to `type="submit"`, which may cause unintended form submissions inside modals.
- **Root cause**: The string `"type"` is passed as the value of the `type` attribute instead of `"button"`.
- **Fix tracked in**: [ADR-0021](../adr/0021-frontend-error-pipeline-and-js-migration-strategy.md) Phase 2 bug #3 · [Roadmap](../roadmap/frontend-pipeline.md#phase-2--targeted-bug-fixes)

---

## Deferred Design Decisions

### [DD-002] No quantity upper limit on cart/order — Web gap
- **Severity**: 🟡 Medium (design debt)
- **Location**: `ECommerceApp.Application/Presale/Checkout/DTOs/AddToCartDto.cs` — no validator exists
- **Issue**: `Shared.Quantity` only validates `value > 0`. There is no maximum quantity cap enforced
  anywhere — neither in the domain, the application layer, nor the Web controllers. A user on the Web
  storefront can add an arbitrary quantity of any single product to the cart.
- **Agreed fix**: Create `AddToCartDtoValidator` (FluentValidation) in the Application layer.
  Web limit: `RuleFor(x => x.Quantity).InclusiveBetween(1, 99)` — sourced from `ApiPurchaseOptions.MaxWebQuantityPerOrderLine`.
  The API limit (max 5 per line) is handled separately via `MaxApiQuantityFilter` — see [ADR-0025](../docs/adr/0025-api-tiered-access-trusted-purchase-policy.md).
- **Future**: Both Web and API limits become backoffice-configurable via `backoffice.PurchaseLimitSettings` + `IMemoryCache`.
- **Fix tracked in**: [orders-atomic-switch.md Step 4b](../../docs/roadmap/orders-atomic-switch.md)

### [DD-001] `StockHold` — missing `Withdrawn` status and non-reversible state transitions
- **Severity**: 🟡 Medium (design debt)
- **Context**: Inventory/Availability BC — `StockHold.Status` enum (`StockHoldStatus`)
- **Issue**: There is no `Withdrawn` status to distinguish a hold that was manually cancelled by an admin from a normal `Released` hold (which is triggered by an order cancellation or timeout). Without this distinction, audit trails and reporting cannot tell why a hold was terminated.
- **Second issue**: The state machine has no enforcement of non-reversible transitions. Once a hold reaches `Released`, it should be permanently terminal because it means the courier has already taken the item (physical goods path). Re-confirming a released hold is currently not blocked at the domain level.
- **Agreed next steps**:
  1. Add `Withdrawn = 4` to `StockHoldStatus` enum.
  2. Add a `Withdraw()` method on `StockHold` domain entity that transitions from `Guaranteed` or `Confirmed` → `Withdrawn`.
  3. Add `CanWithdraw` flag to `StockHoldRowVm` and a "Wycofaj" button on the Reservations view.
  4. Add guard in `StockHold` to throw `DomainException` on illegal reverse transitions (e.g. `Released → Confirmed`, `Fulfilled → Guaranteed`).
  5. Update `StockAuditEntry.ChangeType` if needed (or reuse `Released`).
- **Blocked by**: No current blocker — can be implemented independently.
- **Do NOT implement** until this entry is reviewed and a PR is scoped.

---

## Resolved

### [KI-001] `BusinessException._codes` silently discarded — no Polish error messages displayed ✅
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

---

*Last reviewed: 2026-03-22*
