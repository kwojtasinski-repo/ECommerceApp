## Conformance checklist

- [ ] No `Domain/Backoffice/` folder exists
- [ ] No `Infrastructure/Backoffice/DbContext` — Backoffice has no own DbContext
- [ ] All Backoffice services inject only BC service interfaces — no direct `DbContext` usage
- [ ] Backoffice services issue no commands — all mutations delegate to owning BC service
- [ ] Each Backoffice service aggregates from at most two BC sources; wider views assembled at controller layer
- [ ] `Extensions.cs` registers all Backoffice services via `AddBackoffice(IServiceCollection)`
- [ ] All view models live under `Application/Backoffice/ViewModels/`
