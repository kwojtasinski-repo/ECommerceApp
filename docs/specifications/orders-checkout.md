# Flow: Orders Checkout

> Domain: Presale Checkout + Sales Orders integration
> Status: Draft
> Last verified: 2026-06-05
> Governing ADRs: [docs/adr/0012/0012-presale-checkout-bc-design.md](docs/adr/0012/0012-presale-checkout-bc-design.md), [docs/adr/0014/0014-sales-orders-bc-design.md](docs/adr/0014/0014-sales-orders-bc-design.md)

---

## Purpose

Describe the checkout flow exactly as currently implemented by checkout service/controller and order placement integration.

---

## Scope (current code only)

### Included

- `InitiateAsync` reservation attempt and cart handling.
- `PlaceOrderAsync` reservation checks, commit/revert behavior, and order placement call.
- API result mapping in `CheckoutController`.
- Integration messages `OrderPlaced` and `OrderPlacementFailed` emitted by order service.

### Excluded

- Future checkout state machine/event model not present in code.
- UI details.

---

## Entry points

- `POST api/checkout/initiate` -> `ICheckoutService.InitiateAsync`
- `POST api/checkout/confirm` -> `ICheckoutService.PlaceOrderAsync`

Source: `ECommerceApp.API/Controllers/Presale/CheckoutController.cs`.

---

## Implemented initiate outcomes

`InitiateCheckoutResult` currently used:

- `Completed`
- `CartEmpty`
- `NothingReserved`
- `AlreadyInProgress`

Implementation details:

- If cart empty -> `CartEmpty`.
- If active soft reservation already exists -> `AlreadyInProgress`.
- For each cart line, tries `HoldAsync(...)`.
- If none reserved -> `NothingReserved`.
- If at least one reserved -> removes reserved products from cart and returns `Completed`.

Sources:

- `ECommerceApp.Application/Presale/Checkout/Services/CheckoutService.cs`
- `ECommerceApp.Application/Presale/Checkout/Results/InitiateCheckoutResult.cs`

---

## Implemented confirm outcomes

`CheckoutService.PlaceOrderAsync` currently returns:

- `NoSoftReservations`
- `ReservationsExpired`
- `OrderFailed`
- `Success`

Behavior:

1. Load user reservations.
2. If none -> `NoSoftReservations`.
3. If active reservations absent or acceptance window exceeded -> `ReservationsExpired`.
4. Commit all reservations.
5. Call order client `PlaceOrderAsync(...)`.
6. If failed -> revert all reservations and return `OrderFailed`.
7. If successful -> return `Success(orderId)`.

Sources:

- `ECommerceApp.Application/Presale/Checkout/Services/CheckoutService.cs`
- `ECommerceApp.Application/Presale/Checkout/Results/CheckoutResult.cs`

---

## API mapping currently implemented

`CheckoutController` maps:

- `Success` -> `200 OK`
- `NoSoftReservations` -> `400 BadRequest`
- `StockUnavailable` -> `409 Conflict`
- `OrderFailed` -> `422 UnprocessableEntity`
- other -> `500`

Important current mismatch:

- `ReservationsExpired` exists in `CheckoutResult`, but is not explicitly mapped in controller and falls into default `500` branch.

Source: `ECommerceApp.API/Controllers/Presale/CheckoutController.cs`.

---

## Integration messages used in this flow

- `OrderPlaced`
- `OrderPlacementFailed`

Produced by:

- `ECommerceApp.Application/Sales/Orders/Services/OrderService.cs`

Consumed in checkout area by:

- `ECommerceApp.Application/Presale/Checkout/Handlers/OrderPlacedHandler.cs`
- `ECommerceApp.Application/Presale/Checkout/Handlers/OrderPlacementFailedHandler.cs`

---

## Rules implemented now

- Checkout initiation requires non-empty cart.
- Active checkout blocks re-initiation.
- Confirm requires active soft reservations.
- Expired reservation window blocks successful confirm.
- Failed order placement triggers reservation revert.
- Successful order placement ends checkout flow.
