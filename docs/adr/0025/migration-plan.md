## Migration plan

Implementation is split into three phases tracked in `orders-atomic-switch.md` Step 4:

**Phase 4a — Authorization policy**
1. Add `TrustedApiUser` policy to `API/Startup.cs`.
2. Add `[Authorize(Policy = "TrustedApiUser")]` to write endpoints in `CartController`,
   `CheckoutController`, `OrdersController`.
3. Add claim `api:purchase` emission to `LoginController` JWT assembly.
4. Add ownership check to `GET /api/v2/orders/{id}` and `GET /api/v2/payments/{id}`.

**Phase 4b — Quantity limit**
1. Create `ApiPurchaseOptions` with `MaxQuantityPerOrderLine = 5`.
2. Create `MaxApiQuantityFilter` action filter.
3. Apply filter to `CartController.AddOrUpdate`.
4. Create `AddToCartDtoValidator` in Application layer for the Web 99-per-line limit.

**Phase 4c — Payment URL**
1. Create `WebOptions` class and register in `API/Startup.cs`.
2. Inject `IOptions<WebOptions>` into `API/Controllers/V2/CheckoutController`.
3. Update `Confirm` action to return `{ orderId, paymentUrl }`.
4. Add `WebOptions:BaseUrl` to `API/appsettings.json` and `API/appsettings.Development.json`.
