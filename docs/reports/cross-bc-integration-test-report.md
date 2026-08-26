# Cross-BC Integration Flow Report

> **Living document.** Updated after BC implementation milestones.
> All flows reflect the current implementation state after P0–P3 design amendments.
> For BC status see [`project-state.md`](../../.github/context/project-state.md).
> For roadmap see [`docs/roadmap/README.md`](../roadmap/README.md).

*Last updated: 2026-03-28*

---

## Flow 1: Authentication & Identity

```
┌─────────────────────────────────────────────────────────────┐
│                 FLOW 1: AUTHENTICATION                       │
└─────────────────────────────────────────────────────────────┘

  ┌──────────┐                 ┌─────────────────┐
  │  Browser  │                 │  Identity/IAM    │
  │  (User)   │                 │  BC              │
  └─────┬─────┘                 └────────┬────────┘
        │                                │
        │  GET /Account/Login            │
        ├───────────────────────────────►│
        │                                │
        │  ◄── Login form ──────────────┤
        │                                │
        │  POST /Account/Login           │
        │  {email, password}             │
        ├───────────────────────────────►│
        │                                │
        │                   ┌────────────┴────────────┐
        │                   │ AuthenticationService    │
        │                   │  .SignInAsync()          │
        │                   │                          │
        │                   │  SignInManager            │
        │                   │   .PasswordSignInAsync() │
        │                   │                          │
        │                   │  ┌─ SUCCESS ────────┐   │
        │                   │  │ Set auth cookie   │   │
        │                   │  │ Redirect to /     │   │
        │                   │  └──────────────────┘   │
        │                   │                          │
        │                   │  ┌─ FAILURE ────────┐   │
        │                   │  │ "Invalid creds"   │   │
        │                   │  │ Re-show form      │   │
        │                   │  └──────────────────┘   │
        │                   └─────────────────────────┘
        │                                │
        │  ◄── Redirect / Error ────────┤
        ▼                                ▼

  ┌──────────────────────────────────────────────────┐
  │  API LOGIN (JWT):                                 │
  │                                                   │
  │  POST /api/Login                                  │
  │  {email, password}                                │
  │       │                                           │
  │       ▼                                           │
  │  AuthenticationService.SignInAsync()               │
  │       │                                           │
  │       ├─ Success → JwtManager.IssueToken()        │
  │       │            return {token, ""}              │
  │       │                                           │
  │       └─ Failure → BusinessException              │
  │                    → ExceptionMiddleware           │
  │                    → 400 ExceptionResponse         │
  └──────────────────────────────────────────────────┘
```

**Status**: ✅ Complete — no cross-BC events involved.

---

## Flow 2: Admin Setup — Products, Inventory, Coupons

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 2: ADMIN/OPERATOR SETUP                        │
└─────────────────────────────────────────────────────────────┘

  ┌───────────┐     ┌──────────┐    ┌───────────┐   ┌──────────┐
  │   Admin    │     │ Catalog  │    │ Inventory │   │ Coupons  │
  │ (Browser)  │     │ BC       │    │ BC        │   │ BC       │
  └─────┬──────┘     └────┬─────┘    └─────┬─────┘   └────┬─────┘
        │                  │               │               │
        │ ── STEP 1: Create Category ─────────────────────────
        │ POST V2Category/Add             │               │
        ├─────────────────►│              │               │
        │     CategoryService             │               │
        │     .AddCategory()              │               │
        │◄─── CategoryId ──┤              │               │
        │                  │               │               │
        │ ── STEP 2: Create Tags ──────────────────────────────
        │ POST V2Tag/Add                  │               │
        ├─────────────────►│              │               │
        │     ProductTagService           │               │
        │     .AddTag()                   │               │
        │◄─── TagId ───────┤              │               │
        │                  │               │               │
        │ ── STEP 3: Create Product ───────────────────────────
        │ POST V2Product/Add              │               │
        ├─────────────────►│              │               │
        │     ProductService              │               │
        │     .AddProduct()               │               │
        │◄─── ProductId ───┤              │               │
        │                  │               │               │
        │ ── STEP 4: Publish Product ──────────────────────────
        │ POST api/v2/catalog/{id}/publish│               │
        ├─────────────────►│              │               │
        │     product.Publish()           │               │
        │     publish ─────┼─ProductPublished──►│         │
        │                  │              │  Inventory:    │
        │                  │              │  ProductPublished│
        │                  │              │  Handler creates │
        │                  │              │  ProductSnapshot │
        │                  │              │               │
        │ ── STEP 5: Initialize Stock ─────────────────────────
        │ POST api/v2/inventory/init      │               │
        ├──────────────────┼─────────────►│               │
        │                  │   StockService               │
        │                  │   .InitializeStockAsync()    │
        │                  │   Create StockItem(qty)      │
        │                  │   Audit: Initialized         │
        │                  │   publish ──StockAvailability │
        │                  │             Changed──►Presale │
        │                  │              │               │
        │ ── STEP 6: Create Coupon ────────────────────────────
        │ POST api/coupons                │               │
        ├──────────────────┼──────────────┼──────────────►│
        │                  │              │  CouponService │
        │                  │              │  Coupon.Create │
        │                  │              │  (code, %, desc)
        │◄── couponId ─────┼──────────────┼───────────────┤
        ▼                  ▼              ▼               ▼
