# Orders Checkout Flow

High-level flow from cart confirmation to order creation.
Detailed business rules will be maintained in docs/specifications.

```mermaid
graph TD
    A(User confirms checkout) --> B(Load active soft reservations)
    B --> C{Reservations exist}
    C -->|No| D([Reject checkout])
    C -->|Yes| E(Validate stock availability)
    E --> F{Stock available}
    F -->|No| G([Reject with unavailable item])
    F -->|Yes| H(Create order from presale lines)
    H --> I(Publish order placed event)
    I --> J([Checkout success])
```

References:
- ../../../docs/specifications/orders-checkout.md
- docs/roadmap/presale-slice2.md
- docs/adr/0012/0012-presale-checkout-bc-design.md
- docs/adr/0014/0014-sales-orders-bc-design.md
