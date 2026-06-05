# Orders Checkout Flow

Current implementation flow from initiate/confirm to order placement integration.

```mermaid
graph TD
    A(User calls checkout initiate) --> B(Load cart)
    B --> C{Cart empty}
    C -->|Yes| D([CartEmpty])
    C -->|No| E{Active soft reservation exists}
    E -->|Yes| F([AlreadyInProgress])
    E -->|No| G(Try HoldAsync for each cart line)
    G --> H{Any line reserved}
    H -->|No| I([NothingReserved])
    H -->|Yes| J(Remove reserved lines from cart)
    J --> K([Completed])

    L(User calls checkout confirm) --> M(Load user reservations)
    M --> N{Any reservation}
    N -->|No| O([NoSoftReservations])
    N -->|Yes| P{Still within acceptance window}
    P -->|No| Q([ReservationsExpired])
    P -->|Yes| R(CommitAllForUser)
    R --> S(Call OrderClient.PlaceOrderAsync)
    S --> T{IsSuccess}
    T -->|No| U(RevertAllForUser)
    U --> V([OrderFailed])
    T -->|Yes| W([Success with OrderId])
```

References:

- ../../../docs/specifications/orders-checkout.md
- ECommerceApp.Application/Presale/Checkout/Services/CheckoutService.cs
- ECommerceApp.API/Controllers/Presale/CheckoutController.cs
- ECommerceApp.Application/Sales/Orders/Services/OrderService.cs