```

**Status**: ✅ Complete.

---

## Flow 3: Customer Buying — Happy Path

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 3: HAPPY PATH — BROWSE → BUY → DELIVER        │
└─────────────────────────────────────────────────────────────┘

  ┌────────┐   ┌─────────┐  ┌────────┐  ┌────────┐  ┌──────────┐  ┌───────────┐
  │  User  │   │ Presale │  │ Orders │  │Payments│  │Inventory │  │Fulfillment│
  └───┬────┘   └────┬────┘  └───┬────┘  └───┬────┘  └─────┬────┘  └─────┬─────┘
      │              │           │           │             │              │
      │ ── BROWSE ──────────────────────────────────────────────────────────
      │ GET /api/storefront/products         │             │              │
      ├─────────────►│           │           │             │              │
      │  StorefrontQueryService              │             │              │
      │  .GetPublishedProducts()             │             │              │
      │  (CatalogClient + StockSnapshots)    │             │              │
      │◄─ products ──┤           │           │             │              │
      │  (with availability)     │           │             │              │
      │              │           │           │             │              │
      │ ── ADD TO CART ─────────────────────────────────────────────────────
      │ POST /api/v2/cart        │           │             │              │
      ├─────────────►│           │           │             │              │
      │  CartService.AddOrUpdateAsync()      │             │              │
      │  CartLine.Create(userId, productId, qty)           │              │
      │◄── 200 OK ───┤           │           │             │              │
      │              │           │           │             │              │
      │ ── INITIATE CHECKOUT ──────────────────────────────────────────────
      │ POST /api/v2/checkout/initiate       │             │              │
      ├─────────────►│           │           │             │              │
      │  CheckoutService.InitiateAsync()     │             │              │
      │    │                     │           │             │              │
      │    ├─ foreach cart line:             │             │              │
      │    │  SoftReservationService.HoldAsync()           │              │
      │    │  (captures UnitPrice — immutable)             │              │
      │    │                     │           │             │              │
      │    ├─ Clear reserved lines from cart  │             │              │
      │    │                     │           │             │              │
      │◄── InitiateCheckoutResult            │             │              │
      │    (reserved N, unavailable M)       │             │              │
      │              │           │           │             │              │
      │ ── PLACE ORDER ────────────────────────────────────────────────────
      │ POST /api/v2/checkout/place-order    │             │              │
      ├─────────────►│           │           │             │              │
      │  CheckoutService.PlaceOrderAsync()   │             │              │
      │    │                     │           │             │              │
      │    ├─ CommitAllForUser() │           │             │              │
      │    ├─ OrderClient.PlaceOrderAsync()  │             │              │
      │    │  ──────────────────►│           │             │              │
      │    │    Order.Create()   │           │             │              │
      │    │    CalculateCost()  │           │             │              │
      │    │    publish ─────────┼─ OrderPlaced ─────────────────────────
      │    │                     │     │     │       │     │        │
      │    │                     │     ▼     │       │     ▼        │
      │    │                     │  Payments:│       │  Inventory:  │
      │    │                     │  Create   │       │  Reserve     │
      │    │                     │  Payment  │       │  StockHold   │
      │    │                     │  (pending)│       │  + schedule  │
      │    │                     │           │       │  timeout job │
      │    │                     │     ▼     │       │              │
      │    │                     │  Presale: │       │              │
      │    │                     │  clear    │       │              │
      │    │                     │  commit'd │       │              │
      │    │                     │  reserv's │       │              │
      │    │                     │     ▼     │       │              │
      │    │                     │  Orders:  │       │              │
      │    │                     │  Snapshot │       │              │
      │    │                     │  Handler  │       │              │
      │◄── CheckoutResult(orderId)           │             │              │
      │              │           │           │             │              │
      │ ── PAY ────────────────────────────────────────────────────────────
      │ POST /api/v2/payments/confirm        │             │              │
      ├──────────────┼──────────►│           │             │              │
      │              │  payment.Confirm()    │             │              │
      │              │  publish ─┼─ PaymentConfirmed ──────────────────
      │              │           │     │     │       │     │        │
      │              │           │     ▼     │       │     ▼        │
      │              │           │  Orders:  │       │  Inventory:  │
      │              │           │  order    │       │  Confirm all │
      │              │           │  .Confirm │       │  StockHolds  │
      │              │           │  Payment()│       │  for order   │
      │              │           │           │             │              │
      │ ── SHIPMENT (OPERATOR) ────────────────────────────────────────────
      │              │           │           │             │              │
      │  Operator creates shipment + marks InTransit      │              │
      │              │           │           │             │  Fulfillment:│
      │              │           │           │             │  publish     │
      │              │           │  ◄─ShipmentDispatched───────────┤     │
      │              │           │  order.RecordShipment   │              │
      │              │           │  Dispatched()           │              │
      │              │           │           │             │              │
      │  Operator marks shipment Delivered   │             │              │
      │              │           │           │             │  Fulfillment:│
      │              │           │           │             │  publish     │
      │              │           │           │             │ShipmentDeliv.│
      │              │           │           │             │ (Items[])    │
      │              │           │           │             │      │       │
      │              │      ┌────┴───────────┴─────────────┴──────┤       │
      │              │      │         PARALLEL FAN-OUT (P0)       │       │
      │              │      │                                     │       │
      │              │      │  ┌─ Orders ──────────────────────┐  │       │
      │              │      │  │ OrderShipmentDeliveredHandler │  │       │
      │              │      │  │ order.Fulfill()               │  │       │
      │              │      │  └───────────────────────────────┘  │       │
      │              │      │                                     │       │
      │              │      │  ┌─ Inventory ───────────────────┐  │       │
      │              │      │  │ ShipmentDeliveredHandler      │  │       │
      │              │      │  │ foreach item in Items[]:      │  │       │
      │              │      │  │   stock.FulfillAsync()        │  │       │
      │              │      │  │   hold.Fulfilled              │  │       │
      │              │      │  │   audit + StockAvailability   │  │       │
      │              │      │  │   Changed → Presale           │  │       │
      │              │      │  │ on failure:                   │  │       │
      │              │      │  │   → StockReconciliation       │  │       │
      │              │      │  │     Required (alert)          │  │       │
      │              │      │  └───────────────────────────────┘  │       │
      │              │      └─────────────────────────────────────┘       │
      ▼              ▼           ▼           ▼             ▼              ▼
                                    ✅ ORDER COMPLETE
```

