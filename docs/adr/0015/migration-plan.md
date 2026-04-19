### 12. Migration plan

**Phase 1 — Payments domain layer**
1. `Domain/Sales/Payments/PaymentId.cs` — `sealed record PaymentId(int Value) : TypedId<int>(Value)`
2. `Domain/Sales/Payments/PaymentOrderId.cs` — struct with implicit `int` operator
3. `Domain/Sales/Payments/PaymentStatus.cs` — enum: `Pending`, `Confirmed`, `Expired`, `Refunded`
4. `Domain/Sales/Payments/Events/PaymentConfirmedEvent.cs`, `PaymentExpiredEvent.cs`, `RefundIssuedEvent.cs`
5. `Domain/Sales/Payments/Payment.cs` — `Create`, `Confirm`, `Expire`, `IssueRefund`
6. `Domain/Sales/Payments/IPaymentRepository.cs`

**Phase 2 — Orders aggregate extension**
7. `Domain/Sales/Orders/OrderEventTypes.cs` — add `OrderCancelled` constant
8. `Domain/Sales/Orders/Order.cs` — add `IsCancelled`, `CancelledAt`, `Cancel()` method

**Phase 3 — Integration message updates**
9. `Application/Sales/Orders/Messages/OrderPlaced.cs` — add `decimal TotalAmount`, `int CurrencyId`
10. `Application/Sales/Orders/Services/OrderService.cs` — update `PlaceOrderAsync` to pass `TotalAmount`, `CurrencyId`
11. `Application/Sales/Payments/Messages/PaymentConfirmed.cs` — add `int PaymentId`
12. `Application/Sales/Payments/Messages/PaymentExpired.cs` — new record

**Phase 4 — Application layer (Payments BC)**
13. `Application/Sales/Payments/Handlers/OrderPlacedHandler.cs`
14. `Application/Sales/Payments/Handlers/PaymentWindowExpiredJob.cs`
15. `Application/Sales/Payments/Services/IPaymentService.cs`, `PaymentService.cs`
16. DTOs: `InitializePaymentDto`, `ConfirmPaymentDto` (if manual confirmation is needed via controller)
17. ViewModels: `PaymentVm`, `PaymentDetailsVm`, `PaymentListVm`
18. `Application/Sales/Payments/Services/Extensions.cs` — registers services + handlers + job

**Phase 5 — Application layer (Orders BC extensions)**
19. `Application/Sales/Orders/Services/IOrderService.cs` — add `MarkAsPaidAsync`, `CancelOrderAsync`
20. `Application/Sales/Orders/Services/OrderService.cs` — implement both methods
21. `Application/Sales/Orders/Results/OrderOperationResult.cs` — add `AlreadyCancelled`
22. `Application/Sales/Orders/Handlers/OrderPaymentConfirmedHandler.cs`
23. `Application/Sales/Orders/Handlers/OrderPaymentExpiredHandler.cs`
24. `Application/Sales/Orders/Services/Extensions.cs` — register two new handlers

**Phase 6 — Infrastructure layer (Payments BC)**
25. `Infrastructure/Sales/Payments/PaymentsConstants.cs`, `PaymentsDbContext.cs`, `PaymentsDbContextFactory.cs`
26. `Infrastructure/Sales/Payments/Configurations/PaymentConfiguration.cs`
    — `PaymentId` identity conversion, `PaymentOrderId` conversion, `RowVersion → IsRowVersion()`, `UNIQUE` on `OrderId`
27. `Infrastructure/Sales/Payments/Repositories/PaymentRepository.cs`
28. `Infrastructure/Sales/Payments/Extensions.cs` — registers `AddDbContext<PaymentsDbContext>`, `IDbContextMigrator`, `IPaymentRepository`
29. Register `PaymentWindowExpiredJob` in TimeManagement DI alongside `PaymentWindowTimeoutJob` (Inventory) and `SoftReservationExpiredJob` (Presale)

**Phase 7 — Infrastructure layer (Orders BC schema extension)**
30. `Infrastructure/Sales/Orders/Configurations/OrderConfiguration.cs` — add `IsCancelled` and `CancelledAt` mapping
31. Generate migration for `sales.Orders` extension (new nullable columns) — **separate migration from Orders initial `InitSalesSchema`** per migration policy

**Phase 8 — Unit tests**
32. `UnitTests/Sales/Payments/PaymentAggregateTests.cs` — all state transitions and guards
33. `UnitTests/Sales/Payments/OrderPlacedHandlerTests.cs` — payment created + job scheduled
34. `UnitTests/Sales/Payments/PaymentWindowExpiredJobTests.cs` — null guard, non-Pending guard, happy path
35. `UnitTests/Sales/Orders/OrderAggregateTests.cs` — add `Cancel()` tests
36. `UnitTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs`
37. `UnitTests/Sales/Orders/OrderPaymentExpiredHandlerTests.cs`

**Phase 9 — DB migrations (require approval per migration policy)**
38. `dotnet ef migrations add InitPaymentsSchema --project Infrastructure --context PaymentsDbContext`
    Creates `payments.Payments`.
39. `dotnet ef migrations add AddOrderCancellationFields --project Infrastructure --context OrdersDbContext`
    Adds `IsCancelled bit NOT NULL DEFAULT 0` and `CancelledAt datetime2 NULL` to `sales.Orders`.
40. Submit both migrations for approval — do not apply to production without sign-off.

**Phase 10 — Integration tests and atomic switch**
41. Integration tests for `PaymentService`, `OrderPaymentConfirmedHandler`, `OrderPaymentExpiredHandler`
42. Atomic switch: remove `PaymentHandler.CreatePayment()` + `HandlePaymentChangesOnOrder()` calls from legacy controllers; update `PaymentController` and `OrderController` to use new `IPaymentService` / `IOrderService` APIs
43. Update `bounded-context-map.md` — move Sales/Payments to Completed BCs
