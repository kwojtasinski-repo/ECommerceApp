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
