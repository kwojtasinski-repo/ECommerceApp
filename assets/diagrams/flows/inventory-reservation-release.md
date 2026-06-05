# Inventory Reservation Release Flow

High-level reservation lifecycle around checkout and timeout.
Detailed business rules will be maintained in docs/specifications.

```mermaid
graph TD
    A(User adds item to cart) --> B(Create soft reservation)
    B --> C(Track reservation TTL)
    C --> D{Checkout confirmed before TTL}
    D -->|Yes| E(Consume reservation during order placement)
    D -->|No| F(Expire reservation)
    E --> G([Stock remains allocated to order])
    F --> H(Release stock)
    H --> I([Item available again])
```

References:
- ../../../docs/specifications/inventory-reservation-release.md
- docs/roadmap/presale-slice2.md
- docs/adr/0012/0012-presale-checkout-bc-design.md
- docs/adr/0011/0011-inventory-availability-bc-design.md
