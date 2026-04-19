## Migration plan

**Slice 1 (this ADR):**

1. Create target folder structure: `Domain/Sales/Fulfillment/`, `Application/Sales/Fulfillment/`,
   `Infrastructure/Sales/Fulfillment/`.
2. Create `Domain/Sales/Fulfillment/`: `Refund`, `RefundId`, `RefundItem`, `RefundStatus`,
   `IRefundRepository`.
3. Create `Application/Sales/Fulfillment/`: `IRefundService`, `RefundService` (internal sealed),
   `RefundRequestResult`, `RefundOperationResult`, `RefundApproved` (new shape),
   `RefundApprovedItem`, `RefundRejected`, `IOrderExistenceChecker`, DTOs, ViewModels,
   DI `Extensions.cs`.
   - **Also extend ADR-0015** in parallel:
     - `a)` Add `Payment.Refund(int refundId)` method to `Domain/Sales/Payments/Payment.cs`.
     - `b)` Add `PaymentRefundedEvent` to `Domain/Sales/Payments/Events/`.
     - `c)` Add `ProcessRefundAsync(int orderId, int refundId, CancellationToken ct)` to
       `IPaymentService` and implement in `PaymentService`.
4. Create `Application/Sales/Orders/Handlers/OrderRefundApprovedHandler.cs`. Register in
   `Application/Sales/Orders/Services/Extensions.cs`.
5. Create `Application/Sales/Payments/Handlers/PaymentRefundApprovedHandler.cs`. Register in
   `Application/Sales/Payments/Services/Extensions.cs`.
6. Create `Infrastructure/Sales/Fulfillment/`: `FulfillmentDbContext`, `RefundConfiguration`
   (OwnsMany RefundItems), `RefundRepository`, `OrderExistenceCheckerAdapter`, DI `Extensions.cs`.
7. Register `FulfillmentDbContext` in `Infrastructure/DependencyInjection.cs`.
8. Generate EF migration `InitFulfillmentSchema` targeting `FulfillmentDbContext`.
9. Write unit tests: `RefundAggregateTests`, `RefundServiceTests`, `PaymentRefundApprovedHandlerTests`,
   `OrderRefundApprovedHandlerTests`, `InventoryRefundApprovedHandlerTests` (updated handler).
10. Write integration tests: `RefundServiceIntegrationTests` — `RequestRefundAsync` happy path,
    order not found, duplicate refund; `ApproveRefundAsync` happy path, not found, already processed;
    `RejectRefundAsync` happy path.
11. Atomic switch:
    - Update `Application/Inventory/Availability/Handlers/RefundApprovedHandler.cs`: change `using`
      from `Sales.Payments.Messages` → `Sales.Fulfillment.Messages`; update handler body to
      iterate `message.Items`.
    - Remove the old `Application/Sales/Payments/Messages/RefundApproved.cs`.
    - Remove legacy `RefundService` DI registration and direct `IOrderService` / `IPaymentService`
      calls from legacy `RefundService`.
    - Update controllers / API controllers to use `IRefundService`.

**Slice 2 (deferred — future ADR or ADR-0018 amendment):**

12. Create `Domain/Sales/Fulfillment/`: `Shipment`, `ShipmentId`, `ShipmentLine`,
    `ShipmentStatus`, `IShipmentRepository` (see §11.1–§11.2).
13. Create `Application/Sales/Fulfillment/`: `IShipmentService`, `ShipmentService`
    (internal sealed), `ShipmentOperationResult`, messages (`ShipmentDispatched`,
    `ShipmentDelivered`, `ShipmentPartiallyDelivered`, `ShipmentFailed`),
    `CreateShipmentDto`, `CreateShipmentLineDto`, `ShipmentDetailsVm`, `ShipmentListVm`
    (see §11.3–§11.5).
14. Create `Application/Sales/Orders/Handlers/OrderShipmentDeliveredHandler` and
    `OrderShipmentPartiallyDeliveredHandler` (see §11.6). Register in Orders DI.
    Extend `IOrderService` with `MarkAsPartiallyFulfilledAsync(orderId)`.
    Remove direct controller call to `MarkAsDeliveredAsync` — triggered by `ShipmentDelivered`.
15. Add `Order.MarkAsRefunded(int refundId)` and `IOrderService.MarkAsRefundedAsync(orderId, refundId)`.
    Update `OrderRefundApprovedHandler` to call `MarkAsRefundedAsync` (see §11.7).
16. Create `Infrastructure/Sales/Fulfillment/Repositories/ShipmentRepository` and
    `Configurations/ShipmentConfiguration` (OwnsMany ShipmentLines). EF migration
    `AddShipmentSchema` targeting `FulfillmentDbContext` (see §11.8–§11.9).
17. Write unit tests: `ShipmentAggregateTests`, `ShipmentServiceTests`,
    `OrderShipmentDeliveredHandlerTests`, `OrderShipmentPartiallyDeliveredHandlerTests`.
18. Write integration tests: `ShipmentServiceIntegrationTests` — create, dispatch, deliver,
    partial delivery, fail flows.
