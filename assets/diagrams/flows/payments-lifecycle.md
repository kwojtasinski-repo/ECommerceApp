# Payments Lifecycle Flow

High-level payment state transitions and downstream impact.
Detailed business rules will be maintained in docs/specifications.

```mermaid
graph TD
    A(Order placed event) --> B(Create payment)
    B --> C{Payment outcome}
    C -->|Confirmed| D(Mark order as paid)
    C -->|Expired| E(Cancel order)
    C -->|Pending timeout| F(Run payment window timeout job)
    F --> E
    D --> G([Paid path complete])
    E --> H([Expired path complete])
```

References:
- ../../../docs/specifications/payments-lifecycle.md
- docs/roadmap/payments-atomic-switch.md
- docs/adr/0015/0015-sales-payments-bc-design.md
- docs/adr/0014/0014-sales-orders-bc-design.md
