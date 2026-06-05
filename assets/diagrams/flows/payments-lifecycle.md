# Payments Lifecycle Flow

Current implementation flow based on payment handlers, service, and domain status rules.

```mermaid
graph TD
    A(OrderPlaced message) --> B(Create Payment with Pending status)
    B --> C(Schedule PaymentWindowExpiredJob)

    B --> D(ConfirmAsync API/service path)
    D --> E{Status is Pending}
    E -->|No| F([Already* result])
    E -->|Yes| G(payment.Confirm)
    G --> H(Publish PaymentConfirmed)

    C --> I(PaymentWindowExpiredJob executes)
    I --> J{Status is Pending}
    J -->|No| K([No-op])
    J -->|Yes| L(payment.Expire)
    L --> M(Publish PaymentExpired)

    B --> N(OrderPlacementFailed message)
    N --> O(payment.Cancel)
    O --> P(Cancel timeout job)

    H --> Q(RefundApproved message)
    Q --> R(ProcessRefundAsync)
    R --> S(payment.Refund -> Refunded)
```

References:

- ../../../docs/specifications/payments-lifecycle.md
- ECommerceApp.Application/Sales/Payments/Handlers/*.cs
- ECommerceApp.Application/Sales/Payments/Services/PaymentService.cs
- ECommerceApp.Domain/Sales/Payments/Payment.cs
