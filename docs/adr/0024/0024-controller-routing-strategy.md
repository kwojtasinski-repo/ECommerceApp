# ADR-0024: Controller Routing Strategy for BC Atomic Switches

## Status
Accepted

## Date
2026-03-22

## Context

The ECommerceApp is migrating from a legacy monolithic controller layer to bounded-context-aligned
services via a parallel-change (strangler fig) strategy. The legacy Web controllers are "God Controllers"
that mix multiple BC concerns:

| Legacy controller | Mixes |
|---|---|
| `OrderController` (20 actions) | Cart (Presale), Orders (Sales), Payments (Sales), Fulfillment (Sales) |
| `OrderItemController` (8 actions) | Cart AJAX (Presale), Admin item views (Catalog) |
| `PaymentController` (7 actions) | Payments (Sales), Order lookups (Sales) |

The new BC services have different interfaces, async signatures, and return types compared to the
legacy services. Specifically:

- Legacy `IOrderService.AddOrder(AddOrderDto)` → New `IOrderService.PlaceOrderAsync(PlaceOrderDto)` returning `PlaceOrderResult`
- Legacy `IOrderService.GetOrderDetail(int)` → New `IOrderService.GetOrderDetailsAsync(OrderId)`
- Legacy `IPaymentService.AddPayment(PaymentDto)` → New `IPaymentService.CreatePaymentAsync(CreatePaymentDto)`
- Legacy response shapes use `IsPaid`/`IsDelivered` booleans → New uses `OrderStatus` enum (`Placed`, `Paid`, `Shipped`, `Delivered`, `Cancelled`, `PaymentExpired`)
- Legacy VMs use `int` IDs → New VMs use typed IDs (`OrderId`, `PaymentId`, etc.)

25 Razor views are bound to legacy ViewModels. JavaScript in `ShowMyCart.cshtml` and
`ShowOrdersPaid.cshtml` contains hardcoded AJAX paths to legacy controller actions.

The data stores are completely separate: new BCs use `sales.*` schema tables (via `OrdersDbContext`,
`PaymentsDbContext`) while legacy uses `dbo.*` (via `Context`). Switching a controller from legacy to
new service = switching the data source.

Three routing strategies were evaluated for the Web layer. The API layer has different constraints
(no views, external consumers).

## Decision

### Web layer — ASP.NET Core Areas (parallel routes)

We will create new controllers in ASP.NET Core Areas using a `{Module}/{BC}/{Action}` pattern.
Legacy controllers and routes remain active during the parallel-change period. New views in Area
folders use new ViewModels.

**Physical structure:**

```
Areas/
├── Sales/
│   ├── Controllers/
│   │   ├── OrdersController.cs        ← Application.Sales.Orders.Services.IOrderService
│   │   ├── OrderItemsController.cs    ← Application.Sales.Orders.Services.IOrderItemService
│   │   └── PaymentsController.cs      ← Application.Sales.Payments.Services.IPaymentService
│   └── Views/
│       ├── Orders/
│       │   ├── Index.cshtml
│       │   ├── Details.cshtml
│       │   ├── MyOrders.cshtml
│       │   ├── PaidOrders.cshtml
│       │   └── ByCustomer.cshtml
│       ├── OrderItems/
│       │   ├── Index.cshtml
│       │   ├── ByItem.cshtml
│       │   └── Details.cshtml
│       └── Payments/
│           ├── Index.cshtml
│           ├── Create.cshtml
│           ├── Edit.cshtml
│           ├── Details.cshtml
│           └── MyPayments.cshtml
├── Presale/
│   ├── Controllers/
│   │   └── CheckoutController.cs      ← Application.Presale.Checkout (ICartService, etc.)
│   └── Views/
│       └── Checkout/
│           ├── Cart.cshtml
│           ├── AddItem.cshtml
│           ├── AddOrder.cshtml
│           ├── OrderDetails.cshtml
│           └── Summary.cshtml
└── Identity/                           ← Already exists (Razor Pages, unchanged)
```

**Route mapping (legacy → new):**