**Status**: ✅ Complete — parallel fan-out to Orders + Inventory via `ShipmentDelivered(Items[])`. `StockReconciliationRequired` alert on failures. Legacy `OrderShippedHandler` exists for backward compat, retired after switch.

---

## Flow 4: Payment Timeout (Abandoned Cart)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 4: PAYMENT WINDOW EXPIRES                      │
└─────────────────────────────────────────────────────────────┘

  ┌──────┐   ┌─────────┐  ┌────────┐  ┌────────┐  ┌──────────┐
  │ User │   │ Presale │  │ Orders │  │Payments│  │Inventory │
  └──┬───┘   └────┬────┘  └───┬────┘  └───┬────┘  └────┬─────┘
     │             │           │           │            │
     │ (user placed order but never pays)  │            │
     │             │           │           │            │
     │  ⏰ TimeManagement triggers PaymentWindowExpiredJob
     │             │           │           │            │
     │             │           │     PaymentWindow      │
     │             │           │     ExpiredJob runs    │
     │             │           │     payment.Expire()   │
     │             │           │     publish ────────────────────
     │             │           │     PaymentExpired     │
     │             │           │           │     │      │
     │             │           │     ◄─────┘     │      │
     │             │           │                 │      │
     │             │     Orders:                 │      │
     │             │     OrderPaymentExpired      │      │
     │             │     Handler                  │      │
     │             │     order.ExpirePayment()    │      │
     │             │     (Status → Cancelled)     │      │
     │             │     publish OrderCancelled ──┼──────┼──
     │             │           │           │     │      │  │
     │             │           │           │     │      │  │
     │             │     ◄─────┘           │     │      ▼  │
     │             │                       │     │ Inventory│
     │             │                       │     │ Release  │
     │             │                       │     │ stock per│
     │             │                       │     │ item     │
     │             │                       │     │          │
     │             │  Coupons: ◄─OrderCancelled──┘         │
     │             │  Release coupon (if applied)           │
     │             │                       │               │
     │             │                       │     Inventory's release above publishes
     │             │                       │     StockAvailabilityChanged ──────────►│
     │             │                       │               │
     │             │  Presale: ◄─StockAvailabilityChanged──┘
     │             │  Update StockSnapshot                 │
     ▼             ▼           ▼           ▼               ▼
                         ORDER CANCELLED — STOCK RETURNED
