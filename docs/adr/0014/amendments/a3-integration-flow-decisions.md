## §18 — Integration flow design decisions (post-implementation review)

These decisions were made after a full flow analysis (Presale → Orders → Payments → Inventory).

### Gap 1 — `OrderPlaced` domain event appended before `Cost` is known — non-issue

`Order.Create()` appends `OrderEventType.OrderPlaced` with no payload. `Cost = 0` at this
point because items are not yet assigned. `Cost` is calculated and persisted in the same
`PlaceOrderAsync` unit of work, BEFORE the `OrderPlaced` integration message is published.

`OrderPlaced` domain event has no payload — it is purely a lifecycle timestamp marker.
The `OrderPlaced` integration message (published to `IMessageBroker`) carries the correct `Cost`.

**Future consideration:** If `OrderPlaced` domain event ever needs a cost payload, move
`AppendEvent(OrderEventType.OrderPlaced)` from `Order.Create()` to a separate `FinalizeOrder()`
method called after `CalculateCost()` in `OrderService.PlaceOrderAsync`.

### Gap 2 — `CartLine` and `SoftReservation` leak after order placement — action required

`OrderService.PlaceOrderAsync` assigns cart `OrderItem` entities to the new order but does NOT
clean up `CartLine` rows or `SoftReservation` rows in the Presale BC.

**Resolution:** Add `OrderPlacedHandler` in the Presale/Checkout BC:

```csharp
// Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs
internal sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
{
    // 1. Delete CartLine rows for the placed cart item IDs
    // 2. Delete SoftReservation rows for those items
    // 3. Mark cart as "processing" in cache (hides checkout UI, shows processing state)
}
```

The `SoftReservationExpiredJob` becomes a safety net only — no longer the primary cleanup path.
**Document in ADR-0012:** Presale BC subscribes to `OrderPlaced`; without this handler,
every placed order leaks `CartLine` and `SoftReservation` rows indefinitely.

### Gap 3 — `PaymentConfirmed.Items` is empty — resolved by design

`PaymentService.ConfirmAsync` publishes `PaymentConfirmed` with `Items = []`. The Inventory BC's
`PaymentConfirmedHandler` previously iterated `message.Items` to confirm reservations per item —
this breaks with an empty list.

**Resolution:** `PaymentConfirmedHandler` in Inventory BC is updated to confirm by `OrderId`,
not by item list. Inventory already knows all reservations for the order from `OrderPlaced`:

```csharp
public async Task HandleAsync(PaymentConfirmed message, CancellationToken ct)
{
    await _stockService.ConfirmReservationsByOrderAsync(message.OrderId, ct);
}
```

`PaymentConfirmed.Items` field is kept as an empty array for backward compatibility but is
never populated by the Payments BC. The Inventory BC owns its reservation data.

### Gap 5 — Currency architecture decision

`int CurrencyId` stays as a simple FK column on `Order`. No multi-currency complexity at
the Order aggregate level. This is a single-currency shop; all prices are in one currency.

If the user pays in a different currency, that is a **Payments BC concern**: the `Payment`
aggregate or its event payload carries the payment currency. The `Order.CurrencyId` is the
booking currency only.

Future multi-currency scenario: if the Catalog BC introduces per-currency pricing,
`CurrencyId` is already on `Order` — no migration needed. The payment currency would be
stored in `PaymentConfirmedPayload` alongside `PaymentId`.

---

### Gap 4 — Inventory `PaymentWindowTimeoutJob` conflict — retire on switch

The Inventory BC has its own `PaymentWindowTimeoutJob` that releases stock when the payment
window expires. The new Payments BC `PaymentWindowExpiredJob` handles the same trigger via a
different chain:

```
PaymentWindowExpiredJob (Payments BC) → PaymentExpired
→ OrderPaymentExpiredHandler (Orders BC) → OrderCancelled
→ OrderCancelledHandler (Inventory BC) → releases reservations
```

Running both means double-release of stock.

**Resolution:** Retire `Inventory.PaymentWindowTimeoutJob` as part of the Payments BC atomic
switch. Add to the switch checklist:
> ☐ Deregister `PaymentWindowTimeoutJob` from Inventory BC DI (`Extensions.cs`)
