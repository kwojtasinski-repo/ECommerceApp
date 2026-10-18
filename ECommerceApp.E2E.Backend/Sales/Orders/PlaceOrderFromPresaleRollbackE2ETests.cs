using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Sales.Orders.DTOs;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Orders
{
    internal sealed class OutboxCommitFailureState
    {
        public volatile bool FailAfterEnqueue;
    }

    /// <summary>
    /// <see cref="IOutboxWriter"/> decorator that, once armed, performs the REAL Outbox insert inside the open
    /// transaction and then throws, so the failure lands in the window between "Order row and Outbox row written"
    /// and "transaction committed". Everything else delegates to the real writer.
    /// </summary>
    internal sealed class FailAfterEnqueueOutboxWriter : IOutboxWriter
    {
        private readonly IOutboxWriter _inner;
        private readonly OutboxCommitFailureState _state;

        public FailAfterEnqueueOutboxWriter(IOutboxWriter inner, OutboxCommitFailureState state)
        {
            _inner = inner;
            _state = state;
        }

        public Task EnqueueAsync(IMessage message, CancellationToken ct = default)
            => _inner.EnqueueAsync(message, ct);

        public async Task EnqueueAsync(IMessage message, IOutboxTransaction transaction, CancellationToken ct = default)
        {
            await _inner.EnqueueAsync(message, transaction, ct);
            if (_state.FailAfterEnqueue)
            {
                throw new InvalidOperationException("Simulated failure after Order write and Outbox enqueue, before commit.");
            }
        }
    }

    internal sealed class OrderRollbackE2EWebApplicationFactory : WebApplicationFactory<Startup>
    {
        private readonly string _connectionString;

        public OrderRollbackE2EWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => TestLogging.Configure(logging));
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.test.json"), optional: false, reloadOnChange: false);
                cfg.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["Database:RunMigrationsOnStart"] = "true",
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<OutboxCommitFailureState>();

                var writerDescriptor = services.Single(d => d.ServiceType == typeof(IOutboxWriter));
                services.Remove(writerDescriptor);
                services.AddScoped<IOutboxWriter>(sp =>
                {
                    var inner = (IOutboxWriter)ActivatorUtilities.CreateInstance(sp, writerDescriptor.ImplementationType!);
                    return new FailAfterEnqueueOutboxWriter(inner, sp.GetRequiredService<OutboxCommitFailureState>());
                });
            });

            builder.UseEnvironment("test");
        }
    }

    /// <summary>
    /// Own SQL Server container and host: the failing <see cref="IOutboxWriter"/> must never leak into any other
    /// E2E test in this project.
    /// </summary>
    public sealed class OrderRollbackE2EFixture : IAsyncLifetime
    {
        public const string CollectionName = "OrderRollbackSqlServer";

        private readonly MsSqlContainer _container = new MsSqlBuilder()
            .WithLogger(TestLogging.CreateTestcontainersLogger())
            .WithOutputConsumer(TestLogging.CreateContainerOutputConsumer())
            .Build();
        private OrderRollbackE2EWebApplicationFactory _factory;

        public IServiceProvider Services => (_factory ?? throw new InvalidOperationException(
            $"{nameof(OrderRollbackE2EFixture)} has not been initialized yet.")).Services;

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
            _factory = new OrderRollbackE2EWebApplicationFactory(_container.GetConnectionString());
            _ = _factory.Services;
        }

        public async ValueTask DisposeAsync()
        {
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }

            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Atomicity of the live order placement path on a real SQL Server: when a failure occurs after the Order
    /// aggregate and its <c>OrderPlaced</c> Outbox row were written but before the transaction commits, BOTH must be
    /// rolled back. EF Core's InMemory provider cannot show this. A control placement without the failure runs first
    /// in the same host, proving the order and Outbox rows really are written when nothing fails.
    /// </summary>
    [Collection(OrderRollbackE2EFixture.CollectionName)]
    public sealed class PlaceOrderFromPresaleRollbackE2ETests
    {
        private readonly OrderRollbackE2EFixture _fixture;

        public PlaceOrderFromPresaleRollbackE2ETests(OrderRollbackE2EFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task PlaceOrderFromPresaleAsync_FailureAfterOrderWriteAndOutboxEnqueue_RollsBackOrderAndOutboxRow()
        {
            var ct = TestContext.Current.CancellationToken;
            var failureState = _fixture.Services.GetRequiredService<OutboxCommitFailureState>();
            var controlUserId = $"rollback-control-{Guid.NewGuid():N}";
            var failingUserId = $"rollback-failing-{Guid.NewGuid():N}";

            failureState.FailAfterEnqueue = false;
            var control = await PlaceAsync(controlUserId, ct);
            control.IsSuccess.ShouldBeTrue();
            (await CountOrdersAsync(controlUserId, ct)).ShouldBe(1);
            (await CountOutboxRowsAsync(controlUserId, ct)).ShouldBe(1);

            failureState.FailAfterEnqueue = true;
            try
            {
                var ex = await Should.ThrowAsync<InvalidOperationException>(() => PlaceAsync(failingUserId, ct));
                ex.Message.ShouldContain("Simulated failure");
            }
            finally
            {
                failureState.FailAfterEnqueue = false;
            }

            (await CountOrdersAsync(failingUserId, ct)).ShouldBe(0);
            (await CountOutboxRowsAsync(failingUserId, ct)).ShouldBe(0);
            (await CountOrdersAsync(controlUserId, ct)).ShouldBe(1);
        }

        private async Task<Application.Sales.Orders.Results.PlaceOrderResult> PlaceAsync(string userId, CancellationToken ct)
        {
            using var scope = _fixture.Services.CreateScope();
            var dto = new PlaceOrderFromPresaleDto(
                CustomerId: 1,
                CurrencyId: 1,
                UserId: userId,
                Customer: new OrderCustomerData(
                    "Jan", "Kowalski", "jan@test.com", "123456789", false, null, null,
                    "Główna", "1", null, "67-100", "Nowa Sól", "PL"),
                Lines: new List<PlaceOrderLineDto> { new(ProductId: 987654, Quantity: 1, UnitPrice: 10m) });

            return await scope.ServiceProvider.GetRequiredService<IOrderService>().PlaceOrderFromPresaleAsync(dto, ct);
        }

        private async Task<int> CountOrdersAsync(string userId, CancellationToken ct)
        {
            using var scope = _fixture.Services.CreateScope();
            var orders = await scope.ServiceProvider.GetRequiredService<IOrderRepository>().GetByUserIdAsync(userId, ct);
            return orders.Count;
        }

        private async Task<int> CountOutboxRowsAsync(string userId, CancellationToken ct)
        {
            using var scope = _fixture.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
            return await context.Outbox.AsNoTracking().CountAsync(o => o.Payload.Contains(userId), ct);
        }
    }

    [CollectionDefinition(OrderRollbackE2EFixture.CollectionName)]
    public sealed class OrderRollbackSqlServerCollection : ICollectionFixture<OrderRollbackE2EFixture>
    {
    }
}