```

**Status**: ✅ Complete — event chain: `PaymentExpired` → `OrderCancelled` → Inventory + Coupons. ⚠️ 3-deep handler chain (F4 candidate).
**Update (2026-08-26)**: at the time this flow was drawn, Inventory also ran a second, redundant
`PaymentWindowTimeoutJob` timer that released the same stock independently. That job was retired
2026-08-17 (`payments-atomic-switch.md` Step 6) as a duplicate, always-racing path; the diagram
above now shows the single remaining path.

---

## Flow 5: Coupon Application

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 5: APPLY COUPON TO ORDER                       │
└─────────────────────────────────────────────────────────────┘

  ┌──────┐       ┌─────────┐       ┌────────┐
  │ User │       │ Coupons │       │ Orders │
  └──┬───┘       └────┬────┘       └───┬────┘
     │                 │               │
     │ POST /api/coupons/apply         │
     │ {couponCode, orderId}           │
     ├────────────────►│               │
     │                 │               │
     │    CouponService.ApplyCouponAsync()
     │    │                            │
     │    ├─ OrderExistenceChecker.ExistsAsync()
     │    │  (cross-BC adapter → IOrderService)
     │    │                            │
     │    ├─ Coupon.GetByCodeAsync()    │
     │    │                            │
     │    │  ┌─ CHECKS ─────────────┐  │
     │    │  │ coupon exists?        │  │
     │    │  │ status == Available?  │  │
     │    │  │ order has no coupon?  │  │
     │    │  └──────────────────────┘  │
     │    │                            │
     │    ├─ Coupon.MarkAsUsed()       │
     │    ├─ CouponUsed.Create()       │
     │    ├─ publish ──────────────────┼── CouponApplied
     │    │                            │       │
     │    │                            │  Orders:
     │    │                            │  OrderCouponApplied
     │    │                            │  Handler
     │    │                            │  order.AssignCoupon()
     │    │                            │  (recalculates cost)
     │◄── CouponApplyResult.Applied    │
     │                 │               │
     │  ── SLICE 2 (rule engine) ──────────────────────
     │  CouponWorkflowBuilder         │
     │  → CouponRuleRegistry          │
     │  → CouponRulePipeline          │
     │  → 16 rules total:             │
     │                                 │
     │  ┌─ Constraint (7) ──────────┐  │
     │  │ MinOrderValue             │  │
     │  │ ValidDateRange            │  │
     │  │ MaxUses                   │  │
     │  │ MaxUsesPerUser            │  │
     │  │ FirstPurchaseOnly         │  │
     │  │ SpecialEvent              │  │
     │  │ CouponOversizeGuard ✅    │  │
     │  │  (auto-injected, always   │  │
     │  │   on; per-coupon bypass   │  │
     │  │   via BypassOversizeGuard)│  │
     │  └───────────────────────────┘  │
     │                                 │
     │  ┌─ Scope (4) ───────────────┐  │
     │  │ OrderTotal                │  │
     │  │ PerProduct                │  │
     │  │ PerCategory               │  │
     │  │ PerTag                    │  │
     │  └───────────────────────────┘  │
     │                                 │
     │  ┌─ Discount (5) ────────────┐  │
     │  │ PercentageOff             │  │
     │  │ FixedAmountOff            │  │
     │  │ FreeItem                  │  │
     │  │ FreeCheapestItem          │  │
     │  │ GiftProduct               │  │
     │  └───────────────────────────┘  │
     ▼                 ▼               ▼
```

