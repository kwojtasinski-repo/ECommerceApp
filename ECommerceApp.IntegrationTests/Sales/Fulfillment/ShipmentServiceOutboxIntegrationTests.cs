using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Domain.Sales.Fulfillment;
using ECommerceApp.Domain.Sales.Orders;
using ECommerceApp.Domain.Sales.Orders.ValueObjects;
using ECommerceApp.Shared.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Fulfillment
{
    public class ShipmentServiceOutboxIntegrationTests : BcBaseTest<IShipmentService>
    {
        public ShipmentServiceOutboxIntegrationTests(ITestOutputHelper output) : base(output) { }

        private static OrderCustomer CreateCustomer() => new(
            "Jan", "Kowalski", "jan@test.com", "123456789",
            false, null, null, "Główna", "1", null, "67-100", "Nowa Sól", "Polska");

        private async Task<int> SeedOrderAsync(CancellationToken ct = default)
        {
            var repo = GetRequiredService<IOrderRepository>();
            var order = Order.Create(1, 1, PROPER_CUSTOMER_ID, OrderNumber.Generate(), CreateCustomer());
            return await repo.AddAsync(order);
        }

        private async Task<int> SeedShipmentAsync(int orderId, CancellationToken ct = default)
        {
            var repo = GetRequiredService<IShipmentRepository>();
            var lines = new[] { ShipmentLine.Create(10, 2) };
            var shipment = Shipment.Create(orderId, lines);
            return await repo.AddAsync(shipment, ct);
        }

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

        // Read order through a fresh scope each call to avoid stale tracked instances
        private async Task<Order> GetOrderAsync(int orderId)
        {
            using var scope = Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            return await repo.GetByIdWithItemsAsync(orderId, CancellationToken);
        }

        [Fact]
        public async Task MarkAsFailedAsync_EnqueuesOutboxMessage_AndOrderEventuallyHasShipmentFailedEvent()
        {
            var orderId = await SeedOrderAsync(CancellationToken);
            var shipmentId = await SeedShipmentAsync(orderId, CancellationToken);

            var result = await _service.MarkAsFailedAsync(shipmentId, CancellationToken);
            result.ShouldBe(ShipmentOperationResult.Success);

            await WaitUntilAsync(async () =>
            {
                var order = await GetOrderAsync(orderId);
                return order != null && order.Events.Any(e => e.EventType == OrderEventType.ShipmentFailed);
            }, TimeSpan.FromSeconds(20));

            var finalOrder = await GetOrderAsync(orderId);
            finalOrder.ShouldNotBeNull();
            finalOrder.Events.Count(e => e.EventType == OrderEventType.ShipmentFailed).ShouldBe(1);
        }
    }
}
