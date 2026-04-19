## Migration plan

1. **New BCs** — all new bounded contexts MUST define typed IDs and value objects from day one.
   Use `sealed record <Name>Id(int Value) : TypedId<int>(Value)` for every aggregate ID,
   and `sealed record <VO>` with validation in the constructor for every domain concept.

2. **Existing BCs** — migrate on demand, one BC at a time, as part of a dedicated BC
   modernisation sprint. Priority order (most benefit / least risk first):
   `Customer` → `Order` → `Payment` → `Refund` → `Coupon`.

3. **Shared `DomainException`** — `DomainException` was moved to `ECommerceApp.Domain.Shared`
   when the Catalog/Products BC adopted the VO pattern alongside AccountProfile.
   `ECommerceApp.Domain.AccountProfile.DomainException` still exists for backward compatibility
   and will be consolidated when AccountProfile is updated to use the shared one.

4. **Shared monetary VOs** — `Price` and `Money` live in `ECommerceApp.Domain.Shared`:
   - `Price` (PLN-only, no currency field) — used by Catalog (`Item.Cost`) and Orders (`Order.TotalCost`).
   - `Money` (Amount + CurrencyCode + Rate) — used by Payments (`Payment.Amount`).
     Rate captures the NBP exchange rate at transaction time for audit and PLN conversion.
   - `Price.ToMoney()` bridges Catalog/Orders to the Payment BC at checkout time.

5. **`implementation-patterns.md`** — sections 3 (Value Object) and 4 (Strongly-Typed ID)
   updated to reflect this decision (done alongside this ADR).
