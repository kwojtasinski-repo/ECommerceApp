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

### [KI-006] `given_valid_order_should_update` — timezone comparison failure
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

*Last reviewed: 2026-03-15*
