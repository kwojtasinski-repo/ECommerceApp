## Migration plan

1. Create folder structure across all three layers (Domain, Application, Infrastructure).
2. Implement Domain layer: value objects, aggregates, domain events, repository interfaces.
3. Implement Infrastructure layer: `TimeManagementDbContext`, EF configs, repositories,
   `CronSchedulerService`, `DeferredJobPollerService`, `JobDispatcherService`,
   `InMemoryJobStatusMonitor`, `Extensions.cs`.
4. Implement Application layer: `IScheduledTask`, `IDeferredJobScheduler`, `IJobTrigger`,
   `JobExecutionContext`, `IJobManagementService` / `JobManagementService`, `Extensions.cs`.
5. Implement `CurrencyRateSyncTask` in `Application/Supporting/Currencies/` as first real task.
6. Register via `AddTimeManagement(...)` in `Infrastructure/DependencyInjection.cs` and
   `AddTimeManagementServices()` in `Application/DependencyInjection.cs`.
7. DB migration for `time_management` schema — requires approval per `migration-policy.md`.
8. Implement `JobManagementController` + Views in `ECommerceApp.Web`.
9. Write unit tests for aggregates, value objects, and `JobManagementService`.
10. Write integration tests for `CurrencyRateSyncTask` via `BaseTest<T>`.

No existing code is removed or modified until Step 5 (first task) at the earliest.
Existing `CurrencyRateService` lazy-fetch behaviour remains unchanged until the atomic switch.

---

---
