using ECommerceApp.Application.Messaging;
using ECommerceApp.Application.Presale.Checkout.Contracts;
using ECommerceApp.Application.Presale.Checkout.Services;
using ECommerceApp.Application.Presale.Checkout.ViewModels;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Domain.Sagas;
using ECommerceApp.E2E.Backend.Infrastructure;
using ECommerceApp.Infrastructure.Messaging;
using ECommerceApp.Infrastructure.Sagas;
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

namespace ECommerceApp.E2E.Backend.Presale
{
    internal sealed class CartRecoveryFailureState
    {
        public int RevertFailureInjected;
    }

    internal sealed class ThrowingOrderClient : IOrderClient
    {
        public Task<OrderPlacementResult> PlaceOrderAsync(
            int customerId,
            int currencyId,
            string userId,
            CheckoutCustomer customer,
            IReadOnlyList<CheckoutLine> lines,
            CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated order placement failure for Cart Recovery E2E.");

        public Task<string> GetOrderCustomerEmailAsync(int orderId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<OrderAccessInfo> GetOrderAccessInfoAsync(int orderId, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    internal sealed class FailOnceSoftReservationService : ISoftReservationService
    {
        private readonly ISoftReservationService _inner;
        private readonly CartRecoveryFailureState _state;

        public FailOnceSoftReservationService(ISoftReservationService inner, CartRecoveryFailureState state)
        {
            _inner = inner;
            _state = state;
        }

        public Task<bool> HoldAsync(int productId, string userId, int quantity, CancellationToken ct = default)
            => _inner.HoldAsync(productId, userId, quantity, ct);

        public Task<SoftReservation> GetAsync(int productId, string userId, CancellationToken ct = default)
            => _inner.GetAsync(productId, userId, ct);

        public Task RemoveAsync(int productId, string userId, CancellationToken ct = default)
            => _inner.RemoveAsync(productId, userId, ct);

        public Task RemoveAllForProductAsync(int productId, CancellationToken ct = default)
            => _inner.RemoveAllForProductAsync(productId, ct);

        public Task RemoveAllForUserAsync(string userId, CancellationToken ct = default)
            => _inner.RemoveAllForUserAsync(userId, ct);

        public Task RemoveActiveForUserAsync(string userId, CancellationToken ct = default)
            => _inner.RemoveActiveForUserAsync(userId, ct);

        public Task RemoveCommittedForUserAsync(string userId, CancellationToken ct = default)
            => _inner.RemoveCommittedForUserAsync(userId, ct);

        public Task InvalidateExcessForProductAsync(int productId, int availableQuantity, CancellationToken ct = default)
            => _inner.InvalidateExcessForProductAsync(productId, availableQuantity, ct);

        public Task CommitAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
            => _inner.CommitAllForUserAsync(userId, ct);

        public Task RevertAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref _state.RevertFailureInjected, 1) == 0)
            {
                throw new InvalidOperationException("Simulated synchronous reservation revert failure for Cart Recovery E2E.");
            }

            return _inner.RevertAllForUserAsync(userId, ct);
        }

        public Task<IReadOnlyList<SoftReservation>> GetAllForUserAsync(PresaleUserId userId, CancellationToken ct = default)
            => _inner.GetAllForUserAsync(userId, ct);

        public Task<IReadOnlyList<SoftReservationPriceChangeVm>> GetPriceChangesAsync(PresaleUserId userId, CancellationToken ct = default)
            => _inner.GetPriceChangesAsync(userId, ct);
    }

    internal sealed class CartRecoveryE2EWebApplicationFactory : WebApplicationFactory<Startup>
    {
        private readonly string _connectionString;