| Legacy route | New Area route | Controller | BC |
|---|---|---|---|
| `/Order/Index` | `/Sales/Orders` | `OrdersController.Index` | Sales/Orders |
| `/Order/ShowMyOrders` | `/Sales/Orders/MyOrders` | `OrdersController.MyOrders` | Sales/Orders |
| `/Order/ViewOrderDetails/{id}` | `/Sales/Orders/Details/{id}` | `OrdersController.Details` | Sales/Orders |
| `/Order/EditOrder/{id}` | `/Sales/Orders/Edit/{id}` | `OrdersController.Edit` | Sales/Orders |
| `/Order/ShowOrdersByCustomerId/{id}` | `/Sales/Orders/ByCustomer/{id}` | `OrdersController.ByCustomer` | Sales/Orders |
| `/Order/ShowOrdersPaid` | `/Sales/Orders/PaidOrders` | `OrdersController.PaidOrders` | Sales/Orders |
| `/Order/DispatchOrder` | `/Sales/Orders/Dispatch` | `OrdersController.Dispatch` | Sales/Orders |
| `/Order/OrderRealization/{id}` | `/Sales/Orders/Fulfillment/{id}` | `OrdersController.Fulfillment` | Sales/Orders |
| `/OrderItem/Index` | `/Sales/OrderItems` | `OrderItemsController.Index` | Sales/Orders |
| `/OrderItem/ShowOrderItemsByItemId/{id}` | `/Sales/OrderItems/ByItem/{id}` | `OrderItemsController.ByItem` | Sales/Orders |
| `/OrderItem/ViewOrderItemDetails/{id}` | `/Sales/OrderItems/Details/{id}` | `OrderItemsController.Details` | Sales/Orders |
| `/Payment/Index` | `/Sales/Payments` | `PaymentsController.Index` | Sales/Payments |
| `/Payment/AddPayment/{id}` | `/Sales/Payments/Create/{id}` | `PaymentsController.Create` | Sales/Payments |
| `/Payment/EditPayment/{id}` | `/Sales/Payments/Edit/{id}` | `PaymentsController.Edit` | Sales/Payments |
| `/Payment/ViewPayment/{id}` | `/Sales/Payments/Details/{id}` | `PaymentsController.Details` | Sales/Payments |
| `/Payment/ViewMyPayments` | `/Sales/Payments/MyPayments` | `PaymentsController.MyPayments` | Sales/Payments |
| `/Order/ShowMyCart` | `/Presale/Checkout/Cart` | `CheckoutController.Cart` | Presale/Checkout |
| `/Order/AddOrderItemToCart/{id}` | `/Presale/Checkout/AddItem/{id}` | `CheckoutController.AddItem` | Presale/Checkout |
| `/Order/AddOrder` | `/Presale/Checkout/PlaceOrder` | `CheckoutController.PlaceOrder` | Presale/Checkout |
| `/Order/AddOrderDetails/{id}` | `/Presale/Checkout/OrderDetails/{id}` | `CheckoutController.OrderDetails` | Presale/Checkout |
| `/Order/AddOrderSummary/{id}` | `/Presale/Checkout/Summary/{id}` | `CheckoutController.Summary` | Presale/Checkout |
| `/OrderItem/UpdateOrderItem` | `/Presale/Checkout/UpdateCartItem` | `CheckoutController.UpdateCartItem` | Presale/Checkout |
| `/OrderItem/DeleteOrderItem/{id}` | `/Presale/Checkout/DeleteCartItem/{id}` | `CheckoutController.DeleteCartItem` | Presale/Checkout |

**Routing configuration** — add to `Startup.cs` before the default route:

```csharp
endpoints.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller}/{action=Index}/{id?}");
```

**Navigation links** (`_Layout.cshtml`) will be updated to point to new Area routes when the
corresponding Area controllers are activated. This is done as part of each BC's atomic switch
verification step, not before.

### API layer — hard in-place swap

API controllers at `/api/orders` and `/api/order-items` will have their implementations replaced
in-place. The same route paths are preserved, but the backing services change from legacy to new BC
services.

**Breaking changes (accepted):**

| Change | Before | After |
|---|---|---|
| Order status representation | `"isPaid": true, "isDelivered": false` | `"status": "Paid"` |
| Create order request | `AddOrderDto` | `PlaceOrderDto` with `CartItemIds` |
| Create order response | `int` (order ID) | `PlaceOrderResult` with `OrderId` + `FailureReason` |
| ID types in responses | `int` | Typed ID values (still `int` on wire) |
| Payment fields on order | `paymentId`, `refundId` on order | Separate payment resource |

