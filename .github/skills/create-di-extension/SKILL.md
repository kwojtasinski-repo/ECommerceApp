---
name: create-di-extension
description: >
  Scaffold DI extension classes for a bounded context — Application-side (services,
  handlers, scheduled tasks) and/or Infrastructure-side (DbContext, repositories,
  adapters). Follows the project's internal static extension method pattern.
argument-hint: "<BcName> [application|infrastructure|both]"
---

# Create DI Extension

Generate DI registration extension methods for a bounded context.

## Modes

| Mode | What it generates |
|---|---|
| `application` | Application-layer extension (services, handlers, scheduled tasks) |
| `infrastructure` | Infrastructure-layer extension (DbContext, repositories, adapters, migrator) |
| `both` | Both files |

---

## Application-side template

**File**: `ECommerceApp.Application/{{Module}}/{{BC}}/Extensions/{{BC}}Extensions.cs`

```csharp
using ECommerceApp.Application.{{Module}}.{{BC}}.Handlers;
using ECommerceApp.Application.{{Module}}.{{BC}}.Services;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.{{Module}}.{{BC}}.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Application.{{Module}}.{{BC}}.Extensions
{
    internal static class {{BC}}Extensions
    {
        public static IServiceCollection Add{{BC}}Services(this IServiceCollection services)
        {
            // Services
            services.AddScoped<I{{ServiceName}}, {{ServiceName}}>();

            // Handlers (one per cross-BC message consumed)
            services.AddScoped<IMessageHandler<{{MessageType}}>, {{HandlerName}}>();

            // Scheduled tasks (optional)
            // services.AddScoped<IScheduledTask, {{TaskName}}>();

            return services;
        }
    }
}
```

## Infrastructure-side template

**File**: `ECommerceApp.Infrastructure/{{Module}}/{{BC}}/Extensions/{{BC}}InfraExtensions.cs`

```csharp
using ECommerceApp.Domain.{{Module}}.{{BC}}.Interfaces;
using ECommerceApp.Infrastructure.{{Module}}.{{BC}}.Contexts;
using ECommerceApp.Infrastructure.{{Module}}.{{BC}}.Repositories;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.{{Module}}.{{BC}}.Extensions
{
    internal static class {{BC}}InfraExtensions
    {
        public static IServiceCollection Add{{BC}}Infrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<{{BC}}DbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<{{BC}}DbContext>>();

            // Repositories
            services.AddScoped<I{{RepoName}}, {{RepoName}}>();

            // Adapters (for cross-BC interfaces implemented here)
            // services.AddScoped<I{{AdapterInterface}}, {{AdapterImpl}}>();

            return services;
        }
    }
}
```

## Wiring — manual steps after creation

1. **Application**: Call `services.Add{{BC}}Services()` from `ECommerceApp.Application/DependencyInjection.cs` → `AddApplication()`.
2. **Infrastructure**: Call `services.Add{{BC}}Infrastructure(configuration)` from `ECommerceApp.Infrastructure/DependencyInjection.cs` → `AddInfrastructure()`.
3. Verify the calls compile: `dotnet build --verbosity quiet`.

## Rules

1. Class visibility: `internal static`
2. Method visibility: `public static` (extension method)
3. Return `IServiceCollection` for chaining
4. All services registered as `Scoped` (not Singleton or Transient) — matches EF Core DbContext lifetime
5. Group registrations with comments: `// Services`, `// Handlers`, `// Repositories`
6. Handler registration: `services.AddScoped<IMessageHandler<TMessage>, THandler>()` — one line per handler
7. Read the existing `DependencyInjection.cs` files before wiring to match the call-site pattern
