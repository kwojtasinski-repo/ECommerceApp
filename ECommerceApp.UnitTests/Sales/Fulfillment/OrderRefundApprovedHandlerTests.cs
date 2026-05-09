using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Services;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ECommerceApp.UnitTests.Sales.Fulfillment
{
    public class OrderRefundApprovedHandlerTests
    {
        private readonly Mock<IOrderService> _orders;

        public OrderRefundApprovedHandlerTests()
        {
            _orders = new Mock<IOrderService>();
        }

        private OrderRefundApprovedHandler CreateHandler() => new(_orders.Object);

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCallAddRefundAsyncWithCorrectParameters()
        {
            var message = new RefundApproved(
                RefundId: 5,
                OrderId: 99,
                Items: new List<RefundApprovedItem> { new(10, 2) },
                OccurredAt: DateTime.UtcNow);

            await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

            _orders.Verify(s => s.AddRefundAsync(99, 5, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
