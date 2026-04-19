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
