using ECommerceApp.Application.Sales.Fulfillment.DTOs;
using ECommerceApp.Application.Sales.Fulfillment.Results;
using ECommerceApp.Application.Sales.Fulfillment.Services;
using ECommerceApp.Shared.TestInfrastructure;
using Shouldly;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.IntegrationTests.Sales.Fulfillment
{
    public class ShipmentServiceTests : BcBaseTest<IShipmentService>
    {
        public ShipmentServiceTests(ITestOutputHelper output) : base(output) { }

        // ── CreateShipmentAsync ──────────────────────────────────────────

        [Fact]
        public async Task CreateShipmentAsync_NonExistentOrder_ShouldReturnOrderNotFound()
        {
            var dto = new CreateShipmentDto(
                OrderId: int.MaxValue,
                Lines: new List<CreateShipmentLineDto> { new(ProductId: 1, Quantity: 2) });

            var result = await _service.CreateShipmentAsync(dto, CancellationToken);

            result.ShouldBe(ShipmentOperationResult.OrderNotFound);
        }

        // ── MarkAsInTransitAsync ─────────────────────────────────────────

        [Fact]
        public async Task MarkAsInTransitAsync_NonExistentShipment_ShouldReturnNotFound()
        {
            var result = await _service.MarkAsInTransitAsync(int.MaxValue, "TRACK-001", CancellationToken);

            result.ShouldBe(ShipmentOperationResult.NotFound);
        }

        // ── MarkAsDeliveredAsync ─────────────────────────────────────────

        [Fact]
        public async Task MarkAsDeliveredAsync_NonExistentShipment_ShouldReturnNotFound()
        {
            var result = await _service.MarkAsDeliveredAsync(int.MaxValue, CancellationToken);

            result.ShouldBe(ShipmentOperationResult.NotFound);
        }

        // ── MarkAsFailedAsync ────────────────────────────────────────────

        [Fact]
        public async Task MarkAsFailedAsync_NonExistentShipment_ShouldReturnNotFound()
        {
            var result = await _service.MarkAsFailedAsync(int.MaxValue, CancellationToken);

            result.ShouldBe(ShipmentOperationResult.NotFound);
        }

        // ── MarkAsPartiallyDeliveredAsync ────────────────────────────────

        [Fact]
        public async Task MarkAsPartiallyDeliveredAsync_NonExistentShipment_ShouldReturnNotFound()
        {
            var result = await _service.MarkAsPartiallyDeliveredAsync(int.MaxValue, new List<int> { 1 }, CancellationToken);

            result.ShouldBe(ShipmentOperationResult.NotFound);
        }

        // ── GetShipmentAsync ─────────────────────────────────────────────

        [Fact]
        public async Task GetShipmentAsync_NonExistentShipment_ShouldReturnNull()
        {
            var result = await _service.GetShipmentAsync(int.MaxValue, CancellationToken);

            result.ShouldBeNull();
        }

        // ── GetShipmentsByOrderIdAsync ───────────────────────────────────

        [Fact]
        public async Task GetShipmentsByOrderIdAsync_NonExistentOrder_ShouldReturnEmptyList()
        {
            var result = await _service.GetShipmentsByOrderIdAsync(int.MaxValue, CancellationToken);

            result.ShouldNotBeNull();
            result.Shipments.ShouldNotBeNull();
            result.Shipments.ShouldBeEmpty();
        }
    }
}

