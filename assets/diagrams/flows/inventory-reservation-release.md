# Inventory Reservation and Release Flow

Current implementation flow for hard reservation lifecycle in Inventory.

```mermaid
graph TD
    A(OrderPlaced message) --> B(ReserveAsync per order item)
    B --> C(StockHold status = Guaranteed)

    C --> D(PaymentConfirmed message)
    D --> E(ConfirmHoldsByOrderAsync)
    E --> F(StockHold status = Confirmed)

    C --> G(PaymentExpired / OrderCancelled / OrderPlacementFailed)
    G --> H(ReleaseAsync or ReleaseAllHoldsForOrderAsync)
    H --> I(StockHold status = Released)

    F --> J(ShipmentDelivered)
    J --> K(FulfillAsync)
    K --> L(StockHold status = Fulfilled)

    F --> M(ShipmentFailed or FailedItems in ShipmentPartiallyDelivered)
    M --> N(ReleaseAsync)
    N --> I

    C --> O(WithdrawHoldAsync)
    F --> O
    O --> P(StockHold status = Withdrawn)
```

References:

- ../../../docs/specifications/inventory-reservation-release.md
- ECommerceApp.Application/Inventory/Availability/Services/StockService.cs
- ECommerceApp.Application/Inventory/Availability/Handlers/*.cs
- ECommerceApp.Domain/Inventory/Availability/StockHoldStatus.cs
