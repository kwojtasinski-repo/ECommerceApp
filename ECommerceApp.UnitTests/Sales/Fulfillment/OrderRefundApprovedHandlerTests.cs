using ECommerceApp.Application.Sales.Fulfillment.Messages;
using ECommerceApp.Application.Sales.Orders.Handlers;
using ECommerceApp.Application.Sales.Orders.Services;
using ECommerceApp.Application.Sales.Orders;
using ECommerceApp.Application.Messaging;
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
        private readonly Mock<IOrdersUnitOfWork> _unitOfWork = new();
        private readonly Mock<IOutboxTransaction> _transaction = new();
        private readonly Mock<IProcessedMessageGuard> _processedMessageGuard = new();

        public OrderRefundApprovedHandlerTests()
        {
            _orders = new Mock<IOrderService>();
            _unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_transaction.Object);
            _processedMessageGuard.Setup(g => g.TryMarkProcessedAsync(
                It.IsAny<long>(), It.IsAny<string>(), _transaction.Object, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        private OrderRefundApprovedHandler CreateHandler()
            => new(_orders.Object, _unitOfWork.Object, _processedMessageGuard.Object);

        // ── HandleAsync ───────────────────────────────────────────────────────

        [Fact]
        public async Task HandleAsync_ValidMessage_ShouldCallAddRefundAsyncWithCorrectParameters()
        {
            var message = new RefundApproved(
                RefundId: 5,
                OrderId: 99,
                Items: new List<RefundApprovedItem> { new(10, 2) },
                OccurredAt: DateTime.UtcNow);

            await CreateHandler().HandleAsync(message, 1, TestContext.Current.CancellationToken);

            _orders.Verify(s => s.AddRefundAsync(99, 5, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