**Status**: ✅ Complete — 15 evaluators + 1 auto-injected `CouponOversizeGuard` = 16 total.

---

## Flow 6: Refund Flow

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 6: REFUND REQUEST → APPROVAL                   │
└─────────────────────────────────────────────────────────────┘

  ┌──────┐   ┌───────────┐  ┌────────┐  ┌────────┐  ┌──────────┐
  │ User │   │Fulfillment│  │ Orders │  │Payments│  │Inventory │
  └──┬───┘   └─────┬─────┘  └───┬────┘  └───┬────┘  └────┬─────┘
     │              │            │           │            │
     │ POST api/v2/refunds/request          │            │
     ├─────────────►│            │           │            │
     │  RefundService                       │            │
     │  .RequestRefundAsync()               │            │
     │    │                      │           │            │
     │    ├─ OrderExistenceChecker          │            │
     │    ├─ Refund.Create(orderId, items)  │            │
     │◄── RefundRequestResult    │           │            │
     │              │            │           │            │
     │  (operator approves)      │           │            │
     │              │            │           │            │
     │  RefundService.ApproveAsync()        │            │
     │    refund.Approve()       │           │            │
     │    publish ───────────────┼─ RefundApproved ──────────
     │              │            │     │     │      │     │
     │              │            │     ▼     │      ▼     │
     │              │         Orders:  │  Payments:  │    │
     │              │         Assign   │  Issue       │    │
     │              │         Refund   │  Refund      │    ▼
     │              │         to order │  on payment  │ Inventory:
     │              │            │     │     │        │ Return
     │              │            │     │     │        │ stock per
     │              │            │     │     │        │ item
     │              │            │     │     │        │ audit +
     │              │            │     │     │        │ StockAvail
     │              │            │     │     │        │ Changed
     ▼              ▼            ▼     ▼     ▼        ▼
                            REFUND COMPLETE
```

**Status**: ✅ Complete — 3 consumers: Orders, Payments, Inventory.

---

## Flow 7: Shipment Failure ✅ (Fixed — was gap)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 7: SHIPMENT FAILS  ✅ FIXED (P0)              │
└─────────────────────────────────────────────────────────────┘

  ┌──────────┐   ┌───────────┐  ┌────────┐  ┌──────────┐
  │ Operator │   │Fulfillment│  │ Orders │  │Inventory │
  └────┬─────┘   └─────┬─────┘  └───┬────┘  └────┬─────┘
       │               │            │             │
       │ Mark shipment as failed    │             │
       ├──────────────►│            │             │
       │  shipment.MarkAsFailed()   │             │
       │  publish ──────┼─ShipmentFailed ─────────────
       │               │  (Items[]: ProductId,    │
       │               │   Quantity per line)      │
       │               │      │     │        │    │
       │          ┌────┴──────┴─────┴────────┤    │
       │          │    PARALLEL FAN-OUT (P0)  │    │
       │          │                          │    │
       │          │  ┌─ Orders ────────────┐ │    │
       │          │  │ OrderShipmentFailed  │ │    │
       │          │  │ Handler             │ │    │
       │          │  │ order.RecordShipment │ │    │
       │          │  │ Failure()           │ │    │
       │          │  │ (appends            │ │    │
       │          │  │  ShipmentFailure    │ │    │
       │          │  │  Payload)           │ │    │
       │          │  │ publish ─────────────────────
       │          │  │ OrderRequiresAttention      │
       │          │  │ (operator notification;     │
       │          │  │  no consumer until          │
       │          │  │  Communication BC)          │
       │          │  └─────────────────────┘ │    │
       │          │                          │    │
       │          │  ┌─ Inventory ─────────┐ │    │
       │          │  │ ShipmentFailed       │ │    │
       │          │  │ Handler             │ │    │
       │          │  │ foreach item:       │ │    │
       │          │  │   stock.ReleaseAsync │ │    │
       │          │  │   hold → Released   │ │    │
       │          │  │   audit entry       │ │    │
       │          │  │   StockAvailability  │ │    │
       │          │  │   Changed → Presale │ │    │
       │          │  │ on failure:         │ │    │
       │          │  │   → StockRecon-     │ │    │
       │          │  │     ciliation       │ │    │
       │          │  │     Required        │ │    │
       │          │  │     (Release type)  │ │    │
       │          │  └─────────────────────┘ │    │
       │          └──────────────────────────┘    │
       ▼               ▼            ▼             ▼
                  SHIPMENT FAILED — STOCK RELEASED
```

