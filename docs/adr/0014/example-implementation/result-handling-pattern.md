# Result Handling Pattern

Expected business outcomes in Sales/Orders are returned as explicit values from the application
services. Controllers and callers branch on the result; they do not use exceptions for normal
not-found or state-guard outcomes.

## Place order

`PlaceOrderResult` in `Application/Sales/Orders/Results/PlaceOrderResult.cs` exposes:

- `Success(orderId)`
- `CustomerNotFound(customerId)`
- `CartItemsNotFound()`
- `CartItemsNotOwnedByUser()`
- `PlacementFailed(orderId)` for the current outbox compensation path

The result exposes `IsSuccess`, `OrderId`, `CustomerId`, and `FailureReason` so an endpoint can map
the outcome without inspecting exception messages.

## Single-order operations

`OrderOperationResult` in `Application/Sales/Orders/Results/OrderOperationResult.cs` currently
contains `Success`, `OrderNotFound`, `AlreadyPaid`, `NotPaid`, `NotDelivered`, `AlreadyDelivered`,
`CouponNotAssigned`, and `AlreadyCancelled`.

`OrderService` returns this enum from lifecycle, coupon, refund, update, and delete operations.
The service first loads the aggregate, returns the appropriate guard result when it is absent or
in an invalid state, and only then calls a domain method and persists the change.

Verified callers include `ECommerceApp.IntegrationTests/Sales/Orders/OrderServiceTests.cs` and
the Orders controllers/services under `ECommerceApp.Application/Sales/Orders/`.