These are intentional simplifications. The API is internal (consumed by the SPA/admin frontend only).
No external API versioning is required.

### Identity layer — unchanged

The Identity Area (`Areas/Identity/`) uses Razor Pages and is managed by ASP.NET Core Identity
scaffolding. It remains unchanged by this decision.

## Consequences

### Positive
- Legacy controllers stay compilable and deployable as fallback during parallel-change period
- BC concerns are physically separated into Area folders — clear ownership boundaries
- Route structure reflects domain language (`/Sales/Orders/Details` vs `/Order/ViewOrderDetails`)
- New views are free to use new ViewModels without breaking existing pages
- Each BC switch is independently deployable — Orders switch doesn't require Payments switch
- API routes stay stable — only implementation changes, no URL breaks for frontend

### Negative
- During parallel-change period, two route trees serve similar functionality (legacy + Area)
- Navigation links in `_Layout.cshtml` must be updated per-switch, creating a coordination point
- JavaScript AJAX paths in `ShowMyCart.cshtml` and `ShowOrdersPaid.cshtml` need updating when
  Presale/Checkout and Sales/Orders Area controllers go live
- Developers must know which route tree is "active" for each BC during the transition

### Risks & mitigations
- **Risk**: Users bookmark legacy URLs that stop working after cleanup
  - **Mitigation**: Legacy controllers remain active until post-production cleanup. When removed,
    add redirect rules (301) from legacy routes to new Area routes
- **Risk**: AJAX paths in views break when cart moves to Presale Area
  - **Mitigation**: Update AJAX paths in the same PR that activates the Presale/Checkout controller.
    Test with integration tests before merge
- **Risk**: API breaking changes break frontend
  - **Mitigation**: API is internal-only. Frontend and API are deployed together. Update frontend
    JS/views in the same PR as the API controller swap

## Alternatives considered

- **Option A — In-place swap (Web)** — replace legacy controller implementations directly, keep
  same routes. Rejected because legacy controllers mix 4 BCs; swapping one service while keeping
  others creates inconsistent state. Also prevents parallel-change safety (can't keep legacy
  as fallback).

- **Option B — API-first** — build all new functionality as API endpoints, convert Web to SPA.
  Rejected because it's a full rewrite of the frontend layer, far exceeding the scope of the BC
  migration. The existing Razor views work well for the current use case.

## View Reduction Decisions

During the parallel-change implementation phase the initial route mapping produced more views than
needed. The following decisions were made to keep the new Area views lean and purposeful.

### Dropped views (never created or deleted)

| View | Reason |
|---|---|
| `Areas/Sales/Views/Orders/ByCustomer.cshtml` | Duplicate of `Index` — admin can filter by customerId via the search box. No separate route needed. |
| `Areas/Sales/Views/OrderItems/ByItem.cshtml` | Duplicate of `OrderItems/Index` with a search filter. Same reasoning. |
| `Areas/Sales/Views/Payments/Edit.cshtml` | Payments are immutable after creation — only `Confirm` or `Refund` operations exist. No update service method. |
| `Areas/Presale/Views/Checkout/OrderDetails.cshtml` | `CheckoutController.OrderDetails` is a redirect-only action (→ `PlaceOrder`). The view file is never rendered. |

### Kept views with BackOffice deferral note

The following views exist as "nice-to-have" stubs in the current Area controllers. They were kept
because removing them would require touching already-created controllers. They are candidates for
relocation to a future **BackOffice BC** that will consolidate admin management screens:

| View | Current state | BackOffice intention |
|---|---|---|
| `Areas/Sales/Views/Orders/PaidOrders.cshtml` | Active — paged list of paid orders | Could become a filtered tab on the BackOffice Orders dashboard |
| `Areas/Sales/Views/Orders/Edit.cshtml` | Active — edits only `CustomerId` + `CurrencyId` | Very narrow scope; BackOffice will offer richer order management |
| `Areas/Sales/Views/OrderItems/Index.cshtml` | Active — paged admin list | Order items are already shown inline in `Orders/Details`; BackOffice is the better home |
| `Areas/Sales/Views/OrderItems/Details.cshtml` | Active — single item detail | Future: render as modal/dialog inside Orders admin view |

### Checkout/PlaceOrder — customer data prefill from account profile

