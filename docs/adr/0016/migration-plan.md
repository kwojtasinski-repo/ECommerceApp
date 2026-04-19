## Migration plan

**Slice 1 (this ADR):**

1. Create target folder structure: `Domain/Sales/Coupons/`, `Application/Sales/Coupons/`,
   `Infrastructure/Sales/Coupons/`.
2. Create `Domain/Sales/Coupons/`: `Coupon`, `CouponId`, `CouponUsed`, `CouponUsedId`,
   `CouponStatus`, `ICouponRepository`, `ICouponUsedRepository`.
3. Create `Application/Sales/Coupons/`: `ICouponService`, `CouponService` (internal sealed),
   `CouponApplyResult`, `CouponRemoveResult`, `CouponApplied`, `CouponRemovedFromOrder`,
   `CouponsOrderCancelledHandler`, `IOrderExistenceChecker`, DI `Extensions.cs`.
4. Create `Application/Sales/Orders/Handlers/OrderCouponAppliedHandler` and
   `OrderCouponRemovedHandler`. Register both in `Application/Sales/Orders/Services/Extensions.cs`.
5. Create `Infrastructure/Sales/Coupons/`: `CouponsDbContext`, EF configurations
   (`CouponConfiguration`, `CouponUsedConfiguration`), `CouponRepository`,
   `CouponUsedRepository`, `OrderExistenceCheckerAdapter`, DI `Extensions.cs`.
6. Register `CouponsDbContext` in `Infrastructure/DependencyInjection.cs` alongside other BC
   contexts.
7. Generate EF migration `InitCouponsSchema` targeting `CouponsDbContext`.
8. Write unit tests: `CouponAggregateTests` (domain), `CouponServiceTests` (mocked repos + broker
   + existence checker), `CouponsOrderCancelledHandlerTests`, `OrderCouponAppliedHandlerTests`,
   `OrderCouponRemovedHandlerTests`.
9. Write integration tests: `CouponServiceIntegrationTests` — `ApplyCouponAsync` happy path,
   coupon not found, coupon already used, order not found; `RemoveCouponAsync` happy path,
   no coupon applied.
10. Atomic switch: update controllers / API controllers to use `ICouponService` instead of
    `ICouponHandler`. Remove legacy `CouponHandler` DI registration and all direct references
    to the legacy `CouponHandler` from controllers and services.

**Slice 2 (rule-based model — designed in §9):**

11. Redesign `Coupon` aggregate: replace `DiscountPercent` + `Status` with `RulesJson` + `Version`.
    Add domain guard in `Create()` for scope ↔ targets consistency.
12. Enhance `CouponUsed`: add `UserId`, make `CouponId` nullable, add `RuntimeCouponSnapshot`.
    Remove unique constraints on `CouponId` and `OrderId`.
13. Create new entities: `CouponScopeTarget`, `CouponApplicationRecord`, `SpecialEvent`.
14. Build rule engine: `ICouponRuleRegistry`, `CouponWorkflowBuilder`, `CouponRuleDescriptor`,
    two-tier validation, creation-time parameter validation. Register initial rule vocabulary.
15. Redesign `CouponService` for rule-based evaluation, multi-coupon support, independent
    evaluation strategy. Add `CreateCouponAsync` with full validation pipeline.
16. Replace `CouponApplied` with `OrderPriceAdjusted`. Add `PriceAdjustmentLedger` and
    `PriceAdjustment` to the Orders BC `Order` aggregate (replaces `AssignCoupon`/`RemoveCoupon`).
17. Update `CouponsOrderCancelledHandler` for multi-coupon: returns list, iterates, marks
    `CouponApplicationRecord.WasReversed = true`.
18. Add `ISpecialEventCache`, `NullRuntimeCouponSource`, `CouponsOptions`.
19. Generate EF migration for Slice 2 schema changes. Include data migration for existing
    `sales.Coupons` rows (`DiscountPercent` → `RulesJson`).
20. Add Catalog BC prerequisite: `ProductRenamed`, `CategoryRenamed`, `TagRenamed` messages and
    corresponding aggregate methods. Add `CatalogNameChangedHandler` in Coupons BC.
21. Write unit tests for rule engine, redesigned aggregate, redesigned service, all handlers.
22. Write integration tests for multi-coupon flows, concurrent usage, cancellation/refund.
23. Admin CRUD via V2 controllers using new `ICouponService.CreateCouponAsync`.
24. Atomic switch: migrate legacy views/controllers, remove legacy `CouponType` entity and
    `CouponHandler`.