**Status**: ✅ Complete (P0 fix) — was ❌ MISSING. `ShipmentFailed(Items[])` → parallel fan-out. Stock released. `StockReconciliationRequired` on failures.

---

## Flow 8: Partial Delivery ✅ (New — was gap)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 8: SHIPMENT PARTIALLY DELIVERED ✅ (P0)        │
└─────────────────────────────────────────────────────────────┘

  ┌──────────┐   ┌───────────┐  ┌────────┐  ┌──────────┐
  │ Operator │   │Fulfillment│  │ Orders │  │Inventory │
  └────┬─────┘   └─────┬─────┘  └───┬────┘  └────┬─────┘
       │               │            │             │
       │ Mark shipment partially delivered        │
       ├──────────────►│            │             │
       │  shipment.MarkAsPartiallyDelivered()     │
       │  publish ──────┼─ShipmentPartially ──────────
       │               │  Delivered               │
       │               │  (DeliveredItems[],       │
       │               │   FailedItems[])          │
       │               │      │     │        │    │
       │          ┌────┴──────┴─────┴────────┤    │
       │          │    PARALLEL FAN-OUT (P0)  │    │
       │          │                          │    │
       │          │  ┌─ Orders ────────────┐ │    │
       │          │  │ OrderShipmentPartial │ │    │
       │          │  │ DeliveredHandler     │ │    │
       │          │  │ order.RecordPartial  │ │    │
       │          │  │ Fulfilment()         │ │    │
       │          │  │ (PartialFulfilment   │ │    │
       │          │  │  Payload: ShipmentId │ │    │
       │          │  │  + FailedItems)      │ │    │
       │          │  │ publish ─────────────────────
       │          │  │ OrderRequiresAttention      │
       │          │  └─────────────────────┘ │    │
       │          │                          │    │
       │          │  ┌─ Inventory ─────────┐ │    │
       │          │  │ ShipmentPartially    │ │    │
       │          │  │ DeliveredHandler     │ │    │
       │          │  │                     │ │    │
       │          │  │ foreach delivered:  │ │    │
       │          │  │   stock.FulfillAsync │ │    │
       │          │  │   hold → Fulfilled  │ │    │
       │          │  │                     │ │    │
       │          │  │ foreach failed:     │ │    │
       │          │  │   stock.ReleaseAsync │ │    │
       │          │  │   hold → Released   │ │    │
       │          │  │                     │ │    │
       │          │  │ audit + StockAvail  │ │    │
       │          │  │ Changed → Presale   │ │    │
       │          │  │                     │ │    │
       │          │  │ on failure:         │ │    │
       │          │  │   → StockRecon-     │ │    │
       │          │  │     ciliation       │ │    │
       │          │  │     Required        │ │    │
       │          │  │     (mixed Fulfill  │ │    │
       │          │  │      + Release)     │ │    │
       │          │  └─────────────────────┘ │    │
       │          └──────────────────────────┘    │
       ▼               ▼            ▼             ▼
            PARTIAL DELIVERY — FULFILLED + RELEASED
