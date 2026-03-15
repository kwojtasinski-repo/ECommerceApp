---
name: create-integration-test
description: >
  Scaffold an integration test class using BaseTest<T>, CustomWebApplicationFactory,
  and Shouldly assertions. For service-layer integration tests against an in-memory database.
argument-hint: "<ServiceInterface> [BcName]"
---

# Create Integration Test

Generate an integration test class for a service against the in-memory database.

## File placement

`ECommerceApp.IntegrationTests/Services/{{ServiceName}}Tests.cs`

Or for BC-specific grouping:

`ECommerceApp.IntegrationTests/{{Module}}/{{ServiceName}}Tests.cs`

## Template

```csharp
using ECommerceApp.Application.{{Module}}.{{BC}}.Interfaces;
using ECommerceApp.IntegrationTests.Common;
using Shouldly;
using Xunit;

namespace ECommerceApp.IntegrationTests.{{TestNamespace}}
{
    public class {{ServiceName}}Tests : BaseTest<I{{ServiceName}}>
    {
        [Fact]
        public void given_valid_id_should_return_entity()
        {
            // Arrange
            var id = 1; // seeded via TestDatabaseInitializer

            // Act
            var result = _service.Get(id);

            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldBe(id);
        }

        [Fact]
        public void given_invalid_id_should_return_null()
        {
            // Arrange
            var id = 999;

            // Act
            var result = _service.Get(id);

            // Assert
            result.ShouldBeNull();
        }

        [Fact]
        public async Task given_valid_dto_should_add_entity()
        {
            // Arrange
            var dto = new {{DtoType}}
            {
                // populate required fields
            };

            // Act
            var id = await _service.AddAsync(dto);

            // Assert
            id.ShouldBeGreaterThan(0);

            var added = _service.Get(id);
            added.ShouldNotBeNull();
        }

        [Fact]
        public async Task given_valid_dto_should_update_entity()
        {
            // Arrange
            var existing = _service.Get(1);
            existing.ShouldNotBeNull();
            existing.Name = "Updated Name";

            // Act
            await _service.UpdateAsync(existing);

            // Assert
            var updated = _service.Get(1);
            updated.ShouldNotBeNull();
            updated.Name.ShouldBe("Updated Name");
        }

        [Fact]
        public void given_valid_params_should_return_paginated_list()
        {
            // Arrange
            var pageSize = 10;
            var pageNo = 1;
            var searchString = "";

            // Act
            var result = _service.GetAll(pageSize, pageNo, searchString);

            // Assert
            result.ShouldNotBeNull();
            result.Items.ShouldNotBeEmpty();
            result.Count.ShouldBeGreaterThan(0);
        }
    }
}
```

## Test infrastructure classes (already exist — do NOT recreate)

| Class | Location | Purpose |
|---|---|---|
| `BaseTest<T>` | `IntegrationTests/Common/BaseTest.cs` | Resolves `T` from DI, exposes `_service`, implements `IDisposable` |
| `CustomWebApplicationFactory<T>` | `IntegrationTests/Common/CustomWebApplicationFactory.cs` | Replaces SQL Server with InMemory DB, seeds test data |
| `TestDatabaseInitializer` | `IntegrationTests/Common/TestDatabaseInitializer.cs` | Seeds the in-memory database with test entities |

## Key differences from unit tests

| Aspect | Unit test | Integration test |
|---|---|---|
| Assertion library | FluentAssertions | **Shouldly** |
| Mocking | Moq for all deps | No mocks — real DI container |
| Database | None | InMemory via EF Core |
| Base class | None (standalone) | `BaseTest<T>` |
| Service resolution | Manual construction | From `IServiceProvider` |

## Rules

1. Use **Shouldly** for assertions (not FluentAssertions) — project convention for integration tests
2. Inherit from `BaseTest<I{{ServiceName}}>` — the base class handles DI setup and teardown
3. Access the service via `_service` field (provided by base class)
4. Test data comes from `TestDatabaseInitializer` — check what's seeded before writing assertions
5. For tests requiring an authenticated user, call `SetHttpContextUserId(userId)` from the base class
6. Keep tests independent — each test gets a fresh in-memory database scope
7. Do NOT create new `CustomWebApplicationFactory` or `BaseTest` classes
