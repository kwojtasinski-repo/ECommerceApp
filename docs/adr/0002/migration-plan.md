## Migration plan

No migration is triggered by this ADR itself — it records strategic direction only.

Each sub-decision listed in this ADR will be implemented incrementally:
- A dedicated follow-up ADR must be created and accepted before any sub-decision is implemented.
- Existing code (`AbstractService`, `GenericRepository<T>`, `PaymentHandler`, `CouponHandler`,
  `ItemHandler`, `ExceptionMiddleware`) remains unchanged until a specific follow-up ADR supersedes it.
- The shared `Context` in `ECommerceApp.Infrastructure` is the primary indicator of remaining
  BC coupling and will be decomposed gradually as BC boundaries are formalized.
