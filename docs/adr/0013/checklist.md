## Conformance checklist

- [ ] `IAvailabilityDbContext` exists under `Infrastructure/Inventory/Availability/`; is `internal`
- [ ] `IPresaleDbContext` exists under `Infrastructure/Presale/Checkout/`; is `internal`
- [ ] `ICatalogDbContext` exists under `Infrastructure/Catalog/Products/`; is `internal`
- [ ] `ICurrencyDbContext` exists under `Infrastructure/Supporting/Currencies/`; is `internal`
- [ ] `IUserProfileDbContext` exists under `Infrastructure/AccountProfile/`; is `internal`
- [ ] `ITimeManagementDbContext` exists under `Infrastructure/Supporting/TimeManagement/`; is `internal`
- [ ] Each interface declares only `DbSet<T>` properties owned by that BC + `SaveChangesAsync`
- [ ] Each concrete `DbContext` implements its interface (`: IXxxDbContext` in class declaration)
- [ ] Each concrete BC `DbContext` is `internal` (not `public`)
- [ ] All repository constructors in each BC accept the interface, not the concrete class
- [ ] `DeferredJobPollerService.PollAsync` resolves `ITimeManagementDbContext` from scope
- [ ] `JobDispatcherService.PersistResultAsync` resolves `ITimeManagementDbContext` from scope
- [ ] Each BC's `Extensions.cs` registers the scoped alias alongside `AddDbContext<TContext>`
- [ ] `IamDbContext` is unchanged — not in scope
- [ ] Build passes (`dotnet build`)
- [ ] All existing unit and integration tests pass (`dotnet test`)