        public CartRecoveryE2EWebApplicationFactory(string connectionString)
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
                    ["MessagingOptions:UseBackgroundDispatcher"] = "true",
                    ["MessagingOptions:OutboxPollInterval"] = "00:00:00.100",
                });
            });

            builder.ConfigureServices(services =>
            {
                var brokerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMessageBroker));
                if (brokerDescriptor != null)
                {
                    services.Remove(brokerDescriptor);
                }

                services.AddScoped<IMessageBroker, SynchronousMultiHandlerBroker>();
                services.AddSingleton<CartRecoveryFailureState>();

                var moduleClientDescriptors = services
                    .Where(d => d.ServiceType == typeof(IModuleClient))
                    .ToList();
                foreach (var descriptor in moduleClientDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IModuleClient, PerHandlerScopeModuleClient>();

                var orderClientDescriptor = services.Single(d => d.ServiceType == typeof(IOrderClient));
                services.Remove(orderClientDescriptor);
                services.AddScoped<IOrderClient, ThrowingOrderClient>();

                var reservationDescriptor = services.Single(d => d.ServiceType == typeof(ISoftReservationService));
                services.Remove(reservationDescriptor);
                services.AddScoped<ISoftReservationService>(sp =>
                {
                    var inner = (ISoftReservationService)ActivatorUtilities.CreateInstance(
                        sp,
                        reservationDescriptor.ImplementationType!);
                    return new FailOnceSoftReservationService(
                        inner,
                        sp.GetRequiredService<CartRecoveryFailureState>());
                });
            });

            builder.UseEnvironment("test");
        }

    }

    public sealed class CartRecoveryE2EFixture : IAsyncLifetime
    {
        public const string CollectionName = "CartRecoverySqlServer";

        private readonly MsSqlContainer _container = new MsSqlBuilder()
            .WithLogger(TestLogging.CreateTestcontainersLogger())
            .WithOutputConsumer(TestLogging.CreateContainerOutputConsumer())
            .Build();
        private CartRecoveryE2EWebApplicationFactory _factory;

        public IServiceProvider Services => (_factory ?? throw new InvalidOperationException(
            $"{nameof(CartRecoveryE2EFixture)} has not been initialized yet.")).Services;

        public async ValueTask InitializeAsync()
        {
            await _container.StartAsync();
            _factory = new CartRecoveryE2EWebApplicationFactory(_container.GetConnectionString());
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

    [Collection(CartRecoveryE2EFixture.CollectionName)]
    public sealed class CartRecoverySagaE2ETests : IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly CartRecoveryE2EFixture _sqlFixture;
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public CartRecoverySagaE2ETests(
            CartRecoveryE2EFixture sqlFixture,
            MessageProcessingOperationsFixture messageProcessing)
        {
            _sqlFixture = sqlFixture;
            _messageProcessing = messageProcessing;
        }

        [Fact]
        public async Task PlaceOrderAsync_WhenOrderPlacementAndSynchronousRevertFail_CompletesDurableRecoveryAndRethrowsOriginalException()
        {
            var userId = $"cart-recovery-{Guid.NewGuid():N}";
            using (var scope = _sqlFixture.Services.CreateScope())
            {
                var reservation = SoftReservation.Create(
                    Random.Shared.Next(1_000_000, int.MaxValue),
                    userId,
                    1,
                    10m,
                    DateTime.UtcNow.AddMinutes(10));
                await scope.ServiceProvider
                    .GetRequiredService<ISoftReservationRepository>()
                    .AddAsync(reservation, TestContext.Current.CancellationToken);

                var exception = await Should.ThrowAsync<InvalidOperationException>(() => scope.ServiceProvider
                    .GetRequiredService<ICheckoutService>()
                    .PlaceOrderAsync(
                        new PresaleUserId(userId),
                        customerId: 1,
                        currencyId: 1,
                        new CheckoutCustomer(
                            "Jan", "Kowalski", "jan@test.com", "123456789", false,
                            null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska"),
                        TestContext.Current.CancellationToken));

                exception.Message.ShouldBe("Simulated order placement failure for Cart Recovery E2E.");
            }

            var recovered = await _messageProcessing.WaitUntilAsync(
                new CartRecoveryCompletedOperation(_sqlFixture, userId),
                cancellationToken: TestContext.Current.CancellationToken);
            recovered.ShouldBeTrue();
        }

        private sealed class CartRecoveryCompletedOperation : IMessageProcessingOperation<bool>
        {
            private readonly CartRecoveryE2EFixture _fixture;
            private readonly string _userId;

            public CartRecoveryCompletedOperation(CartRecoveryE2EFixture fixture, string userId)
            {
                _fixture = fixture;
                _userId = userId;
            }

            public async Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                using var scope = _fixture.Services.CreateScope();
                var saga = await scope.ServiceProvider
                    .GetRequiredService<SagasDbContext>()
                    .Sagas
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        instance => instance.SagaType == "CartRecovery"
                            && instance.CorrelationId == _userId,
                        cancellationToken);
                if (saga?.Status != SagaInstanceStatus.Completed)
                {
                    return false;
                }

                var reservations = await scope.ServiceProvider
                    .GetRequiredService<ISoftReservationRepository>()
                    .GetByUserIdAsync(new PresaleUserId(_userId), cancellationToken);
                return reservations.Count == 1
                    && reservations[0].Status == SoftReservationStatus.Active;
            }

            public bool IsCompleted(bool state) => state;

            public string Describe(bool state)
                => $"Cart Recovery saga for {_userId} did not complete with an active reverted reservation.";
        }
    }

    [CollectionDefinition(CartRecoveryE2EFixture.CollectionName)]
    public sealed class CartRecoverySqlServerCollection : ICollectionFixture<CartRecoveryE2EFixture>
    {
    }
}
