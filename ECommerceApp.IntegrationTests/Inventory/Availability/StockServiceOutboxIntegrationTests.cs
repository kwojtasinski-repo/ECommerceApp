using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Domain.Presale.Checkout;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Inventory.Availability
{
    /// <summary>
    /// Closes the Phase 3 validation gap flagged for the Inventory/Availability BC: every other
    /// retrofitted BC has an outbox-integration test proving service call → real <c>Outbox</c> row →
    /// <c>Dispatched</c> → real downstream handler side effect (see e.g.
    /// <see cref="ECommerceApp.IntegrationTests.Sales.Payments.PaymentServiceOutboxIntegrationTests"/>),
    /// but Inventory/Availability had none — the only existing test in this BC
    /// (<c>CrossBC.InventoryEventChainTests</c>) publishes directly via <c>IMessageBroker</c>, bypassing
    /// the Outbox entirely.
    /// <para>
    /// Downstream effect observed here: <see cref="ECommerceApp.Application.Presale.Checkout.Handlers.StockAvailabilityChangedHandler"/>
    /// (Presale/Checkout BC) upserts a <see cref="StockSnapshot"/> row whenever it processes a
    /// dispatched <c>StockAvailabilityChanged</c> message.
    /// </para>
    /// </summary>
    public class StockServiceOutboxIntegrationTests
        : BcBaseTest<IStockService>, IClassFixture<MessageProcessingOperationsFixture>
    {
        private readonly MessageProcessingOperationsFixture _messageProcessing;

        public StockServiceOutboxIntegrationTests(
            ITestOutputHelper output,
            MessageProcessingOperationsFixture messageProcessing) : base(output)
        {
            _messageProcessing = messageProcessing;
        }

        private async Task<StockSnapshot> GetSnapshotAsync(
            int productId,
            CancellationToken cancellationToken)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();
            return await repo.FindByProductIdAsync(productId, cancellationToken);
        }

        [Fact]
        public async Task InitializeStockAsync_EnqueuesOutboxMessage_AndStockSnapshotEventuallyReflectsQuantity()
        {
            var productId = new Random().Next(1_000_000, int.MaxValue);

            var result = await _service.InitializeStockAsync(productId, initialQuantity: 25, CancellationToken);
            result.ShouldBeTrue();

            var snapshot = await _messageProcessing.WaitUntilAsync(
                new StockSnapshotQuantityOperation(this, productId, 25));

            snapshot.ShouldNotBeNull();
            snapshot.AvailableQuantity.ShouldBe(25);
        }

        private sealed class StockSnapshotQuantityOperation
            : IMessageProcessingOperation<StockSnapshot>
        {
            private readonly StockServiceOutboxIntegrationTests _test;
            private readonly int _productId;
            private readonly int _expectedQuantity;

            public StockSnapshotQuantityOperation(
                StockServiceOutboxIntegrationTests test,
                int productId,
                int expectedQuantity)
            {
                _test = test;
                _productId = productId;
                _expectedQuantity = expectedQuantity;
            }

            public Task<StockSnapshot> ReadAsync(CancellationToken cancellationToken)
            {
                return _test.GetSnapshotAsync(_productId, cancellationToken);
            }

            public bool IsCompleted(StockSnapshot state)
            {
                return state is not null && state.AvailableQuantity == _expectedQuantity;
            }

            public string Describe(StockSnapshot state)
            {
                return state is null
                    ? $"Stock snapshot for product {_productId} was not created."
                    : $"Stock snapshot for product {_productId} has quantity {state.AvailableQuantity}, expected {_expectedQuantity}.";
            }
        }
    }
}
