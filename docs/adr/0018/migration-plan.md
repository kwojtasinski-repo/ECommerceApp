## Migration plan

1. Implement `INotificationService` stub (logs to `ILogger` — no real delivery)
2. Implement `IMessageHandler<T>` for each subscribed message — wire via `AddCommunicationServices()`
3. Activate after Fulfillment Slice 1 is switched (provides `RefundApproved` / `RefundRejected`)
4. Infrastructure layer provides `SignalRNotificationService` + `NotificationHub`; `AddCommunicationInfrastructure()` overrides stub with real SignalR impl
5. Add `CouponExpired` handler after Coupons Slice 2
6. Client-side: connect JS SignalR client to `/hubs/notifications`, subscribe to `ReceiveNotification`
