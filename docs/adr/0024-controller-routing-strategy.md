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

## Migration plan

Implementation follows the atomic switch roadmaps:

1. **Orders switch** (`docs/roadmap/orders-atomic-switch.md`) — Steps 3–4 create Area controllers
   in `Areas/Sales/Controllers/` for Orders and OrderItems. Step 4 swaps API controllers in-place.
2. **Payments switch** (`docs/roadmap/payments-atomic-switch.md`) — Step 3 creates
   `Areas/Sales/Controllers/PaymentsController.cs` and swaps API payment endpoints.
3. **Presale/Checkout** — Creates `Areas/Presale/Controllers/CheckoutController.cs` with cart,
   add-to-cart, and place-order actions (unblocked after Orders switch).
4. **Startup.cs** — Area route registration is added in the first switch that creates an Area
   controller (Orders switch Step 3).
5. **`_Layout.cshtml`** — Navigation links updated per-switch during the verification step.
6. **Legacy cleanup** — Legacy controllers, views, and routes removed after production validation
   of all switches.

## Conformance checklist

- [ ] Area controllers use `[Area("Sales")]` or `[Area("Presale")]` attribute
- [ ] Area controllers inject new BC services (`Application.Sales.Orders.Services.IOrderService`),
      not legacy services (`Application.Services.Orders.IOrderService`)
- [ ] Area views live under `Areas/{Module}/Views/{Controller}/` matching ASP.NET Core conventions
- [ ] Area views use new ViewModels (`Application.Sales.Orders.ViewModels.*`), not legacy VMs
- [ ] `Startup.cs` includes area route: `{area:exists}/{controller}/{action=Index}/{id?}`
- [ ] API controllers preserve existing route attributes (`[Route("api/orders")]`, etc.)
- [ ] Legacy controllers are NOT modified or deleted during the activation phase
- [ ] `_Layout.cshtml` navigation links updated in the same PR that activates each Area controller
- [ ] AJAX paths in views updated in the same PR that moves the corresponding action to an Area

## References

- [ADR-0014](0014-sales-orders-bc-design.md) — Sales/Orders BC Design
- [ADR-0015](0015-sales-payments-bc-design.md) — Sales/Payments BC Design
- [Orders atomic switch roadmap](../roadmap/orders-atomic-switch.md)
- [Payments atomic switch roadmap](../roadmap/payments-atomic-switch.md)
- [Bounded context map](../architecture/bounded-context-map.md)
- [ASP.NET Core Areas documentation](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/areas)
