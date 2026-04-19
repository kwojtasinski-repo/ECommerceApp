## §17 — Event payload records (design revision)

Each business event that carries context stores it as a typed JSON payload in
`OrderEvent.Payload` (serialized via `JsonSerializer.Serialize<T>`).

```csharp
// Domain/Sales/Orders/Events/Payloads/
public record PaymentConfirmedPayload(int PaymentId);
public record OrderCancelledPayload(string Reason);    // "PaymentExpired" | "ManualOperator" | "CustomerRequest"
public record CouponAppliedPayload(int CouponUsedId, int DiscountPercent);
public record CouponRemovedPayload(int CouponUsedId);
public record RefundAssignedPayload(int RefundId);
public record PartialFulfilmentPayload(IReadOnlyList<FulfilledItem> Items);
public record FulfilledItem(int ItemId, int Quantity);
```

Events with no supplementary context (`OrderPlaced`, `OrderPaymentExpired`, `OrderFulfilled`)
pass `null` payload.

**`PaymentId` and `RefundId` removal from `Order` columns:**
`int? PaymentId` and `int? RefundId` are removed from `Order`. Their values are now stored in
`PaymentConfirmedPayload.PaymentId` and `RefundAssignedPayload.RefundId` respectively.

Consequences:
- `WHERE PaymentId = X` SQL queries are replaced by: load the `OrderPaymentConfirmed` event
  for the order and deserialize the payload. Rare operation; acceptable cost.
- Partial payments are naturally supported: multiple `OrderPaymentConfirmed` events per order,
  each with a different `PaymentId`. No schema change required.

**Payload storage format:** `nvarchar(max)` JSON (human-readable, debuggable via SQL query,
sufficient for the small payloads in this domain).

---
