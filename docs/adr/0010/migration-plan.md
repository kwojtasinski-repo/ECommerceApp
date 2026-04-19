## Migration plan

1. Create `Application/Messaging/` with 6 contract files + `Extensions.cs`.
2. Create `Infrastructure/Messaging/` with 4 implementation files + `BackgroundMessageDispatcher` + `Extensions.cs`.
3. Register via `AddMessagingServices()` in `Application/DependencyInjection.cs` and
   `AddMessagingInfrastructure()` in `Infrastructure/DependencyInjection.cs`.
4. Add `"Messaging": { "UseBackgroundDispatcher": true }` to `appsettings.json`.
5. Create first message: `CheckoutCompleted` in `Application/Orders/Messages/`.
6. Create first handler: `CheckoutCompletedHandler` in `Application/Supporting/TimeManagement/Handlers/`.
7. Replace `IDeferredJobScheduler` direct injection in `OrderService` with `IMessageBroker.PublishAsync`.
8. Write unit tests for `CheckoutCompletedHandler` and the broker dispatch path.

No existing code is removed until Step 7. Parallel change strategy applies.

---
