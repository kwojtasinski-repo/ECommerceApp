---
name: create-unit-test
description: >
  Scaffold a unit test class for an ECommerceApp service, handler, or aggregate.
  Creates an xUnit test file with Moq-based constructor, private factory method,
  and Arrange/Act/Assert test stubs. Works for both new BC services and legacy services.
argument-hint: "<ServiceOrClassName> [BC path like Sales/Orders]"
---

# Create Unit Test

Generate a unit test class following the project conventions.

## File placement

- **New BC service/handler**: `ECommerceApp.UnitTests/<Module>/<BC>/<TestClassName>.cs`
  - Example: `ECommerceApp.UnitTests/Sales/Orders/OrderPaymentConfirmedHandlerTests.cs`
- **Legacy service**: `ECommerceApp.UnitTests/Services/<Domain>/<TestClassName>.cs`
  - Example: `ECommerceApp.UnitTests/Services/Item/ItemServiceTests.cs`

## Template — Service test (Moq-based)

```csharp
using {{ServiceNamespace}};
using {{DomainNamespace}};
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.{{ModulePath}}
{
    public class {{ClassName}}Tests
    {
        private readonly Mock<{{IDependency1}}> _{{dep1}};
        private readonly Mock<{{IDependency2}}> _{{dep2}};
        // Add mocks for each constructor dependency

        public {{ClassName}}Tests()
        {
            _{{dep1}} = new Mock<{{IDependency1}}>();
            _{{dep2}} = new Mock<{{IDependency2}}>();
        }

        private {{ClassName}} CreateSut() => new(
            _{{dep1}}.Object,
            _{{dep2}}.Object);

        [Fact]
        public async Task MethodName_Scenario_ExpectedResult()
        {
            // Arrange
            _{{dep1}}.Setup(r => r.SomeMethod(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(/* expected return */);

            // Act
            var result = await CreateSut().MethodAsync(/* args */);

            // Assert
            result.Should().NotBeNull();
        }
    }
}
```

## Template — Aggregate test (no mocks)

```csharp
using {{DomainNamespace}};
using ECommerceApp.Domain.Shared;
using FluentAssertions;
using Xunit;

namespace ECommerceApp.UnitTests.{{ModulePath}}
{
    public class {{AggregateName}}Tests
    {
        [Fact]
        public void Create_ValidParameters_ShouldCreateEntity()
        {
            var entity = {{AggregateName}}.Create(/* valid params */);

            entity.Should().NotBeNull();
            // Assert on properties
        }

        [Fact]
        public void Create_InvalidParameter_ShouldThrowDomainException()
        {
            var act = () => {{AggregateName}}.Create(/* invalid params */);

            act.Should().Throw<DomainException>().WithMessage("*expected message*");
        }
    }
}
```

## Template — Handler test

```csharp
using {{HandlerNamespace}};
using {{MessageNamespace}};
using {{DomainNamespace}};
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.{{ModulePath}}
{
    public class {{HandlerName}}Tests
    {
        private readonly Mock<{{IRepository}}> _repo;

        public {{HandlerName}}Tests()
        {
            _repo = new Mock<{{IRepository}}>();
        }

        private {{HandlerName}} CreateHandler() => new(_repo.Object);

        private static {{MessageType}} CreateMessage(/* params */)
            => new(/* values */);

        [Fact]
        public async Task HandleAsync_EntityNotFound_ShouldNotUpdate()
        {
            _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(({{Entity}}?)null);

            await CreateHandler().HandleAsync(CreateMessage());

            _repo.Verify(r => r.UpdateAsync(It.IsAny<{{Entity}}>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldProcessAndUpdate()
        {
            var entity = /* create test entity */;
            _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);

            await CreateHandler().HandleAsync(CreateMessage());

            _repo.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
```

## Rules

1. Test class name = `{ClassUnderTest}Tests`
2. Use `CreateSut()` or `CreateHandler()` private factory — never `new` in test methods
3. Use constructor for Mock setup, not per-method
4. Follow `MethodName_Scenario_ExpectedResult` naming
5. Use FluentAssertions (`Should()`) — not raw Assert
6. Use `CancellationToken` in async setups: `It.IsAny<CancellationToken>()`
7. One concept per test method
