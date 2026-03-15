---
name: create-dbcontext
description: >
  Scaffold a per-BC DbContext with schema constants, design-time factory, and
  Infrastructure DI extension method. Follows ADR-0013 per-BC DbContext pattern.
  Creates 4 files: DbContext, Constants, Factory, Extensions.
argument-hint: "<BcName> <SchemaName> <Entity1,Entity2,...> [InfraPath like Sales/Orders]"
---

# Create Per-BC DbContext

Generate the 4 infrastructure files every bounded context needs.

## File placement

All files go under `ECommerceApp.Infrastructure/<Module>/<BC>/`:

- `{{BcName}}DbContext.cs`
- `{{BcName}}Constants.cs`
- `{{BcName}}DbContextFactory.cs`
- `Extensions.cs`

## 1. Constants

```csharp
namespace ECommerceApp.Infrastructure.{{InfraNamespace}}
{
    internal static class {{BcName}}Constants
    {
        public const string SchemaName = "{{schemaName}}";
    }
}
```

## 2. DbContext

```csharp
using ECommerceApp.Domain.{{DomainNamespace}};
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ECommerceApp.Infrastructure.{{InfraNamespace}}
{
    internal sealed class {{BcName}}DbContext : DbContext
    {
        public DbSet<{{Entity1}}> {{Entity1Plural}} => Set<{{Entity1}}>();
        // Add DbSet for each entity

        public {{BcName}}DbContext(DbContextOptions<{{BcName}}DbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema({{BcName}}Constants.SchemaName);
            modelBuilder.ApplyConfigurationsFromAssembly(
                GetType().Assembly,
                t => t.Namespace != null && t.Namespace.StartsWith("ECommerceApp.Infrastructure.{{InfraNamespace}}.Configurations"));
            modelBuilder.UseUtcDateTimes();
        }
    }
}
```

## 3. Design-Time Factory

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECommerceApp.Infrastructure.{{InfraNamespace}}
{
    internal sealed class {{BcName}}DbContextFactory : IDesignTimeDbContextFactory<{{BcName}}DbContext>
    {
        public {{BcName}}DbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<{{BcName}}DbContext>();
            optionsBuilder.UseSqlServer("Server=.;Database=ECommerceAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
            return new {{BcName}}DbContext(optionsBuilder.Options);
        }
    }
}
```

## 4. Infrastructure Extensions (DI registration)

```csharp
using ECommerceApp.Domain.{{DomainNamespace}};
using ECommerceApp.Infrastructure.{{InfraNamespace}}.Repositories;
using ECommerceApp.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerceApp.Infrastructure.{{InfraNamespace}}
{
    internal static class Extensions
    {
        public static IServiceCollection Add{{BcName}}Infrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<{{BcName}}DbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<IDbContextMigrator, DbContextMigrator<{{BcName}}DbContext>>();

            return services
                .AddScoped<I{{Entity1}}Repository, {{Entity1}}Repository>();
                // Add repository registrations for each entity
        }
    }
}
```

## After creation — manual steps

1. Register `Add{{BcName}}Infrastructure(configuration)` in `Infrastructure/DependencyInjection.cs`
2. Create EF configurations under `Configurations/` subfolder (use `/create-ef-configuration`)
3. Create repositories under `Repositories/` subfolder
4. **DO NOT** run migrations without human approval — see `migration-policy.instructions.md`

## Rules

1. DbContext visibility: `internal sealed` (not public)
2. Use `=> Set<T>()` expression-bodied DbSet properties (not `{ get; set; }`)
3. Always call `modelBuilder.UseUtcDateTimes()` — project-wide convention
4. Configuration namespace filter must match `Configurations` subfolder exactly
5. Connection string comes from `IConfiguration` — never hardcode in Extensions.cs
6. Design-time factory uses hardcoded connection string — this is intentional for `dotnet ef` CLI
