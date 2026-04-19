## Conformance checklist

- [ ] `Refund` aggregate lives under `Domain/Sales/Fulfillment/` — not `Domain/Model/`
- [ ] `Refund` has private setters and a `private` parameterless constructor for EF Core
- [ ] `RefundId` follows the `TypedId<int>` pattern (ADR-0006)
- [ ] `RefundItem` is an owned entity — no `RefundItemId` typed ID; no separate aggregate root
- [ ] `Refund.OrderId` is a plain `int` — no navigation property to `Order`
- [ ] `RefundItem.ProductId` is a plain `int` — no navigation property to Catalog
- [ ] `RefundService` is `internal sealed`
- [ ] `RefundApproved` lives in `Application/Sales/Fulfillment/Messages/` — NOT in `Payments/Messages/`
- [ ] `RefundApproved` carries `IReadOnlyList<RefundApprovedItem> Items` — not single `ProductId`/`Quantity`
- [ ] `IOrderExistenceChecker` lives in `Application/Sales/Fulfillment/Contracts/`
- [ ] `FulfillmentDbContext` uses schema `"fulfillment"` — DbSet: `Refunds`; `RefundItems` via `OwnsMany`
- [ ] No FK from `fulfillment.Refunds.OrderId` to Orders table — cross-BC boundary
- [ ] No FK from `fulfillment.RefundItems.ProductId` to Catalog table — cross-BC boundary
- [ ] `Payment.Refund(int refundId)` is added to `Domain/Sales/Payments/Payment.cs` before `PaymentRefundApprovedHandler` is registered
- [ ] Old `Application/Sales/Payments/Messages/RefundApproved.cs` is removed at atomic switch step 11
- [ ] Legacy `RefundService` is NOT removed until atomic switch (step 11) is verified green
- [ ] `Shipment` is NOT present in Slice 1 domain or infrastructure
