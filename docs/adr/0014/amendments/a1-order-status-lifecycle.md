## §16 — `OrderStatus` lifecycle column (design revision)

`Order` carries a single `OrderStatus Status { get; private set; }` column that represents
the authoritative current lifecycle state. It replaces the four removed scalar properties:
`bool IsPaid`, `bool IsDelivered`, `bool IsCancelled`, `DateTime? CancelledAt`, `DateTime? Delivered`.

```csharp
public enum OrderStatus
{
    Placed,               // order created — awaiting payment
    PaymentConfirmed,     // paid — awaiting fulfilment
    PartiallyFulfilled,   // operator released some items; waiting on remainder
    Fulfilled,            // all items released / delivered
    Cancelled,            // payment expired, denied, or manually cancelled
    Refunded              // refund approved and processed
}
```

**Rules:**
- `Status` has `private set` — only domain methods advance it.
- Every state transition method updates `Status` **and** appends the corresponding `OrderEvent`
  in one operation. Both are saved in a single `SaveChangesAsync` — they can never diverge.
- `Status` is the indexed column for list queries: `WHERE Status = 'PaymentConfirmed'`.
- New lifecycle states are added as enum values — no new columns, no new migrations beyond
  the initial schema creation.

**EF Core mapping:**
```csharp
builder.Property(o => o.Status)
       .HasConversion<string>()
       .HasMaxLength(30)
       .IsRequired();
builder.HasIndex(o => o.Status);
```

**Timestamps derived from events (not stored as columns):**

| Old column | Derived from |
|---|---|
| `DateTime? Delivered` | `OrderFulfilled` event `OccurredAt` |
| `DateTime? CancelledAt` | `OrderCancelled` event `OccurredAt` |

When an API or ViewModel needs "when was this order delivered?", it loads the `OrderFulfilled`
event for that `OrderId`. For list views that do not need timestamps, only `Status` is needed —
no event loading required.

**`CalculateCost()` and `DiscountPercent` note:**
`int? DiscountPercent` and `int? CouponUsedId` remain as columns for now. `CalculateCost()`
uses `DiscountPercent` directly. Both will be removed when the Coupons BC is introduced.
See §18 for the deferred Coupons BC work.

---