**Agreed design (2026-03-22):**

The `PlaceOrder` form always submits full human-readable customer data (name, address, phone, etc.).
`customerId` is **never a visible or editable field** for the user.

Flow:

1. `CheckoutController.PlaceOrder` GET calls `IUserProfileService.GetByUserIdAsync(userId)` to
   retrieve the user's saved profile data.
2. Profile data is passed to the view and **prefills** all checkout form fields (firstName, lastName,
   email, phoneNumber, address fields). User can edit any field before submitting.
3. Profile management lives in the **Account/Profile** section (`UserProfileService`) — the checkout
   form borrows from it but never manages it. No "save address" button on the checkout form itself.
4. On POST, all form data is sent. `customerId` resolution is handled server-side (pending design —
   see open item below).
5. `currencyId` will be resolved from a default or user preference, not a manual input.

**Open item**: How `customerId` (Sales BC integer ID) is resolved server-side from the authenticated
`userId` needs a concrete implementation decision before the switch goes live. Options:
- Resolve via `IUserProfileService` (UserProfile.Id ≈ customerId if IDs are aligned)
- Add a dedicated `ICustomerResolver.GetOrCreateAsync(userId)` contract in the Checkout BC
- Keep as a hidden field pre-populated by the controller from profile data
This is tracked in `orders-atomic-switch.md` Step 3a.

### API layer — temporary closure and replacement strategy

**Agreed decision (2026-03-22):**

The legacy API controllers (`/api/orders`, `/api/order-items`, `/api/payments`) will be **completely
replaced** — not updated in-place — as part of the atomic switch. The affected 1–2 external
consumers have been notified and accepted a ~1 day downtime window during the cutover.

Full API scope discussion (what endpoints to expose and their contract shapes) is deferred to a
separate session before the API replacement PR is opened. No API controller changes are made until
that discussion is complete.

**Do NOT modify any API controllers** until the API scope session is done and documented.

The current `Payments/Create` and `Payments/Details` views are standalone. The agreed future
direction is to surface payment status and the confirm-payment action directly within
`Orders/Details`, so users access payment information through their order rather than navigating to
a separate payment route. `MyPayments` remains a stub until `IPaymentService.GetByUserIdAsync` is
implemented.

### AddItem (Presale/Checkout) — inline product action (deferred to post-switch)

`Presale/Checkout/AddItem` exists as a standalone view (no quantity input — one click = one item added).
This standalone route is a POC fallback only. When the frontend is wired **after the BC atomic switch**,
the following agreed design applies:

- **Trigger point**: `V2Product/Index` (product listing) and `V2Product/Details` (product detail page)
  each get an inline "Dodaj do koszyka" button. No separate navigation to an `AddItem` page.
- **Mechanism**: AJAX POST — user stays on the current page; no full navigation occurs.
- **Endpoint**: POST to `CheckoutController.UpdateCartItem(productId, quantity: 1)` — this action
  already returns `Ok()` and is AJAX-compatible. No new endpoint needed.
- **Anti-forgery**: The JS caller must send the `__RequestVerificationToken` header (read from the
  meta tag or hidden field already rendered by `_Layout.cshtml`). `[ValidateAntiForgeryToken]` will
  be added to `UpdateCartItem` when the frontend is wired.
- **Success feedback**: Toast notification + button text change ("Dodano ✓") to confirm the item
  was added without the user leaving the page. Silent failure is not acceptable.
- **`AddItem` view fate**: Once the inline buttons are live, `Presale/Checkout/AddItem.cshtml` and
  the `CheckoutController.AddItem` GET/POST actions can be removed in the legacy cleanup pass.

> ⏱ **Do NOT implement** `V2Product` button wiring until the BC atomic switch is complete and
> the new Presale/Checkout controller is the active route. Track in frontend wiring task.

---

## References

- [ADR-0014](0014-sales-orders-bc-design.md) — Sales/Orders BC Design
- [ADR-0015](0015-sales-payments-bc-design.md) — Sales/Payments BC Design
- [Orders atomic switch roadmap](../roadmap/orders-atomic-switch.md)
- [Payments atomic switch roadmap](../roadmap/payments-atomic-switch.md)
- [Bounded context map](../architecture/bounded-context-map.md)
- [ASP.NET Core Areas documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/areas)
