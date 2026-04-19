## Migration plan

Apply BC-by-BC in any order. Each BC is a self-contained, non-breaking change.

**For each BC (steps 1–5 are identical):**

1. Create `IXxxDbContext.cs` in the BC's Infrastructure folder declaring the `DbSet<T>`
   properties and `SaveChangesAsync`.
2. Add `: IXxxDbContext` to the concrete class declaration.
3. Update all repository constructors in the BC to accept `IXxxDbContext`.
4. Add `services.AddScoped<IXxxDbContext>(sp => sp.GetRequiredService<XxxDbContext>())`
   in the BC's `Extensions.cs` / `AddXxxInfrastructure(...)` method.
5. Change the concrete class from `public` to `internal` (skip for `PresaleDbContext` —
   already `internal sealed`).

**TimeManagement additional step (step 6):**

6. In `DeferredJobPollerService.PollAsync` and `JobDispatcherService.PersistResultAsync`,
   replace `GetRequiredService<TimeManagementDbContext>()` with
   `GetRequiredService<ITimeManagementDbContext>()`.

**Gate condition:** Apply this migration only when 80–100% of the new BC implementations
are complete (i.e., Inventory/Availability, Presale/Checkout, Catalog, Currencies,
AccountProfile, and TimeManagement are all in production or staged for switch). Applying
earlier adds churn on BCs that are still actively changing shape.