```

**Status**: ✅ Complete (P0 fix) — was ❌ MISSING. Inventory handles both Fulfill + Release. `StockReconciliationRequired` with mixed `StockOperationType` on failures.

---

## Flow 9: Coupon Oversize Guard ✅ (Fixed — was gap)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 9: COUPON OVERSIZE GUARD ✅ (P2)               │
└─────────────────────────────────────────────────────────────┘

  ┌──────┐            ┌─────────────────────────────┐
  │ User │            │   Coupons BC (Slice 2)       │
  └──┬───┘            └──────────────┬──────────────┘
     │                               │
     │  Apply coupon "HOLIDAY50"     │
     │  (fixed-amount-off $50)       │
     │  Order total: $25             │
     ├──────────────────────────────►│
     │                               │
     │     CouponWorkflowBuilder     │
     │     builds pipeline:          │
     │       1. ValidDateRange  ✅   │
     │       2. MaxUses         ✅   │
     │       3. MinOrderValue   ✅   │
     │       4. OrderTotal scope ✅  │
     │       5. FixedAmountOff       │
     │       6. CouponOversizeGuard  │
     │          (auto-injected ✅)    │
     │                               │
     │  ┌─ GUARD EVALUATION ───────┐ │
     │  │ coupon.amount ($50) >    │ │
     │  │ order.total ($25)?       │ │
     │  │                          │ │
     │  │ ── IF BypassOversizeGuard│ │
     │  │    == false (default):   │ │
     │  │    → FAIL                │ │
     │  │    "Coupon value $50     │ │
     │  │     exceeds order total  │ │
     │  │     $25"                 │ │
     │  │                          │ │
     │  │ ── IF BypassOversizeGuard│ │
     │  │    == true (per-coupon): │ │
     │  │    → PASS                │ │
     │  │    Math.Min(50,25) = $25 │ │
     │  │    reduction applied     │ │
     │  │    (user "wastes" $25    │ │
     │  │     of coupon value)     │ │
     │  └──────────────────────────┘ │
     │                               │
     │◄── Result: Rejected or Applied│
     ▼                               ▼
```

**Status**: ✅ Complete (P2 fix) — was ❌ MISSING. Always-on, per-coupon `BypassOversizeGuard` override.

---

## Flow 10: Catalog → Coupons Name Sync ✅ (New — was gap)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 10: CATALOG → COUPONS NAME SYNC ✅ (P3)        │
└─────────────────────────────────────────────────────────────┘

  ┌──────────┐   ┌──────────┐       ┌──────────┐
  │  Admin   │   │ Catalog  │       │ Coupons  │
  └────┬─────┘   └────┬─────┘       └────┬─────┘
       │               │                  │
       │ ── Rename Product ────────────────────────
       │ PUT api/v2/catalog/{id}          │
       ├──────────────►│                  │
       │  product.UpdateName()            │
       │  publish ──────┼─ProductNameChanged
       │               │         │        │
       │               │         ▼        │
       │               │    Coupons:      │
       │               │    ProductName   │
       │               │    ChangedHandler│
       │               │    scopeTarget   │
       │               │    .UpdateNameAsync()
       │               │                  │
       │ ── Rename Category ───────────────────────
       │ PUT V2Category/Edit              │
       ├──────────────►│                  │
       │  publish ──────┼─CategoryNameChanged
       │               │         │        │
       │               │         ▼        │
       │               │    CategoryName  │
       │               │    ChangedHandler│
       │               │    scopeTarget   │
       │               │    .UpdateNameAsync()
       │               │                  │
       │ ── Rename Tag ────────────────────────────
       │ PUT V2Tag/Edit                   │
       ├──────────────►│                  │
       │  publish ──────┼─TagNameChanged  │
       │               │         │        │
       │               │         ▼        │
       │               │    TagName       │
       │               │    ChangedHandler│
       │               │    scopeTarget   │
       │               │    .UpdateNameAsync()
       ▼               ▼                  ▼
          SCOPE TARGET NAMES KEPT IN SYNC
