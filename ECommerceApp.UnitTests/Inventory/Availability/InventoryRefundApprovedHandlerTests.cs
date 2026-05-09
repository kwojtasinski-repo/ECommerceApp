using ECommerceApp.Application.Inventory.Availability.Handlers;
using ECommerceApp.Application.Inventory.Availability.Services;
using ECommerceApp.Application.Sales.Fulfillment.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Inventory.Availability
{
    public class InventoryRefundApprovedHandlerTests
    {
        private readonly Mock<IStockService> _stockService;
        private readonly RefundApprovedHandler _handler;

        public InventoryRefundApprovedHandlerTests()
        {
            _stockService = new Mock<IStockService>();
            _handler = new RefundApprovedHandler(_stockService.Object);
        }

        [Fact]
        public async Task HandleAsync_ShouldCallReturnForEachItem()
        {
            var message = new RefundApproved(
                RefundId: 1,
                OrderId: 1,
                Items: new List<RefundApprovedItem>
                {
                    new RefundApprovedItem(ProductId: 42, Quantity: 3),
                    new RefundApprovedItem(ProductId: 10, Quantity: 1)
                },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReturnAsync(42, 3, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.Verify(s => s.ReturnAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ShouldNotCallOtherStockMethods()
        {
            var message = new RefundApproved(
                RefundId: 2,
                OrderId: 5,
                Items: new List<RefundApprovedItem>
                {
                    new RefundApprovedItem(ProductId: 10, Quantity: 1)
                },
                OccurredAt: DateTime.UtcNow);

            await _handler.HandleAsync(message, TestContext.Current.CancellationToken);

            _stockService.Verify(s => s.ReturnAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
            _stockService.VerifyNoOtherCalls();
        }
    }
}
