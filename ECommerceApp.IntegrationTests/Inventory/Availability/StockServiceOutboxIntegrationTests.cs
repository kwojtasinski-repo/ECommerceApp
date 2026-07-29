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
    public class StockServiceOutboxIntegrationTests : BcBaseTest<IStockService>
    {
        public StockServiceOutboxIntegrationTests(ITestOutputHelper output) : base(output) { }

        private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await condition())
                    return;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        private async Task<StockSnapshot> GetSnapshotAsync(int productId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IStockSnapshotRepository>();
            return await repo.FindByProductIdAsync(productId, CancellationToken);
        }

        [Fact]
        public async Task InitializeStockAsync_EnqueuesOutboxMessage_AndStockSnapshotEventuallyReflectsQuantity()
        {
            var productId = new Random().Next(1_000_000, int.MaxValue);

            var result = await _service.InitializeStockAsync(productId, initialQuantity: 25, CancellationToken);
            result.ShouldBeTrue();

            await WaitUntilAsync(
                async () => await GetSnapshotAsync(productId) is { } snapshot && snapshot.AvailableQuantity == 25,
                TimeSpan.FromSeconds(20));

            var snapshot = await GetSnapshotAsync(productId);
            snapshot.ShouldNotBeNull();
            snapshot.AvailableQuantity.ShouldBe(25);
        }
    }
}
