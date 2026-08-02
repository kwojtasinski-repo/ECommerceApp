using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerceApp.Application.Messaging;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Shared.TestInfrastructure;
using ECommerceApp.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace ECommerceApp.E2E.Backend.Sales.Orders
{
    /// <summary>
    /// <see cref="IOrderItemRepository"/> decorator whose <see cref="AssignToOrderAsync"/> always
    /// throws, used to engineer a deterministic mid-transaction failure for
    /// <see cref="OrderServiceRollbackE2ETests"/> — the same "force a failure, then prove the
    /// rollback" technique <c>CrossContextTransactionScopeE2ETests</c> uses via an explicit
    /// <c>RollbackAsync()</c> call, just triggered through an unhandled exception instead, since
    /// that is the realistic production failure mode (a handler/repository throws) this test targets.
    /// Every other member delegates to the real, DI-resolved implementation unchanged.
    /// </summary>
    internal sealed class AssignToOrderThrowingRepository : IOrderItemRepository
    {
        private readonly IOrderItemRepository _inner;

        public AssignToOrderThrowingRepository(IOrderItemRepository inner)
        {
            _inner = inner;
        }

        public Task<OrderItem> GetByIdAsync(int id, CancellationToken ct = default)
            => _inner.GetByIdAsync(id, ct);

        public Task<int> AddAsync(OrderItem item, CancellationToken ct = default)
            => _inner.AddAsync(item, ct);

        public Task DeleteAsync(int id, CancellationToken ct = default)
            => _inner.DeleteAsync(id, ct);

        public Task<IReadOnlyList<OrderItem>> GetByIdsAsync(IReadOnlyList<int> ids, CancellationToken ct = default)
            => _inner.GetByIdsAsync(ids, ct);

        public Task<IReadOnlyList<OrderItem>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
            => _inner.GetByOrderIdAsync(orderId, ct);

        public Task AssignToOrderAsync(IReadOnlyList<int> itemIds, int orderId, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "Simulated failure between the Order aggregate write and the Outbox commit (AssignToOrderThrowingRepository).");

        public Task SetSnapshotsAsync(IReadOnlyList<(int ItemId, OrderProductSnapshot Snapshot)> snapshots, CancellationToken ct = default)
            => _inner.SetSnapshotsAsync(snapshots, ct);

        public Task<IReadOnlyList<OrderItem>> GetUnsnapshottedOrderItemsAsync(int batchSize, CancellationToken ct = default)
            => _inner.GetUnsnapshottedOrderItemsAsync(batchSize, ct);

        public Task<IReadOnlyList<OrderItem>> GetCartItemsByUserIdAsync(string userId, CancellationToken ct = default)
            => _inner.GetCartItemsByUserIdAsync(userId, ct);

        public Task<IReadOnlyList<int>> GetCartItemIdsByUserIdAsync(string userId, CancellationToken ct = default)
            => _inner.GetCartItemIdsByUserIdAsync(userId, ct);

        public Task<IReadOnlyList<OrderItem>> GetAllPagedAsync(int pageSize, int pageNo, string search, CancellationToken ct = default)
            => _inner.GetAllPagedAsync(pageSize, pageNo, search, ct);

        public Task<int> GetAllPagedCountAsync(string search, CancellationToken ct = default)
            => _inner.GetAllPagedCountAsync(search, ct);

        public Task<int> GetCartItemCountByUserIdAsync(string userId, CancellationToken ct = default)
            => _inner.GetCartItemCountByUserIdAsync(userId, ct);
    }

    /// <summary>
    /// Dedicated <see cref="WebApplicationFactory{TEntryPoint}"/> for the Order rollback-atomicity
    /// proof. Built the same way as <see cref="Infrastructure.SqlServerE2EWebApplicationFactory"/>
    /// (real production DI graph, real migrations against a throwaway SQL Server container) plus one
    /// extra override: <see cref="IOrderItemRepository"/> is decorated with
    /// <see cref="AssignToOrderThrowingRepository"/> so <c>PlaceOrderAsync</c> can be driven into a
    /// genuine mid-transaction failure without touching production code.
    /// </summary>
    internal sealed class OrderRollbackE2EWebApplicationFactory : WebApplicationFactory<Startup>
    {
        private readonly string _connectionString;

        public OrderRollbackE2EWebApplicationFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
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
                var brokerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageBroker));
                if (brokerDescriptor != null)
                    services.Remove(brokerDescriptor);

                services.AddScoped<IMessageBroker, SynchronousMultiHandlerBroker>();

                var orderItemRepoDescriptor = services.Single(d => d.ServiceType == typeof(IOrderItemRepository));
                services.Remove(orderItemRepoDescriptor);
                services.AddScoped<IOrderItemRepository>(sp =>
                {
                    var inner = (IOrderItemRepository)ActivatorUtilities.CreateInstance(sp, orderItemRepoDescriptor.ImplementationType!);
                    return new AssignToOrderThrowingRepository(inner);
                });
            });

            builder.UseEnvironment("test");
        }
    }

    /// <summary>
    /// Spins up its own ephemeral SQL Server container + host, isolated from the shared
    /// <see cref="Infrastructure.MsSqlE2EFixture"/>/<c>SqlServerE2E</c> collection, because this
    /// fixture's DI graph carries the <see cref="AssignToOrderThrowingRepository"/> override — that
    /// override must never leak into any other E2E test in this project.
    /// </summary>
    public sealed class OrderRollbackE2EFixture : IAsyncLifetime
    {
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

    [CollectionDefinition("OrderRollbackSqlServer")]
    public sealed class OrderRollbackSqlServerCollection : ICollectionFixture<OrderRollbackE2EFixture>
    {
    }
}
