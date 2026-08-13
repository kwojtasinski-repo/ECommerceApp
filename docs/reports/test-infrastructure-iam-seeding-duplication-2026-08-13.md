# Test infrastructure — duplicated IAM seeding in `BcWebApplicationFactory` (2026-08-13)

> Deferred cleanup, recorded so it is not rediscovered from scratch. Written while fixing an
> unrelated flake in `ECommerceApp.Web.E2E`; the same defect existed there and **was** fixed
> (commit `d2e829f7`). This report covers the copy that remains in shared test infrastructure.
>
> **Status: analysed, not applied.** Deliberate — see §5.

---

## 1. TL;DR

| | |
|---|---|
| **Where** | `ECommerceApp.Shared.TestInfrastructure/BcWebApplicationFactory.cs` |
| **What** | A private `EnsureIamDbContextCreatedAndSeeded` re-does seeding the base class already does |
| **Why it exists** | Ordering: the base seeds before this factory's DbContext sweep runs |
| **Is anything broken today?** | **No.** Behaviour is correct; the cost is ~25 duplicated lines and one extra `BuildServiceProvider()` per host |
| **Blast radius if changed** | `BcWebApplicationFactory` → `BcBaseTest<T>` → most of `ECommerceApp.IntegrationTests` (247 tests) |
| **Recommendation** | Do **not** change it as a standalone cleanup. Fold it into deliberate work on this layer |

---

## 2. The ordering that causes it

`CustomWebApplicationFactory<TStartup>.ConfigureWebHost` registers one `ConfigureServices` callback
that does four things in order:

```csharp
services.ReplaceDbContextWithInMemory<IamDbContext>(IamDatabaseName);  // 1
services.AddScoped<IDatabaseInitializer, TestDatabaseInitializer>();   // 2
OverrideServicesImplementation(services);                              // 3  ← extension point
var sp = services.BuildServiceProvider();                              // 4
// ... EnsureCreated() + Utilities.InitializeIamUsers(...) ...
```

`BcWebApplicationFactory` does **not** use hook (3). It overrides `ConfigureWebHost` and registers a
*second* `ConfigureServices` callback, which ASP.NET runs after the first one has completed —
including after step 4 has already seeded.

Inside that second callback, `BcDbContextTestSetup.ReplaceAllBcDbContextsWithInMemory` re-points every
`DbContextOptions<>` at a fresh InMemory store. `IamDbContext` is registered with a plain
`AddDbContext` (`ECommerceApp.Infrastructure/Identity/IAM/Auth/Extensions.cs`), so the sweep catches it
like any other context — only `DbContextOptions<Context>` is excluded.

Net effect: the base seeded store A, the running host reads store B. `BcWebApplicationFactory` papers
over this by seeding again, itself, after the sweep — hence the duplication.

---

## 3. What was measured, and what it corrected

Measured on `PlaywrightWebApplicationFactory`, which had the identical structure, by resolving
`IamDbContext` from two live hosts and reading `InMemoryOptionsExtension.StoreName`:

```
A iam=BcTestDb_IamDbContext_82ad3428467c43faa5d7c6f9e5fd6b40
B iam=BcTestDb_IamDbContext_75b2271a3a174c07bf8eb13a23ddc6de
B sees A's user = False
```

Two facts follow, and the second contradicted a comment that had been in the code:

1. The effective IAM store name comes from the **sweep**, not from `IamDatabaseName`. That virtual
   property only governs factories which never run the sweep.
2. `IamDbContext` therefore **does** get a per-instance GUID store — it is not on one fixed shared
   name. The `catch` block in `EnsureIamDbContextCreatedAndSeeded` used to justify itself by the
   opposite claim ("two test classes' constructors can race to seed the same fixed-Id test users").
   No such cross-instance race is possible for a factory that runs the sweep. That comment has been
   corrected in place; the `try`/`catch` itself is kept as ordinary defence against a seeding failure
   taking down host startup.

Before this was measured, an earlier session had added an `IamDatabaseName` override to
`PlaywrightWebApplicationFactory` believing a shared IAM database was the cause of test failures. It
was not, and the override was inert — it has since been reverted, leaving shared infrastructure
untouched by that work.

---

## 4. The change, if and when it is made

Use the hook the base class already provides, and delete the re-seed:

```diff
 public class BcWebApplicationFactory : CustomWebApplicationFactory<Startup>
 {
-    protected override void ConfigureWebHost(IWebHostBuilder builder)
-    {
-        base.ConfigureWebHost(builder);
-
-        builder.ConfigureServices(services =>
-        {
-            BcDbContextTestSetup.ReplaceAllBcDbContextsWithInMemory(services);
-            BcDbContextTestSetup.MakeAllBcDbContextsTransient(services);
-            ReplaceMessageBrokerWithSynchronous(services);
-            BcDbContextTestSetup.ReplaceDbContextMigratorsWithNoOp(services);
-            EnsureIamDbContextCreatedAndSeeded(services);
-            BcDbContextTestSetup.EnsureAllBcDbContextsCreated(services);
-        });
-    }
+    protected override void OverrideServicesImplementation(IServiceCollection services)
+    {
+        BcDbContextTestSetup.ReplaceAllBcDbContextsWithInMemory(services);
+        BcDbContextTestSetup.MakeAllBcDbContextsTransient(services);
+        ReplaceMessageBrokerWithSynchronous(services);
+        BcDbContextTestSetup.ReplaceDbContextMigratorsWithNoOp(services);
+        BcDbContextTestSetup.EnsureAllBcDbContextsCreated(services);
+    }
 
-    private static void EnsureIamDbContextCreatedAndSeeded(IServiceCollection services) { /* ~25 lines */ }
 }
```

Running the sweep inside hook (3) means the base's own `BuildServiceProvider()` and seeding at step 4
already see the swept options, so the seed lands in the store the host opens.

Follow-ups that come with it: unused `using`s (`ILogger`, `IamDbContext`, `Exception`) and the fact
that the surviving `try`/`catch` moves into the base, which logs at `LogError` rather than
`LogWarning`.

This is exactly the change applied to `PlaywrightWebApplicationFactory` in `d2e829f7`, where it also
fixed a real defect: there, nothing re-seeded after the sweep, so `test@test` and the other default
users were simply absent from the running host.

---

## 5. Why it is deferred

The benefit is ~25 lines of duplication and one redundant service-provider build per host. The
exposure is the backbone of the integration suite:

```
BcWebApplicationFactory
  └─ BcBaseTest<T>
       └─ ~15+ test classes across ECommerceApp.IntegrationTests (247 tests)
```

Each test class *is* a factory instance, and that project runs with
`parallelizeTestCollections: true` and `maxParallelThreads: -1`. A cosmetic refactor of that surface,
on its own, is a poor trade.

Unlike the `Web.E2E` case there is no user-visible defect to fix: `BcWebApplicationFactory` seeds
after its own sweep, so its hosts do have their default users.

---

## 6. Verification gate when picked up

```
dotnet test ECommerceApp.IntegrationTests/ECommerceApp.IntegrationTests.csproj
dotnet test ECommerceApp.Web.IntegrationTests/ECommerceApp.Web.IntegrationTests.csproj
dotnet test ECommerceApp.E2E.Backend/ECommerceApp.E2E.Backend.csproj
```

Baseline recorded 2026-08-13, all green: 247, 20 and 19 tests respectively.

A cheap extra check worth doing at the same time: assert inside a `BcBaseTest`-derived test that
`UserManager.FindByEmailAsync("test@test")` returns non-null. That pins the property this whole
report is about, and would have caught the `Web.E2E` variant of the defect immediately.
