## Migration plan

See § 5 Atomic switch steps above.

Coordinate with:
- **ADR-0014 (Sales/Orders switch)** — `Order.User` nav prop removal must be coordinated with the Orders BC EF schema; do step 6 as part of the Orders atomic switch or before it.
- **ADR-0013 (per-BC DbContext interfaces)** — `IamDbContext` is the authoritative user store; other BCs may query it via a read-only `IUserQueryContext` interface when needed.