```

**Status**: ✅ Complete (P3 fix) — was ❌ MISSING. 3 messages + 3 handlers + `IScopeTargetRepository`.

---

## Flow 11: Stock Reconciliation Alert ✅ (New)

```
┌─────────────────────────────────────────────────────────────┐
│          FLOW 11: STOCK RECONCILIATION ALERT ✅ (NEW)        │
└─────────────────────────────────────────────────────────────┘

  ┌──────────┐   ┌──────────┐       ┌───────────────┐
  │Fulfillment│  │Inventory │       │ Communication │
  │ publishes │  │ handlers │       │ BC (future)   │
  └─────┬─────┘  └────┬─────┘       └──────┬────────┘
        │              │                    │
        │ ShipmentDelivered / ShipmentFailed│
        │ / ShipmentPartiallyDelivered      │
        ├─────────────►│                    │
        │              │                    │
        │  Inventory handler processes      │
        │  stock operations (Fulfill/Release)
        │              │                    │
        │  ┌─ IF ANY OPERATION FAILS ────┐  │
        │  │                             │  │
        │  │  Collect failures:          │  │
        │  │  StockOperationFailure[]    │  │
        │  │    .ProductId               │  │
        │  │    .Quantity                │  │
        │  │    .OperationType           │  │
        │  │      (Fulfill | Release)    │  │
        │  │                             │  │
        │  │  Publish:                   │  │
        │  │  StockReconciliationRequired│  │
        │  │    .OrderId                 │  │
        │  │    .Failures[]             │  │
        │  │    .OccurredAt             │  │
        │  │                             │  │
        │  │  No consumer yet (alert)    │  │
        │  │  Future: Communication BC ──┼──►
        │  │  → operator notification    │  │
        │  └─────────────────────────────┘  │
        ▼              ▼                    ▼
         ALERT PUBLISHED — MANUAL RECONCILIATION
```

**Status**: ✅ Complete — Inventory-owned alert with `StockOperationType` discriminator. Published by all 3 shipment handlers.

---

## Integration Coverage Matrix

| Event | Publisher | Consumers | Status |
|---|---|---|---|
| `OrderPlaced` | Orders | Payments, Inventory, Presale, Orders(snapshot) | ✅ |
| `PaymentConfirmed` | Payments | Orders, Inventory | ✅ |
| `PaymentExpired` | Payments | Orders | ✅ |
| `OrderCancelled` | Orders | Inventory, Coupons | ✅ |
| `OrderShipped` | Orders | Inventory | ⚠️ Legacy (retire after switch) |
| `CouponApplied` | Coupons | Orders | ✅ |
| `CouponRemovedFromOrder` | Coupons | Orders | ✅ |
| `OrderPriceAdjusted` | Coupons | Orders | ✅ |
| `RefundApproved` | Fulfillment | Orders, Payments, Inventory | ✅ |
| `RefundRejected` | Fulfillment | Orders | ✅ |
| `ShipmentDispatched` | Fulfillment | Orders | ✅ |
| `ShipmentDelivered` | Fulfillment | Orders, Inventory | ✅ P0 |
| `ShipmentFailed` | Fulfillment | Orders, Inventory | ✅ P0 |
| `ShipmentPartiallyDelivered` | Fulfillment | Orders, Inventory | ✅ P0 |
| `ProductPublished` | Catalog | Inventory | ✅ |
| `ProductUnpublished` | Catalog | Inventory | ✅ |
| `ProductDiscontinued` | Catalog | Inventory | ✅ |
| `ProductNameChanged` | Catalog | Coupons | ✅ P3 |
| `CategoryNameChanged` | Catalog | Coupons | ✅ P3 |
| `TagNameChanged` | Catalog | Coupons | ✅ P3 |
| `StockAvailabilityChanged` | Inventory | Presale | ✅ |
| `StockReconciliationRequired` | Inventory | — (alert, future Comms) | ✅ NEW |
| `OrderRequiresAttention` | Orders | — (future Comms) | ✅ NEW |

### Scorecard

| Metric | Value |
|---|---|
| Total event flows | 27 |
| ✅ Complete | 26 |
| ⚠️ Legacy (to retire) | 1 |
| ❌ Missing | 0 |
