## Plan: Restore cart contents on `OrderPlacementFailed` compensation

> Produced following the required plan structure from `.github/agents/planner.md`.
> Full background, code-verified rationale, and rejected alternatives: see
> [`docs/roadmap/order-placement-compensation-followup.md`](../../docs/roadmap/order-placement-compensation-followup.md)
> (§ "Workstream 1 spec" and § "File map for workstream 1"). This plan file is the executable
> summary of that spec — if anything here is ambiguous, that doc is the source of truth.

### Scope
- **BC(s)**: Presale (Checkout)
- **Governing ADR(s)**: ADR-0026 (Order Placement Saga — Option A), amendment section
- **Risk**: low — additive method + one call site change, no schema change, no cross-BC contract change
- **Behavioral change**: yes — `Presale.OrderPlacementFailedHandler` currently only logs; after this
  change it restores the user's cart

### Files to add
- `ECommerceApp.Application/Presale/Checkout/DTOs/CartRestoreItem.cs` — `public record CartRestoreItem(int ProductId, int Quantity);`. Kept Presale-local (not reusing `Sales.Orders.Messages.OrderPlacedItem`) to avoid leaking a Sales.Orders contract into a Presale service interface.

### Files to modify
- `ECommerceApp.Application/Presale/Checkout/Services/ICartService.cs` — add `Task RestoreAsync(PresaleUserId userId, IReadOnlyList<CartRestoreItem> items, CancellationToken ct = default);`. Verified only one implementer exists (`CartService`), so this is a safe additive change.
- `ECommerceApp.Application/Presale/Checkout/Services/CartService.cs` — implement `RestoreAsync`:
  - For each item: `CartLine.Create(userId.Value, item.ProductId, item.Quantity)` then `_cartRepo.UpsertAsync(line, ct)`. **Note the `.Value`** — `PresaleUserId` has no implicit conversion to `string`, only from it; `CartLine.Create` takes `string`. Omitting `.Value` will not compile.
  - Overwrite semantics, not additive — do NOT route through `AddToCartAsync` (its `MaxQuantityPerOrderLine` limit check could silently shrink or drop a legitimate restore).
  - No catalog-existence pre-check — `RefreshCacheAsync` already tolerates products missing from the catalog (existing behavior, see `GetCartAsync`).
  - Call `RefreshCacheAsync(userId, ct)` once, after the loop — not per item.
- `ECommerceApp.Application/Presale/Checkout/Handlers/OrderPlacementFailedHandler.cs`:
  - Inject `ICartService` via constructor (add alongside the existing `ILogger<OrderPlacementFailedHandler>` parameter — no DI registration change needed, `ICartService` is already registered in `Extensions.cs:22`).
  - Replace the TODO block with:
    ```csharp
    try
    {
        var items = message.Items.Select(i => new CartRestoreItem(i.ProductId, i.Quantity)).ToList();
        await _cartService.RestoreAsync(new PresaleUserId(message.UserId), items, ct);
        _logger.LogInformation(
            "OrderPlacementFailed for order {OrderId}. Cart for user {UserId} restored. Reason: {Reason}",
            message.OrderId, message.UserId, message.Reason);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "OrderPlacementFailed for order {OrderId}. Failed to restore cart for user {UserId}.",
            message.OrderId, message.UserId);
    }
    ```
  - Catch-and-log, do not rethrow — matches the existing idempotent/best-effort pattern used by `Payments.OrderPlacementFailedHandler` and `Inventory.OrderPlacementFailedHandler`.
- `ECommerceApp.IntegrationTests/CrossBC/OrderPlacementFailedFanOutTests.cs` — update the class docstring (currently claims Presale compensation is "logs warning (cart restore deferred)") to describe the new restore behavior.

### Files to delete
- None.

### Tests required (behavioral change = yes)
- Unit: `ECommerceApp.UnitTests/Presale/Checkout/CartServiceTests.cs`
  - `RestoreAsync_ShouldUpsertEachLineAndRefreshCache` — verify `UpsertAsync` called once per item, cache refreshed.
  - `RestoreAsync_ProductNotInCatalog_ShouldStillRestoreLine` — catalog mock returns empty summaries, line is still upserted.
- Unit: `ECommerceApp.UnitTests/Presale/Checkout/OrderPlacementFailedHandlerTests.cs`
  - Add `Mock<ICartService>` to the constructor. New test: `HandleAsync_ShouldCallRestoreAsyncWithMessageItems`.
  - New test: `HandleAsync_WhenRestoreAsyncThrows_ShouldLogAndNotThrow`.
- Integration: `ECommerceApp.IntegrationTests/CrossBC/OrderPlacementFailedFanOutTests.cs`
  - New test under a `// ── Presale BC compensation ──` section (mirror existing `// ── Payments BC ──` / `// ── Inventory BC ──` headers): seed a cart via `GetRequiredService<ICartService>()`, publish `OrderPlaced` then `OrderPlacementFailed`, assert the cart lines exist again via `ICartService.GetCartAsync`.

### Steps (atomic, ordered)
1. Add `CartRestoreItem.cs` DTO.
2. Add `RestoreAsync` to `ICartService` interface.
3. Implement `RestoreAsync` in `CartService`. Build.
4. Add the two `CartServiceTests` unit tests. Run unit tests for this file only, confirm green.
5. Wire `ICartService` into `OrderPlacementFailedHandler` constructor + replace the TODO block per the snippet above.
6. Add the two `OrderPlacementFailedHandlerTests` unit tests (need a `Mock<ICartService>` added to the test class constructor). Run unit tests for this file only, confirm green.
7. Add the integration test in `OrderPlacementFailedFanOutTests.cs` + update the class docstring.
8. Run the full embedded verification loop (see below).

### Verification commands @verifier will run
- `dotnet build ECommerceApp.sln --configuration Release --nologo`
- `dotnet test ECommerceApp.UnitTests/ECommerceApp.UnitTests.csproj --configuration Release --no-build --nologo`
- `dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj --configuration Release --no-build --nologo`
- ArchUnitNET (part of UnitTests)

### Risks / open questions
- **Known accepted limitation** (not a defect to fix here): if the user manually re-adds a product to their cart in the short window before compensation runs, `RestoreAsync` overwrites that line with the restored quantity rather than merging. Documented in the followup doc, out of scope for this phase.
- Soft reservations (`ISoftReservationService`) are explicitly untouched by this phase — see followup doc rationale. If code review disagrees, that's a scope question for the human, not something `@implementer` should silently expand into.

### Rollback plan
- Revert the four modified/added files via `git checkout` on this phase's commit (single, self-contained change — no migration, no DI registration changes beyond constructor injection, no other BC touched).
