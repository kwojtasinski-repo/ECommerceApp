## Conformance checklist

- [ ] No `Domain/Supporting/Communication/` folder exists (no domain model)
- [ ] All handlers implement `IMessageHandler<T>` — no direct service-to-service calls
- [ ] `INotificationService` is the only external delivery dependency injected into handlers
- [ ] No message publishing from Communication BC handlers — consumer only
- [ ] `Extensions.cs` registers all handlers via `AddCommunicationServices(IServiceCollection)`
- [ ] `IOrderUserResolver` implemented in providing BC (Orders Infrastructure) — no email resolver needed
